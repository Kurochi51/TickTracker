using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dalamud.Logging;
using Dalamud.Utility;
using Dalamud.Game.ClientState.Conditions;

namespace TickTracker;

public class Utilities
{
    private static Configuration config => TickTrackerSystem.config;
    public static readonly HashSet<uint> healthRegenList = new();
    public static readonly HashSet<uint> manaRegenList = new();
    public enum TerritoryIntendedUseType : byte
    {
        Diadem = 47,
        IslandSanctuary = 49
    }

    public static readonly HashSet<string> RegenKeywords = new()
    {
        "regenerating",
        "restoring",
        "restore",
        "recovering"
    };

    public static readonly HashSet<string> TimeKeywords = new()
    {
        "gradually",
        "over time"
    };

    public static readonly HashSet<string> HealthKeywords = new()
    {
        "hp",
        "health"
    };

    public static readonly HashSet<string> ManaKeywords = new()
    {
        "mp",
        "mana"
    };

    public static bool WindowCondition(WindowType type)
    {
        if (!config.PluginEnabled)
        {
            return false;
        }
        try
        {
            var DisplayThisWindow = type switch
            {
                WindowType.HpWindow => config.HPVisible,
                WindowType.MpWindow => config.MPVisible,
                _ => throw new Exception("Unknown Window")
            };
            return DisplayThisWindow;
        }
        catch (Exception e)
        {
            PluginLog.Error("{error} triggered by {type}.", e.Message, type);
            return false;
        }
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

    public static bool KeywordMatch(string text, HashSet<string> keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    public static bool InDuty()
    {
        var dutyBound = Service.Condition[ConditionFlag.BoundByDuty] || Service.Condition[ConditionFlag.BoundByDuty56] || Service.Condition[ConditionFlag.BoundByDuty95] || Service.Condition[ConditionFlag.BoundToDuty97];
        if (dutyBound == true)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public static bool InIgnoredInstances()
    {
        var area = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>()?.GetRow(Service.ClientState.TerritoryType);
        if (area is not null)
        {
            if (Enum.IsDefined(typeof(TerritoryIntendedUseType), area.TerritoryIntendedUse))
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

    public static bool inCustcene()
        => Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Service.Condition[ConditionFlag.WatchingCutscene] || Service.Condition[ConditionFlag.WatchingCutscene78] || Service.Condition[ConditionFlag.Occupied38];

    public static async void InitializeLists(Lumina.Excel.ExcelSheet<Lumina.Excel.GeneratedSheets.Status> statusSheet)
    {
        await Task.Run(() =>
        {
            foreach (var stat in statusSheet.Where(s => s.RowId is not 307 and not 1419 and not 135))
            {
                var text = stat.Description.ToDalamudString().TextValue;
                if (KeywordMatch(text, RegenKeywords) && KeywordMatch(text, TimeKeywords))
                {
                    if (KeywordMatch(text, HealthKeywords))
                    {
                        healthRegenList.Add(stat.RowId);
                    }
                    if (KeywordMatch(text, ManaKeywords))
                    {
                        manaRegenList.Add(stat.RowId);
                    }
                }
            }
            PluginLog.Debug("HP regen list generated with {HPcount} status effects.", healthRegenList.Count);
            PluginLog.Debug("MP regen list generated with {MPcount} status effects.", manaRegenList.Count);
        });
    }
}
