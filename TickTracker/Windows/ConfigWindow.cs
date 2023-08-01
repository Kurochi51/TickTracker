using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace TickTracker.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private static Configuration config => TickTrackerSystem.config;
        private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
        private const string HPBarName = "HPBar";
        private const string MPBarName = "MPBar";
        private double now;
        private bool configVisible;
        public double LastHPTick = 1, LastMPTick = 1;
        public bool HPFastTick, MPFastTick;
        public bool HPBarVisible { get; set; }
        public bool MPBarVisible { get; set; }
        public bool ConfigVisible
        {
            get => this.configVisible;
            set => this.configVisible = value;
        }
        private readonly Vector2 barFillPosOffset = new(1, 1);
        private readonly Vector2 barFillSizeOffset = new(-1, 0);
        private readonly Vector2 barWindowPadding = new(8, 14);
        private readonly Vector2 configInitialSize = new(350, 450);
        private const ImGuiWindowFlags LockedBarFlags = ImGuiWindowFlags.NoBackground |
                                                        ImGuiWindowFlags.NoMove |
                                                        ImGuiWindowFlags.NoResize |
                                                        ImGuiWindowFlags.NoNav |
                                                        ImGuiWindowFlags.NoInputs;

        public ConfigWindow(Plugin plugin) : base("Timer Settings")
        {
            Size = configInitialSize;
            SizeCondition = ImGuiCond.Appearing;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public override void Draw()
        {
            now = ImGui.GetTime();
            if (HPBarVisible && config.HPVisible) DrawHPBarWindow();
            if (MPBarVisible && config.MPVisible) DrawMPBarWindow();
            if (ConfigVisible) DrawConfigWindow();
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
                configVisible = false;
            }
            ImGui.SetCursorPos(originPos);
        }

        private void DrawHPBarWindow()
        {
            ImGui.SetNextWindowSize(config.HPBarSize, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(config.HPBarPosition, ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
            var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
            if (config.LockBar) windowFlags |= LockedBarFlags;
            if (ImGui.Begin(HPBarName, windowFlags))
            {
                UpdateSavedWindowConfig(ImGui.GetWindowPos(), ImGui.GetWindowSize(), "HP");
                float progress;
                if (HPFastTick)
                {
                    progress = (float)((now - LastHPTick) / FastTickInterval);
                }
                else
                {
                    progress = (float)((now - LastHPTick) / ActorTickInterval);
                }
                if (progress > 1)
                {
                    progress = 1;
                }

                // Setup bar rects
                var topLeft = ImGui.GetWindowContentRegionMin();
                var bottomRight = ImGui.GetWindowContentRegionMax();
                var barWidth = bottomRight.X - topLeft.X;
                var filledSegmentEnd = new Vector2((barWidth * progress) + barWindowPadding.X, bottomRight.Y - 1);

                // Convert imgui window-space rects to screen-space
                var windowPosition = ImGui.GetWindowPos();
                topLeft += windowPosition;
                bottomRight += windowPosition;
                filledSegmentEnd += windowPosition;

                // Draw main bar
                const float cornerSize = 4f;
                const float borderThickness = 1.35f;
                var drawList = ImGui.GetWindowDrawList();
                var barBackgroundColor = ImGui.GetColorU32(config.HPBarBackgroundColor);
                var barFillColor = ImGui.GetColorU32(config.HPBarFillColor);
                var barBorderColor = ImGui.GetColorU32(config.HPBarBorderColor);
                drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight + barFillSizeOffset, barBackgroundColor, cornerSize, ImDrawFlags.RoundCornersAll);
                drawList.AddRectFilled(topLeft + barFillPosOffset, filledSegmentEnd, barFillColor, cornerSize, ImDrawFlags.RoundCornersAll);
                drawList.AddRect(topLeft, bottomRight, barBorderColor, cornerSize, ImDrawFlags.RoundCornersAll, borderThickness);
                ImGui.End();
            }
            ImGui.PopStyleVar();
        }

        private void DrawMPBarWindow()
        {
            ImGui.SetNextWindowSize(config.MPBarSize, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(config.MPBarPosition, ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
            var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
            if (config.LockBar) windowFlags |= LockedBarFlags;
            if (ImGui.Begin(MPBarName, windowFlags))
            {
                UpdateSavedWindowConfig(ImGui.GetWindowPos(), ImGui.GetWindowSize(), "MP");
                float progress;
                if (MPFastTick)
                {
                    progress = (float)((now - LastMPTick) / FastTickInterval);
                }
                else
                {
                    progress = (float)((now - LastMPTick) / ActorTickInterval);
                }
                if (progress > 1)
                {
                    progress = 1;
                }

                // Setup bar rects
                var topLeft = ImGui.GetWindowContentRegionMin();
                var bottomRight = ImGui.GetWindowContentRegionMax();
                var barWidth = bottomRight.X - topLeft.X;
                var filledSegmentEnd = new Vector2((barWidth * progress) + barWindowPadding.X, bottomRight.Y - 1);

                // Convert imgui window-space rects to screen-space
                var windowPosition = ImGui.GetWindowPos();
                topLeft += windowPosition;
                bottomRight += windowPosition;
                filledSegmentEnd += windowPosition;

                // Draw main bar
                const float cornerSize = 4f;
                const float borderThickness = 1.35f;
                var drawList = ImGui.GetWindowDrawList();
                var barBackgroundColor = ImGui.GetColorU32(config.MPBarBackgroundColor);
                var barFillColor = ImGui.GetColorU32(config.MPBarFillColor);
                var barBorderColor = ImGui.GetColorU32(config.MPBarBorderColor);
                drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight + barFillSizeOffset, barBackgroundColor, cornerSize, ImDrawFlags.RoundCornersAll);
                drawList.AddRectFilled(topLeft + barFillPosOffset, filledSegmentEnd, barFillColor, cornerSize, ImDrawFlags.RoundCornersAll);
                drawList.AddRect(topLeft, bottomRight, barBorderColor, cornerSize, ImDrawFlags.RoundCornersAll, borderThickness);
                ImGui.End();
            }
            ImGui.PopStyleVar();
        }

        private void DrawConfigWindow()
        {
            ImGui.SetNextWindowSize(configInitialSize, ImGuiCond.Appearing);
            ImGui.Begin("Timer Settings", ref configVisible, ImGuiWindowFlags.NoResize);

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
            ImGui.End();
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
            var HPbarPosition = new[] { (int)config.HPBarPosition.X, (int)config.HPBarPosition.Y };
            if (DragInput2(ref HPbarPosition, "HPPositionX", "HPPositionY", "HP Bar Position"))
            {
                config.HPBarPosition = new Vector2(HPbarPosition[0], HPbarPosition[1]);
                ImGui.SetWindowPos(HPBarName, config.HPBarPosition);
            }

            var HPbarSize = new[] { (int)config.HPBarSize.X, (int)config.HPBarSize.Y };
            if (DragInput2(ref HPbarSize, "HPSizeX", "HPSizeY", "HP Bar Size"))
            {
                config.HPBarSize = new Vector2(HPbarSize[0], HPbarSize[1]);
                ImGui.SetWindowSize(HPBarName, config.HPBarSize);
            }
            ImGui.Spacing();

            var MPbarPosition = new[] { (int)config.MPBarPosition.X, (int)config.MPBarPosition.Y };
            if (DragInput2(ref MPbarPosition, "MPPositionX", "MPPositionY", "MP Bar Position"))
            {
                config.MPBarPosition = new Vector2(MPbarPosition[0], MPbarPosition[1]);
                ImGui.SetWindowPos(MPBarName, config.MPBarPosition);
            }

            var MPbarSize = new[] { (int)config.MPBarSize.X, (int)config.MPBarSize.Y };
            if (DragInput2(ref MPbarSize, "MPSizeX", "MPSizeY", "MP Bar Size"))
            {
                config.MPBarSize = new Vector2(MPbarSize[0], MPbarSize[1]);
                ImGui.SetWindowSize(MPBarName, config.MPBarSize);
            }
            ImGui.Spacing();
            ImGui.Unindent();
        }

        private static void UpdateSavedWindowConfig(Vector2 currentPos, Vector2 currentSize, string val)
        {
            if (config.LockBar ||
                (currentPos.Equals(config.HPBarPosition) &&
                currentSize.Equals(config.HPBarSize)) ||
                (currentPos.Equals(config.MPBarPosition) &&
                currentSize.Equals(config.MPBarSize)))
            {
                return;
            }
            if (val.Equals("HP"))
            {
                config.HPBarPosition = currentPos;
                config.HPBarSize = currentSize;
            }
            else if (val.Equals("MP"))
            {
                config.MPBarPosition = currentPos;
                config.MPBarSize = currentSize;
            }
            config.Save();
        }

        private static bool DragInput2(ref int[] vector, string label1, string label2, string description)
        {
            var change = false;
            ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
            if (ImGui.DragInt($"##{label1}", ref vector[0]))
            {
                change = true;
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
            if (ImGui.DragInt($"##{label2}", ref vector[1]))
            {
                change = true;
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.Text(description);
            return change;
        }
    }
}
