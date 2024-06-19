using ImGuiNET;
using Dalamud.Plugin.Services;
using TickTracker.Enums;
using TickTracker.Helpers;

namespace TickTracker.Windows;

public class HPBar : BarWindowBase
{
    private readonly Configuration config;
    public HPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.HpWindow, "HPBarWindow")
    {
        config = _config;
        Size = ConfigSize = config.HPBarSize;
        Position = ConfigPos = config.HPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        DrawProgress(Progress, config.HPBarBackgroundColor, config.HPBarFillColor, config.HPBarBorderColor, config.HPIconColor);
    }
}
