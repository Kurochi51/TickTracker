using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Lumina.Excel;
using Dalamud.Memory;
using Dalamud.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Dalamud.Interface.Windowing;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.JobGauge;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using TickTracker.Windows;
using Dalamud.Logging;

namespace TickTracker;

public sealed class Plugin : IDalamudPlugin
{
    /// <summary>
    /// A <see cref="HashSet{T}" /> list of Status IDs that trigger HP regen
    /// </summary>
    private static readonly HashSet<uint> HealthRegenList = new();
    /// <summary>
    /// A <see cref="HashSet{T}" /> list of Status IDs that trigger MP regen
    /// </summary>
    private static readonly HashSet<uint> ManaRegenList = new();
    /// <summary>
    /// A <see cref="HashSet{T}" /> list of Status IDs that stop HP regen
    /// </summary>
    private static readonly HashSet<uint> DisabledHealthRegenList = new();
    /// <summary>
    ///     A <see cref="Configuration"/> instance to be referenced across the plugin.
    /// </summary>
    public static Configuration config { get; set; } = null!;

    // DamageInfo Delegate & Hook
    private unsafe delegate void ReceiveActionEffectDelegate(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail);

    // Function that triggers when client receives a network packet with an update for nearby actors
    private unsafe delegate void ReceivePrimaryActorUpdateDelegate(uint objectId, uint* packetData, byte unkByte);

    // Different Function that triggers when client receives a network packet with an update for nearby actors
    // Seems to trigger when a regen would be active, or there's a CP/GP update
    private unsafe delegate void ReceiveSecondaryActorUpdateDelegate(uint objectId, byte* packetData, byte unkByte);

    [Signature("40 55 53 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70", DetourName = nameof(ReceiveActionEffect))]
    private readonly Hook<ReceiveActionEffectDelegate>? receiveActionEffectHook = null;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 83 3D ?? ?? ?? ?? ?? 41 0F B6 E8 48 8B DA 8B F1 0F 84 ?? ?? ?? ?? 48 89 7C 24 ??", DetourName = nameof(PrimaryActorTickUpdate))]
    private readonly Hook<ReceivePrimaryActorUpdateDelegate>? receivePrimaryActorUpdateHook = null;

    [Signature("48 8B C4 55 57 41 56 48 83 EC 60", DetourName = nameof(SecondaryActorTickUpdate))]
    private readonly Hook<ReceiveSecondaryActorUpdateDelegate>? receiveSecondaryActorUpdateHook = null;

    private readonly Utilities utilities;
    private readonly DalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly Framework framework;
    private readonly IGameGui gameGui;
    private readonly ICommandManager commandManager;
    private readonly Condition condition;
    private readonly IDataManager dataManager;
    private readonly JobGauges jobGauges;

    public string Name => "Tick Tracker";
    private const string CommandName = "/tick";
    public WindowSystem WindowSystem = new("TickTracker");
    public static DebugWindow DebugWindow { get; set; } = null!;
    private ConfigWindow ConfigWindow { get; init; }
    private HPBar HPBarWindow { get; init; }
    private MPBar MPBarWindow { get; init; }
    private GPBar GPBarWindow { get; init; }
    private bool inCombat, healTriggered, mpGainTriggered;
    private bool syncAvailable = true, nullSheet = true, finishedLoading = false;
    private double syncValue = 1;
    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    private uint lastHPValue = 0, lastMPValue = 0, lastGPValue = 0;
    private uint currentHP = 1, currentMP = 1, currentGP = 1, maxHP = 2, maxMP = 2, maxGP = 2;
    private unsafe AtkUnitBase* NameplateAddon => (AtkUnitBase*)gameGui.GetAddonByName("NamePlate");

    public Plugin(DalamudPluginInterface _pluginInterface,
        IClientState _clientState,
        Framework _framework,
        IGameGui _gameGui,
        ICommandManager _commandManager,
        Condition _condition,
        IDataManager _dataManager,
        JobGauges _jobGauges)
    {
        pluginInterface = _pluginInterface;
        clientState = _clientState;
        framework = _framework;
        gameGui = _gameGui;
        commandManager = _commandManager;
        condition = _condition;
        dataManager = _dataManager;
        jobGauges = _jobGauges;

        SignatureHelper.Initialise(this);
        if (receiveActionEffectHook is null || receivePrimaryActorUpdateHook is null || receiveSecondaryActorUpdateHook is null)
        {
            throw new Exception("Atleast one hook failed, and the plugin is not functional.");
        }
        receiveActionEffectHook.Enable();
        receivePrimaryActorUpdateHook.Enable();
        receiveSecondaryActorUpdateHook.Enable();

        utilities = new Utilities(pluginInterface, condition, dataManager, clientState);
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow(pluginInterface);
        HPBarWindow = new HPBar(clientState, utilities);
        MPBarWindow = new MPBar(clientState, utilities);
        GPBarWindow = new GPBar(clientState, utilities);
        DebugWindow = new DebugWindow();
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(HPBarWindow);
        WindowSystem.AddWindow(MPBarWindow);
        WindowSystem.AddWindow(GPBarWindow);
        WindowSystem.AddWindow(DebugWindow);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open or close Tick Tracker's config window.",
        });
        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        framework.Update += OnFrameworkUpdate;
        clientState.TerritoryChanged += TerritoryChanged;
        _ = Task.Run(InitializeLuminaSheet);
    }

    private bool PluginEnabled(bool target)
    {
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

    private void OnFrameworkUpdate(Framework framework)
    {
        if (clientState is { IsLoggedIn: false } or { IsPvPExcludingDen: true } || nullSheet) return;
        if (clientState is not { LocalPlayer: { } player }) return;

        var now = DateTime.Now.TimeOfDay.TotalSeconds;
        var HealthRegen = player.StatusList.Any(e => HealthRegenList.Contains(e.StatusId));
        var DisabledHPregen = player.StatusList.Any(e => DisabledHealthRegenList.Contains(e.StatusId));
        var ManaRegen = player.StatusList.Any(e => ManaRegenList.Contains(e.StatusId));
        var gauge = player.ClassJob.Id == 25 ? jobGauges.Get<BLMGauge>() : null;
        var DisabledMPregen = gauge is not null && gauge.InAstralFire;
        var jobType = player.ClassJob.GameData?.ClassJobCategory.Row ?? 0;
        var Enemy = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
        inCombat = condition[ConditionFlag.InCombat];
        currentHP = player.CurrentHp;
        maxHP = player.MaxHp;
        currentMP = player.CurrentMp;
        maxMP = player.MaxMp;
        currentGP = player.CurrentGp;
        maxGP = player.MaxGp;
        unsafe
        {
            if (!PluginEnabled(Enemy) || !Utilities.IsAddonReady(NameplateAddon) || !NameplateAddon->IsVisible || utilities.inCustcene())
            {
                HPBarWindow.IsOpen = false;
                MPBarWindow.IsOpen = false;
                GPBarWindow.IsOpen = false;
                return;
            }
        }
        var shouldShowHPBar = !config.HideOnFullResource ||
                            (config.AlwaysShowInCombat && inCombat) ||
                            (config.AlwaysShowWithHostileTarget && Enemy) ||
                            (config.AlwaysShowInDuties && utilities.InDuty());
        var shouldShowMPBar = !config.HideOnFullResource ||
                            (config.AlwaysShowInCombat && inCombat) ||
                            (config.AlwaysShowWithHostileTarget && Enemy) ||
                            (config.AlwaysShowInDuties && utilities.InDuty());
        HPBarWindow.IsOpen = shouldShowHPBar || (currentHP != maxHP);
        GPBarWindow.IsOpen = (jobType == 32 && (!config.HideOnFullResource || (currentGP != maxGP)) && config.GPVisible) || !config.LockBar;
        MPBarWindow.IsOpen = (jobType != 32 && (shouldShowMPBar || (currentMP != maxMP))) || !config.GPVisible;
        if (syncValue + ActorTickInterval <= now || syncAvailable)
        {
            if (syncAvailable)
            {
                syncValue = now;
                syncAvailable = false;
            }
            else
            {
                syncValue += ActorTickInterval;
            }
        }
        UpdateHPTick(now, HealthRegen, DisabledHPregen);
        UpdateMPTick(now, ManaRegen, DisabledMPregen);
        UpdateGPTick(now);
    }

    private void UpdateHPTick(double currentTime, bool hpRegen, bool regenHalt)
    {
        if (hpRegen && currentHP != maxHP && !HPBarWindow.FastTick)
        {
            HPBarWindow.FastRegenSwitch = true;
        }
        HPBarWindow.FastTick = (hpRegen && currentHP != maxHP);

        if (currentHP == maxHP)
        {
            HPBarWindow.LastTick = syncValue;
        }
        else if (lastHPValue != currentHP && !HPBarWindow.FastTick && HPBarWindow.CanUpdate)
        {
            // CanUpdate is only set on server tick, heal trigger is irrelevant
            HPBarWindow.LastTick = currentTime;
            HPBarWindow.CanUpdate = false;
            if (healTriggered)
            {
                healTriggered = false;
            }
        }
        else if (lastHPValue != currentHP && HPBarWindow.FastTick)
        {
            if (HPBarWindow.CanUpdate)
            {
                HPBarWindow.LastTick = currentTime;
                HPBarWindow.CanUpdate = false;
                if (healTriggered)
                {
                    healTriggered = false;
                }
            }
            else if (HPBarWindow.DelayedUpdate)
            {
                HPBarWindow.LastTick = currentTime;
                HPBarWindow.DelayedUpdate = false;
                if (healTriggered)
                {
                    healTriggered = false;
                }
            }
        }
        else if (finishedLoading)
        {
            // The rare case when you teleport without resource full so no assignment of LastTick causes an overflow
            HPBarWindow.LastTick = syncValue;
        }

        if (!HPBarWindow.FastTick && syncValue < HPBarWindow.LastTick && !healTriggered)
        {
            syncValue = HPBarWindow.LastTick;
        }

        HPBarWindow.RegenHalted = regenHalt;
        lastHPValue = currentHP;
    }

    private void UpdateMPTick(double currentTime, bool mpRegen, bool regenHalt)
    {
        if (mpRegen && currentMP != maxMP && !MPBarWindow.FastTick)
        {
            MPBarWindow.FastRegenSwitch = true;
        }
        MPBarWindow.FastTick = (mpRegen && currentMP != maxMP);

        if (currentMP == maxMP)
        {
            MPBarWindow.LastTick = syncValue;
        }
        else if (lastMPValue != currentMP && !MPBarWindow.FastTick && MPBarWindow.CanUpdate)
        {
            MPBarWindow.LastTick = currentTime;
            MPBarWindow.CanUpdate = false;
            if (mpGainTriggered)
            {
                mpGainTriggered = false;
            }
        }
        else if (lastMPValue != currentMP && MPBarWindow.FastTick)
        {
            if (MPBarWindow.CanUpdate)
            {
                MPBarWindow.LastTick = currentTime;
                MPBarWindow.CanUpdate = false;
                if (mpGainTriggered)
                {
                    mpGainTriggered = false;
                }
            }
            else if (MPBarWindow.DelayedUpdate)
            {
                MPBarWindow.LastTick = currentTime;
                MPBarWindow.DelayedUpdate = false;
                if (mpGainTriggered)
                {
                    mpGainTriggered = false;
                }
            }
        }
        else if (finishedLoading)
        {
            MPBarWindow.LastTick = syncValue;
        }

        if (!MPBarWindow.FastTick && syncValue < MPBarWindow.LastTick && !mpGainTriggered)
        {
            syncValue = MPBarWindow.LastTick;
        }

        MPBarWindow.RegenHalted = regenHalt;
        lastMPValue = currentMP;
    }

    private void UpdateGPTick(double currentTime)
    {
        var regenHalt = condition[ConditionFlag.Gathering];
        if (!regenHalt && GPBarWindow.RegenHalted)
        {
            GPBarWindow.LastTick = syncValue;
        }
        if (currentGP == maxGP)
        {
            GPBarWindow.LastTick = syncValue;
        }
        else if (lastGPValue != currentGP)
        {
            if (GPBarWindow.CanUpdate)
            {
                GPBarWindow.LastTick = currentTime;
                GPBarWindow.CanUpdate = false;
            }
            else if (GPBarWindow.DelayedUpdate)
            {
                GPBarWindow.LastTick = currentTime;
                GPBarWindow.DelayedUpdate = false;
            }
        }
        else if (finishedLoading)
        {
            GPBarWindow.LastTick = syncValue;
            finishedLoading = false;
        }

        GPBarWindow.RegenHalted = regenHalt;
        lastGPValue = currentGP;
    }

    private void InitializeLuminaSheet()
    {
        var statusSheet = RetrieveSheet();
        if (statusSheet is null)
        {
            return;
        }
        List<int> bannedStatus = new() { 135, 307, 751, 1419, 1465, 1730, 2326 };
        var filteredSheet = statusSheet.Where(s => !bannedStatus.Exists(rowId => rowId == s.RowId));
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount / 2,
        };
        Parallel.ForEach(filteredSheet, parallelOptions, stat =>
        {
            var text = stat.Description.ToDalamudString().TextValue;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            if (Utilities.WholeKeywordMatch(text, Utilities.RegenNullKeywords) && Utilities.WholeKeywordMatch(text, Utilities.HealthKeywords))
            {
                DisabledHealthRegenList.Add(stat.RowId);
                DebugWindow.DisabledHealthRegenDictionary.TryAdd(stat.RowId, stat.Name);
            }
            if (Utilities.WholeKeywordMatch(text, Utilities.RegenNullKeywords) && Utilities.WholeKeywordMatch(text, Utilities.ManaKeywords))
            {
                DebugWindow.DisabledManaRegenDictionary.TryAdd(stat.RowId, stat.Name);
            }
            if (Utilities.KeywordMatch(text, Utilities.RegenKeywords) && Utilities.KeywordMatch(text, Utilities.TimeKeywords))
            {
                if (Utilities.KeywordMatch(text, Utilities.HealthKeywords))
                {
                    HealthRegenList.Add(stat.RowId);
                    DebugWindow.HealthRegenDictionary.TryAdd(stat.RowId, stat.Name);
                }
                if (Utilities.KeywordMatch(text, Utilities.ManaKeywords))
                {
                    ManaRegenList.Add(stat.RowId);
                    DebugWindow.ManaRegenDictionary.TryAdd(stat.RowId, stat.Name);
                }
            }
        });
        nullSheet = false;
        PluginLog.Debug("HP regen list generated with {HPcount} status effects.", HealthRegenList.Count);
        PluginLog.Debug("MP regen list generated with {MPcount} status effects.", ManaRegenList.Count);
    }

    private ExcelSheet<Lumina.Excel.GeneratedSheets.Status>? RetrieveSheet()
    {
        try
        {
            var sheet = dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>(Dalamud.ClientLanguage.English);
            if (sheet is null)
            {
                nullSheet = true;
                PluginLog.Fatal("Invalid lumina sheet!");
                return null;
            }
            return sheet;
        }
        catch (Exception e)
        {
            nullSheet = true;
            PluginLog.Fatal("Retrieving lumina sheet failed!");
            PluginLog.Fatal(e.Message);
            return null;
        }
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

            var name = MemoryHelper.ReadStringNullTerminated((nint)sourceCharacter->GameObject.GetName());
            var castTarget = sourceCharacter->GetCastInfo()->CastTargetID;
            var target = sourceCharacter->GetTargetId();

            if (target != player.OwnerId && castTarget != player.OwnerId && target != player.ObjectId && castTarget != player.ObjectId)
            {
                return;
            }
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
                    healTriggered = true;
                }
                else if (effectArray[i].type == ActionEffectType.MpGain)
                {
                    mpGainTriggered = true;
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "An error has occured with the ReceiveActionEffect detour.");
        }
    }

    /// <summary>
    /// This detour function is triggered every time the client receives
    /// a network packet containing an update for the nearby actors
    /// with hp, mana, gp
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
            HPBarWindow.CanUpdate = true;
            MPBarWindow.CanUpdate = true;
            GPBarWindow.CanUpdate = true;
            syncAvailable = true;
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "An error has occured with the PrimaryActorTickUpdate detour.");
        }
    }

    private unsafe void SecondaryActorTickUpdate(uint objectId, byte* packetData, byte unkByte)
    {
        receiveSecondaryActorUpdateHook!.Original(objectId, packetData, unkByte);
        try
        {
            if (clientState is not { LocalPlayer: { } player })
            {
                return;
            }
#if DEBUG
            var unk1 = *((uint*)packetData + 1); // HP without buffs?
            var unk2 = *((ushort*)packetData + 6); // Current resource, MP or GP or CP
            var unk3 = *((ushort*)packetData + 7); // Seems to be the maximum MP / GP / CP respective to the current job
            var unk4 = *((uint*)packetData + 2); // Max HP?
#endif
            if (objectId != player.ObjectId)
            {
                return;
            }
            HPBarWindow.DelayedUpdate = true;
            MPBarWindow.DelayedUpdate = true;
            GPBarWindow.DelayedUpdate = true;
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "An error has occured with the SecondaryActorTickUpdate detour.");
        }
    }

    private async void Loading(int pollingPeriod)
    {
        var timer = new Stopwatch();
        var loading = condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
        timer.Start();
        while (loading)
        {
            if (timer.ElapsedMilliseconds <= pollingPeriod)
            {
                var remainingTime = pollingPeriod - (int)timer.ElapsedMilliseconds;
                await Task.Delay(remainingTime).ConfigureAwait(false);
                timer.Restart();
            }
            loading = condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
            if (!loading)
            {
                timer.Reset();
                break;
            }
        }
        finishedLoading = true;
    }

    private void TerritoryChanged(object? sender, ushort e)
    {
        finishedLoading = false;
        _ = Task.Run(() => Loading(1000));
    }

    public void Dispose()
    {
        receiveActionEffectHook?.Disable();
        receiveActionEffectHook?.Dispose();
        receivePrimaryActorUpdateHook?.Disable();
        receivePrimaryActorUpdateHook?.Dispose();
        receiveSecondaryActorUpdateHook?.Disable();
        receiveSecondaryActorUpdateHook?.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        DebugWindow.Dispose();
        pluginInterface.UiBuilder.Draw -= DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        commandManager.RemoveHandler(CommandName);
        framework.Update -= OnFrameworkUpdate;
        clientState.TerritoryChanged -= TerritoryChanged;
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
