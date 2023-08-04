using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;

namespace TickTracker.Windows;

public class HPBar : Window, IDisposable
{
    private static Configuration config => TickTrackerSystem.config;
    private readonly WindowType window = WindowType.HpWindow;
    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    public double now;
    public double LastTick = 1;
    public bool FastTick, UpdateAvailable=false;
    private readonly Vector2 barFillPosOffset = new(1, 1);
    private readonly Vector2 barFillSizeOffset = new(-1, 0);
    private readonly Vector2 barWindowPadding = new(8, 14);
    private readonly Vector2 intendedSize = new(180, 50);

    private const ImGuiWindowFlags DefaultFlags = ImGuiWindowFlags.NoScrollbar |
                                                  ImGuiWindowFlags.NoTitleBar |
                                                  ImGuiWindowFlags.NoCollapse;

    private const ImGuiWindowFlags LockedBarFlags = ImGuiWindowFlags.NoBackground |
                                                    ImGuiWindowFlags.NoMove |
                                                    ImGuiWindowFlags.NoResize |
                                                    ImGuiWindowFlags.NoNav |
                                                    ImGuiWindowFlags.NoInputs;
    public HPBar() : base ("HPBarWindow")
    {
        Size = config.HPBarSize;
        SizeCondition = ImGuiCond.Always;
        //PluginLog.Debug("Starting with this size: {size} in Constructor", Size);

        Position = config.HPBarPosition;
        PositionCondition = ImGuiCond.Always;
    }

    public override bool DrawConditions()
    {
        if (!Service.ClientState.IsLoggedIn)
        {
            return false;
        }
        if (!Utilities.WindowCondition(WindowType.HpWindow))
        {
            return false;
        }
        if (UpdateAvailable)
        {
            UpdateAvailable = false;
            return true;
        }
        return true;
    }

    public override void PreDraw()
    {
        Flags = DefaultFlags;
        if (config.LockBar)
        {
            Flags |= LockedBarFlags;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
        now = DateTime.Now.TimeOfDay.TotalSeconds;
    }

    public override void Draw()
    {
        UpdateWindow();
        var progress = (float)((now - LastTick) / (FastTick ? FastTickInterval : ActorTickInterval));
        if (progress > 1)
        {
            progress = 1;
        }

        DrawProgress(progress);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
    }

    private void UpdateWindow()
    {
        if (config.LockBar)
        {
            //PluginLog.Debug("Size is {size} and Position is {pos} in UpdateWindow", ImGui.GetWindowSize(), ImGui.GetWindowPos());
            return;
        }
        var windowPos = this.Position;//ImGui.GetWindowPos();
        var windowSize = this.Size;//ImGui.GetWindowSize();
        if (IsFocused)
        {
            Utilities.UpdateWindowConfig((Vector2)windowPos!, (Vector2)windowSize!, window);
            //PluginLog.Debug("Size is {size} in focused UpdateWindow", windowSize);
        }
        else
        {
            if (windowPos != config.HPBarPosition)
            {
                //ImGui.SetWindowPos(config.HPBarPosition);
                this.Size = config.HPBarPosition;
            }
            if (windowSize != config.HPBarSize)
            {
                //PluginLog.Debug("Size is {size} in unfocused UpdateWindow", windowSize);
                //ImGui.SetWindowSize(config.HPBarSize);
                this.Size = config.HPBarSize;
            }
        }
    }

    private void DrawProgress(float progress)
    {
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
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
