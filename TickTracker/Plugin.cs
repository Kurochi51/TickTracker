using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Lumina.Excel;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Logging;
using Dalamud.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.JobGauge;
using Dalamud.Game.ClientState.JobGauge.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TickTracker.Windows;

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

    private readonly DalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly Framework framework;
    private readonly IGameGui gameGui;
    private readonly ICommandManager commandManager;
    private readonly Condition condition;
    private readonly IDataManager dataManager;
    private readonly Utilities utilities;
    private readonly JobGauges jobGauges;

    public string Name => "Tick Tracker";
    private const string CommandName = "/tick";
    public WindowSystem WindowSystem = new("TickTracker");
    private ConfigWindow ConfigWindow { get; init; }
    private HPBar HPBarWindow { get; init; }
    private MPBar MPBarWindow { get; init; }
    public static DebugWindow DebugWindow { get; set; } = null!;
    private bool inCombat, nullSheet = true;
    private double syncValue = 1;
    private int lastHPValue = -1, lastMPValue = -1;
    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    private uint currentHP = 1, currentMP = 1, maxHP = 2, maxMP = 2;
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
        utilities = new Utilities(pluginInterface, condition, dataManager, clientState);
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow(pluginInterface);
        HPBarWindow = new HPBar(clientState, utilities);
        MPBarWindow = new MPBar(clientState, utilities);
        DebugWindow = new DebugWindow();
        WindowSystem.AddWindow(DebugWindow);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(HPBarWindow);
        WindowSystem.AddWindow(MPBarWindow);

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
        var now = DateTime.Now.TimeOfDay.TotalSeconds;
        if (clientState is { IsLoggedIn: false } or { IsPvPExcludingDen: true } || nullSheet)
        {
            return;
        }
        bool HealthRegen, DisabledHPregen, ManaRegen, DisabledMPregen, Enemy;
        if (clientState is { LocalPlayer: { } player })
        {
            HealthRegen = player.StatusList.Any(e => HealthRegenList.Contains(e.StatusId));
            DisabledHPregen = player.StatusList.Any(e => DisabledHealthRegenList.Contains(e.StatusId));
            ManaRegen = player.StatusList.Any(e => ManaRegenList.Contains(e.StatusId));
            DisabledMPregen = false;
            if (player.ClassJob.Id == 25)
            {
                var gauge = jobGauges.Get<BLMGauge>();
                DisabledMPregen = gauge.InAstralFire;
            }
            Enemy = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
            inCombat = condition[ConditionFlag.InCombat];
            currentHP = player.CurrentHp;
            maxHP = player.MaxHp;
            currentMP = player.CurrentMp;
            maxMP = player.MaxMp;
        }
        else
        {
            return;
        }
        unsafe
        {
            if (!PluginEnabled(Enemy) || !Utilities.IsAddonReady(NameplateAddon) || !NameplateAddon->IsVisible || utilities.inCustcene())
            {
                HPBarWindow.IsOpen = false;
                MPBarWindow.IsOpen = false;
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
        MPBarWindow.IsOpen = shouldShowMPBar || (currentMP != maxMP);
        if ((lastHPValue < currentHP && !HPBarWindow.FastTick) || (lastMPValue < currentMP && !MPBarWindow.FastTick))
        {
            syncValue = now;
        }
        UpdateHPTick(now, HealthRegen, DisabledHPregen);
        UpdateMPTick(now, ManaRegen, DisabledMPregen);
        if (syncValue + ActorTickInterval <= now)
        {
            syncValue += ActorTickInterval;
        }
    }

    private void UpdateHPTick(double currentTime, bool hpRegen, bool regenHalt)
    {
        HPBarWindow.FastTick = (hpRegen && currentHP != maxHP);
        if (currentHP == maxHP)
        {
            HPBarWindow.LastTick = syncValue;
        }
        else if (lastHPValue < currentHP)
        {
            HPBarWindow.LastTick = currentTime;
        }
        else if (HPBarWindow.LastTick + (HPBarWindow.FastTick ? FastTickInterval : ActorTickInterval) <= currentTime)
        {
            HPBarWindow.LastTick += HPBarWindow.FastTick ? FastTickInterval : ActorTickInterval;
        }
        HPBarWindow.RegenHalted = regenHalt;
        lastHPValue = (int)currentHP;
        HPBarWindow.UpdateAvailable = true;
    }

    private void UpdateMPTick(double currentTime, bool mpRegen, bool regenHalt)
    {
        MPBarWindow.FastTick = (mpRegen && currentMP != maxMP);
        if (currentMP == maxMP)
        {
            MPBarWindow.LastTick = syncValue;
        }
        else if (lastMPValue < currentMP)
        {
            MPBarWindow.LastTick = currentTime;
        }
        else if (MPBarWindow.LastTick + (MPBarWindow.FastTick ? FastTickInterval : ActorTickInterval) <= currentTime)
        {
            MPBarWindow.LastTick += MPBarWindow.FastTick ? FastTickInterval : ActorTickInterval;
        }
        MPBarWindow.RegenHalted = regenHalt;
        lastMPValue = (int)currentMP;
        MPBarWindow.UpdateAvailable = true;
    }

    private void InitializeLuminaSheet()
    {
        ExcelSheet<Lumina.Excel.GeneratedSheets.Status>? sheet;
        try
        {
            var statusSheet = dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>(Dalamud.ClientLanguage.English);
            if (statusSheet == null)
            {
                nullSheet = true;
                PluginLog.Fatal("Invalid lumina sheet!");
                return;
            }
            sheet = statusSheet;
        }
        catch (Exception e)
        {
            nullSheet = true;
            PluginLog.Fatal("Retrieving lumina sheet failed! Exception: {e}", e.Message);
            return;
        }
        List<int> bannedStatus = new() { 135, 307, 751, 1419, 1465, 1730, 2326 };
        var filteredSheet = sheet.Where(s => !bannedStatus.Exists(rowId => rowId == s.RowId));
        foreach (var stat in filteredSheet)
        {
            var text = stat.Description.ToDalamudString().TextValue;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }
            if (Utilities.WholeKeywordMatch(text, Utilities.RegenNullKeywords) && Utilities.WholeKeywordMatch(text, Utilities.HealthKeywords))
            {
                DisabledHealthRegenList.Add(stat.RowId);
                DebugWindow.DisabledHealthRegenDictionary.Add(stat.RowId, stat.Name);
            }
            if (Utilities.WholeKeywordMatch(text, Utilities.RegenNullKeywords) && Utilities.WholeKeywordMatch(text, Utilities.ManaKeywords))
            {
                // Since only Astral Fire meets the criteria, and Astral Fire isn't a status effect anymore, it's unnecessary to store it in a HashSet.
                // However I'd like to keep it in the dictionary for clarity.
                DebugWindow.DisabledManaRegenDictionary.Add(stat.RowId, stat.Name);
            }
            if (Utilities.KeywordMatch(text, Utilities.RegenKeywords) && Utilities.KeywordMatch(text, Utilities.TimeKeywords))
            {
                if (Utilities.KeywordMatch(text, Utilities.HealthKeywords))
                {
                    HealthRegenList.Add(stat.RowId);
                    DebugWindow.HealthRegenDictionary.Add(stat.RowId, stat.Name);
                }
                if (Utilities.KeywordMatch(text, Utilities.ManaKeywords))
                {
                    ManaRegenList.Add(stat.RowId);
                    DebugWindow.ManaRegenDictionary.Add(stat.RowId, stat.Name);
                }
            }
        }
        nullSheet = false;
        PluginLog.Debug("HP regen list generated with {HPcount} status effects.", HealthRegenList.Count);
        PluginLog.Debug("MP regen list generated with {MPcount} status effects.", ManaRegenList.Count);
    }

    private void TerritoryChanged(object? sender, ushort e)
    {
        lastHPValue = -1;
        lastMPValue = -1;
    }

    public void Dispose()
    {
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
