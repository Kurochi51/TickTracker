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
    public GPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.GpWindow, "GPBarWindow")
    {
        config = _config;
        utilities = _utilities;
        Size = config.GPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.GPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        var progress = Progress;
        if (ProgressHalted)
        {
            progress = PreviousProgress;
        }
        DrawProgress(progress, config.GPBarBackgroundColor, config.GPBarFillColor, config.GPBarBorderColor);
        PreviousProgress = progress;
    }

    private void UpdateWindow()
    {
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        if (config.LockBar)
        {
            WindowPosition = windowPos;
            WindowSize = windowSize;
            return;
        }
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
        WindowPosition = windowPos;
        WindowSize = windowSize;
    }
}
