using Dalamud.Configuration;
using System.Numerics;

namespace TickTracker;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool PluginEnabled= true;
    public bool LockBar=false;
    public bool HideOutOfCombat=false;
    public bool AlwaysShowInDuties = false;
    public bool AlwaysShowWithHostileTarget = false;
    public bool AlwaysShowInCombat = false;
    public bool HPVisible = true;
    public bool MPVisible = true;
    public bool HideOnFullResource = false;
    public Vector2 HPBarSize= new(190, 40);
    public Vector2 HPBarPosition = new(800, 500);
    public Vector4 HPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f);
    public Vector4 HPBarBackgroundColor = new(0f, 0f, 0f, 1f);
    public Vector4 HPBarFillColor = new(0.276f, 0.8f, 0.24f, 1f);
    public Vector2 MPBarSize = new(190, 40);
    public Vector2 MPBarPosition = new(800, 500);
    public Vector4 MPBarBorderColor = new(0.246f, 0.262f, 0.270f, 1f);
    public Vector4 MPBarBackgroundColor = new(0f, 0f, 0f, 1f);
    public Vector4 MPBarFillColor = new(0.753f, 0.271f, 0.482f, 1f);

    public void Save() => Services.PluginInterface.SavePluginConfig(this);

}
