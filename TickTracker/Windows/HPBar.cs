using System.Numerics;

using Dalamud.Plugin.Services;
using TickTracker.Helpers;
using TickTracker.Enums;

namespace TickTracker.Windows;

public class HPBar : BarWindowBase
{
    private readonly Configuration config;
    protected override Vector2 ConfigSize => config.HPBarSize;
    protected override Vector2 ConfigPos => config.HPBarPosition;

    public HPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.HpWindow, "HPBarWindow")
    {
        config = _config;
        Size = config.HPBarSize;
        Position = config.HPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        DrawProgress(Progress, config.HPBarBackgroundColor, config.HPBarFillColor, config.HPBarBorderColor, config.HPIconColor);
    }
}
