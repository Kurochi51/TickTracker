using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;

namespace TickTracker
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool PluginEnabled { get; set; } = true;
        public bool LockBar { get; set; }
        public bool HideOutOfCombat { get; set; }
        public bool AlwaysShowInDuties { get; set; }
        public bool AlwaysShowWithHostileTarget { get; set; }
        public Vector2 HPBarSize { get; set; } = new Vector2(190, 40);
        public Vector2 HPBarPosition { get; set; } = new Vector2(800, 500);
        public Vector4 HPBarBorderColor { get; set; } = new Vector4(0.246f, 0.262f, 0.270f, 1f);
        public Vector4 HPBarBackgroundColor { get; set; } = new Vector4(0f, 0f, 0f, 1f);
        public Vector4 HPBarFillColor { get; set; } = new Vector4(0.276f, 0.8f, 0.24f, 1f);
        public Vector2 MPBarSize { get; set; } = new Vector2(190, 40);
        public Vector2 MPBarPosition { get; set; } = new Vector2(800, 500);
        public Vector4 MPBarBorderColor { get; set; } = new Vector4(0.451f, 0.796f, 0.969f, 1f);
        public Vector4 MPBarBackgroundColor { get; set; } = new Vector4(0.031f, 0.173f, 0.332f, 1f);
        public Vector4 MPBarFillColor { get; set; } = new Vector4(1f, 1f, 1f, 1f);

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
        public void ResetPropertyToDefault(string colorProp)
        {
            var configType = this.GetType();
            var instance = Activator.CreateInstance(configType);
            var defaultValue = configType.GetProperty(colorProp)?.GetValue(instance);
            configType.GetProperty(colorProp)?.SetValue(this, defaultValue);
            this.Save();
        }
    }
}
