using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Plugin.Services;

namespace TickTracker;

/// <summary>
///     A class that contains different helper functions necessary for this plugin's operation
/// </summary>
public class Utilities
{
    public Utilities(Dalamud.Game.ClientState.Conditions.Condition condition, IDataManager dataManager, IClientState clientState)
    {
        _condition = condition;
        _dataManager = dataManager;
        _clientState = clientState;
    }

    private static Configuration Config => Plugin.Config;

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

    private readonly Dalamud.Game.ClientState.Conditions.Condition _condition;
    private readonly IDataManager _dataManager;
    private readonly IClientState _clientState;

    /// <summary>
    ///     Indicates if the <paramref name="window"/> is allowed to be drawn
    /// </summary>
    public static bool WindowCondition(WindowType window)
    {
        if (!Config.PluginEnabled)
        {
            return false;
        }
        try
        {
            var displayThisWindow = window switch
            {
                WindowType.HpWindow => Config.HPVisible,
                WindowType.MpWindow => Config.MPVisible,
                _ => throw new Exception("Unknown Window")
            };
            return displayThisWindow;
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
            if (!currentPos.Equals(Config.HPBarPosition))
            {
                Config.HPBarPosition = currentPos;
                Config.Save();
            }
            if (!currentSize.Equals(Config.HPBarSize))
            {
                Config.HPBarSize = currentSize;
                Config.Save();
            }
        }
        if (window == WindowType.MpWindow)
        {
            if (!currentPos.Equals(Config.MPBarPosition))
            {
                Config.MPBarPosition = currentPos;
                Config.Save();
            }
            if (!currentSize.Equals(Config.MPBarSize))
            {
                Config.MPBarSize = currentSize;
                Config.Save();
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
    public bool InDuty()
    {
        var dutyBound = _condition[ConditionFlag.BoundByDuty] || _condition[ConditionFlag.BoundByDuty56] || _condition[ConditionFlag.BoundByDuty95] || _condition[ConditionFlag.BoundToDuty97];
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
    public bool InIgnoredInstances()
    {
        var area = _dataManager.GetExcelSheet<TerritoryType>()!.GetRow(_clientState.TerritoryType);
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
    public bool InCustcene()
        => _condition[ConditionFlag.OccupiedInCutSceneEvent] || _condition[ConditionFlag.WatchingCutscene] || _condition[ConditionFlag.WatchingCutscene78] || _condition[ConditionFlag.Occupied38];

    /// <summary>
    ///     Check if the <paramref name="addon"/> can be accessed.
    /// </summary>
    /// <returns><see langword="true"/> if addon is initialized and ready for use, otherwise <see langword="false"/>.</returns>
    public static unsafe bool IsAddonReady(AtkUnitBase* addon)
    {
        if (addon is null) return false;
        if (addon->RootNode is null) return false;
        if (addon->RootNode->ChildNode is null) return false;

        return true;
    }

}
