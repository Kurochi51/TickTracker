using ImGuiNET;
using Dalamud.Plugin.Services;
using TickTracker.Enums;
using TickTracker.Helpers;

namespace TickTracker.Windows;

public class MPBar : BarWindowBase
{
    private readonly Configuration config;
    public MPBar(IClientState _clientState, IPluginLog _pluginLog, Utilities _utilities, Configuration _config) : base(_clientState, _pluginLog, _utilities, _config, WindowType.MpWindow, "MPBarWindow")
    {
        config = _config;
        Size = ConfigSize = config.MPBarSize;
        Position = ConfigPos = config.MPBarPosition;
    }

    public override void Draw()
    {
        UpdateWindow();
        DrawProgress(Progress, config.MPBarBackgroundColor, config.MPBarFillColor, config.MPBarBorderColor, config.MPIconColor);
    }
}
