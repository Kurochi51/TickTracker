using System;

using ImGuiNET;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace TickTracker.Windows;

public class MPBar : BarWindowBase
{
    private readonly Utilities utilities;
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Not going to re-add this everytime I need to log something.")]
    //private readonly IPluginLog log;
    public MPBar(IClientState _clientState,/* IPluginLog _pluginLog,*/ Utilities _utilities) : base(_clientState,/* _pluginLog,*/ _utilities, Enum.WindowType.MpWindow, "MPBarWindow")
    {
        Size = config.MPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.MPBarPosition;
        utilities = _utilities;
        //log = _pluginLog;
    }

    public override void Draw()
    {
        var now = DateTime.Now.TimeOfDay.TotalSeconds;
        UpdateWindow();
        var progress = (now - LastTick) / (FastTick ? FastTickInterval : ActorTickInterval);
        if (FastRegenSwitch && progress > 1)
        {
            progress /= 2;
            if (CanUpdate)
            {
                FastRegenSwitch = false;
            }
        }
        if (RegenHalted)
        {
            progress = PreviousProgress;
        }
        DrawProgress(progress, config.MPBarBackgroundColor, config.MPBarFillColor, config.MPBarBorderColor);
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
