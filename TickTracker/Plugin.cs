using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Hooking;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Config;
using Dalamud.Game.Command;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using TickTracker.Windows;
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
    /// A <see cref="HashSet{T}" /> of Status IDs that trigger HP regen
    /// </summary>
    private HashSet<uint> healthRegenSet = new();
    /// <summary>
    /// A <see cref="HashSet{T}" /> of Status IDs that trigger MP regen
    /// </summary>
    private HashSet<uint> manaRegenSet = new();
    /// <summary>
    /// A <see cref="HashSet{T}" /> of Status IDs that stop HP regen
    /// </summary>
    private HashSet<uint> disabledHealthRegenSet = new();

    // Function that triggers when client receives a network packet with an update for nearby actors
    private unsafe delegate void ReceiveActorUpdateDelegate(uint objectId, uint* packetData, byte unkByte);

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 83 3D ?? ?? ?? ?? ?? 41 0F B6 E8 48 8B DA 8B F1 0F 84 ?? ?? ?? ?? 48 89 7C 24 ??", DetourName = nameof(ActorTickUpdate))]
    private readonly Hook<ReceiveActorUpdateDelegate>? receiveActorUpdateHook = null;

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
    private readonly IGameConfig gameConfig;
    private readonly IAddonLifecycle addonLifecycle;

    private ConfigWindow ConfigWindow { get; set; } = null!;
    private DebugWindow DebugWindow { get; set; } = null!;
    private HPBar HPBarWindow { get; set; } = null!;
    private MPBar MPBarWindow { get; set; } = null!;
    private GPBar GPBarWindow { get; set; } = null!;
#if DEBUG
    public static DevWindow DevWindow { get; set; } = null!;
#endif
    public static Vector2 Resolution { get; private set; }

    public WindowSystem WindowSystem { get; } = new("TickTracker");
    private const string CommandName = "/tick";

    private const float RegularTickInterval = 3, FastTickInterval = 1.5f;
    private const uint DiscipleOfTheLand = 32, PugilistID = 2, LancerID = 4, ArcherID = 5;
    private const byte NonCombatJob = 0, MeleeDPS = 3, PhysRangedDPS = 4;

    private readonly List<BarWindowBase> barWindows;
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
        IGameConfig _gameConfig,
        IAddonLifecycle _addonLifecycle,
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
        gameConfig = _gameConfig;
        addonLifecycle = _addonLifecycle;

        _interopProvider.InitializeFromAttributes(this);

        if (receiveActorUpdateHook is null)
        {
            throw new NotSupportedException("Hook not found in current game version. The plugin is non functional.");
        }
        receiveActorUpdateHook.Enable();

        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        utilities = new Utilities(pluginInterface, config, condition, _dataManager, clientState, log);

        InitializeWindows();

        var barWindowList = new List<BarWindowBase>();
        foreach (var window in WindowSystem.Windows.OfType<BarWindowBase>())
        {
            barWindowList.Add(window);
        }
        barWindows = barWindowList.Count > 0 ? barWindowList : Enumerable.Empty<BarWindowBase>().ToList();

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open or close Tick Tracker's config window.",
        });
        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        framework.Update += OnFrameworkUpdate;
        clientState.TerritoryChanged += TerritoryChanged;
        gameConfig.SystemChanged += CheckResolutionChange;
        addonLifecycle.RegisterListener(AddonEvent.PostUpdate, addonsLookup, CheckBarCollision);

        _ = Task.Run(InitializeLuminaSheet);
        InitializeResolution();
    }

    private void InitializeWindows()
    {
        DebugWindow = new DebugWindow(pluginInterface);
        ConfigWindow = new ConfigWindow(pluginInterface, config, DebugWindow);
        HPBarWindow = new HPBar(clientState, log, utilities, config);
        MPBarWindow = new MPBar(clientState, log, utilities, config);
        GPBarWindow = new GPBar(clientState, log, utilities, config);
#if DEBUG
        DevWindow = new DevWindow();
        WindowSystem.AddWindow(DevWindow);
#endif
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(HPBarWindow);
        WindowSystem.AddWindow(MPBarWindow);
        WindowSystem.AddWindow(GPBarWindow);
        WindowSystem.AddWindow(DebugWindow);
    }

    /// <summary>
    ///     Grab the resolution from the <see cref="Device.SwapChain"/> on plugin init, and propagate it as the <see cref="Window.WindowSizeConstraints.MaximumSize"/>
    ///     to the appropriate windows.
    /// </summary>
    private unsafe void InitializeResolution()
    {
        Resolution = new Vector2(Device.Instance()->SwapChain->Width, Device.Instance()->SwapChain->Height);
        Utilities.ChangeWindowConstraints(DebugWindow, Resolution);
#if DEBUG
        Utilities.ChangeWindowConstraints(DevWindow, Resolution);
#endif
    }

    private void OnFrameworkUpdate(IFramework _framework)
    {
#if DEBUG
        DevWindowThings(clientState?.LocalPlayer, (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds, HPBarWindow);
#endif
        if (clientState is { IsLoggedIn: false } or { IsPvPExcludingDen: true } || nullSheet)
        {
            return;
        }
        if (clientState is not { LocalPlayer: { } player })
        {
            return;
        }

        unsafe
        {
            if (!PluginEnabled(player) || !utilities.IsAddonReady(NameplateAddon))
            {
                HPBarWindow.IsOpen = MPBarWindow.IsOpen = GPBarWindow.IsOpen = false;
                return;
            }
            if (!NameplateAddon->IsVisible || utilities.InCustcene() || player.IsDead)
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
        else if (syncValue + RegularTickInterval <= now)
        {
            syncValue += RegularTickInterval;
        }
        if (fastValue + FastTickInterval <= now)
        {
            fastValue += FastTickInterval;
        }
        if (regenValue + RegularTickInterval <= now)
        {
            regenValue += RegularTickInterval;
        }
        if (loadingTask is { IsCompleted: true })
        {
            finishedLoading = true;
            loadingTask = null;
        }
        ProcessTicks(now, player);
    }

    private void ProcessTicks(double currentTime, PlayerCharacter player)
    {
        // HP section
        HPBarWindow.RegenActive = player.StatusList.Any(e => healthRegenSet.Contains(e.StatusId));
        HPBarWindow.TickHalted = player.StatusList.Any(e => disabledHealthRegenSet.Contains(e.StatusId));
        var currentHP = player.CurrentHp;
        var fullHP = currentHP == player.MaxHp;

        // MP Section
        MPBarWindow.RegenActive = player.StatusList.Any(e => manaRegenSet.Contains(e.StatusId));
        var blmGauge = player.ClassJob.Id == 25 ? jobGauges.Get<BLMGauge>() : null;
        MPBarWindow.TickHalted = blmGauge is not null && blmGauge.InAstralFire;
        var currentMP = player.CurrentMp;
        var fullMP = currentMP == player.MaxMp;

        // GP Section
        GPBarWindow.TickHalted = condition[ConditionFlag.Gathering];
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
        if (window.RegenActive)
        {
            window.Tick = fullResource ? regenValue : fastValue;
            window.FastRegen = !fullResource;
            window.Progress = (currentTime - window.Tick) / (fullResource ? RegularTickInterval : FastTickInterval);
            return;
        }
        if (fullResource || finishedLoading || window.TickHalted)
        {
            window.Tick = syncValue;
        }
        else if (lastResource != currentResource && window.TickUpdate)
        {
            window.Tick = currentTime;
            window.TickUpdate = false;
        }
        window.Progress = (currentTime - window.Tick) / RegularTickInterval;
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
        var disabledHealthRegenBag = new ConcurrentBag<uint>();
        var healthRegenBag = new ConcurrentBag<uint>();
        var manaRegenBag = new ConcurrentBag<uint>();
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
                disabledHealthRegenBag.Add(stat.RowId);
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
                    healthRegenBag.Add(stat.RowId);
                    DebugWindow.HealthRegenDictionary.TryAdd(stat.RowId, stat.Name);
                }
                if (Utilities.KeywordMatch(text, utilities.ManaKeywords))
                {
                    manaRegenBag.Add(stat.RowId);
                    DebugWindow.ManaRegenDictionary.TryAdd(stat.RowId, stat.Name);
                }
            }
        });
        healthRegenSet = healthRegenBag.ToHashSet();
        manaRegenSet = manaRegenBag.ToHashSet();
        disabledHealthRegenSet = disabledHealthRegenBag.ToHashSet();
        nullSheet = false;
        log.Debug("HP regen list generated with {HPcount} status effects.", healthRegenSet.Count);
        log.Debug("MP regen list generated with {MPcount} status effects.", manaRegenSet.Count);
    }

    /// <summary>
    /// This detour function is triggered every time the client receives
    /// a network packet containing an update for the nearby actors
    /// with HP, MP, GP
    /// </summary>
    /// HP = *(int*)packetData;
    /// MP = *((ushort*)packetData + 2);
    /// GP = *((short*)packetData + 3); // Goes up to 10000 and is tracked and updated at all times
    private unsafe void ActorTickUpdate(uint objectId, uint* packetData, byte unkByte)
    {
        receiveActorUpdateHook!.Original(objectId, packetData, unkByte);
        try
        {
            if (clientState is not { LocalPlayer: { } player })
            {
                return;
            }
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
        return true;
    }

    private unsafe void UpdateBarState(PlayerCharacter player)
    {
        var jobType = player.ClassJob.GameData?.ClassJobCategory.Row ?? 0;
        var jobID = player.ClassJob.Id;
        var altJobType = player.ClassJob.GameData?.Unknown44 ?? 0;
        var Enemy = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
        var inCombat = condition[ConditionFlag.InCombat];
        var shouldShowHPBar = !config.HideOnFullResource ||
                            (config.AlwaysShowInCombat && inCombat) ||
                            (config.AlwaysShowWithHostileTarget && Enemy) ||
                            (config.AlwaysShowInDuties && utilities.InDuty()) || player.CurrentHp != player.MaxHp;
        var shouldShowMPBar = !config.HideOnFullResource ||
                            (config.AlwaysShowInCombat && inCombat) ||
                            (config.AlwaysShowWithHostileTarget && Enemy) ||
                            (config.AlwaysShowInDuties && utilities.InDuty()) || player.CurrentMp != player.MaxMp;
        var hideForMeleeRangedDPS = (altJobType is MeleeDPS or PhysRangedDPS || (altJobType is NonCombatJob && jobID is PugilistID or LancerID or ArcherID)) && config.HideMpBarOnMeleeRanged;
        var hideForGPBar = jobType is DiscipleOfTheLand && config.GPVisible;
        HPBarWindow.IsOpen = shouldShowHPBar || !config.LockBar;
        MPBarWindow.IsOpen = (shouldShowMPBar && !hideForMeleeRangedDPS && !hideForGPBar) || !config.LockBar;
        GPBarWindow.IsOpen = (jobType is DiscipleOfTheLand && (!config.HideOnFullResource || (player.CurrentGp != player.MaxGp)) && config.GPVisible) || !config.LockBar;
    }

    private void TerritoryChanged(ushort e)
    {
        loadingTask = Task.Run(async () => await utilities.Loading(1000).ConfigureAwait(false));
    }

    /// <summary>
    ///     Retrieve the resolution from the <see cref="Device.SwapChain"/> on resolution or screen mode changes.
    /// </summary>
    private unsafe void CheckResolutionChange(object? sender, ConfigChangeEvent e)
    {
        var configOption = e.Option.ToString();
        if (configOption is "FullScreenWidth" or "FullScreenHeight" or "ScreenWidth" or "ScreenHeight" or "ScreenMode")
        {
            Resolution = new Vector2(Device.Instance()->SwapChain->Width, Device.Instance()->SwapChain->Height);
        }
        Utilities.ChangeWindowConstraints(DebugWindow, Resolution);
#if DEBUG
        Utilities.ChangeWindowConstraints(DevWindow, Resolution);
#endif
    }

    /// <summary>
    ///     Delegate used to hide a <see cref="BarWindowBase"/> window if there's an overlap in-between it
    ///     and the addon that triggered the function.
    /// </summary>
    private unsafe void CheckBarCollision(AddonEvent type, AddonArgs args)
    {
        if (!config.CollisionDetection || (config.DisableCollisionInCombat && condition[ConditionFlag.InCombat]) || !config.LockBar)
        {
            return;
        }
        var currentAddon = (AtkUnitBase*)args.Addon;
        if (!currentAddon->IsVisible)
        {
            return;
        }
        var scaled = (int)currentAddon->Scale != 100;
        foreach (var barWindow in barWindows.Where(window => utilities.AddonOverlap(currentAddon, window, scaled)))
        {
            barWindow.IsOpen = false;
        }
    }

#if DEBUG
    private unsafe void DevWindowThings(PlayerCharacter? player, double currentTime, BarWindowBase window)
    {
        DevWindow.IsOpen = true;
        var cultureFormat = System.Globalization.CultureInfo.InvariantCulture;
        if (player is not null)
        {
            DevWindow.Print(window.WindowName + ": " + player.CurrentHp.ToString() + " / " + player.MaxHp.ToString());
        }
        DevWindow.Print("Current Time: " + currentTime.ToString(cultureFormat));
        DevWindow.Print("RegenActive: " + window.RegenActive.ToString());
        DevWindow.Print("Progress: " + window.Progress.ToString(cultureFormat));
        DevWindow.Print("NormalTick: " + window.Tick.ToString(cultureFormat));
        DevWindow.Print("NormalUpdate: " + window.TickUpdate.ToString());
        DevWindow.Print("Sync Value: " + syncValue.ToString(cultureFormat));
        DevWindow.Print("Regen Value: " + regenValue.ToString(cultureFormat));
        DevWindow.Print("Fast Value: " + fastValue.ToString(cultureFormat));
        DevWindow.Print("Swapchain resolution: " + Resolution.X.ToString(cultureFormat) + "x" + Resolution.Y.ToString(cultureFormat));
    }
#endif

    public void Dispose()
    {
        receiveActorUpdateHook?.Disable();
        receiveActorUpdateHook?.Dispose();
        commandManager.RemoveHandler(CommandName);
        DebugWindow.Dispose();
        WindowSystem.RemoveAllWindows();
        framework.Update -= OnFrameworkUpdate;
        addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, addonsLookup, CheckBarCollision);
        pluginInterface.UiBuilder.Draw -= DrawUI;
        clientState.TerritoryChanged -= TerritoryChanged;
        gameConfig.SystemChanged -= CheckResolutionChange;
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
