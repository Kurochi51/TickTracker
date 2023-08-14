using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Logging;
using Dalamud.Utility;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TickTracker.Windows;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace TickTracker;

public sealed unsafe class Plugin : IDalamudPlugin
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
    /// A <see cref="HashSet{T}" /> list of Status IDs that stop MP regen
    /// </summary>
    private static readonly HashSet<uint> DisabledManaRegenList = new();
    /// <summary>
    ///     A <see cref="Configuration"/> instance to be referenced across the plugin.
    /// </summary>
    public static Configuration config { get; set; } = null!;

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
    private static AtkUnitBase* NameplateAddon => (AtkUnitBase*)Service.GameGui.GetAddonByName("NamePlate");

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        config = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow(this);
        HPBarWindow = new HPBar();
        MPBarWindow = new MPBar();
        DebugWindow = new DebugWindow();
        WindowSystem.AddWindow(DebugWindow);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(HPBarWindow);
        WindowSystem.AddWindow(MPBarWindow);


        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open or close Tick Tracker's config window.",
        });
        Service.PluginInterface.UiBuilder.Draw += DrawUI;
        Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        Service.Framework.Update += OnFrameworkUpdate;
        Service.ClientState.TerritoryChanged += TerritoryChanged;
        _ = Task.Run(InitializeLuminaSheet);
    }

    private bool PluginEnabled(bool target)
    {
        if (Service.Condition[ConditionFlag.InDuelingArea])
        {
            return false;
        }
        if (config.HideOutOfCombat && !inCombat)
        {
            var showingBecauseInDuty = config.AlwaysShowInDuties && Utilities.InDuty();
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
        if (Service.ClientState is { IsLoggedIn: false } or { IsPvPExcludingDen: true } || nullSheet)
        {
            return;
        }
        bool HealthRegen;
        bool DisabledHPregen;
        bool ManaRegen;
        bool DisabledMPregen;
        bool Enemy;
        if (Service.ClientState is { LocalPlayer: { } player })
        {
            HealthRegen = player.StatusList.Any(e => HealthRegenList.Contains(e.StatusId));
            DisabledHPregen = player.StatusList.Any(e => DisabledHealthRegenList.Contains(e.StatusId));
            ManaRegen = player.StatusList.Any(e => ManaRegenList.Contains(e.StatusId));
            DisabledMPregen = player.StatusList.Any(e => DisabledManaRegenList.Contains(e.StatusId));
            Enemy = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
            inCombat = Service.Condition[ConditionFlag.InCombat];
            currentHP = player.CurrentHp;
            maxHP = player.MaxHp;
            currentMP = player.CurrentMp;
            maxMP = player.MaxMp;
        }
        else
        {
            return;
        }
        if (!PluginEnabled(Enemy) || !Utilities.IsAddonReady(NameplateAddon) || !NameplateAddon->IsVisible || Utilities.inCustcene())
        {
            HPBarWindow.IsOpen = false;
            MPBarWindow.IsOpen = false;
            return;
        }
        var shouldShowHPBar = !config.HideOnFullResource ||
                            (config.AlwaysShowInCombat && inCombat) ||
                            (config.AlwaysShowWithHostileTarget && Enemy) ||
                            (config.AlwaysShowInDuties && Utilities.InDuty());
        var shouldShowMPBar = !config.HideOnFullResource ||
                            (config.AlwaysShowInCombat && inCombat) ||
                            (config.AlwaysShowWithHostileTarget && Enemy) ||
                            (config.AlwaysShowInDuties && Utilities.InDuty());
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
        else if (HPBarWindow.LastTick + (HPBarWindow.FastTick ? FastTickInterval : ActorTickInterval) <= currentTime && !regenHalt)
        {
            HPBarWindow.LastTick += HPBarWindow.FastTick ? FastTickInterval : ActorTickInterval;
        }
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
        else if (MPBarWindow.LastTick + (MPBarWindow.FastTick ? FastTickInterval : ActorTickInterval) <= currentTime && !regenHalt)
        {
            MPBarWindow.LastTick += MPBarWindow.FastTick ? FastTickInterval : ActorTickInterval;
        }
        lastMPValue = (int)currentMP;
        MPBarWindow.UpdateAvailable = true;
    }

    private void InitializeLuminaSheet()
    {
        ExcelSheet<Status>? sheet;
        try
        {
            var statusSheet = Service.DataManager.GetExcelSheet<Status>(Dalamud.ClientLanguage.English);
            if (statusSheet == null)
            {
                nullSheet = true;
                PluginLog.Error("Invalid lumina sheet!");
                return;
            }
            sheet = statusSheet;
        }
        catch (Exception e)
        {
            nullSheet = true;
            PluginLog.Error("Retrieving lumina sheet failed! Exception: {e}", e.Message);
            return;
        }
        List<int> bannedStatus = new() { 135, 307, 1419, 1465, 1730, 2326 };
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
                DisabledManaRegenList.Add(stat.RowId);
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
        Service.PluginInterface.UiBuilder.Draw -= DrawUI;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        Service.CommandManager.RemoveHandler(CommandName);
        Service.Framework.Update -= OnFrameworkUpdate;
        Service.ClientState.TerritoryChanged -= TerritoryChanged;
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
