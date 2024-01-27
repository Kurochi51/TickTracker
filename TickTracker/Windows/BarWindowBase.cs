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
using TickTracker.Helpers;

namespace TickTracker.Windows;

public abstract class BarWindowBase : Window
{
    protected WindowType WindowType { get; set; }
    protected const ImGuiWindowFlags DefaultFlags = ImGuiWindowFlags.NoScrollbar |
                                              ImGuiWindowFlags.NoTitleBar |
                                              ImGuiWindowFlags.NoCollapse;

    protected const ImGuiWindowFlags LockedBarFlags = ImGuiWindowFlags.NoBackground |
                                                    ImGuiWindowFlags.NoMove |
                                                    ImGuiWindowFlags.NoResize |
                                                    ImGuiWindowFlags.NoNav |
                                                    ImGuiWindowFlags.NoInputs;
    protected IPluginLog Log { get; }
    public bool TickUpdate { get; set; }
    public bool TickHalted { get; set; }
    public bool RegenActive { get; set; }
    public bool FastRegen { get; set; }
    public double Tick { get; set; }
    public double Progress { get; set; }
    public double PreviousProgress { get; set; }
    public Vector2 WindowPosition { get; protected set; }
    public Vector2 WindowSize { get; protected set; }

    private const FontAwesomeIcon RegenIcon = FontAwesomeIcon.Forward;
    private const FontAwesomeIcon FastIcon = FontAwesomeIcon.FastForward;
    private const FontAwesomeIcon PauseIcon = FontAwesomeIcon.Pause;
    private const FontAwesomeIcon InvalidIcon = FontAwesomeIcon.None;
    private const float CornerRounding = 4f;     // Maybe make it user configurable?
    private const float BorderThickness = 1.35f; // Maybe make it user configurable?

    private readonly IClientState clientState;
    private readonly Configuration config;
    private readonly Utilities utilities;

    private readonly Dictionary<string, Vector2> iconDictionary = new(StringComparer.Ordinal);
    private readonly Vector2 invalidSize = new(0, 0);
    private readonly Vector2 barFillPosOffset = new(1, 1);
    private readonly Vector2 barFillSizeOffset = new(-1, 0);
    private readonly Vector2 barWindowPadding = new(8, 14);
    private float currentFontSize;

    protected BarWindowBase(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config, WindowType type, string name) : base(name)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.FirstUseEver;

        clientState = _clientState;
        Log = _pluginLog;
        utilities = _utilities;
        config = _config;
        WindowType = type;

        iconDictionary.Add(RegenIcon.ToIconString(), invalidSize);
        iconDictionary.Add(FastIcon.ToIconString(), invalidSize);
        iconDictionary.Add(PauseIcon.ToIconString(), invalidSize);
    }

    public override bool DrawConditions()
        => clientState.IsLoggedIn && utilities.WindowCondition(WindowType);

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
            Log.Debug("Size of icons changed.");
        }
        Flags = DefaultFlags;
        if (config.LockBar)
        {
            Flags |= LockedBarFlags;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
    }

    protected void DrawProgress(double progress, Vector4 backgroundColor, Vector4 fillColor, Vector4 borderColor, Vector4 iconColor)
    {
        currentFontSize = ImGui.GetFontSize();
        var floatProgress = (float)progress;
        var topLeft = ImGui.GetWindowContentRegionMin() + ImGui.GetWindowPos();
        var bottomRight = ImGui.GetWindowContentRegionMax() + ImGui.GetWindowPos();
        bottomRight = (floatProgress <= 0) ? bottomRight - barFillSizeOffset : bottomRight;

        // Calculate floatProgress bar dimensions
        var barWidth = bottomRight.X - topLeft.X;
        var filledWidth = new Vector2((barWidth * Math.Max(floatProgress, 0.0001f)) + topLeft.X, bottomRight.Y);
        filledWidth = (floatProgress <= 0) ? filledWidth - barFillSizeOffset : filledWidth;

        // Draw main bar
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(topLeft + barFillPosOffset, bottomRight, ImGui.GetColorU32(backgroundColor), CornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(topLeft + barFillPosOffset, filledWidth, ImGui.GetColorU32(fillColor), CornerRounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRect(topLeft, bottomRight, ImGui.GetColorU32(borderColor), CornerRounding, ImDrawFlags.RoundCornersAll, BorderThickness);

        var icon = InvalidIcon;
        if (TickHalted)
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
