using System.Numerics;

using Dalamud.Plugin.Services;
using TickTracker.Helpers;
using TickTracker.Enums;

namespace TickTracker.Windows;

public class MPBar : BarWindowBase
{
    private readonly Configuration config;
    protected override Vector2 ConfigSize => config.MPBarSize;
    protected override Vector2 ConfigPos => config.MPBarPosition;

    public MPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.MpWindow, "MPBarWindow")
    {
        config = _config;
        Size = config.MPBarSize;
        Position = config.MPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        DrawProgress(Progress, config.MPBarBackgroundColor, config.MPBarFillColor, config.MPBarBorderColor, config.MPIconColor);
    }
}
