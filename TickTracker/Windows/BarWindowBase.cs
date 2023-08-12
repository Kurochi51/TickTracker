using ImGuiNET;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
namespace TickTracker.Windows;

public abstract class BarWindowBase : Window
{
    protected static Configuration Config => Plugin.Config;

    protected WindowType WindowType { get; set; }
    protected const ImGuiWindowFlags _defaultFlags = ImGuiWindowFlags.NoScrollbar |
                                              ImGuiWindowFlags.NoTitleBar |
                                              ImGuiWindowFlags.NoCollapse;

    protected const ImGuiWindowFlags _lockedBarFlags = ImGuiWindowFlags.NoBackground |
                                                    ImGuiWindowFlags.NoMove |
                                                    ImGuiWindowFlags.NoResize |
                                                    ImGuiWindowFlags.NoNav |
                                                    ImGuiWindowFlags.NoInputs;
    private readonly IClientState _clientState;
    public bool UpdateAvailable { get; set; } = false;
    public bool FastTick { get; set; } = false;

    public BarWindowBase(IClientState clientState, WindowType type, string name) : base(name)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.FirstUseEver;

        _clientState = clientState;
        WindowType = type;
    }

    public override bool DrawConditions()
    {
        if (!_clientState.IsLoggedIn)
        {
            return false;
        }
        if (!Utilities.WindowCondition(WindowType))
        {
            return false;
        }
        if (UpdateAvailable)
        {
            UpdateAvailable = false;
            return true;
        }
        return true;
    }

    public override void PreDraw()
    {
        var barWindowPadding = new Vector2(8, 14);
        Flags = _defaultFlags;
        if (Config.LockBar)
        {
            Flags |= _lockedBarFlags;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, barWindowPadding);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
    }
}
