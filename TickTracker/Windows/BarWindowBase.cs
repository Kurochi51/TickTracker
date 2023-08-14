using ImGuiNET;
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

namespace TickTracker.Windows;

public abstract class BarWindowBase : Window
{
    protected static Configuration config => Plugin.config;

    protected Enum.WindowType WindowType { get; set; }
    protected const ImGuiWindowFlags _defaultFlags = ImGuiWindowFlags.NoScrollbar |
                                              ImGuiWindowFlags.NoTitleBar |
                                              ImGuiWindowFlags.NoCollapse;

    protected const ImGuiWindowFlags _lockedBarFlags = ImGuiWindowFlags.NoBackground |
                                                    ImGuiWindowFlags.NoMove |
                                                    ImGuiWindowFlags.NoResize |
                                                    ImGuiWindowFlags.NoNav |
                                                    ImGuiWindowFlags.NoInputs;
    public bool UpdateAvailable { get; set; } = false;
    public bool RegenHalted { get; set; } = false;
    public bool FastTick { get; set; } = false;
    public double LastTick { get; set; } = 1;
    public float PreviousProgress { get; set; } = -1;
    public const float ActorTickInterval = 3, FastTickInterval = 1.5f;
    private readonly IClientState clientState;

    protected BarWindowBase(IClientState _clientState, Enum.WindowType type, string name) : base(name)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.FirstUseEver;

        clientState = _clientState;
        WindowType = type;
    }

    public override bool DrawConditions()
    {
        if (!clientState.IsLoggedIn)
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
        if (config.LockBar)
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
