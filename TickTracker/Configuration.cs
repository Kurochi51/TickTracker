using Dalamud.Configuration;
using Dalamud.Plugin;
using System.Numerics;

namespace TickTracker;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool PluginEnabled = true;
    public bool LockBar = false;
    public bool HideOutOfCombat = false;
    public bool AlwaysShowInDuties = false;
    public bool AlwaysShowWithHostileTarget = false;
    public bool AlwaysShowInCombat = false;
    public bool HPVisible = true;
    public bool MPVisible = true;
    public bool GPVisible = true;
    public bool HideOnFullResource = false;

    public Vector2 HPBarPosition = new(600, 500);
    public Vector2 HPBarSize = new(180, 50);
    public Vector4 HPBarBackgroundColor = new(0f, 0f, 0f, 1f);
    public Vector4 HPBarFillColor = new(0.276f, 0.8f, 0.24f, 1f);
    public Vector4 HPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f);

    public Vector2 MPBarPosition = new(900, 500);
    public Vector2 MPBarSize = new(180, 50);
    public Vector4 MPBarBackgroundColor = new(0f, 0f, 0f, 1f);
    public Vector4 MPBarFillColor = new(0.753f, 0.271f, 0.482f, 1f);
    public Vector4 MPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f);

    public Vector2 GPBarPosition = new(750, 400);
    public Vector2 GPBarSize = new(180, 50);
    public Vector4 GPBarBackgroundColor = new(0f, 0f, 0f, 1f);
    public Vector4 GPBarFillColor = new(0.169f, 0.747f, 0.892f, 1f);
    public Vector4 GPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f);

    public void Save(DalamudPluginInterface pi) => pi.SavePluginConfig(this);

}
