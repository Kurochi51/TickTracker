using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Conditions;

namespace TickTracker;

public class Utilities
{
    private static Configuration config => TickTrackerSystem.config;

    /// <summary>
    ///     A set of words that indicate regeneration
    /// </summary>
    public static readonly HashSet<string> RegenKeywords = new()
    {
        "regenerating",
        "restoring",
        "restore",
        "recovering"
    };

    /// <summary>
    ///     A set of words that indicate an effect over time
    /// </summary>
    public static readonly HashSet<string> TimeKeywords = new()
    {
        "gradually",
        "over time"
    };

    /// <summary>
    ///     A set of words that indicate health
    /// </summary>
    public static readonly HashSet<string> HealthKeywords = new()
    {
        "hp",
        "health"
    };

    /// <summary>
    ///     A set of words that indicate mana
    /// </summary>
    public static readonly HashSet<string> ManaKeywords = new()
    {
        "mp",
        "mana"
    };

    /// <summary>
    ///     Indicates if the <paramref name="window"/> is ready to be drawn
    /// </summary>
    public static bool WindowCondition(WindowType window)
    {
        if (!config.PluginEnabled)
        {
            return false;
        }
        try
        {
            var DisplayThisWindow = window switch
            {
                WindowType.HpWindow => config.HPVisible,
                WindowType.MpWindow => config.MPVisible,
                _ => throw new Exception("Unknown Window")
            };
            return DisplayThisWindow;
        }
        catch (Exception e)
        {
            PluginLog.Error("{error} triggered by {type}.", e.Message, window);
            return false;
        }
    }

    /// <summary>
    ///     Saves the window size and position for the indicated window.
    /// </summary>
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

    /// <summary>
    ///     Returns whether the <paramref name="text"/> contains elements from <paramref name="keywords"/> or not.
    /// </summary>
    /// <returns>True if the <paramref name="text"/> has atleast one word, false otherwise.</returns>
    public static bool KeywordMatch(string text, HashSet<string> keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Returns if the player is bound by duty.
    /// </summary>
    /// <returns>True if bound by duty, false otherwise.</returns>
    public static bool InDuty()
    {
        var dutyBound = Service.Condition[ConditionFlag.BoundByDuty] || Service.Condition[ConditionFlag.BoundByDuty56] || Service.Condition[ConditionFlag.BoundByDuty95] || Service.Condition[ConditionFlag.BoundToDuty97];
        if (dutyBound == true && !InIgnoredInstances())
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    ///     Returns if the player is in an ignored instance.
    /// </summary>
    /// <returns>True if <see cref="TerritoryIntendedUseType.IslandSanctuary"/> or <see cref="TerritoryIntendedUseType.Diadem"/>, false otherwise.</returns>
    public static bool InIgnoredInstances()
    {
        var area = Service.DataManager.GetExcelSheet<TerritoryType>()!.GetRow(Service.ClientState.TerritoryType);
        if (area is not null)
        {
            if (area.TerritoryIntendedUse is (byte)TerritoryIntendedUseType.IslandSanctuary or (byte)TerritoryIntendedUseType.Diadem)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }    
    }

    /// <summary>
    ///     Returns if the player is in a cutscene.
    /// </summary>
    /// <returns>True if in a cutscene, false otherwise.</returns>
    public static bool inCustcene()
        => Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Service.Condition[ConditionFlag.WatchingCutscene] || Service.Condition[ConditionFlag.WatchingCutscene78] || Service.Condition[ConditionFlag.Occupied38];

}
