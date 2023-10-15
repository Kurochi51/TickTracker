using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.GeneratedSheets;
using TickTracker.Enums;

namespace TickTracker;

/// <summary>
///     A class that contains different helper functions necessary for this plugin's operation
/// </summary>
public partial class Utilities
{
    /// <summary>
    ///     A set of words that indicate regeneration
    /// </summary>
    public IEnumerable<string> RegenKeywords { get; } = new[]
    {
        "regenerating",
        "restoring",
        "restore",
        "recovering",
    };

    /// <summary>
    ///     A set of words that indicate an effect over time
    /// </summary>
    public IEnumerable<string> TimeKeywords { get; } = new[]
    {
        "gradually",
        "over time",
    };

    /// <summary>
    ///     A set of words that indicate health
    /// </summary>
    public IEnumerable<string> HealthKeywords { get; } = new[]
    {
        "hp",
        "health",
    };

    /// <summary>
    ///     A set of words that indicate mana
    /// </summary>
    public IEnumerable<string> ManaKeywords { get; } = new[]
    {
        "mp",
        "mana",
    };

    /// <summary>
    ///     A set of words that indicate the halt of regen
    /// </summary>
    public IEnumerable<string> RegenNullKeywords { get; } = new[]
    {
        "null",
        "nullified",
        "stop",
        "stopped",
    };

    private readonly DalamudPluginInterface pluginInterface;
    private readonly ICondition condition;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly Configuration config;

    public Utilities(DalamudPluginInterface _pluginInterface, Configuration _config, ICondition _condition, IDataManager _dataManager, IClientState _clientState, IPluginLog _pluginLog)
    {
        pluginInterface = _pluginInterface;
        config = _config;
        condition = _condition;
        dataManager = _dataManager;
        clientState = _clientState;
        log = _pluginLog;
    }

    /// <summary>
    ///     Indicates if the <paramref name="window"/> is allowed to be drawn
    /// </summary>
    public bool WindowCondition(WindowType window)
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
                WindowType.GpWindow => config.GPVisible,
                _ => throw new Exception("Unknown Window")
            };
            return DisplayThisWindow;
        }
        catch (Exception e)
        {
            log.Error("{error} triggered by {type}.", e.Message, window.ToString());
            return false;
        }
    }

    /// <summary>
    ///     Saves the size and position for the indicated <paramref name="window"/>.
    /// </summary>
    public void UpdateWindowConfig(Vector2 currentPos, Vector2 currentSize, WindowType window)
    {
        if (window == WindowType.HpWindow)
        {
            if (!currentPos.Equals(config.HPBarPosition))
            {
                config.HPBarPosition = currentPos;
                config.Save(pluginInterface);
            }
            if (!currentSize.Equals(config.HPBarSize))
            {
                config.HPBarSize = currentSize;
                config.Save(pluginInterface);
            }
        }
        if (window == WindowType.MpWindow)
        {
            if (!currentPos.Equals(config.MPBarPosition))
            {
                config.MPBarPosition = currentPos;
                config.Save(pluginInterface);
            }
            if (!currentSize.Equals(config.MPBarSize))
            {
                config.MPBarSize = currentSize;
                config.Save(pluginInterface);
            }
        }
        if (window == WindowType.GpWindow)
        {
            if (!currentPos.Equals(config.GPBarPosition))
            {
                config.GPBarPosition = currentPos;
                config.Save(pluginInterface);
            }
            if (!currentSize.Equals(config.GPBarSize))
            {
                config.GPBarSize = currentSize;
                config.Save(pluginInterface);
            }
        }
    }

    /// <summary>
    ///     Check the <paramref name="text"/> for elements from <paramref name="keywords"/>.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="text"/> contains any element, otherwise <see langword="false"/>.</returns>
    public static bool KeywordMatch(string text, IEnumerable<string> keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Check each word from <paramref name="text"/> for an exact match with any element from <paramref name="keywords"/>.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="text"/> matches any element, otherwise <see langword="false"/>.</returns>
    public static bool WholeKeywordMatch(string text, IEnumerable<string> keywords)
    {
        var words = KeywordsRegex()
            .Split(text)
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim());
        return keywords.Any(keyword => words.Any(word => word.Equals(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    ///     Checks the player's <see cref="ConditionFlag" /> for BoundByDuty
    /// </summary>
    /// <returns><see langword="true"/> if any matching flag is set, otherwise <see langword="false"/>.</returns>
    public bool InDuty()
    {
        var dutyBound = condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95];
        return dutyBound && !InIgnoredInstances();
    }

    /// <summary>
    ///     Checks the current <see cref="TerritoryType.TerritoryIntendedUse"/> for a match against specific areas.
    /// </summary>
    /// <returns><see langword="true"/> if <see cref="TerritoryIntendedUseType.IslandSanctuary"/> 
    /// or <see cref="TerritoryIntendedUseType.Diadem"/>, otherwise <see langword="false"/>.</returns>
    public bool InIgnoredInstances()
    {
        var area = dataManager.GetExcelSheet<TerritoryType>()!.GetRow(clientState.TerritoryType);
        if (area is null)
        {
            return false;
        }
        return area.TerritoryIntendedUse is (byte)TerritoryIntendedUseType.IslandSanctuary or (byte)TerritoryIntendedUseType.Diadem;
    }

    /// <summary>
    ///     Checks the player's <see cref="ConditionFlag" /> for different cutscene flags.
    /// </summary>
    /// <returns><see langword="true"/> if any matching flag is set, otherwise <see langword="false"/>.</returns>
    public bool inCustcene()
        => condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78] || condition[ConditionFlag.Occupied38];

    /// <summary>
    ///     Check if the <paramref name="addon"/> can be accessed.
    /// </summary>
    /// <returns><see langword="true"/> if addon is initialized and ready for use, otherwise <see langword="false"/>.</returns>
    public unsafe bool IsAddonReady(AtkUnitBase* addon)
    {
        if (addon is null)
        {
            return false;
        }
        if (addon->RootNode is null)
        {
            return false;
        }
        if (addon->RootNode->ChildNode is null)
        {
            return false;
        }

        return true;
    }
    /// <summary>
    ///     Check if <paramref name="sourceCharacter"/> is currently targetting <paramref name="targetCharacter"/>, whether it's manually set, or the result of using an action.
    /// </summary>
    public unsafe bool IsTarget(PlayerCharacter targetCharacter, Character* sourceCharacter)
    {
        ITuple sourceTarget = (sourceCharacter->GetCastInfo()->CastTargetID, sourceCharacter->GetSoftTargetId().ObjectID, sourceCharacter->GetTargetId(), sourceCharacter->LookTargetId.ObjectID);
        var targetID = (object)targetCharacter.ObjectId;
        for (var i = 0; i < sourceTarget.Length; i++)
        {
            var sourceTargetID = sourceTarget[i]; // 3758096384 which is E000000 means that the target isn't set
            if (sourceTargetID is not null && sourceTargetID.Equals(targetID))
            {
                return true;
            }
        }
        return false;
    }

    [System.Text.RegularExpressions.GeneratedRegex("\\W+", System.Text.RegularExpressions.RegexOptions.Compiled, 500)]
    private static partial System.Text.RegularExpressions.Regex KeywordsRegex();
}
