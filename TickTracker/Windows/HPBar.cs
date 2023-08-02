using Dalamud.Interface.Windowing;
using System;
using System.Numerics;
using ImGuiNET;

namespace TickTracker.Windows;

public class HPBar : Window
{
    private static Configuration config => TickTrackerSystem.config;
    private const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    private double now;
    public double LastHPTick = 1;
    public bool HPFastTick;
    private readonly Vector2 barFillPosOffset = new(1, 1);
    private readonly Vector2 barFillSizeOffset = new(-1, 0);
    private readonly Vector2 barWindowPadding = new(8, 14);

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
        SizeCondition = ImGuiCond.FirstUseEver;

        Position = config.HPBarPosition;
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions()
    {
        if (!Services.ClientState.IsLoggedIn)
        {
            return false;
        }
        if (!Utilities.WindowCondition(WindowType.HpWindow))
        {
            return false;
        }
        return true;
    }

    public override void PreDraw()
    {
        //PushStyles
        Flags = DefaultFlags;
        if (config.LockBar)
        {
            Flags |= LockedBarFlags;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
        now = ImGui.GetTime();
    }

    public override void Draw()
    {
        UpdateSavedWindowConfig(ImGui.GetWindowPos(), ImGui.GetWindowSize());
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
    }

    public override void PostDraw()
    {
        //PopStyles
        ImGui.PopStyleVar();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static void UpdateSavedWindowConfig(Vector2 currentPos, Vector2 currentSize)
    {
        if (config.LockBar)
        {
            return;
        }
        if (!currentPos.Equals(config.HPBarPosition))
        {
            config.HPBarPosition = currentPos;
            config.Save();
        }
        if (!currentSize.Equals(config.HPBarSize))
        {
            config.HPBarSize = currentSize;
            config.Save();
        }
    }
}
