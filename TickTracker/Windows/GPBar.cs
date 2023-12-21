using ImGuiNET;
using Dalamud.Plugin.Services;
using TickTracker.Enums;
using TickTracker.Helpers;

namespace TickTracker.Windows;

public class GPBar : BarWindowBase
{
    private readonly Configuration config;
    private readonly Utilities utilities;
    public GPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.GpWindow, "GPBarWindow")
    {
        config = _config;
        utilities = _utilities;
        Size = config.GPBarSize;
        Position = config.GPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        if (TickHalted)
        {
            Progress = PreviousProgress;
        }
        DrawProgress(Progress, config.GPBarBackgroundColor, config.GPBarFillColor, config.GPBarBorderColor, config.GPIconColor);
        PreviousProgress = Progress;
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
