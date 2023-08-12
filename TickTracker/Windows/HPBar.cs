using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
namespace TickTracker.Windows;

public class HPBar : BarWindowBase
{
    private const float _actorTickInterval = 3, _fastTickInterval = 1.5f;
    public double LastTick = 1;

    public HPBar(IClientState clientState) : base(clientState, WindowType.HpWindow, "HPBarWindow")
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        Size = Config.HPBarSize * ImGuiHelpers.GlobalScale;

        Position = Config.HPBarPosition;
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
            if (windowPos != Config.HPBarPosition)
            {
                ImGui.SetWindowPos(Config.HPBarPosition);
            }
            if (windowSize != Config.HPBarSize)
            {
                ImGui.SetWindowSize(Config.HPBarSize);
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
        drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight + barFillSizeOffset, ImGui.GetColorU32(Config.HPBarBackgroundColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(topLeft + barFillPosOffset, filledWidth, ImGui.GetColorU32(Config.HPBarFillColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRect(topLeft, bottomRight, ImGui.GetColorU32(Config.HPBarBorderColor), cornerRounding, ImDrawFlags.RoundCornersAll, borderThickness);
    }
}
