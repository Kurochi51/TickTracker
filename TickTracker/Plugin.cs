using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private Framework Framework { get; set; }
        private Condition Condition { get; set; }
        private ClientState clientState;
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("TickTracker");

        private ConfigWindow ConfigWindow { get; init; }
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
        [PluginService] public static IGameGui GameGui { get; private set; } = null!;
        private static AtkUnitBase* NameplateAddon => (AtkUnitBase*)GameGui.GetAddonByName("NamePlate");

        public Plugin(DalamudPluginInterface pluginInterface, 
                      CommandManager commandManager, 
                      Framework framework, 
                      Condition condition, 
                      ClientState clientState)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Framework = framework;
            this.Condition = condition;
            this.clientState = clientState;
            lastHPTickTime = ImGui.GetTime();
            lastMPTickTime = ImGui.GetTime();

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            ConfigWindow = new ConfigWindow(this);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open or close Tick Tracker's config window."
            });
            PluginInterface.UiBuilder.Draw += ConfigWindow.Draw;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Framework.Update += FrameworkOnUpdateEvent;
            clientState.TerritoryChanged += TerritoryChanged;
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
            if (Configuration.HideOutOfCombat && !inCombat && isLoggedIn)
            {
                var inDuty = Condition[ConditionFlag.BoundByDuty];
                var battleTarget = clientState.LocalPlayer?.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
                var showingBecauseInDuty = Configuration.AlwaysShowInDuties && inDuty;
                var showingBecauseHasTarget = Configuration.AlwaysShowWithHostileTarget && battleTarget;
                if (!(showingBecauseInDuty || showingBecauseHasTarget))
                {
                    return false;
                }
            }
            if (!isLoggedIn)
            {
                return false;
            }
            return Configuration.PluginEnabled;
        }

        private void FrameworkOnUpdateEvent(Framework framework)
        {
            var now = ImGui.GetTime();
            if (now - lastUpdate < PollingInterval)
            {
                return;
            }
            if (clientState is { IsLoggedIn: var loggedIn })
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
            if (clientState is { LocalPlayer: { } player })
            {
                // Since we got this far, clientState / LocalPlayer is null checked already
                LucidDream = clientState.LocalPlayer.StatusList.Any(e => e.StatusId == 1204);
                Regen = clientState.LocalPlayer.StatusList.Any(e => regenStatusID.Contains(e.StatusId));
                Target = clientState.LocalPlayer.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
                inCombat = Condition[ConditionFlag.InCombat];
                specialState = Condition[ConditionFlag.Occupied38];
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
            if (Configuration.HideOnFullResource)
            {
                if ((Configuration.AlwaysShowInCombat && inCombat) || 
                    (Configuration.AlwaysShowWithHostileTarget && Target) || 
                    (Configuration.AlwaysShowInDuties && Condition[ConditionFlag.BoundByDuty]))
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
            PluginInterface.UiBuilder.Draw -= ConfigWindow.Draw;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            CommandManager.RemoveHandler(CommandName);
            Framework.Update -= FrameworkOnUpdateEvent;
            clientState.TerritoryChanged -= TerritoryChanged;
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
