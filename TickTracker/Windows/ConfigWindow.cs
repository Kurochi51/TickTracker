using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Components;
using ImGuiNET;

namespace TickTracker.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration configuration;
    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    private const string BarName = "TimerBar";
    private double now;
    private bool configVisible;
    public double LastTick = 1;
    public bool FastTick;
    public bool BarVisible { get; set; }
    public bool ConfigVisible
    {
        get => this.configVisible;
        set => this.configVisible = value;
    }
    private readonly Vector2 barFillPosOffset = new(1, 1);
    private readonly Vector2 barFillSizeOffset = new(-1, 0);
    private readonly Vector2 barWindowPadding = new(8, 14);
    private readonly Vector2 configInitialSize = new(300, 350);
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
        if (BarVisible) DrawBarWindow();
        if (ConfigVisible) DrawConfigWindow();
    }

    private void SaveAndClose()
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

    private void DrawBarWindow()
    {
        ImGui.SetNextWindowSize(configuration.BarSize, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(configuration.BarPosition, ImGuiCond.FirstUseEver);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
        var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        if (configuration.LockBar) windowFlags |= LockedBarFlags;
        ImGui.Begin(BarName, windowFlags);
        UpdateSavedWindowConfig(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        float progress;
        if (FastTick)
        {
            progress = (float)((now - LastTick) / FastTickInterval);
        }
        else
        {
            progress = (float)((now - LastTick) / ActorTickInterval);
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
        var barBackgroundColor = ImGui.GetColorU32(configuration.BarBackgroundColor);
        var barFillColor = ImGui.GetColorU32(configuration.BarFillColor);
        var barBorderColor = ImGui.GetColorU32(configuration.BarBorderColor);
        drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight + barFillSizeOffset, barBackgroundColor);
        drawList.AddRectFilled(topLeft + barFillPosOffset, filledSegmentEnd, barFillColor);
        drawList.AddRect(topLeft, bottomRight, barBorderColor, cornerSize, ImDrawFlags.RoundCornersAll,borderThickness);
        ImGui.End();
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
        SaveAndClose();
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
                int[] barPosition = { (int)configuration.BarPosition.X, (int)configuration.BarPosition.Y };
                if (ImGui.DragInt2("Position", ref barPosition[0]))
                {
                    ImGui.SetWindowPos(BarName, new Vector2(barPosition[0], barPosition[1]));
                }

                int[] barSize = { (int)configuration.BarSize.X, (int)configuration.BarSize.Y };
                if (ImGui.DragInt2("Size", ref barSize[0]))
                {
                    ImGui.SetWindowSize(BarName, new Vector2(barSize[0], barSize[1]));
                }
                ImGui.Unindent();
            }

            ImGui.Text("Bar Background Color");
            ImGui.SameLine();
            var newBarBackgroundColor = ImGuiComponents.ColorPickerWithPalette(2, "Bar Background Color",
                configuration.BarBackgroundColor);
            ImGui.SameLine();
            if (ImGui.Button("Reset##ResetBarBackgroundColor"))
            {
                configuration.ResetPropertyToDefault("BarBackgroundColor");
                newBarBackgroundColor = configuration.BarBackgroundColor;
            }

            ImGui.Text("Bar Fill Color");
            ImGui.SameLine();
            var newBarFillColor = ImGuiComponents.ColorPickerWithPalette(3, "Bar Fill Color",
                configuration.BarFillColor);
            ImGui.SameLine();
            if (ImGui.Button("Reset##ResetBarFillColor"))
            {
                configuration.ResetPropertyToDefault("BarFillColor");
                newBarFillColor = configuration.BarFillColor;
            }

            ImGui.Text("Bar Border Color");
            ImGui.SameLine();
            var newBarBorderColor = ImGuiComponents.ColorPickerWithPalette(4, "Bar Border Color",
                configuration.BarBorderColor);
            ImGui.SameLine();
            if (ImGui.Button("Reset##ResetBarBorderColor"))
            {
                configuration.ResetPropertyToDefault("BarBorderColor");
                newBarBorderColor = configuration.BarBorderColor;
            }

            if (!newBarBackgroundColor.Equals(configuration.BarBackgroundColor) ||
                !newBarFillColor.Equals(configuration.BarFillColor) ||
                !newBarBorderColor.Equals(configuration.BarBorderColor))
            {
                configuration.BarBackgroundColor = newBarBackgroundColor;
                configuration.BarFillColor = newBarFillColor;
                configuration.BarBorderColor = newBarBorderColor;
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

            ImGui.EndTabItem();
        }
    }

    private void UpdateSavedWindowConfig(Vector2 currentPos, Vector2 currentSize)
    {
        if (configuration.LockBar ||
            currentPos.Equals(configuration.BarPosition) && currentSize.Equals(configuration.BarSize))
        {
            return;
        }
        configuration.BarPosition = currentPos;
        configuration.BarSize = currentSize;
        configuration.Save();
    }
}
