using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TickTracker;

/// <summary>
///     A class that contains different helper functions necessary for this plugin's operation
/// </summary>
public unsafe class Utilities
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
    ///     Indicates if the <paramref name="window"/> is allowed to be drawn
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
    ///     Saves the size and position for the indicated <paramref name="window"/>.
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
    ///     Check the <paramref name="text"/> for elements from <paramref name="keywords"/>.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="text"/> matches any element, otherwise <see langword="false"/>.</returns>
    public static bool KeywordMatch(string text, HashSet<string> keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Checks the player's <see cref="ConditionFlag" /> for BoundByDuty
    /// </summary>
    /// <returns><see langword="true"/> if any matching flag is set, otherwise <see langword="false"/>.</returns>
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
    ///     Checks the current <see cref="TerritoryType.TerritoryIntendedUse"/> for a match against specific areas.
    /// </summary>
    /// <returns><see langword="true"/> if <see cref="TerritoryIntendedUseType.IslandSanctuary"/> 
    /// or <see cref="TerritoryIntendedUseType.Diadem"/>, otherwise <see langword="false"/>.</returns>
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
    ///     Checks the player's <see cref="ConditionFlag" /> for different cutscene flags.
    /// </summary>
    /// <returns><see langword="true"/> if any matching flag is set, otherwise <see langword="false"/>.</returns>
    public static bool inCustcene()
        => Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Service.Condition[ConditionFlag.WatchingCutscene] || Service.Condition[ConditionFlag.WatchingCutscene78] || Service.Condition[ConditionFlag.Occupied38];

    /// <summary>
    ///     Check if the <paramref name="addon"/> can be accessed.
    /// </summary>
    /// <returns><see langword="true"/> if addon is initialized and ready for use, otherwise <see langword="false"/>.</returns>
    public static bool IsAddonReady(AtkUnitBase* addon)
    {
        if (addon is null) return false;
        if (addon->RootNode is null) return false;
        if (addon->RootNode->ChildNode is null) return false;

        return true;
    }

}
