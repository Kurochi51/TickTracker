using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using TickTracker.Enums;
using TickTracker.Helpers;

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
        if (TickHalted)
        {
            Progress = PreviousProgress;
        }
        DrawProgress(Progress, config.HPBarBackgroundColor, config.HPBarFillColor, config.HPBarBorderColor, config.HPIconColor);
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
            if (windowPos != config.HPBarPosition)
            {
                ImGui.SetWindowPos(config.HPBarPosition);
            }
            if (windowSize != config.HPBarSize)
            {
                ImGui.SetWindowSize(config.HPBarSize);
            }
        }
        WindowPosition = windowPos;
        WindowSize = windowSize;
    }
}
