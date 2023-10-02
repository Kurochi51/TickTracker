using System;
using System.Numerics;

using ImGuiNET;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
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
    public bool FastRegenSwitch { get; set; }
    public bool RegenHalted { get; set; }
    public bool FastTick { get; set; }
    public bool CanUpdate { get; set; } = true;
    public bool DelayedUpdate { get; set; } = true;
    public double LastTick { get; set; } = 1;
    public double PreviousProgress { get; set; } = -1;
    public static readonly float ActorTickInterval = 3, FastTickInterval = 1.5f;

    private readonly IClientState clientState;
    private readonly Configuration config;
    private readonly Utilities utilities;

    protected BarWindowBase(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config, WindowType type, string name) : base(name)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.FirstUseEver;

        clientState = _clientState;
        log = _pluginLog;
        utilities = _utilities;
        config = _config;
        WindowType = type;
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

    public static void DrawProgress(double progress, Vector4 backgroundColor, Vector4 fillColor, Vector4 borderColor)
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
        var filledWidth = new Vector2((barWidth * (float)Math.Max(progress, 0.0001f)) + topLeft.X, bottomRight.Y);
        filledWidth = (progress <= 0) ? filledWidth - barFillSizeOffset : filledWidth;

        // Draw main bar
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight, ImGui.GetColorU32(backgroundColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(topLeft + barFillPosOffset, filledWidth, ImGui.GetColorU32(fillColor), cornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRect(topLeft, bottomRight, ImGui.GetColorU32(borderColor), cornerRounding, ImDrawFlags.RoundCornersAll, borderThickness);
    }
}
