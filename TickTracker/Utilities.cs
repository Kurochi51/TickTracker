using Dalamud.Game.ClientState.Conditions;
using System;

namespace TickTracker;

public class Utilities
{
    public static bool WindowCondition(WindowType type)
    {
        if (!TickTrackerSystem.config.PluginEnabled || !TickTrackerSystem.config.HPVisible)
        {
            return false;
        }
        var DisplayThisWindow = type switch
        {
            WindowType.HpWindow => TickTrackerSystem.config.HPVisible,
            WindowType.MpWindow => TickTrackerSystem.config.MPVisible,
            _ => throw new Exception("Unknown Window")
        };
        return true;
    }
}
