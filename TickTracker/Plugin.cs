using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Lumina.Excel;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Logging;
using Dalamud.Utility;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.JobGauge;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using TickTracker.Windows;
using System.Diagnostics;

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

    //DamageInfo Delegate & Hook
    private unsafe delegate void ReceiveActionEffectDelegate(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail);
    private readonly Hook<ReceiveActionEffectDelegate> receiveActionEffectHook;

    private readonly DalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly Framework framework;
    private readonly IGameGui gameGui;
    private readonly ICommandManager commandManager;
    private readonly Condition condition;
    private readonly IDataManager dataManager;
    private readonly Utilities utilities;
    private readonly JobGauges jobGauges;
    private readonly ISigScanner sigScanner;

    public string Name => "Tick Tracker";
    private const string CommandName = "/tick";
    public WindowSystem WindowSystem = new("TickTracker");
    private ConfigWindow ConfigWindow { get; init; }
    private HPBar HPBarWindow { get; init; }
    private MPBar MPBarWindow { get; init; }
    public static DebugWindow DebugWindow { get; set; } = null!;
    private bool inCombat, nullSheet = true, healTriggered;
    private double syncValue = 1;
    private int lastHPValue = -1, lastMPValue = -1;
    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    private uint currentHP = 1, currentMP = 1, maxHP = 2, maxMP = 2;
    private readonly Stopwatch sw;
    private unsafe AtkUnitBase* NameplateAddon => (AtkUnitBase*)gameGui.GetAddonByName("NamePlate");

    public Plugin(DalamudPluginInterface _pluginInterface,
        IClientState _clientState,
        Framework _framework,
        IGameGui _gameGui,
        ICommandManager _commandManager,
        Condition _condition,
        IDataManager _dataManager,
        JobGauges _jobGauges,
        ISigScanner _scanner)
    {
        pluginInterface = _pluginInterface;
        clientState = _clientState;
        framework = _framework;
        gameGui = _gameGui;
        commandManager = _commandManager;
        condition = _condition;
        dataManager = _dataManager;
        jobGauges = _jobGauges;
        sigScanner = _scanner;
        sw = new Stopwatch();
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
        try
        {
            unsafe
            {
                // DamageInfo sig
                var receiveActionEffectFuncPtr = sigScanner.ScanText("40 55 53 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70");
                receiveActionEffectHook = Hook<ReceiveActionEffectDelegate>.FromAddress(receiveActionEffectFuncPtr, ReceiveActionEffect);
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e,"Plugin could not be initialized.");
            receiveActionEffectHook?.Disable();
            receiveActionEffectHook?.Dispose();
            commandManager.RemoveHandler(CommandName);
            WindowSystem.RemoveAllWindows();
            ConfigWindow.Dispose();
            DebugWindow.Dispose();
            throw;
        }
        receiveActionEffectHook.Enable();
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
        if (((lastHPValue < currentHP && !HPBarWindow.FastTick) || (lastMPValue < currentMP && !MPBarWindow.FastTick)))
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
        // need to figure out how to handle healTriggered
        // right now I don't want to wait until the next action happens to set it to false
        // but I can't set it false the very first time I encounter it

        // maybe go further up the chain, and never assign current hp if heal triggered?
        if (healTriggered)
        {
            return;
        }
        
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

        if (!HPBarWindow.FastTick && syncValue < HPBarWindow.LastTick)
        {
            syncValue = HPBarWindow.LastTick;
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

        if (!MPBarWindow.FastTick)
        {
            syncValue = MPBarWindow.LastTick;
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
            PluginLog.Fatal("Retrieving lumina sheet failed!");
            PluginLog.Fatal(e.Message);
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

    // DamageInfo stripped function
    private unsafe void ReceiveActionEffect(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail)
    {
        //healTriggered = false;
        try
        {
            PlayerCharacter? player;
            // Can this even be called if LocalPlayer isn't set? who knows, better safe than sorry
            if (clientState is { LocalPlayer: { } character })
            {
                player = character;
            }
            else
            {
                receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
                return;
            }

            var name = MemoryHelper.ReadStringNullTerminated((nint)sourceCharacter->GameObject.GetName());
            var castTarget = sourceCharacter->GetCastInfo()->CastTargetID;
            var target = sourceCharacter->GetTargetId();
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
                if (effectArray[i].type != Enum.ActionEffectType.Heal)
                {
                    continue;
                }
                if (sourceId == player.ObjectId && (target == player.OwnerId || castTarget == player.OwnerId))
                {
                    PluginLog.Debug("Self-healing.");
                    healTriggered = true;
                }
                else if (target == player.ObjectId || castTarget == player.ObjectId)
                {
                    PluginLog.Debug("Healed by {n}", name);
                    healTriggered = true;
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "An error has occured with the delegate.");
        }

        receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
    }

    private void TerritoryChanged(object? sender, ushort e)
    {
        lastHPValue = -1;
        lastMPValue = -1;
    }

    public void Dispose()
    {
        receiveActionEffectHook?.Disable();
        receiveActionEffectHook?.Dispose();
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
