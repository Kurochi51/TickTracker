using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Lumina.Excel;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Hooking;
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
    private readonly Hook<ReceiveActionEffectDelegate> receiveActionEffectHook;

    // Function that triggers when client receives a network packet with an update for nearby actors
    private unsafe delegate void ReceiveActorUpdateDelegate(uint objectId, uint* packetData, byte unkByte);
    private readonly Hook<ReceiveActorUpdateDelegate> receiveActorUpdateHook;

#if DEBUG
    private unsafe delegate void AltReceiveActorUpdateDelegate(uint objectId, byte* packetData, byte unkByte);
    private readonly Hook<AltReceiveActorUpdateDelegate> altReceiveActorUpdateHook;
#endif

    private readonly Utilities utilities;
    private readonly DalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly Framework framework;
    private readonly IGameGui gameGui;
    private readonly ICommandManager commandManager;
    private readonly Condition condition;
    private readonly IDataManager dataManager;
    private readonly JobGauges jobGauges;
    private readonly ISigScanner sigScanner;
    private readonly IPluginLog log;
    private readonly unsafe CharacterManager* characterManager;
#if DEBUG
    private readonly Stopwatch sw;
#endif

    public string Name => "Tick Tracker";
    private const string CommandName = "/tick";
    public WindowSystem WindowSystem = new("TickTracker");
    public static DebugWindow DebugWindow { get; set; } = null!;
    private ConfigWindow ConfigWindow { get; init; }
    private HPBar HPBarWindow { get; init; }
    private MPBar MPBarWindow { get; init; }
    private bool inCombat, nullSheet = true, healTriggered, syncAvailable = true;
    private double syncValue = 1;
    private int lastHPValue = -1, lastMPValue = -1;
    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    private uint currentHP = 1, currentMP = 1, maxHP = 2, maxMP = 2;
    private unsafe AtkUnitBase* NameplateAddon => (AtkUnitBase*)gameGui.GetAddonByName("NamePlate");

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long")]
    public Plugin(DalamudPluginInterface _pluginInterface,
        IClientState _clientState,
        Framework _framework,
        IGameGui _gameGui,
        ICommandManager _commandManager,
        Condition _condition,
        IDataManager _dataManager,
        JobGauges _jobGauges,
        ISigScanner _scanner,
        IPluginLog _pluginLog)
    {
        unsafe
        {
            characterManager = CharacterManager.Instance();
        }
        pluginInterface = _pluginInterface;
        clientState = _clientState;
        framework = _framework;
        gameGui = _gameGui;
        commandManager = _commandManager;
        condition = _condition;
        dataManager = _dataManager;
        jobGauges = _jobGauges;
        sigScanner = _scanner;
        log = _pluginLog;
#if DEBUG
        sw = new Stopwatch();
        sw.Start();
#endif
        try
        {
            unsafe
            {
#if DEBUG
                var altActorUpdateSig = "48 8B C4 55 57 41 56 48 83 EC 60";
                var altActorUpdateFuncPtr = sigScanner.ScanText(altActorUpdateSig);
                altReceiveActorUpdateHook = Hook<AltReceiveActorUpdateDelegate>.FromAddress(altActorUpdateFuncPtr, AltActorTickUpdate);
#endif

                var actorUpdateSignature = "48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 83 3D ?? ?? ?? ?? ?? 41 0F B6 E8 48 8B DA 8B F1 0F 84 ?? ?? ?? ?? 48 89 7C 24 ??";
                var actorUpdateFuncPtr = sigScanner.ScanText(actorUpdateSignature);
                receiveActorUpdateHook = Hook<ReceiveActorUpdateDelegate>.FromAddress(actorUpdateFuncPtr, ActorTickUpdate);

                // DamageInfo sig
                var receiveActionEffectSignature = "40 55 53 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70";
                var receiveActionEffectFuncPtr = sigScanner.ScanText(receiveActionEffectSignature);
                receiveActionEffectHook = Hook<ReceiveActionEffectDelegate>.FromAddress(receiveActionEffectFuncPtr, ReceiveActionEffect);
            }
        }
        catch (Exception e)
        {
            log.Error(e, "Plugin could not be initialized. Hooks failed.");
#if DEBUG
            altReceiveActorUpdateHook?.Disable();
            altReceiveActorUpdateHook?.Dispose();
#endif
            receiveActorUpdateHook?.Disable();
            receiveActorUpdateHook?.Dispose();
            receiveActionEffectHook?.Disable();
            receiveActionEffectHook?.Dispose();
            throw;
        }
#if DEBUG
        altReceiveActorUpdateHook.Enable();
#endif
        receiveActorUpdateHook.Enable();
        receiveActionEffectHook.Enable();

        utilities = new Utilities(pluginInterface, condition, dataManager, clientState, log);
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow(pluginInterface);
        HPBarWindow = new HPBar(clientState, log, utilities);
        MPBarWindow = new MPBar(clientState, log, utilities);
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
        if (clientState is not { LocalPlayer: { } player })
        {
            return;
        }
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
        /*if (jobType == 32) // 32 = Disciple of the Land
        {
            // TODO: Implement GP bar support
        }*/
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
        else if (lastHPValue < currentHP && HPBarWindow.CanUpdate && !HPBarWindow.FastTick)
        {
            // CanUpdate is only set on server tick, heal trigger is irrelevant
            HPBarWindow.LastTick = currentTime;
            HPBarWindow.CanUpdate = false;
            if (healTriggered)
            {
                healTriggered = false;
            }
        }
        else if (lastHPValue < currentHP && HPBarWindow.FastTick)
        {
            // if there's a heal triggered but the progress would restart, I want to update regardless
            var progress = (currentTime - HPBarWindow.LastTick) / FastTickInterval;
            if (healTriggered && progress < 0.9)
            {
                healTriggered = false;
            }
            else
            {
                HPBarWindow.LastTick = currentTime;
            }
        }

        if (!HPBarWindow.FastTick && syncValue < HPBarWindow.LastTick && !healTriggered)
        {
            syncValue = HPBarWindow.LastTick;
        }

        HPBarWindow.RegenHalted = regenHalt;
        lastHPValue = (int)currentHP;
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
        else if (lastMPValue < currentMP && MPBarWindow.CanUpdate && !MPBarWindow.FastTick)
        {
            MPBarWindow.LastTick = currentTime;
            MPBarWindow.CanUpdate = false;
        }
        else if (lastMPValue < currentMP && MPBarWindow.FastTick)
        {
            MPBarWindow.LastTick = currentTime;
        }

        if (!MPBarWindow.FastTick && syncValue < MPBarWindow.LastTick)
        {
            syncValue = MPBarWindow.LastTick;
        }

        MPBarWindow.RegenHalted = regenHalt;
        lastMPValue = (int)currentMP;
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
        log.Debug("HP regen list generated with {HPcount} status effects.", HealthRegenList.Count);
        log.Debug("MP regen list generated with {MPcount} status effects.", ManaRegenList.Count);
    }

    private ExcelSheet<Lumina.Excel.GeneratedSheets.Status>? RetrieveSheet()
    {
        try
        {
            var sheet = dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>(Dalamud.ClientLanguage.English);
            if (sheet is null)
            {
                nullSheet = true;
                log.Fatal("Invalid lumina sheet!");
                return null;
            }
            return sheet;
        }
        catch (Exception e)
        {
            nullSheet = true;
            log.Fatal("Retrieving lumina sheet failed!");
            log.Fatal(e.Message);
            return null;
        }
    }

    // DamageInfo stripped function
    /// <summary>
    /// This detour function is triggered every time the client receives
    /// a network packet containing an action that happens in the vecinity of the user.
    /// </summary>
    private unsafe void ReceiveActionEffect(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail)
    {
        // The goal is only to intercept and inspect the values, not alter and feed different values back
        receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
        try
        {
            // Can this even be called if LocalPlayer isn't set? who knows, better safe than sorry
            if (clientState is not { LocalPlayer: { } player })
            {
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
                    log.Verbose("Self-healing.");
                    healTriggered = true;
                }
                else if (target == player.ObjectId || castTarget == player.ObjectId)
                {
                    log.Verbose("Healed by {n}", name);
                    healTriggered = true;
                }
            }
        }
        catch (Exception e)
        {
            log.Error(e, "An error has occured with the ReceiveActionEffect detour.");
        }
    }

#if DEBUG
    private unsafe void AltActorTickUpdate(uint objectId, byte* packetData, byte unkByte)
    {
        altReceiveActorUpdateHook.Original(objectId, packetData, unkByte);
        try
        {
            log.Warning("Alt detour happened.");
            var unk1 = *((uint*)packetData + 1);
            var unk2 = *((ushort*)packetData + 6);
        }
        catch (Exception e)
        {
            log.Error(e, "An error has occured with the AltActorTickUpdate detour.");
        }
    }
#endif

    /// <summary>
    /// This detour function is triggered every time the client receives
    /// a network packet containing an update for the nearby actors
    /// with hp, mana, gp
    /// </summary>
    private unsafe void ActorTickUpdate(uint objectId, uint* packetData, byte unkByte)
    {
        receiveActorUpdateHook.Original(objectId, packetData, unkByte);
        try
        {
            // Can this even be called if LocalPlayer isn't set? who knows, better safe than sorry
            if (clientState is not { LocalPlayer: { } player })
            {
                return;
            }
#if DEBUG
            var networkHP = *(int*)packetData;
            var networkMP = *((ushort*)packetData + 2);
            var networkGP = *((short*)packetData + 3); // Goes up to 10000 and is tracked and updated at all times
            if (sw.ElapsedMilliseconds >= 1000)
            {
                var character = characterManager->LookupBattleCharaByObjectId((int)objectId)->Character;
                var name = MemoryHelper.ReadStringNullTerminated((nint)character.GameObject.GetName());
                DebugWindow.name1 = name ?? "invalid";
                DebugWindow.name2 = string.Empty;
                DebugWindow.variable4 = unkByte;
                if (objectId != player.ObjectId)
                {
                    DebugWindow.name2 = "Supposedly ";
                }
                DebugWindow.variable1 = networkHP;
                DebugWindow.variable2 = networkMP;
                DebugWindow.variable3 = networkGP;
                sw.Restart();
            }
#endif
            if (objectId != player.ObjectId)
            {
                return;
            }
            HPBarWindow.CanUpdate = true;
            MPBarWindow.CanUpdate = true;
            syncAvailable = true;
        }
        catch (Exception e)
        {
            log.Error(e, "An error has occured with the ActorTickUpdate detour.");
        }
    }

    private void TerritoryChanged(object? sender, ushort e)
    {
        lastHPValue = -1;
        lastMPValue = -1;
    }

    public void Dispose()
    {
#if DEBUG
        altReceiveActorUpdateHook?.Disable();
        altReceiveActorUpdateHook?.Dispose();
#endif
        receiveActorUpdateHook?.Disable();
        receiveActorUpdateHook?.Dispose();
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
