using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
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
    public bool RegenActive { get; set; }
    public bool FastRegen { get; set; }
    public bool TickUpdate { get; set; }
    public double Tick { get; set; }
    public double PreviousProgress { get; set; }
    public double Progress { get; set; }
    public Vector2 WindowPosition { get; set; }
    public Vector2 WindowSize { get; set; }

    private const FontAwesomeIcon RegenIcon = FontAwesomeIcon.Forward;
    private const FontAwesomeIcon FastIcon = FontAwesomeIcon.FastForward;
    private const FontAwesomeIcon PauseIcon = FontAwesomeIcon.Pause;
    private const FontAwesomeIcon InvalidIcon = FontAwesomeIcon.None;

    private readonly IClientState clientState;
    private readonly Configuration config;
    private readonly Utilities utilities;

    private readonly IDictionary<string, Vector2> iconDictionary = new Dictionary<string, Vector2>(StringComparer.Ordinal);
    private Vector2 invalidSize = new(0, 0);
    private float currentFontSize;

    protected BarWindowBase(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config, WindowType type, string name) : base(name)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.FirstUseEver;

        clientState = _clientState;
        log = _pluginLog;
        utilities = _utilities;
        config = _config;
        WindowType = type;

        iconDictionary.Add(RegenIcon.ToIconString(), invalidSize);
        iconDictionary.Add(FastIcon.ToIconString(), invalidSize);
        iconDictionary.Add(PauseIcon.ToIconString(), invalidSize);
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
        if (currentFontSize != ImGui.GetFontSize())
        {
            using (var font = ImRaii.PushFont(UiBuilder.IconFont))
            {
                foreach (var iconString in iconDictionary.Keys)
                {
                    iconDictionary[iconString] = ImGui.CalcTextSize(iconString);
                }
            }
            log.Debug("Size of icons changed.");
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
        currentFontSize = ImGui.GetFontSize();
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

        var icon = InvalidIcon;
        if (ProgressHalted)
        {
            icon = PauseIcon;
        }
        else if (RegenActive)
        {
            icon = RegenIcon;
            if (FastRegen)
            {
                icon = FastIcon;
            }
        }
        if (icon is InvalidIcon || iconDictionary.Count is 0)
        {
            return;
        }

        var iconString = icon.ToIconString();
        var iconPair = iconDictionary.First(i => i.Key.Equals(iconString, StringComparison.Ordinal));
        var color = ColorHelpers.RgbaVector4ToUint(iconColor);
        var pos = (topLeft + barFillPosOffset + bottomRight) / 2;
        pos = iconPair.Value == invalidSize ? pos : pos - (iconPair.Value / 2);
        drawList.AddText(UiBuilder.IconFont, currentFontSize * 0.85f, pos, color, iconPair.Key);
    }
}
