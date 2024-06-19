using ImGuiNET;
using Dalamud.Plugin.Services;
using TickTracker.Enums;
using TickTracker.Helpers;

namespace TickTracker.Windows;

public class GPBar : BarWindowBase
{
    private readonly Configuration config;
    public GPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.GpWindow, "GPBarWindow")
    {
        config = _config;
        Size = ConfigSize = config.GPBarSize;
        Position = ConfigPos = config.GPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        DrawProgress(Progress, config.GPBarBackgroundColor, config.GPBarFillColor, config.GPBarBorderColor, config.GPIconColor);
    }
}
