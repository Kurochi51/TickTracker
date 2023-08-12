using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace TickTracker.Windows;

public class MPBar : BarWindowBase
{
    private const float _actorTickInterval = 3, _fastTickInterval = 1.5f;
    public double LastTick = 1;

    public MPBar(IClientState clientState) : base(clientState, WindowType.MpWindow, "MPBarWindow")
    {
        Size = Config.MPBarSize * ImGuiHelpers.GlobalScale;

        PositionCondition = ImGuiCond.FirstUseEver;
        Position = Config.MPBarPosition;
    }

    public override void Draw()
    {
        var now = DateTime.Now.TimeOfDay.TotalSeconds;
        UpdateWindow();
        var progress = (float)((now - LastTick) / (FastTick ? _fastTickInterval : _actorTickInterval));
        if (progress > 1)
        {
            progress = 1;
        }

        DrawProgress(progress);
    }

    private void UpdateWindow()
    {
        if (Config.LockBar)
        {
            return;
        }
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        if (IsFocused)
        {
            Utilities.UpdateWindowConfig(windowPos, windowSize, WindowType);
        }
        else
        {
            if (windowPos != Config.MPBarPosition)
            {
                ImGui.SetWindowPos(Config.MPBarPosition);
            }
            if (windowSize != Config.MPBarSize)
            {
                ImGui.SetWindowSize(Config.MPBarSize);
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
        drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight + barFillSizeOffset, ImGui.GetColorU32(Config.MPBarBackgroundColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(topLeft + barFillPosOffset, filledWidth, ImGui.GetColorU32(Config.MPBarFillColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRect(topLeft, bottomRight, ImGui.GetColorU32(Config.MPBarBorderColor), cornerRounding, ImDrawFlags.RoundCornersAll, borderThickness);
    }
}
