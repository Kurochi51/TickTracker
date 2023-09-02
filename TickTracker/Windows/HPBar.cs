using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace TickTracker.Windows;

public class HPBar : BarWindowBase
{
    private readonly Utilities utilities;
    public HPBar(IClientState _clientState, Utilities _utilities) : base(_clientState, Enum.WindowType.HpWindow, "HPBarWindow")
    {
        Size = config.HPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.HPBarPosition;
        utilities = _utilities;
    }

    public override void Draw()
    {
        var now = DateTime.Now.TimeOfDay.TotalSeconds;
        UpdateWindow();
        var progress = (float)((now - LastTick) / (FastTick ? FastTickInterval : ActorTickInterval));
        if (RegenHalted)
        {
            progress = PreviousProgress;
        }
        if (progress > 1)
        {
            progress = 1;
            CanUpdate = true;
        }
        else
        {
            CanUpdate = false;
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
