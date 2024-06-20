using System.Numerics;

using Dalamud.Plugin.Services;
using TickTracker.Helpers;
using TickTracker.Enums;

namespace TickTracker.Windows;

public class GPBar : BarWindowBase
{
    private readonly Configuration config;
    protected override Vector2 ConfigSize => config.GPBarSize;
    protected override Vector2 ConfigPos => config.GPBarPosition;

    public GPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.GpWindow, "GPBarWindow")
    {
        config = _config;
        Size = config.GPBarSize;
        Position = config.GPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        DrawProgress(Progress, config.GPBarBackgroundColor, config.GPBarFillColor, config.GPBarBorderColor, config.GPIconColor);
    }
}
