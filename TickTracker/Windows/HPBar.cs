using ImGuiNET;
using System;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace TickTracker.Windows;

public class HPBar : BarWindowBase
{
    private readonly Utilities utilities;
    private readonly IPluginLog log;
    private bool test = true;
    public HPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities) : base(_clientState, _pluginLog, _utilities, Enum.WindowType.HpWindow, "HPBarWindow")
    {
        Size = config.HPBarSize * ImGuiHelpers.GlobalScale;
        Position = config.HPBarPosition;
        utilities = _utilities;
        log = _pluginLog;
    }

    public override void Draw()
    {
        var now = DateTime.Now.TimeOfDay.TotalSeconds;
        UpdateWindow();
        var progress = (float)((now - LastTick) / (FastTick ? FastTickInterval : ActorTickInterval));
        FastRegenSwitch = FastTick;
        if (!FastTick)
        {
            test = true;
            FastRegenSwitch = true;
        }
        if (RegenHalted)
        {
            progress = PreviousProgress;
        }
        if (progress > 1 && FastRegenSwitch && test)
        {
            progress /= 2;
            FastRegenSwitch = false;
            test = false;
            /*log.Debug("Progress is over 1: {c}", progress);
            log.Debug("LastTick is: {a} and FastTick is {b}", LastTick, FastTick);
            log.Debug("now is {a} now - LastTick is {b} and now - LastTick / currentInterval is {c}", now, now - LastTick, (now - LastTick) / (FastTick ? FastTickInterval : ActorTickInterval));
            progress = FastTick ? progress / 2 : 0;*/
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
