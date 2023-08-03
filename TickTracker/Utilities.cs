using System;
using System.Numerics;

namespace TickTracker;

public class Utilities
{
    private static Configuration config => TickTrackerSystem.config;

    public static bool WindowCondition(WindowType type)
    {
        if (!config.PluginEnabled)
        {
            return false;
        }
        var DisplayThisWindow = type switch
        {
            WindowType.HpWindow => config.HPVisible,
            WindowType.MpWindow => config.MPVisible,
            _ => throw new Exception("Unknown Window")
        };
        return DisplayThisWindow;
    }

    public static void UpdateWindowConfig(Vector2 currentPos, Vector2 currentSize, WindowType window)
    {
        if (window == WindowType.HpWindow)
        {
            if (!currentPos.Equals(config.HPBarPosition))
            {
                config.HPBarPosition = currentPos;
                config.Save();
            }
            if (!currentSize.Equals(config.HPBarSize))
            {
                config.HPBarSize = currentSize;
                config.Save();
            }
        }
        if (window == WindowType.MpWindow)
        {
            if (!currentPos.Equals(config.MPBarPosition))
            {
                config.MPBarPosition = currentPos;
                config.Save();
            }
            if (!currentSize.Equals(config.MPBarSize))
            {
                config.MPBarSize = currentSize;
                config.Save();
            }
        }
    }
}
