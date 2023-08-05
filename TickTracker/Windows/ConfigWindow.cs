using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;

namespace TickTracker.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private static Configuration config => TickTrackerSystem.config;

        public ConfigWindow(Plugin plugin) : base("Timer Settings")
        {
            Size = new(320, 400);
            SizeCondition = ImGuiCond.Appearing;
            Flags = ImGuiWindowFlags.NoResize;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public override void Draw()
        {
            var pluginEnabled = config.PluginEnabled;
            if (ImGui.Checkbox("Enable plugin", ref pluginEnabled))
            {
                config.PluginEnabled = pluginEnabled;
                config.Save();
            }

            if (ImGui.BeginTabBar("ConfigTabBar", ImGuiTabBarFlags.None))
            {
                DrawAppearanceTab();
                DrawBehaviorTab();
                ImGui.EndTabBar();
            }
            Close();
        }

        public override void OnClose()
        {
            config.Save();
        }

        private void Close()
        {
            var originPos = ImGui.GetCursorPos();
            // Place close button in bottom right + some padding / extra space
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 10f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f);
            if (ImGui.Button("Close"))
            {
                config.Save();
                this.IsOpen = false;
            }
            ImGui.SetCursorPos(originPos);
        }

        private static void DrawAppearanceTab()
        {
            if (ImGui.BeginTabItem("Appearance"))
            {
                ImGui.Spacing();
                var lockBar = config.LockBar;
                if (ImGui.Checkbox("Lock bar size and position", ref lockBar))
                {
                    config.LockBar = lockBar;
                    config.Save();
                }

                if (!config.LockBar)
                {
                    DrawBarPositions();
                }
                DrawColorOptions();
                ImGui.EndTabItem();
            }
        }

        private static void DrawBehaviorTab()
        {
            if (ImGui.BeginTabItem("Behavior"))
            {
                ImGui.Spacing();
                var hpVisible = config.HPVisible;
                if (ImGui.Checkbox("Show HP Bar", ref hpVisible))
                {
                    config.HPVisible = hpVisible;
                    config.Save();
                }
                var mpVisible = config.MPVisible;
                if (ImGui.Checkbox("Show MP Bar", ref mpVisible))
                {
                    config.MPVisible = mpVisible;
                    config.Save();
                }
                var fullResource = config.HideOnFullResource;
                if (ImGui.Checkbox("Hide bar on full resource", ref fullResource))
                {
                    config.HideOnFullResource = fullResource;
                    config.Save();
                }
                ImGui.Indent();
                var showInCombat = config.AlwaysShowInCombat;
                if (ImGui.Checkbox("Always show in combat", ref showInCombat))
                {
                    config.AlwaysShowInCombat = showInCombat;
                    config.Save();
                }
                ImGui.Unindent();
                var hideOutOfCombat = config.HideOutOfCombat;
                if (ImGui.Checkbox("Hide while not in combat", ref hideOutOfCombat))
                {
                    config.HideOutOfCombat = hideOutOfCombat;
                    config.Save();
                }

                ImGui.Indent();
                var showInDuties = config.AlwaysShowInDuties;
                if (ImGui.Checkbox("Always show while in duties", ref showInDuties))
                {
                    config.AlwaysShowInDuties = showInDuties;
                    config.Save();
                }

                var showWithHostileTarget = config.AlwaysShowWithHostileTarget;
                if (ImGui.Checkbox("Always show with enemy target", ref showWithHostileTarget))
                {
                    config.AlwaysShowWithHostileTarget = showWithHostileTarget;
                    config.Save();
                }
                ImGui.Unindent();
                ImGui.EndTabItem();
            }
        }

        private static void DrawColorOptions()
        {
            if (ImGui.ColorEdit4("HP Bar Background Color", ref config.HPBarBackgroundColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
            {
                config.Save();
            }
            ImGui.SameLine();
            var resetButtonX = ImGui.GetCursorPosX();
            if (ImGui.Button("Reset##ResetHPBarBackgroundColor"))
            {
                config.HPBarBackgroundColor = new(0f, 0f, 0f, 1f);
                config.Save();
            }

            if (ImGui.ColorEdit4("HP Bar Fill Color", ref config.HPBarFillColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
            {
                config.Save();
            }
            ImGui.SameLine();
            ImGui.SetCursorPosX(resetButtonX);
            if (ImGui.Button("Reset##HPResetBarFillColor"))
            {
                config.HPBarFillColor = new(0.276f, 0.8f, 0.24f, 1f);
                config.Save();
            }

            if (ImGui.ColorEdit4("HP Bar Border Color", ref config.HPBarBorderColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
            {
                config.Save();
            }
            ImGui.SameLine();
            ImGui.SetCursorPosX(resetButtonX);
            if (ImGui.Button("Reset##HPResetBarBorderColor"))
            {
                config.HPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f);
                config.Save();
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.ColorEdit4("MP Bar Background Color", ref config.MPBarBackgroundColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
            {
                config.Save();
            }
            ImGui.SameLine();
            resetButtonX = ImGui.GetCursorPosX();
            if (ImGui.Button("Reset##ResetMPBarBackgroundColor"))
            {
                config.MPBarBackgroundColor = new(0f, 0f, 0f, 1f);
                config.Save();
            }

            if (ImGui.ColorEdit4("MP Bar Fill Color", ref config.MPBarFillColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
            {
                config.Save();
            }
            ImGui.SameLine();
            ImGui.SetCursorPosX(resetButtonX);
            if (ImGui.Button("Reset##MPResetBarFillColor"))
            {
                config.MPBarFillColor = new(0.753f, 0.271f, 0.482f, 1f);
                config.Save();
            }

            if (ImGui.ColorEdit4("MP Bar Border Color", ref config.MPBarBorderColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
            {
                config.Save();
            }
            ImGui.SameLine();
            ImGui.SetCursorPosX(resetButtonX);
            if (ImGui.Button("Reset##MPResetBarBorderColor"))
            {
                config.MPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f);
                config.Save();
            }
        }

        private static void DrawBarPositions()
        {
            ImGui.Indent();
            ImGui.Spacing();
            var HPBarPos = config.HPBarPosition;
            var HPBarSize = config.HPBarSize;
            if (DragInput2(ref HPBarPos, "HPPositionX", "HPPositionY", "HP Bar Position"))
            {
                Utilities.UpdateWindowConfig(HPBarPos, HPBarSize, WindowType.HpWindow);
            }

            if (DragInput2(ref HPBarSize, "HPSizeX", "HPSizeY", "HP Bar Size"))
            {
                Utilities.UpdateWindowConfig(HPBarPos, HPBarSize, WindowType.HpWindow);
            }

            ImGui.Spacing();

            var MPBarPos = config.MPBarPosition;
            var MPBarSize = config.MPBarSize;
            if (DragInput2(ref MPBarPos, "MPPositionX", "MPPositionY", "MP Bar Position"))
            {
                Utilities.UpdateWindowConfig(MPBarPos, MPBarSize, WindowType.MpWindow);
            }

            if (DragInput2(ref MPBarSize, "MPSizeX", "MPSizeY", "MP Bar Size"))
            {
                Utilities.UpdateWindowConfig(MPBarPos, MPBarSize, WindowType.MpWindow);
            }
            ImGui.Spacing();
            ImGui.Unindent();
        }

        private static bool DragInput2(ref Vector2 vector, string label1, string label2, string description)
        {
            var change = false;
            var x = (int)vector.X;
            var y = (int)vector.Y;
            ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
            if (ImGui.DragInt($"##{label1}", ref x))
            {
                vector.X = x;
                change = true;
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
            if (ImGui.DragInt($"##{label2}", ref y))
            {
                vector.Y = y;
                change = true;
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.Text(description);
            return change;
        }
    }
}
