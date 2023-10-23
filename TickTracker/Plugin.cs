using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Dalamud.Memory;
using Dalamud.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using TickTracker.Windows;
using TickTracker.Enums;
using TickTracker.Structs;
using TickTracker.Helpers;

namespace TickTracker;

public sealed class Plugin : IDalamudPlugin
{
    /// <summary>
    /// A <see cref="List{T}"/> of addons to fetch for collision checks.
    /// </summary>
    private readonly List<string> addonsLookup = new()
    {
        "Talk",
        "ActionDetail",
        "ItemDetail",
        "Inventory",
        "Character",
    };

    /// <summary>
    /// A <see cref="HashSet{T}" /> based list of Status IDs that trigger HP regen
    /// </summary>
    private readonly ConcurrentSet<uint> healthRegenList = new();
    /// <summary>
    /// A <see cref="HashSet{T}" /> based list of Status IDs that trigger MP regen
    /// </summary>
    private readonly ConcurrentSet<uint> manaRegenList = new();
    /// <summary>
    /// A <see cref="HashSet{T}" /> based list of Status IDs that stop HP regen
    /// </summary>
    private readonly ConcurrentSet<uint> disabledHealthRegenList = new();

    // DamageInfo Delegate & Hook
    private unsafe delegate void ReceiveActionEffectDelegate(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail);

    // Function that triggers when client receives a network packet with an update for nearby actors
    private unsafe delegate void ReceivePrimaryActorUpdateDelegate(uint objectId, uint* packetData, byte unkByte);

    [Obsolete("The update delegates aren't affected by stuff like healing, so this has become unnecessary")]
    [Signature("40 55 53 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70", DetourName = nameof(ReceiveActionEffect))]
    private readonly Hook<ReceiveActionEffectDelegate>? receiveActionEffectHook = null;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 83 3D ?? ?? ?? ?? ?? 41 0F B6 E8 48 8B DA 8B F1 0F 84 ?? ?? ?? ?? 48 89 7C 24 ??", DetourName = nameof(PrimaryActorTickUpdate))]
    private readonly Hook<ReceivePrimaryActorUpdateDelegate>? receivePrimaryActorUpdateHook = null;

    private readonly DalamudPluginInterface pluginInterface;
    private readonly Configuration config;
    private readonly Utilities utilities;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly ICommandManager commandManager;
    private readonly ICondition condition;
    private readonly IJobGauges jobGauges;
    private readonly IPluginLog log;

    private ConfigWindow ConfigWindow { get; init; }
    private DebugWindow DebugWindow { get; init; }
    private HPBar HPBarWindow { get; init; }
    private MPBar MPBarWindow { get; init; }
    private GPBar GPBarWindow { get; init; }
#if DEBUG
    public static DevWindow DevWindow { get; set; } = null!;
#endif

    public WindowSystem WindowSystem { get; } = new("TickTracker");
    private readonly string commandName = "/tick";
    private readonly List<BarWindowBase> windowList = new();

    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    private double syncValue, regenValue, fastValue;
    private bool finishedLoading;
    private bool syncAvailable = true, nullSheet = true;
    private uint lastHPValue, lastMPValue, lastGPValue;
    private Task? loadingTask;
    private unsafe AtkUnitBase* NameplateAddon => (AtkUnitBase*)gameGui.GetAddonByName("NamePlate");

    public Plugin(DalamudPluginInterface _pluginInterface,
        IClientState _clientState,
        IFramework _framework,
        IGameGui _gameGui,
        ICommandManager _commandManager,
        ICondition _condition,
        IDataManager _dataManager,
        IJobGauges _jobGauges,
        IPluginLog _pluginLog,
        IGameInteropProvider _interopProvider)
    {
        pluginInterface = _pluginInterface;
        clientState = _clientState;
        framework = _framework;
        gameGui = _gameGui;
        commandManager = _commandManager;
        condition = _condition;
        jobGauges = _jobGauges;
        log = _pluginLog;

        _interopProvider.InitializeFromAttributes(this);

        if (receiveActionEffectHook is null || receivePrimaryActorUpdateHook is null)
        {
            throw new Exception("At least one hook failed, and the plugin is not functional.");
        }
        receiveActionEffectHook.Enable();
        receivePrimaryActorUpdateHook.Enable();

        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        utilities = new Utilities(pluginInterface, config, condition, _dataManager, clientState, log);
        DebugWindow = new DebugWindow();
        ConfigWindow = new ConfigWindow(pluginInterface, config, DebugWindow);
        HPBarWindow = new HPBar(clientState, log, utilities, config);
        MPBarWindow = new MPBar(clientState, log, utilities, config);
        GPBarWindow = new GPBar(clientState, log, utilities, config);
        windowList.Add(HPBarWindow);
        windowList.Add(MPBarWindow);
        windowList.Add(GPBarWindow);
#if DEBUG
        DevWindow = new DevWindow();
        WindowSystem.AddWindow(DevWindow);
#endif
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(HPBarWindow);
        WindowSystem.AddWindow(MPBarWindow);
        WindowSystem.AddWindow(GPBarWindow);
        WindowSystem.AddWindow(DebugWindow);

        commandManager.AddHandler(commandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open or close Tick Tracker's config window.",
        });
        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        framework.Update += OnFrameworkUpdate;
        clientState.TerritoryChanged += TerritoryChanged;
        _ = Task.Run(InitializeLuminaSheet);
    }

    private void TerritoryChanged(ushort e)
    {
        loadingTask = Task.Run(async () => await utilities.Loading(1000).ConfigureAwait(false));
    }

    private bool PluginEnabled(PlayerCharacter player)
    {
        var target = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
        var inCombat = condition[ConditionFlag.InCombat];

        if (condition[ConditionFlag.InDuelingArea])
        {
            return false;
        }
        if (config.HideOutOfCombat && !inCombat)
        {
            var showingBecauseInDuty = config.AlwaysShowInDuties && utilities.InDuty();
            var showingBecauseHasTarget = config.AlwaysShowWithHostileTarget && target;
            if (!(showingBecauseInDuty || showingBecauseHasTarget))
            {
                return false;
            }
        }
        return config.PluginEnabled;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (clientState is { IsLoggedIn: false } or { IsPvPExcludingDen: true } || nullSheet)
        {
            return;
        }
        if (clientState is not { LocalPlayer: { } player })
        {
            return;
        }

#if DEBUG
        DevWindowThings(player, (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds, GPBarWindow);
#endif
        unsafe
        {
            if (!PluginEnabled(player) || !utilities.IsAddonReady(NameplateAddon))
            {
                HPBarWindow.IsOpen = MPBarWindow.IsOpen = GPBarWindow.IsOpen = false;
                return;
            }
            if (!NameplateAddon->IsVisible || utilities.inCustcene() || player.IsDead)
            {
                HPBarWindow.IsOpen = MPBarWindow.IsOpen = GPBarWindow.IsOpen = false;
                return;
            }
        }
        UpdateBarState(player);
        var now = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        if (syncAvailable)
        {
            syncValue = now;
            regenValue = fastValue = syncValue - FastTickInterval;
            syncAvailable = false;
        }
        else if (syncValue + ActorTickInterval <= now)
        {
            syncValue += ActorTickInterval;
        }
        if (fastValue + FastTickInterval <= now)
        {
            fastValue += FastTickInterval;
        }
        if (regenValue + ActorTickInterval <= now)
        {
            regenValue += ActorTickInterval;
        }
        if (loadingTask is not null && loadingTask.IsCompleted)
        {
            finishedLoading = true;
            loadingTask = null;
        }
        ProcessTicks(now, player);
    }

    private void ProcessTicks(double currentTime, PlayerCharacter player)
    {
        // HP section
        HPBarWindow.RegenProgressActive = player.StatusList.Any(e => healthRegenList.Contains(e.StatusId));
        HPBarWindow.ProgressHalted = player.StatusList.Any(e => disabledHealthRegenList.Contains(e.StatusId));
        var currentHP = player.CurrentHp;
        var fullHP = currentHP == player.MaxHp;

        // MP Section
        MPBarWindow.RegenProgressActive = player.StatusList.Any(e => manaRegenList.Contains(e.StatusId));
        var blmGauge = player.ClassJob.Id == 25 ? jobGauges.Get<BLMGauge>() : null;
        MPBarWindow.ProgressHalted = blmGauge is not null && blmGauge.InAstralFire;
        var currentMP = player.CurrentMp;
        var fullMP = currentMP == player.MaxMp;

        // GP Section
        GPBarWindow.ProgressHalted = condition[ConditionFlag.Gathering];
        var currentGP = player.CurrentGp;
        var fullGP = currentGP == player.MaxGp;

        UpdateTick(HPBarWindow, currentTime, currentHP, fullHP, lastHPValue);
        UpdateTick(MPBarWindow, currentTime, currentMP, fullMP, lastMPValue);
        UpdateTick(GPBarWindow, currentTime, currentGP, fullGP, lastGPValue);

        lastHPValue = currentHP;
        lastMPValue = currentMP;
        lastGPValue = currentGP;
    }

    private void UpdateTick(BarWindowBase window, double currentTime, uint currentResource, bool fullResource, uint lastResource)
    {
        if (window.RegenProgressActive)
        {
            window.Tick = fullResource ? regenValue : fastValue;
            window.Progress = (currentTime - window.Tick) / (fullResource ? ActorTickInterval : FastTickInterval);
            return;
        }
        if (fullResource || finishedLoading || window.ProgressHalted)
        {
            window.Tick = syncValue;
        }
        else if (lastResource != currentResource && window.TickUpdate)
        {
            window.Tick = currentTime;
            window.TickUpdate = false;
        }
        window.Progress = (currentTime - window.Tick) / ActorTickInterval;
    }

    private void InitializeLuminaSheet()
    {
        var statusSheet = utilities.RetrieveSheet<Lumina.Excel.GeneratedSheets.Status>();
        if (statusSheet is null)
        {
            return;
        }
        List<int> bannedStatus = new() { 135, 307, 751, 1419, 1465, 1730, 2326 };
        var filteredSheet = statusSheet.Where(s => !bannedStatus.Exists(rowId => rowId == s.RowId));
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount / 2, // Surely no one will cause an access issue by using an 128 core CPU hahaha
        };
        Parallel.ForEach(filteredSheet, parallelOptions, stat =>
        {
            var text = stat.Description.ToDalamudString().TextValue;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            if (Utilities.WholeKeywordMatch(text, utilities.RegenNullKeywords) && Utilities.WholeKeywordMatch(text, utilities.HealthKeywords))
            {
                disabledHealthRegenList.Add(stat.RowId);
                DebugWindow.DisabledHealthRegenDictionary.TryAdd(stat.RowId, stat.Name);
            }
            if (Utilities.WholeKeywordMatch(text, utilities.RegenNullKeywords) && Utilities.WholeKeywordMatch(text, utilities.ManaKeywords))
            {
                DebugWindow.DisabledManaRegenDictionary.TryAdd(stat.RowId, stat.Name);
            }
            if (Utilities.KeywordMatch(text, utilities.RegenKeywords) && Utilities.KeywordMatch(text, utilities.TimeKeywords))
            {
                if (Utilities.KeywordMatch(text, utilities.HealthKeywords))
                {
                    healthRegenList.Add(stat.RowId);
                    DebugWindow.HealthRegenDictionary.TryAdd(stat.RowId, stat.Name);
                }
                if (Utilities.KeywordMatch(text, utilities.ManaKeywords))
                {
                    manaRegenList.Add(stat.RowId);
                    DebugWindow.ManaRegenDictionary.TryAdd(stat.RowId, stat.Name);
                }
            }
        });
        nullSheet = false;
        log.Debug("HP regen list generated with {HPcount} status effects.", healthRegenList.Count);
        log.Debug("MP regen list generated with {MPcount} status effects.", manaRegenList.Count);
    }

    // DamageInfo stripped function
    /// <summary>
    /// This detour function is triggered every time the client receives
    /// a network packet containing an action that's triggered by a player
    /// in the vecinity of the user
    /// </summary>
    private unsafe void ReceiveActionEffect(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail)
    {
        receiveActionEffectHook!.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
        try
        {
            if (clientState is not { LocalPlayer: { } player })
            {
                return;
            }
            if (!utilities.IsTarget(player, sourceCharacter))
            {
                return;
            }

            var name = MemoryHelper.ReadStringNullTerminated((nint)sourceCharacter->GameObject.GetName());

            var entryCount = effectHeader->TargetCount switch
            {
                0 => 0,
                1 => 8,
                <= 8 => 64,
                <= 16 => 128,
                <= 24 => 192,
                <= 32 => 256,
                _ => 0
            };
            for (var i = 0; i < entryCount; i++)
            {
                if (effectArray[i].type == ActionEffectType.Heal)
                {
                    var logMessage = sourceId == player.ObjectId ? "Self healing" : "Healed by " + name;
                    log.Debug(logMessage);
                }
                else if (effectArray[i].type == ActionEffectType.MpGain)
                {
                    var logMessage = sourceId == player.ObjectId ? "Restoring your own mana" : "Mana resotred by " + name;
                    log.Debug(logMessage);
                }
                else if (effectArray[i].type != ActionEffectType.Nothing && effectArray[i].type != ActionEffectType.Damage)
                {
                    var logMessage = "Received action type: " + effectArray[i].type + " from " + name;
                    log.Debug(logMessage);
                }
            }
        }
        catch (Exception e)
        {
            log.Error(e, "An error has occured with the ReceiveActionEffect detour.");
        }
    }

    /// <summary>
    /// This detour function is triggered every time the client receives
    /// a network packet containing an update for the nearby actors
    /// with HP, MP, GP
    /// </summary>
    private unsafe void PrimaryActorTickUpdate(uint objectId, uint* packetData, byte unkByte)
    {
        receivePrimaryActorUpdateHook!.Original(objectId, packetData, unkByte);
        try
        {
            if (clientState is not { LocalPlayer: { } player })
            {
                return;
            }
#if DEBUG
            var networkHP = *(int*)packetData;
            var networkMP = *((ushort*)packetData + 2);
            var networkGP = *((short*)packetData + 3); // Goes up to 10000 and is tracked and updated at all times
#endif
            if (objectId != player.ObjectId)
            {
                return;
            }
            HPBarWindow.TickUpdate = player.CurrentHp != player.MaxHp;
            MPBarWindow.TickUpdate = player.CurrentMp != player.MaxMp;
            GPBarWindow.TickUpdate = true;
            syncAvailable = true;
            finishedLoading = false;
        }
        catch (Exception e)
        {
            log.Error(e, "An error has occured with the PrimaryActorTickUpdate detour.");
        }
    }

    private unsafe void UpdateBarState(PlayerCharacter player)
    {
        var jobType = player.ClassJob.GameData?.ClassJobCategory.Row ?? 0;
        var Enemy = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
        var inCombat = condition[ConditionFlag.InCombat];
        var shouldShowHPBar = !config.HideOnFullResource ||
                            (config.AlwaysShowInCombat && inCombat) ||
                            (config.AlwaysShowWithHostileTarget && Enemy) ||
                            (config.AlwaysShowInDuties && utilities.InDuty());
        var shouldShowMPBar = !config.HideOnFullResource ||
                            (config.AlwaysShowInCombat && inCombat) ||
                            (config.AlwaysShowWithHostileTarget && Enemy) ||
                            (config.AlwaysShowInDuties && utilities.InDuty());
        HPBarWindow.IsOpen = shouldShowHPBar || (player.CurrentHp != player.MaxHp);
        MPBarWindow.IsOpen = (jobType != 32 && (shouldShowMPBar || (player.CurrentMp != player.MaxMp))) || !config.GPVisible;
        GPBarWindow.IsOpen = (jobType == 32 && (!config.HideOnFullResource || (player.CurrentGp != player.MaxGp)) && config.GPVisible) || !config.LockBar;
        if (!config.CollisionDetection || (config.DisableCollisionInCombat && inCombat))
        {
            return;
        }
        var AddonList = new List<nint>();
        foreach (var name in addonsLookup)
        {
            var addonPointer = gameGui.GetAddonByName(name);
            if (addonPointer != nint.Zero)
            {
                AddonList.Add(addonPointer);
            }
        }
        foreach (var addon in AddonList)
        {
            var currentAddon = (AtkUnitBase*)addon;
            if (!utilities.IsAddonReady(currentAddon))
            {
                continue;
            }
            if (currentAddon->IsVisible)
            {
                var scaled = currentAddon->Scale != 100;
                foreach (var window in windowList.Where(w => utilities.AddonOverlap(currentAddon, w, scaled)))
                {
                    window.IsOpen = false;
                }
            }
        }
    }

#if DEBUG
    private unsafe void DevWindowThings(PlayerCharacter player, double currentTime, BarWindowBase window)
    {
        DevWindow.IsOpen = true;
        DevWindow.printLines.Add("HP: " + player.CurrentHp.ToString() + " / " + player.MaxHp.ToString());
        DevWindow.printLines.Add("Current Time: " + currentTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DevWindow.printLines.Add("RegenProgressActive: " + window.RegenProgressActive.ToString());
        DevWindow.printLines.Add("Progress: " + window.Progress.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DevWindow.printLines.Add("NormalTick: " + window.Tick.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DevWindow.printLines.Add("NormalUpdate: " + window.TickUpdate.ToString());
        DevWindow.printLines.Add("Sync Value: " + syncValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DevWindow.printLines.Add("Regen Value: " + regenValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DevWindow.printLines.Add("Fast Value: " + fastValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
#endif

    public void Dispose()
    {
        receiveActionEffectHook?.Disable();
        receiveActionEffectHook?.Dispose();
        receivePrimaryActorUpdateHook?.Disable();
        receivePrimaryActorUpdateHook?.Dispose();
        commandManager.RemoveHandler(commandName);
        WindowSystem.RemoveAllWindows();
        framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= DrawUI;
        clientState.TerritoryChanged -= TerritoryChanged;
        pluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
    }

    private void OnCommand(string command, string args)
    {
        ConfigWindow.Toggle();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        ConfigWindow.Toggle();
    }
}
