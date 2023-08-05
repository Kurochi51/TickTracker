using System;
using System.Linq;
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
using System.Threading.Tasks;

namespace TickTracker
{
    public sealed unsafe class Plugin : IDalamudPlugin
    {
        public string Name => "Tick Tracker";
        private const string CommandName = "/tick";
        public WindowSystem WindowSystem = new("TickTracker");
        private ConfigWindow ConfigWindow { get; init; }
        private HPBar HPBarWindow { get; init; }
        private MPBar MPBarWindow { get; init; }
        private static Configuration config => TickTrackerSystem.config;
        private static HashSet<string> regen => Utilities.RegenKeywords;
        private static HashSet<string> time => Utilities.TimeKeywords;
        private static HashSet<string> health => Utilities.HealthKeywords;
        private static HashSet<string> mana => Utilities.ManaKeywords;
        private readonly HashSet<uint> healthRegenList = new();
        private readonly HashSet<uint> manaRegenList = new();
        private readonly List<double> times = new();
        private bool inCombat;
        private readonly bool nullSheet;
        private double syncValue = 1;
        private int lastHPValue = -1, lastMPValue = -1;
        private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
        private uint currentHP = 1, currentMP = 1, maxHP = 2, maxMP = 2;
        private static AtkUnitBase* NameplateAddon => (AtkUnitBase*)Service.GameGui.GetAddonByName("NamePlate");

        public Plugin(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();
            pluginInterface.Create<TickTrackerSystem>();
            ConfigWindow = new ConfigWindow(this);
            HPBarWindow = new HPBar();
            MPBarWindow = new MPBar();
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(HPBarWindow);
            WindowSystem.AddWindow(MPBarWindow);

            Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open or close Tick Tracker's config window."
            });
            Service.PluginInterface.UiBuilder.Draw += DrawUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.Framework.Update += FrameworkOnUpdateEvent;
            Service.ClientState.TerritoryChanged += TerritoryChanged;
            var statusSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>(Dalamud.ClientLanguage.English);
            if (statusSheet != null)
            {
                nullSheet = false;
                /*Task.Run(() => {
                    foreach (var stat in statusSheet.Where(s => s.RowId is not 307 and not 1419 and not 135))
                    {
                        var text = stat.Description.ToDalamudString().TextValue;
                        if (Utilities.KeywordMatch(text, regen) && Utilities.KeywordMatch(text, time))
                        {
                            if (Utilities.KeywordMatch(text, health))
                            {
                                healthRegenList.Add(stat.RowId);
                            }
                            if (Utilities.KeywordMatch(text, mana))
                            {
                                manaRegenList.Add(stat.RowId);
                            }
                        }
                    }
                    PluginLog.Debug("HP regen list generated with {HPcount} status effects.", healthRegenList.Count);
                    PluginLog.Debug("MP regen list generated with {MPcount} status effects.", manaRegenList.Count);
                });*/
            }
            else
            {
                nullSheet = true;
                PluginLog.Error("Status sheet couldn't get queued.");
            }
        }

        public static bool IsAddonReady(AtkUnitBase* addon)
        {
            if (addon is null) return false;
            if (addon->RootNode is null) return false;
            if (addon->RootNode->ChildNode is null) return false;

            return true;
        }

        private bool PluginEnabled(bool enemy)
        {
            if (config.HideOutOfCombat && !inCombat)
            {
                var showingBecauseInDuty = config.AlwaysShowInDuties && Utilities.InDuty() && !Utilities.InIgnoredInstances();
                var showingBecauseHasTarget = config.AlwaysShowWithHostileTarget && enemy;
                if (!(showingBecauseInDuty || showingBecauseHasTarget))
                {
                    return false;
                }
            }
            return config.PluginEnabled;
        }

        private void FrameworkOnUpdateEvent(Framework framework)
        {
            var now = DateTime.Now.TimeOfDay.TotalSeconds;
            if (Service.ClientState is { IsLoggedIn: false } || nullSheet || healthRegenList.Count is 0 || manaRegenList.Count is 0)
            {
                return;
            }
            bool LucidDream;
            bool Regen;
            bool Target;
            if (Service.ClientState is { LocalPlayer: { } player })
            {
                LucidDream = player.StatusList.Any(e => manaRegenList.Contains(e.StatusId));
                Regen = player.StatusList.Any(e => healthRegenList.Contains(e.StatusId));
                Target = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
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
            if (!PluginEnabled(Target) || !IsAddonReady(NameplateAddon) || !NameplateAddon->IsVisible || Utilities.inCustcene())
            {
                HPBarWindow.IsOpen = false;
                MPBarWindow.IsOpen = false; 
                return;
            }
            var shouldShowHPBar = !config.HideOnFullResource || 
                                (config.AlwaysShowInCombat && inCombat) ||
                                (config.AlwaysShowWithHostileTarget && Target) ||
                                (config.AlwaysShowInDuties && Service.Condition[ConditionFlag.BoundByDuty]);
            var shouldShowMPBar = !config.HideOnFullResource || 
                                (config.AlwaysShowInCombat && inCombat) ||
                                (config.AlwaysShowWithHostileTarget && Target) ||
                                (config.AlwaysShowInDuties && Service.Condition[ConditionFlag.BoundByDuty]);
            HPBarWindow.IsOpen = shouldShowHPBar || (currentHP != maxHP);
            MPBarWindow.IsOpen = shouldShowMPBar || (currentMP != maxMP);
            if ((lastHPValue < currentHP && !HPBarWindow.FastTick) || (lastMPValue < currentMP && !MPBarWindow.FastTick))
            {
                syncValue = now;
            }
            UpdateHPTick(now, Regen);
            UpdateMPTick(now, LucidDream);
            if (syncValue + ActorTickInterval <= now)
            {
                syncValue += ActorTickInterval;
            }
        }

        private void UpdateHPTick(double currentTime, bool regen)
        {
            HPBarWindow.FastTick = (regen && currentHP != maxHP);
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
            lastHPValue = (int)currentHP;
            HPBarWindow.UpdateAvailable = true;
        }

        private void UpdateMPTick(double currentTime, bool lucid)
        {
            MPBarWindow.FastTick = (lucid && currentMP != maxMP);
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
            lastMPValue = (int)currentMP;
            MPBarWindow.UpdateAvailable = true;
        }

        private void TerritoryChanged(object? sender, ushort e)
        {
            lastHPValue = -1;
            lastMPValue = -1;
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();
            HPBarWindow.Dispose();
            MPBarWindow.Dispose();
            ConfigWindow.Dispose();
            Service.PluginInterface.UiBuilder.Draw -= DrawUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            Service.CommandManager.RemoveHandler(CommandName);
            Service.Framework.Update -= FrameworkOnUpdateEvent;
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
}
