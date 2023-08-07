using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface.Windowing;

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

        public override void OnClose()
        {
            config.Save();
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
            DrawCloseButton();
        }

        private void DrawCloseButton()
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
                var modified = false;

                modified |= ImGui.Checkbox("Show HP Bar", ref config.HPVisible);
                modified |= ImGui.Checkbox("Show MP Bar", ref config.MPVisible);
                modified |= ImGui.Checkbox("Hide bar on full resource", ref config.HideOnFullResource);

                ImGui.Indent();
                modified |= ImGui.Checkbox("Always show in combat", ref config.AlwaysShowInCombat);
                ImGui.Unindent();

                modified |= ImGui.Checkbox("Hide while not in combat", ref config.HideOutOfCombat);

                ImGui.Indent();
                modified |= ImGui.Checkbox("Always show while in duties", ref config.AlwaysShowInDuties);
                modified |= ImGui.Checkbox("Always show with enemy target", ref config.AlwaysShowWithHostileTarget);
                ImGui.Unindent();

                if (modified)
                {
                    config.Save();
                }
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
                config.HPBarBackgroundColor = new(0f, 0f, 0f, 1f); // Default Color: Black - #000000
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
                config.HPBarFillColor = new(0.276f, 0.8f, 0.24f, 1f); // Default Color: Green - #46CC3D
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
                config.HPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f); // Default Color: Dark Grey - #3F4345
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
                config.MPBarBackgroundColor = new(0f, 0f, 0f, 1f); // Default Color: Black - #000000
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
                config.MPBarFillColor = new(0.753f, 0.271f, 0.482f, 1f); // Default Color: Pink - #C0457B
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
                config.MPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f); // Default Color: Dark Grey - #3F4345
                config.Save();
            }
        }

        private static void DrawBarPositions()
        {
            ImGui.Indent();
            ImGui.Spacing();
            if (DragInput2(ref config.HPBarPosition, "HPPositionX", "HPPositionY", "HP Bar Position"))
            {
                config.Save();
            }
            if (DragInput2Size(ref config.HPBarSize, "HPSizeX", "HPSizeY", "HP Bar Size"))
            {
                config.Save();
            }

            ImGui.Spacing();

            if (DragInput2(ref config.MPBarPosition, "MPPositionX", "MPPositionY", "MP Bar Position"))
            {
                config.Save();
            }

            if (DragInput2Size(ref config.MPBarSize, "MPSizeX", "MPSizeY", "MP Bar Size"))
            {
                config.Save();
            }
            ImGui.Spacing();
            ImGui.Unindent();
        }
        /// <summary>
        ///     The <see cref="ImGui.DragInt2" /> we have at home.
        /// </summary>
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
        /// <summary>
        ///     This exists because the bar windows don't actually go under 32x32 because of the drawn bar inside it.
        ///     The max value is arbitrarly set to 1920x1080.
        /// </summary>
        private static bool DragInput2Size(ref Vector2 vector, string label1, string label2, string description)
        {
            var change = false;
            var x = (int)vector.X;
            var y = (int)vector.Y;
            ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
            if (ImGui.DragInt($"##{label1}", ref x, 1, 32, 1920, "%d", ImGuiSliderFlags.AlwaysClamp))
            {
                vector.X = x;
                change = true;
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
            if (ImGui.DragInt($"##{label2}", ref y, 1, 32, 1080, "%d", ImGuiSliderFlags.AlwaysClamp))
            {
                vector.Y = y;
                change = true;
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.Text(description);
            return change;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
