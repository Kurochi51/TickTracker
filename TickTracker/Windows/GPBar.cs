using System;

using ImGuiNET;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace TickTracker.Windows;

public class GPBar : BarWindowBase
{
    private readonly Utilities utilities;
    public GPBar(IClientState _clientState, Utilities _utilities) : base(_clientState, _utilities, Enum.WindowType.GpWindow, "GPBarWindow")
    {
        Size = config.GPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.GPBarPosition;
        utilities = _utilities;
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
