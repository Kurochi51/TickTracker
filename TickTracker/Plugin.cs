using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using TickTracker.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;

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
        private readonly HashSet<uint> regenStatusID = new()
        {
            158,
            237,
            739,
            835,
            836,
            897,
            1330,
            1879,
            1911,
            1944,
            2070,
            2617,
            2620,
            2637,
            2938,
        };
        private bool inCombat, specialState, inDuty;
        private double lastHPTickTime = 1, lastMPTickTime = 1;
        private int lastHPValue = -1, lastMPValue = -1;
        private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
        private const double PollingInterval = 1d / 30;
        private uint currentHP = 1, currentMP = 1, maxHP = 2, maxMP = 2;
        private static AtkUnitBase* NameplateAddon => (AtkUnitBase*)Services.GameGui.GetAddonByName("NamePlate");

        public Plugin(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Services>();
            pluginInterface.Create<TickTrackerSystem>();
            lastHPTickTime = ImGui.GetTime();
            lastMPTickTime = ImGui.GetTime();
            ConfigWindow = new ConfigWindow(this);
            HPBarWindow = new HPBar();
            MPBarWindow = new MPBar();
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(HPBarWindow);
            WindowSystem.AddWindow(MPBarWindow);

            Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open or close Tick Tracker's config window."
            });
            Services.PluginInterface.UiBuilder.Draw += DrawUI;
            Services.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Services.Framework.Update += FrameworkOnUpdateEvent;
            Services.ClientState.TerritoryChanged += TerritoryChanged;
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
                var showingBecauseInDuty = config.AlwaysShowInDuties && inDuty;
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
            var now = ImGui.GetTime();
            if (Services.ClientState is { IsLoggedIn: false })
            {
                return;
            }
            var LucidDream = false;
            var Regen = false;
            var Target = false;
            if (Services.ClientState is { LocalPlayer: { } player })
            {
                LucidDream = player.StatusList.Any(e => e.StatusId == 1204);
                Regen = player.StatusList.Any(e => regenStatusID.Contains(e.StatusId));
                Target = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
                inCombat = Services.Condition[ConditionFlag.InCombat];
                specialState = Services.Condition[ConditionFlag.Occupied38];
                inDuty = Services.Condition[ConditionFlag.BoundByDuty];
                currentHP = player.CurrentHp;
                maxHP = player.MaxHp;
                currentMP = player.CurrentMp;
                maxMP = player.MaxMp;
            }
            else
            {
                return;
            }
            if (!PluginEnabled(Target) || !IsAddonReady(NameplateAddon) || !NameplateAddon->IsVisible || specialState)
            {
                HPBarWindow.IsOpen = false;
                MPBarWindow.IsOpen = false;
                return;
            }
            var shouldShowHPBar = !config.HideOnFullResource || 
                                (config.AlwaysShowInCombat && inCombat) ||
                                (config.AlwaysShowWithHostileTarget && Target) ||
                                (config.AlwaysShowInDuties && Services.Condition[ConditionFlag.BoundByDuty]);
            var shouldShowMPBar = !config.HideOnFullResource || 
                                (config.AlwaysShowInCombat && inCombat) ||
                                (config.AlwaysShowWithHostileTarget && Target) ||
                                (config.AlwaysShowInDuties && Services.Condition[ConditionFlag.BoundByDuty]);
            HPBarWindow.IsOpen = shouldShowHPBar || (currentHP != maxHP);
            MPBarWindow.IsOpen = shouldShowMPBar || (currentMP != maxMP);
            ProcessTick(now, Regen, LucidDream);
        }

        private void ProcessTick(double currentTime, bool regen, bool lucid)
        {
            // Use FastTick only if lucid dream or a regen effect is active
            HPBarWindow.HPFastTick = (regen && currentHP != maxHP);
            MPBarWindow.MPFastTick = (lucid && currentMP != maxMP);
            if (lastHPValue < currentHP)
            {
                lastHPTickTime = currentTime;
            }
            else if (lastHPTickTime + (HPBarWindow.HPFastTick ? FastTickInterval : ActorTickInterval) <= currentTime)
            {
                lastHPTickTime += HPBarWindow.HPFastTick ? FastTickInterval : ActorTickInterval;
            }

            if (lastMPValue < currentMP)
            {
                lastMPTickTime = currentTime;
            }
            else if (lastMPTickTime + (MPBarWindow.MPFastTick ? FastTickInterval : ActorTickInterval) <= currentTime)
            {
                lastMPTickTime += MPBarWindow.MPFastTick ? FastTickInterval : ActorTickInterval;
            }
            HPBarWindow.LastHPTick = lastHPTickTime;
            MPBarWindow.LastMPTick = lastMPTickTime;
            lastHPValue = (int)currentHP;
            lastMPValue = (int)currentMP;
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
            ConfigWindow.Dispose();
            Services.PluginInterface.UiBuilder.Draw -= ConfigWindow.Draw;
            Services.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            Services.CommandManager.RemoveHandler(CommandName);
            Services.Framework.Update -= FrameworkOnUpdateEvent;
            Services.ClientState.TerritoryChanged -= TerritoryChanged;
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
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
