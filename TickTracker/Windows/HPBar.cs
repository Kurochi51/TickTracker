using System;

using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using TickTracker.Enums;

namespace TickTracker.Windows;

public class HPBar : BarWindowBase
{
    private readonly Configuration config;
    private readonly Utilities utilities;
    public HPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.HpWindow, "HPBarWindow")
    {
        config = _config;
        utilities = _utilities;
        Size = config.HPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.HPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        //var progress = (float)(RegenProgressActive ? RegenProgress : Progress);
        var progress = Progress;
        if (ProgressHalted)
        {
            progress = PreviousProgress;
        }
        DrawProgress(progress, config.HPBarBackgroundColor, config.HPBarFillColor, config.HPBarBorderColor);
        PreviousProgress = progress;
    }

    private void UpdateWindow()
    {
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        if (config.LockBar)
        {
            WindowCoords = windowPos;
            WindowSize = windowSize;
            return;
        }
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
        WindowCoords = windowPos;
        WindowSize = windowSize;
    }
}
