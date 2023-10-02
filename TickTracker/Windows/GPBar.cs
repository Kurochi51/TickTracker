using System;

using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using TickTracker.Enums;

namespace TickTracker.Windows;

public class GPBar : BarWindowBase
{
    private readonly Configuration config;
    private readonly Utilities utilities;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Not going to re-add this everytime I need to log something.")]
    private readonly IPluginLog log;
    public GPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.GpWindow, "GPBarWindow")
    {
        log = _pluginLog;
        config = _config;
        utilities = _utilities;
        Size = config.GPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.GPBarPosition;
    }

    public override void Draw()
    {
        var now = DateTime.Now.TimeOfDay.TotalSeconds;
        UpdateWindow();
        var progress = (now - LastTick) / ActorTickInterval;
        if (RegenHalted)
        {
            progress = PreviousProgress;
        }
        DrawProgress(progress, config.GPBarBackgroundColor, config.GPBarFillColor, config.GPBarBorderColor);
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
            if (windowPos != config.GPBarPosition)
            {
                ImGui.SetWindowPos(config.GPBarPosition);
            }
            if (windowSize != config.GPBarSize)
            {
                ImGui.SetWindowSize(config.GPBarSize);
            }
        }
    }
}
