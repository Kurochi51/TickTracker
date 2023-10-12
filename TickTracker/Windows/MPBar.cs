using System;

using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using TickTracker.Enums;

namespace TickTracker.Windows;

public class MPBar : BarWindowBase
{
    private readonly Configuration config;
    private readonly Utilities utilities;
    public MPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.MpWindow, "MPBarWindow")
    {
        config = _config;
        utilities = _utilities;
        Size = config.MPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.MPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        //var now = DateTime.Now.TimeOfDay.TotalSeconds;
        //var progress = (now - LastTick) / (FastTick ? FastTickInterval : ActorTickInterval);
        if (FastRegenSwitch && Progress > 1)
        {
            Progress /= 2;
            if (CanUpdate)
            {
                FastRegenSwitch = false;
            }
        }
        if (RegenHalted)
        {
            Progress = PreviousProgress;
        }
        DrawProgress(Progress, config.MPBarBackgroundColor, config.MPBarFillColor, config.MPBarBorderColor);
        PreviousProgress = Progress;
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
            if (windowPos != config.MPBarPosition)
            {
                ImGui.SetWindowPos(config.MPBarPosition);
            }
            if (windowSize != config.MPBarSize)
            {
                ImGui.SetWindowSize(config.MPBarSize);
            }
        }
    }
}
