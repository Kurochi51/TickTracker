using System;
using System.Linq;
using System.Timers;
using System.Numerics;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets2;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Dalamud;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using TickTracker.Enums;
using TickTracker.Windows;

namespace TickTracker.Helpers;

/// <summary>
///     A class that contains different helper functions necessary for this plugin's operation
/// </summary>
public partial class Utilities(DalamudPluginInterface _pluginInterface,
    Configuration _config,
    ICondition _condition,
    IDataManager _dataManager,
    IClientState _clientState,
    IPluginLog _pluginLog)
{
    /// <summary>
    ///     A set of words that indicate regeneration
    /// </summary>
    public FrozenSet<string> RegenKeywords { get; } = CreateFrozenSet(["regenerating", "restoring", "restore", "recovering"]);

    /// <summary>
    ///     A set of words that indicate an effect over time
    /// </summary>
    public FrozenSet<string> TimeKeywords { get; } = CreateFrozenSet(["gradually", "over time"]);

    /// <summary>
    ///     A set of words that indicate health
    /// </summary>
    public FrozenSet<string> HealthKeywords { get; } = CreateFrozenSet(["hp", "health"]);

    /// <summary>
    ///     A set of words that indicate mana
    /// </summary>
    public FrozenSet<string> ManaKeywords { get; } = CreateFrozenSet(["mp", "mana"]);

    /// <summary>
    ///     A set of words that indicate the halt of regen
    /// </summary>
    public FrozenSet<string> RegenNullKeywords { get; } = CreateFrozenSet(["null", "nullified", "stop", "stopped"]);

    private readonly DalamudPluginInterface pluginInterface = _pluginInterface;
    private readonly Configuration config = _config;
    private readonly ICondition condition = _condition;
    private readonly IDataManager dataManager = _dataManager;
    private readonly IClientState clientState = _clientState;
    private readonly IPluginLog log = _pluginLog;

    private const float OffsetX = 10f;
    private const float OffsetY = 25f;

    /// <summary>
    ///     Indicates if the <paramref name="window"/> is allowed to be drawn
    /// </summary>
    public bool WindowCondition(WindowType window)
    {
        try
        {
            var DisplayThisWindow = window switch
            {
                WindowType.HpWindow => config.HPVisible,
                WindowType.MpWindow => config.MPVisible,
                WindowType.GpWindow => config.GPVisible,
                _ => throw new NotSupportedException("Unknown Window"),
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
        if (window is WindowType.HpWindow)
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
        if (window is WindowType.MpWindow)
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
        if (window is WindowType.GpWindow)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InDuty()
        => (condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95])
        && !InIgnoredInstances();

    /// <summary>
    ///     Checks the current <see cref="TerritoryType.TerritoryIntendedUse"/> for a match against specific areas.
    /// </summary>
    /// <returns><see langword="true"/> if <see cref="TerritoryIntendedUseType.Diadem"/>, otherwise <see langword="false"/>.</returns>
    public bool InIgnoredInstances()
    {
        var territory = GetSheet<TerritoryType>()?.GetRow(clientState.TerritoryType);
        if (territory is null)
        {
            return false;
        }
        return (TerritoryIntendedUseType)territory.TerritoryIntendedUse is TerritoryIntendedUseType.Diadem;
    }

    /// <summary>
    ///     Checks the player's <see cref="ConditionFlag" /> for different cutscene flags.
    /// </summary>
    /// <returns><see langword="true"/> if any matching flag is set, otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InCutscene()
        => condition[ConditionFlag.OccupiedInCutSceneEvent]
        || condition[ConditionFlag.WatchingCutscene]
        || condition[ConditionFlag.WatchingCutscene78]
        || condition[ConditionFlag.Occupied38];

    /// <summary>
    ///     Check if the <paramref name="addon"/> can be safely accessed.
    /// </summary>
    /// <returns><see langword="true"/> if addon is initialized and ready for use, otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool IsAddonReady(AtkUnitBase* addon)
        => addon is not null && addon->RootNode is not null && addon->RootNode->ChildNode is not null;

    /// <summary>
    ///     Check if <paramref name="sourceCharacter"/> is currently targeting <paramref name="targetCharacter"/>, whether it's manually set, or the result of using an action.
    /// </summary>
    public unsafe bool IsTarget(PlayerCharacter targetCharacter, Character* sourceCharacter)
    {
        ITuple sourceTarget = (sourceCharacter->GetCastInfo()->CastTargetID, sourceCharacter->GetSoftTargetId().ObjectID, sourceCharacter->GetTargetId(), sourceCharacter->Gaze.Controller.GazesSpan[0].TargetInfo.TargetId.ObjectID);
        var targetID = (object)targetCharacter.ObjectId;
        for (var i = 0; i < sourceTarget.Length; i++)
        {
            var sourceTargetID = sourceTarget[i];
            if (sourceTargetID is null or 0xE0000000)
            {
                continue;
            }
            if (sourceTargetID.Equals(targetID))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    ///     Check if <paramref name="window"/> is overlapping <paramref name="addon"/>
    /// </summary>
    public unsafe bool AddonOverlap(AtkUnitBase* addon, BarWindowBase window, bool scaledAddon)
    {
        var addonPos = (X: addon->X, Y: addon->Y);
        var addonSize = (width: addon->GetScaledWidth(scaledAddon) - OffsetX, height: addon->GetScaledHeight(scaledAddon) - OffsetY);

        var topLeft = (window.WindowPosition.X, window.WindowPosition.Y);
        var topRight = (X: window.WindowPosition.X + window.WindowSize.X, window.WindowPosition.Y);
        var bottomLeft = (window.WindowPosition.X, Y: window.WindowPosition.Y + window.WindowSize.Y);
        var bottomRight = (X: window.WindowPosition.X + window.WindowSize.X, Y: window.WindowPosition.Y + window.WindowSize.Y);

        var addonTopLeft = (addonPos.X, addonPos.Y);
        var addonTopRight = (X: addonPos.X + addonSize.width, addonPos.Y);
        var addonBottomLeft = (addonPos.X, Y: addonPos.Y + addonSize.height);
        var addonBottomRight = (X: addonPos.X + addonSize.width, Y: addonPos.Y + addonSize.height);

        var topLeftCollision = topLeft.X > addonTopLeft.X
            && topLeft.X < addonTopRight.X
            && topLeft.Y > addonTopLeft.Y
            && topLeft.Y < addonBottomLeft.Y;
        var topRightCollision = topRight.X < addonTopRight.X
            && topRight.X > addonTopLeft.X
            && topRight.Y > addonTopRight.Y
            && topRight.Y < addonBottomRight.Y;
        var bottomLeftCollision = bottomLeft.X > addonBottomLeft.X
            && bottomLeft.X < addonBottomRight.X
            && bottomLeft.Y < addonBottomLeft.Y
            && bottomLeft.Y > addonTopLeft.Y;
        var bottomRightCollision = bottomRight.X < addonBottomRight.X
            && bottomRight.X > addonBottomLeft.X
            && bottomRight.Y < addonBottomRight.Y
            && bottomRight.Y > addonTopRight.Y;

        return topLeftCollision || topRightCollision || bottomLeftCollision || bottomRightCollision;
    }

    /// <summary>
    ///     Spawn a <see cref="Task"/> that checks if the <see cref="ConditionFlag"/> for loading is present every polling period.
    /// </summary>
    public async Task Loading(long pollingPeriodMilliseconds)
    {
        var loadingTimer = new Stopwatch();
        var loading = condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
        loadingTimer.Start();
        while (loading)
        {
            if (loadingTimer.ElapsedMilliseconds <= pollingPeriodMilliseconds)
            {
                var remainingTime = pollingPeriodMilliseconds - loadingTimer.ElapsedMilliseconds;
                await Task.Delay((int)remainingTime).ConfigureAwait(false);
                loadingTimer.Restart();
            }
            loading = condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
        }
        loadingTimer.Reset();
    }

    public async Task<bool> CheckIPC(double msInterval, System.Action func, CancellationToken cToken)
    {
        try
        {
            func.Invoke();
            return true;
        }
        catch
        {
            var timer = new System.Timers.Timer();
            timer.Interval = msInterval;
            timer.AutoReset = true;
            timer.Elapsed += timerCheck;
            timer.Start();
            var ipcAvailable = false;

            void timerCheck(object? sender, ElapsedEventArgs e)
            {
                if (cToken.IsCancellationRequested)
                {
                    timer.Stop();
                    return;
                }
                try
                {
                    func.Invoke();
                    ipcAvailable = true;
                    timer.Stop();
                }
                catch
                {
                    log.Warning("IPC not available.");
                    ipcAvailable = false;
                }
            }

            while (timer.Enabled && !cToken.IsCancellationRequested)
            {
                await Task.Delay((int)msInterval, cToken).ConfigureAwait(false);
            }
            timer.Dispose();

            return ipcAvailable;
        }
    }

    /// <summary>
    ///     Attempt to retrieve an <see cref="ExcelSheet{T}"/>, optionally in a specific <paramref name="language"/>.
    /// </summary>
    /// <returns><see cref="ExcelSheet{T}"/> or <see langword="null"/> if <see cref="IDataManager.GetExcelSheet{T}(ClientLanguage)"/> returns an invalid sheet.</returns>
    public ExcelSheet<T>? GetSheet<T>(ClientLanguage language = ClientLanguage.English) where T : ExcelRow
    {
        try
        {
            var sheet = dataManager.GetExcelSheet<T>(language);
            if (sheet is null)
            {
                log.Fatal("Invalid lumina sheet!");
            }
            return sheet;
        }
        catch (Exception e)
        {
            log.Fatal("Retrieving lumina sheet failed!");
            log.Fatal(e.Message);
            return null;
        }
    }

    /// <summary>
    ///     Change the <see cref="Window.SizeConstraints"/> of <paramref name="window"/> to the provided <paramref name="sizeLimit"/>.
    /// </summary>
    /// <param name="window">The desired <see cref="Window"/> to change size constraints</param>
    /// <param name="sizeLimit">The maximum size of the <paramref name="window"/></param>
    public static void ChangeWindowConstraints(Window window, Vector2 sizeLimit)
    {
        if (window.SizeConstraints.HasValue)
        {
            window.SizeConstraints = new Window.WindowSizeConstraints
            {
                MinimumSize = window.SizeConstraints.Value.MinimumSize,
                MaximumSize = sizeLimit,
            };
        }
    }

    /// <summary>
    ///     Function that returns a <see cref="FrozenSet{T}"/> from the provided <see cref="IEnumerable{T}"/> <paramref name="collection"/>.
    /// <para>This is used mostly to remove some boilerplate.</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrozenSet<T> CreateFrozenSet<T>(IEnumerable<T> collection)
        => collection.ToFrozenSet();

    public void Benchmark(System.Action func, int iterations, string benchmarkName)
    {
        var stopwatch = new Stopwatch();
        var innerSw = new Stopwatch();
        List<double> timers = [];
        stopwatch.Restart();
        for (var i = 0; i < iterations; i++)
        {
            innerSw.Restart();
            func.Invoke();
            innerSw.Stop();
            timers.Add(innerSw.Elapsed.TotalNanoseconds / 1000);
        }
        stopwatch.Stop();
        log.Debug(benchmarkName + " benchmark took {t} ms", stopwatch.Elapsed.TotalMilliseconds);
        log.Debug(benchmarkName + " Average: {a} µs - Min: {b} µs - Max: {c} µs", Truncate(timers.Average()), timers.Min(), timers.Max());

        static double Truncate(double item)
            => Math.Truncate(item * 1000) / 1000;
    }

    [System.Text.RegularExpressions.GeneratedRegex("\\W+", System.Text.RegularExpressions.RegexOptions.Compiled, 500)]
    private static partial System.Text.RegularExpressions.Regex KeywordsRegex();
}
