using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using TickTracker.Enums;
using TickTracker.Helpers;

namespace TickTracker.Windows;

public class MPBar : BarWindowBase
{
    private readonly Configuration config;
    private readonly Utilities utilities;
    public MPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.MpWindow, "MPBarWindow")
    {
        config = _config;
        utilities = _utilities;
        Size = config.MPBarSize;
        Position = config.MPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        if (TickHalted)
        {
            Progress = PreviousProgress;
        }
        DrawProgress(Progress, config.MPBarBackgroundColor, config.MPBarFillColor, config.MPBarBorderColor, config.MPIconColor);
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
            if (windowPos != config.MPBarPosition)
            {
                ImGui.SetWindowPos(config.MPBarPosition);
            }
            if (windowSize != config.MPBarSize)
            {
                ImGui.SetWindowSize(config.MPBarSize);
            }
        }
        WindowPosition = windowPos;
        WindowSize = windowSize;
    }
}
