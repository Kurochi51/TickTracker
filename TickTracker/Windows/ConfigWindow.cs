using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Components;
using ImGuiNET;

namespace TickTracker.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private Configuration configuration;
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
            configuration = plugin.Configuration;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public override void Draw()
        {
            now = ImGui.GetTime();
            if (HPBarVisible && configuration.HPVisible) DrawHPBarWindow();
            if (MPBarVisible && configuration.MPVisible) DrawMPBarWindow();
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
                configVisible = false;
            }
            ImGui.SetCursorPos(originPos);
        }

        private void DrawHPBarWindow()
        {
            ImGui.SetNextWindowSize(configuration.HPBarSize, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(configuration.HPBarPosition, ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
            var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
            if (configuration.LockBar) windowFlags |= LockedBarFlags;
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
                var filledSegmentEnd = new Vector2(barWidth * progress + barWindowPadding.X, bottomRight.Y - 1);

                // Convert imgui window-space rects to screen-space
                var windowPosition = ImGui.GetWindowPos();
                topLeft += windowPosition;
                bottomRight += windowPosition;
                filledSegmentEnd += windowPosition;

                // Draw main bar
                const float cornerSize = 2f;
                const float borderThickness = 1.35f;
                var drawList = ImGui.GetWindowDrawList();
                var barBackgroundColor = ImGui.GetColorU32(configuration.HPBarBackgroundColor);
                var barFillColor = ImGui.GetColorU32(configuration.HPBarFillColor);
                var barBorderColor = ImGui.GetColorU32(configuration.HPBarBorderColor);
                drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight + barFillSizeOffset, barBackgroundColor);
                drawList.AddRectFilled(topLeft + barFillPosOffset, filledSegmentEnd, barFillColor);
                drawList.AddRect(topLeft, bottomRight, barBorderColor, cornerSize, ImDrawFlags.RoundCornersAll, borderThickness);
                ImGui.End();
            }
            ImGui.PopStyleVar();
        }

        private void DrawMPBarWindow()
        {
            ImGui.SetNextWindowSize(configuration.MPBarSize, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(configuration.MPBarPosition, ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
            var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
            if (configuration.LockBar) windowFlags |= LockedBarFlags;
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
                var filledSegmentEnd = new Vector2(barWidth * progress + barWindowPadding.X, bottomRight.Y - 1);

                // Convert imgui window-space rects to screen-space
                var windowPosition = ImGui.GetWindowPos();
                topLeft += windowPosition;
                bottomRight += windowPosition;
                filledSegmentEnd += windowPosition;

                // Draw main bar
                const float cornerSize = 2f;
                const float borderThickness = 1.35f;
                var drawList = ImGui.GetWindowDrawList();
                var barBackgroundColor = ImGui.GetColorU32(configuration.MPBarBackgroundColor);
                var barFillColor = ImGui.GetColorU32(configuration.MPBarFillColor);
                var barBorderColor = ImGui.GetColorU32(configuration.MPBarBorderColor);
                drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight + barFillSizeOffset, barBackgroundColor);
                drawList.AddRectFilled(topLeft + barFillPosOffset, filledSegmentEnd, barFillColor);
                drawList.AddRect(topLeft, bottomRight, barBorderColor, cornerSize, ImDrawFlags.RoundCornersAll, borderThickness);
                ImGui.End();
            }
            ImGui.PopStyleVar();
        }

        private void DrawConfigWindow()
        {
            ImGui.SetNextWindowSize(configInitialSize, ImGuiCond.Appearing);
            ImGui.Begin("Timer Settings", ref configVisible, ImGuiWindowFlags.NoResize);

            var pluginEnabled = configuration.PluginEnabled;
            if (ImGui.Checkbox("Enable plugin", ref pluginEnabled))
            {
                configuration.PluginEnabled = pluginEnabled;
                configuration.Save();
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

        private void DrawAppearanceTab()
        {
            if (ImGui.BeginTabItem("Appearance"))
            {
                ImGui.Spacing();
                var lockBar = configuration.LockBar;
                if (ImGui.Checkbox("Lock bar size and position", ref lockBar))
                {
                    configuration.LockBar = lockBar;
                    configuration.Save();
                }

                if (!configuration.LockBar)
                {
                    ImGui.Indent();
                    ImGui.Spacing();
                    var HPbarPosition = new[] { (int)configuration.HPBarPosition.X, (int)configuration.HPBarPosition.Y };
                    if (DragInput2(ref HPbarPosition, "HPPositionX", "HPPositionY", "HP Bar Position"))
                    {
                        configuration.HPBarPosition = new Vector2(HPbarPosition[0], HPbarPosition[1]);
                        ImGui.SetWindowPos(HPBarName, configuration.HPBarPosition);
                    }

                    var HPbarSize = new[] { (int)configuration.HPBarSize.X, (int)configuration.HPBarSize.Y };
                    if (DragInput2(ref HPbarSize, "HPSizeX", "HPSizeY", "HP Bar Size"))
                    {
                        configuration.HPBarSize = new Vector2(HPbarSize[0], HPbarSize[1]);
                        ImGui.SetWindowSize(HPBarName, configuration.HPBarSize);
                    }
                    ImGui.Spacing();

                    var MPbarPosition = new[] { (int)configuration.MPBarPosition.X, (int)configuration.MPBarPosition.Y };
                    if (DragInput2(ref MPbarPosition, "MPPositionX", "MPPositionY", "MP Bar Position"))
                    {
                        configuration.MPBarPosition = new Vector2(MPbarPosition[0], MPbarPosition[1]);
                        ImGui.SetWindowPos(MPBarName, configuration.MPBarPosition);
                    }

                    var MPbarSize = new[] { (int)configuration.MPBarSize.X, (int)configuration.MPBarSize.Y };
                    if (DragInput2(ref MPbarSize, "MPSizeX", "MPSizeY", "MP Bar Size"))
                    {
                        configuration.MPBarSize = new Vector2(MPbarSize[0], MPbarSize[1]);
                        ImGui.SetWindowSize(MPBarName, configuration.MPBarSize);
                    }
                    ImGui.Spacing();
                    ImGui.Unindent();
                }

                ImGui.Text("HP Bar Background Color");
                ImGui.SameLine();
                var newHPBarBackgroundColor = ImGuiComponents.ColorPickerWithPalette(2, "HP Bar Background Color",
                    configuration.HPBarBackgroundColor);
                ImGui.SameLine();
                if (ImGui.Button("Reset##ResetHPBarBackgroundColor"))
                {
                    configuration.ResetPropertyToDefault("HPBarBackgroundColor");
                    newHPBarBackgroundColor = configuration.HPBarBackgroundColor;
                }

                ImGui.Text("HP Bar Fill Color");
                ImGui.SameLine();
                var newHPBarFillColor = ImGuiComponents.ColorPickerWithPalette(3, "HP Bar Fill Color",
                    configuration.HPBarFillColor);
                ImGui.SameLine();
                if (ImGui.Button("Reset##HPResetBarFillColor"))
                {
                    configuration.ResetPropertyToDefault("HPBarFillColor");
                    newHPBarFillColor = configuration.HPBarFillColor;
                }

                ImGui.Text("HP Bar Border Color");
                ImGui.SameLine();
                var newHPBarBorderColor = ImGuiComponents.ColorPickerWithPalette(4, "HP Bar Border Color",
                    configuration.HPBarBorderColor);
                ImGui.SameLine();
                if (ImGui.Button("Reset##HPResetBarBorderColor"))
                {
                    configuration.ResetPropertyToDefault("HPBarBorderColor");
                    newHPBarBorderColor = configuration.HPBarBorderColor;
                }
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("MP Bar Background Color");
                ImGui.SameLine();
                var newMPBarBackgroundColor = ImGuiComponents.ColorPickerWithPalette(5, "MP Bar Background Color",
                    configuration.MPBarBackgroundColor);
                ImGui.SameLine();
                if (ImGui.Button("Reset##ResetMPBarBackgroundColor"))
                {
                    configuration.ResetPropertyToDefault("MPBarBackgroundColor");
                    newMPBarBackgroundColor = configuration.MPBarBackgroundColor;
                }

                ImGui.Text("MP Bar Fill Color");
                ImGui.SameLine();
                var newMPBarFillColor = ImGuiComponents.ColorPickerWithPalette(6, "MP Bar Fill Color",
                    configuration.MPBarFillColor);
                ImGui.SameLine();
                if (ImGui.Button("Reset##MPResetBarFillColor"))
                {
                    configuration.ResetPropertyToDefault("MPBarFillColor");
                    newMPBarFillColor = configuration.MPBarFillColor;
                }

                ImGui.Text("MP Bar Border Color");
                ImGui.SameLine();
                var newMPBarBorderColor = ImGuiComponents.ColorPickerWithPalette(7, "MP Bar Border Color",
                    configuration.MPBarBorderColor);
                ImGui.SameLine();
                if (ImGui.Button("Reset##MPResetBarBorderColor"))
                {
                    configuration.ResetPropertyToDefault("MPBarBorderColor");
                    newMPBarBorderColor = configuration.MPBarBorderColor;
                }

                if (!newHPBarBackgroundColor.Equals(configuration.HPBarBackgroundColor) ||
                    !newHPBarFillColor.Equals(configuration.HPBarFillColor) ||
                    !newHPBarBorderColor.Equals(configuration.HPBarBorderColor) ||
                    !newMPBarBackgroundColor.Equals(configuration.MPBarBackgroundColor) ||
                    !newMPBarFillColor.Equals(configuration.MPBarFillColor) ||
                    !newMPBarBorderColor.Equals(configuration.MPBarBorderColor))
                {
                    configuration.HPBarBackgroundColor = newHPBarBackgroundColor;
                    configuration.HPBarFillColor = newHPBarFillColor;
                    configuration.HPBarBorderColor = newHPBarBorderColor;
                    configuration.MPBarBackgroundColor = newMPBarBackgroundColor;
                    configuration.MPBarFillColor = newMPBarFillColor;
                    configuration.MPBarBorderColor = newMPBarBorderColor;
                    configuration.Save();
                }

                ImGui.EndTabItem();
            }
        }

        private void DrawBehaviorTab()
        {
            if (ImGui.BeginTabItem("Behavior"))
            {
                ImGui.Spacing();
                var hideOutOfCombat = configuration.HideOutOfCombat;
                if (ImGui.Checkbox("Hide while not in combat", ref hideOutOfCombat))
                {
                    configuration.HideOutOfCombat = hideOutOfCombat;
                    configuration.Save();
                }

                ImGui.Indent();
                var showInDuties = configuration.AlwaysShowInDuties;
                if (ImGui.Checkbox("Always show while in duties", ref showInDuties))
                {
                    configuration.AlwaysShowInDuties = showInDuties;
                    configuration.Save();
                }

                var showWithHostileTarget = configuration.AlwaysShowWithHostileTarget;
                if (ImGui.Checkbox("Always show with enemy target", ref showWithHostileTarget))
                {
                    configuration.AlwaysShowWithHostileTarget = showWithHostileTarget;
                    configuration.Save();
                }
                ImGui.Unindent();
                var hpVisible = configuration.HPVisible;
                if (ImGui.Checkbox("Show HP Bar", ref hpVisible))
                {
                    configuration.HPVisible = hpVisible;
                    configuration.Save();
                }
                var mpVisible = configuration.MPVisible;
                if (ImGui.Checkbox("Show MP Bar", ref mpVisible))
                {
                    configuration.MPVisible = mpVisible;
                    configuration.Save();
                }
                ImGui.EndTabItem();
            }
        }

        private void UpdateSavedWindowConfig(Vector2 currentPos, Vector2 currentSize, string val)
        {
            if (configuration.LockBar ||
                (currentPos.Equals(configuration.HPBarPosition) &&
                currentSize.Equals(configuration.HPBarSize)) ||
                (currentPos.Equals(configuration.MPBarPosition) &&
                currentSize.Equals(configuration.MPBarSize)))
            {
                return;
            }
            if (val.Equals("HP"))
            {
                configuration.HPBarPosition = currentPos;
                configuration.HPBarSize = currentSize;
            }
            else if (val.Equals("MP"))
            {
                configuration.MPBarPosition = currentPos;
                configuration.MPBarSize = currentSize;
            }
            configuration.Save();
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
