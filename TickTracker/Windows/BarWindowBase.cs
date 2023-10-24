using System;
using System.Numerics;

using ImGuiNET;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using TickTracker.Enums;

namespace TickTracker.Windows;

public abstract class BarWindowBase : Window
{
    protected WindowType WindowType { get; set; }
    protected const ImGuiWindowFlags _defaultFlags = ImGuiWindowFlags.NoScrollbar |
                                              ImGuiWindowFlags.NoTitleBar |
                                              ImGuiWindowFlags.NoCollapse;

    protected const ImGuiWindowFlags _lockedBarFlags = ImGuiWindowFlags.NoBackground |
                                                    ImGuiWindowFlags.NoMove |
                                                    ImGuiWindowFlags.NoResize |
                                                    ImGuiWindowFlags.NoNav |
                                                    ImGuiWindowFlags.NoInputs;
    public IPluginLog log { get; }
    public bool ProgressHalted { get; set; }
    public bool RegenProgressActive { get; set; }
    public bool TickUpdate { get; set; }
    public double Tick { get; set; }
    public double PreviousProgress { get; set; }
    public double Progress { get; set; }
    public Vector2 WindowPosition { get; set; }
    public Vector2 WindowSize { get; set; }

    private readonly IClientState clientState;
    private readonly Configuration config;
    private readonly Utilities utilities;

    private bool firstTime = true;
    private Vector2 invalidSize = new(0, 0);
    private Vector2 iconSize;

    protected BarWindowBase(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config, WindowType type, string name) : base(name)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.FirstUseEver;

        clientState = _clientState;
        log = _pluginLog;
        utilities = _utilities;
        config = _config;
        WindowType = type;
        iconSize = invalidSize;
    }

    public override bool DrawConditions()
    {
        if (!clientState.IsLoggedIn)
        {
            return false;
        }
        if (!utilities.WindowCondition(WindowType))
        {
            return false;
        }
        return true;
    }

    public override void PreDraw()
    {
        if (firstTime)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            iconSize = ImGui.CalcTextSize(FontAwesomeIcon.Forward.ToIconString());
            ImGui.PopFont();
            firstTime = false;
        }
        var barWindowPadding = new Vector2(8, 14);
        Flags = _defaultFlags;
        if (config.LockBar)
        {
            Flags |= _lockedBarFlags;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
    }

    public void DrawProgress(double progress, Vector4 backgroundColor, Vector4 fillColor, Vector4 borderColor, Vector4 iconColor)
    {
        var floatProgress = (float)progress;
        var cornerRounding = 4f; // Maybe make it user configurable?
        var borderThickness = 1.35f; // Maybe make it user configurable?
        var barFillPosOffset = new Vector2(1, 1);
        var barFillSizeOffset = new Vector2(-1, 0);
        var topLeft = ImGui.GetWindowContentRegionMin() + ImGui.GetWindowPos();
        var bottomRight = ImGui.GetWindowContentRegionMax() + ImGui.GetWindowPos();
        bottomRight = (floatProgress <= 0) ? bottomRight - barFillSizeOffset : bottomRight;

        // Calculate floatProgress bar dimensions
        var barWidth = bottomRight.X - topLeft.X;
        var filledWidth = new Vector2((barWidth * Math.Max(floatProgress, 0.0001f)) + topLeft.X, bottomRight.Y);
        filledWidth = (floatProgress <= 0) ? filledWidth - barFillSizeOffset : filledWidth;

        // Draw main bar
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight, ImGui.GetColorU32(backgroundColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(topLeft + barFillPosOffset, filledWidth, ImGui.GetColorU32(fillColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRect(topLeft, bottomRight, ImGui.GetColorU32(borderColor), cornerRounding, ImDrawFlags.RoundCornersAll, borderThickness);

        if (RegenProgressActive)
        {
            var color = ColorHelpers.RgbaVector4ToUint(iconColor);
            var icon = FontAwesomeIcon.Forward.ToIconString();
            var pos = (topLeft + barFillPosOffset + bottomRight) / 2;
            pos = iconSize == invalidSize ? pos : pos - (iconSize / 2);
            drawList.AddText(UiBuilder.IconFont, ImGui.GetFontSize() * 0.85f, pos, color, icon);
        }
    }
}
