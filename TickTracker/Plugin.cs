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

namespace TickTracker
{
    public sealed unsafe class Plugin : IDalamudPlugin
    {
        public string Name => "Tick Tracker";
        private const string CommandName = "/tick";
        public WindowSystem WindowSystem = new("TickTracker");
        private ConfigWindow ConfigWindow { get; init; }
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
        private bool inCombat, isLoggedIn, specialState;
        private double lastUpdate = 0, lastHPTickTime = 1, lastMPTickTime = 1;
        private int lastHPValue = -1, lastMPValue = -1;
        private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
        private const double PollingInterval = 1d / 30;
        private uint currentHP = 1, currentMP = 1, maxHP = 2, maxMP = 2;
        //[PluginService] public static IGameGui GameGui { get; private set; } = null!;
        private static AtkUnitBase* NameplateAddon => (AtkUnitBase*)Services.GameGui.GetAddonByName("NamePlate");

        public Plugin(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Services>();
            pluginInterface.Create<TickTrackerSystem>();
            /*this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Framework = framework;
            this.Condition = condition;
            this.clientState = clientState;*/
            lastHPTickTime = ImGui.GetTime();
            lastMPTickTime = ImGui.GetTime();
            //Configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            //Configuration.Initialize(PluginInterface);

            ConfigWindow = new ConfigWindow(this);

            Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open or close Tick Tracker's config window."
            });
            Services.PluginInterface.UiBuilder.Draw += ConfigWindow.Draw;
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

        private bool PluginEnabled()
        {
            if (config.HideOutOfCombat && !inCombat && isLoggedIn)
            {
                var inDuty = Services.Condition[ConditionFlag.BoundByDuty];
                var battleTarget = Services.ClientState.LocalPlayer?.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
                var showingBecauseInDuty = config.AlwaysShowInDuties && inDuty;
                var showingBecauseHasTarget = config.AlwaysShowWithHostileTarget && battleTarget;
                if (!(showingBecauseInDuty || showingBecauseHasTarget))
                {
                    return false;
                }
            }
            if (!isLoggedIn)
            {
                return false;
            }
            return config.PluginEnabled;
        }

        private void FrameworkOnUpdateEvent(Framework framework)
        {
            var now = ImGui.GetTime();
            if (now - lastUpdate < PollingInterval)
            {
                return;
            }
            if (Services.ClientState is { IsLoggedIn: var loggedIn })
            {
                isLoggedIn = loggedIn;
                if (!isLoggedIn)
                {
                    return;
                }
            }
            lastUpdate = now;
            var LucidDream = false;
            var Regen = false;
            var Target = false;
            if (Services.ClientState is { LocalPlayer: { } player })
            {
                // Since we got this far, clientState / LocalPlayer is null checked already
                LucidDream = player.StatusList.Any(e => e.StatusId == 1204);
                Regen = player.StatusList.Any(e => regenStatusID.Contains(e.StatusId));
                Target = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
                inCombat = Services.Condition[ConditionFlag.InCombat];
                specialState = Services.Condition[ConditionFlag.Occupied38];
                currentHP = player.CurrentHp;
                maxHP = player.MaxHp;
                currentMP = player.CurrentMp;
                maxMP = player.MaxMp;
            }
            if (!PluginEnabled() || !IsAddonReady(NameplateAddon) || !NameplateAddon->IsVisible || specialState)
            {
                ConfigWindow.HPBarVisible = false;
                ConfigWindow.MPBarVisible = false;
                return;
            }
            if (config.HideOnFullResource)
            {
                if ((config.AlwaysShowInCombat && inCombat) || 
                    (config.AlwaysShowWithHostileTarget && Target) || 
                    (config.AlwaysShowInDuties && Services.Condition[ConditionFlag.BoundByDuty]))
                {
                    ConfigWindow.HPBarVisible = true;
                    ConfigWindow.MPBarVisible = true;
                }
                else
                {
                    ConfigWindow.HPBarVisible = currentHP != maxHP;
                    ConfigWindow.MPBarVisible = currentMP != maxMP;
                }
            }
            else
            {
                ConfigWindow.HPBarVisible = true;
                ConfigWindow.MPBarVisible = true;
            }
            // Use FastTick only if lucid dream or a regen effect is active, and the respecitve resource isn't capped
            ConfigWindow.HPFastTick = (Regen && currentHP != maxHP);
            ConfigWindow.MPFastTick = (LucidDream && currentMP != maxMP);
            if (lastHPValue < currentHP)
            {
                lastHPTickTime = now;
            }
            else if (lastHPTickTime + (ConfigWindow.HPFastTick ? FastTickInterval : ActorTickInterval) <= now)
            {
                lastHPTickTime += ConfigWindow.HPFastTick ? FastTickInterval : ActorTickInterval;
            }

            if (lastMPValue < currentMP)
            {
                lastMPTickTime = now;
            }
            else if (lastMPTickTime + (ConfigWindow.MPFastTick ? FastTickInterval : ActorTickInterval) <= now)
            {
                lastMPTickTime += ConfigWindow.MPFastTick ? FastTickInterval : ActorTickInterval;
            }
            ConfigWindow.LastHPTick = lastHPTickTime;
            ConfigWindow.LastMPTick = lastMPTickTime;
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
            ConfigWindow.ConfigVisible = !ConfigWindow.ConfigVisible;
            ConfigWindow.Toggle();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.ConfigVisible = !ConfigWindow.ConfigVisible;
            ConfigWindow.Toggle();
        }
    }
}
