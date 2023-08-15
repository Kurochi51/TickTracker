using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace TickTracker.Windows;

public class HPBar : BarWindowBase
{
    private readonly Utilities utilities;
    public HPBar(IClientState _clientState, Utilities _utilities) : base(_clientState, Enum.WindowType.HpWindow, "HPBarWindow")
    {
        Size = config.HPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.HPBarPosition;
        utilities = _utilities;
    }

    public override void Draw()
    {
        var now = DateTime.Now.TimeOfDay.TotalSeconds;
        UpdateWindow();
        var progress = (float)((now - LastTick) / (FastTick ? FastTickInterval : ActorTickInterval));
        if (RegenHalted)
        {
            progress = PreviousProgress;
        }
        if (progress > 1)
        {
            progress = 1;
        }
        DrawProgress(progress);
        PreviousProgress = progress;
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
            utilities.UpdateWindowConfig(windowPos, windowSize, WindowType);
        }
        else
        {
            if (windowPos != config.HPBarPosition)
            {
                ImGui.SetWindowPos(config.HPBarPosition);
            }
            if (windowSize != config.HPBarSize)
            {
                ImGui.SetWindowSize(config.HPBarSize);
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
        bottomRight = (progress <= 0) ? bottomRight - barFillSizeOffset : bottomRight;

        // Calculate progress bar dimensions
        var barWidth = bottomRight.X - topLeft.X;
        var filledWidth = new Vector2((barWidth * Math.Max(progress, 0.0001f)) + topLeft.X, bottomRight.Y);
        filledWidth = (progress <= 0) ? filledWidth - barFillSizeOffset : filledWidth;

        // Draw main bar
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight, ImGui.GetColorU32(config.HPBarBackgroundColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(topLeft + barFillPosOffset, filledWidth, ImGui.GetColorU32(config.HPBarFillColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRect(topLeft, bottomRight, ImGui.GetColorU32(config.HPBarBorderColor), cornerRounding, ImDrawFlags.RoundCornersAll, borderThickness);
    }
}
