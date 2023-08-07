using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace TickTracker.Windows;

public class MPBar : Window, IDisposable
{
    private static Configuration config => TickTrackerSystem.config;
    private readonly WindowType window = WindowType.MpWindow;
    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    public double LastTick = 1;
    public bool FastTick, UpdateAvailable = false;

    private const ImGuiWindowFlags DefaultFlags = ImGuiWindowFlags.NoScrollbar |
                                                  ImGuiWindowFlags.NoTitleBar |
                                                  ImGuiWindowFlags.NoCollapse;

    private const ImGuiWindowFlags LockedBarFlags = ImGuiWindowFlags.NoBackground |
                                                    ImGuiWindowFlags.NoMove |
                                                    ImGuiWindowFlags.NoResize |
                                                    ImGuiWindowFlags.NoNav |
                                                    ImGuiWindowFlags.NoInputs;
    public MPBar() : base("MPBarWindow")
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        Size = config.MPBarSize * ImGuiHelpers.GlobalScale;

        PositionCondition = ImGuiCond.FirstUseEver;
        Position = config.MPBarPosition;
    }

    public override bool DrawConditions()
    {
        if (!Service.ClientState.IsLoggedIn)
        {
            return false;
        }
        if (!Utilities.WindowCondition(WindowType.MpWindow))
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
        var barWindowPadding = new Vector2(8, 14);
        Flags = DefaultFlags;
        if (config.LockBar)
        {
            Flags |= LockedBarFlags;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
    }

    public override void Draw()
    {
        var now = DateTime.Now.TimeOfDay.TotalSeconds;
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
            return;
        }
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        if (IsFocused)
        {
            Utilities.UpdateWindowConfig(windowPos, windowSize, window);
        }
        else
        {
            if (windowPos != config.MPBarPosition)
            {
                ImGui.SetWindowPos(config.MPBarPosition);
            }
            if (windowSize != config.MPBarSize)
            {
                ImGui.SetWindowSize(config.MPBarSize);
            }
        }
    }

    private static void DrawProgress(float progress)
    {
        var cornerRounding = 4f; // Maybe make it user configurable?
        var borderThickness = 1.35f; // Maybe make it user configurable?
        var barFillPosOffset = new Vector2(1, 1);
        var barFillSizeOffset = new Vector2(-1, 0);
        var topLeft = ImGui.GetWindowContentRegionMin() + ImGui.GetWindowPos();
        var bottomRight = ImGui.GetWindowContentRegionMax() + ImGui.GetWindowPos();

        // Calculate progress bar dimensions
        var barWidth = bottomRight.X - topLeft.X;
        var filledWidth = new Vector2((barWidth * progress) + topLeft.X, bottomRight.Y - 1);

        // Draw main bar
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight + barFillSizeOffset, ImGui.GetColorU32(config.MPBarBackgroundColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(topLeft + barFillPosOffset, filledWidth, ImGui.GetColorU32(config.MPBarFillColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRect(topLeft, bottomRight, ImGui.GetColorU32(config.MPBarBorderColor), cornerRounding, ImDrawFlags.RoundCornersAll, borderThickness);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
