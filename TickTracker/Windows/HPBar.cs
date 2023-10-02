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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Not going to re-add this everytime I need to log something.")]
    private readonly IPluginLog log;
    public HPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.HpWindow, "HPBarWindow")
    {
        log = _pluginLog;
        config = _config;
        utilities = _utilities;
        Size = config.HPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.HPBarPosition;
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
        DrawProgress(progress, config.HPBarBackgroundColor, config.HPBarFillColor, config.HPBarBorderColor);
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
            if (windowPos != config.HPBarPosition)
            {
                ImGui.SetWindowPos(config.HPBarPosition);
            }
            if (windowSize != config.HPBarSize)
            {
                ImGui.SetWindowSize(config.HPBarSize);
            }
        }
    }
}
