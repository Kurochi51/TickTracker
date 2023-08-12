using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Logging;
using Dalamud.Utility;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TickTracker.Windows;
using Dalamud.Plugin.Services;

namespace TickTracker
{
    public sealed unsafe class Plugin : IDalamudPlugin
    {
        /// <summary>
        /// A <see cref="HashSet{T}" /> list of Status IDs that trigger HP regen
        /// </summary>
        private static readonly HashSet<uint> _healthRegenList = new();
        /// <summary>
        /// A <see cref="HashSet{T}" /> list of Status IDs that trigger MP regen
        /// </summary>
        private static readonly HashSet<uint> _manaRegenList = new();
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly IClientState _clientState;
        private readonly Framework _framework;
        private readonly IGameGui _gameGui;
        private readonly ICommandManager _commandManager;
        private readonly Condition _condition;
        private readonly Utilities _utilities;

        /// <summary>
        ///     A <see cref="Configuration"/> instance to be referenced across the plugin.
        /// </summary>
        public static Configuration Config { get; set; } = null!;

        public string Name => "Tick Tracker";
        private const string _commandName = "/tick";
        public WindowSystem WindowSystem = new("TickTracker");
        private ConfigWindow ConfigWindow { get; init; }
        private HPBar HPBarWindow { get; init; }
        private MPBar MPBarWindow { get; init; }
        public static DebugWindow DebugWindow { get; set; } = null!;
        private bool _inCombat, _nullSheet = true;
        private double _syncValue = 1;
        private int _lastHPValue = -1, _lastMPValue = -1;
        private const float _actorTickInterval = 3, _fastTickInterval = 1.5f;
        private uint _currentHP = 1, _currentMP = 1, _maxHP = 2, _maxMP = 2;
        private AtkUnitBase* NameplateAddon => (AtkUnitBase*)_gameGui.GetAddonByName("NamePlate");

        public Plugin(DalamudPluginInterface pluginInterface, IClientState clientState,
            Framework framework, IGameGui gameGui, ICommandManager commandManager, Condition condition,
            IDataManager dataManager
            )
        {
            _pluginInterface = pluginInterface;
            _clientState = clientState;
            _framework = framework;
            _gameGui = gameGui;
            _commandManager = commandManager;
            _condition = condition;
            _utilities = new Utilities(condition, dataManager, clientState);

            Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            // this is a workaround as Configuration wouldn't be able to deserialize otherwise
            // it's pretty ugly to use a setter to set the PluginInterface here but there's no other option
            // it's also why I personally don't use Configuration but have my own methods to save and load configs
            Config.PluginInterface = pluginInterface; 

            ConfigWindow = new ConfigWindow();
            HPBarWindow = new HPBar(clientState);
            MPBarWindow = new MPBar(clientState);
            DebugWindow = new DebugWindow();
            WindowSystem.AddWindow(DebugWindow);
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(HPBarWindow);
            WindowSystem.AddWindow(MPBarWindow);

            commandManager.AddHandler(_commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open or close Tick Tracker's config window."
            });
            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            framework.Update += OnFrameworkUpdate;
            clientState.TerritoryChanged += TerritoryChanged;
            Task.Run(() =>
            {
                var statusSheet = dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>(Dalamud.ClientLanguage.English);
                if (statusSheet != null)
                {
                    foreach (var stat in statusSheet.Where(s => s.RowId is not 307 and not 1419 and not 135))
                    {
                        var text = stat.Description.ToDalamudString().TextValue;
                        if (Utilities.KeywordMatch(text, Utilities.RegenKeywords) && Utilities.KeywordMatch(text, Utilities.TimeKeywords))
                        {
                            if (Utilities.KeywordMatch(text, Utilities.HealthKeywords))
                            {
                                _healthRegenList.Add(stat.RowId);
                                DebugWindow.HealthRegenDictionary.Add(stat.RowId, stat.Name);
                            }
                            if (Utilities.KeywordMatch(text, Utilities.ManaKeywords))
                            {
                                _manaRegenList.Add(stat.RowId);
                                DebugWindow.ManaRegenDictionary.Add(stat.RowId, stat.Name);
                            }
                        }
                    }
                    _nullSheet = false;
                    PluginLog.Debug("HP regen list generated with {HPcount} status effects.", _healthRegenList.Count);
                    PluginLog.Debug("MP regen list generated with {MPcount} status effects.", _manaRegenList.Count);
                }
                else
                {
                    _nullSheet = true;
                    PluginLog.Error("Status sheet couldn't get queued.");
                }
            });
        }

        private bool PluginEnabled(bool target)
        {
            if (_condition[ConditionFlag.InDuelingArea])
            {
                return false;
            }
            if (Config.HideOutOfCombat && !_inCombat)
            {
                var showingBecauseInDuty = Config.AlwaysShowInDuties && _utilities.InDuty();
                var showingBecauseHasTarget = Config.AlwaysShowWithHostileTarget && target;
                if (!(showingBecauseInDuty || showingBecauseHasTarget))
                {
                    return false;
                }
            }
            return Config.PluginEnabled;
        }

        private void OnFrameworkUpdate(Framework framework)
        {
            var now = DateTime.Now.TimeOfDay.TotalSeconds;
            if (_clientState is { IsLoggedIn: false } or { IsPvPExcludingDen: true } || _nullSheet)
            {
                return;
            }
            bool LucidDream;
            bool Regen;
            bool Enemy;
            if (_clientState is { LocalPlayer: { } player })
            {
                LucidDream = player.StatusList.Any(e => _manaRegenList.Contains(e.StatusId));
                Regen = player.StatusList.Any(e => _healthRegenList.Contains(e.StatusId));
                Enemy = player.TargetObject?.ObjectKind == ObjectKind.BattleNpc;
                _inCombat = _condition[ConditionFlag.InCombat];
                _currentHP = player.CurrentHp;
                _maxHP = player.MaxHp;
                _currentMP = player.CurrentMp;
                _maxMP = player.MaxMp;
            }
            else
            {
                return;
            }
            if (!PluginEnabled(Enemy) || !Utilities.IsAddonReady(NameplateAddon) || !NameplateAddon->IsVisible || _utilities.InCustcene())
            {
                HPBarWindow.IsOpen = false;
                MPBarWindow.IsOpen = false;
                return;
            }
            var shouldShowHPBar = !Config.HideOnFullResource ||
                                (Config.AlwaysShowInCombat && _inCombat) ||
                                (Config.AlwaysShowWithHostileTarget && Enemy) ||
                                (Config.AlwaysShowInDuties && _utilities.InDuty());
            var shouldShowMPBar = !Config.HideOnFullResource ||
                                (Config.AlwaysShowInCombat && _inCombat) ||
                                (Config.AlwaysShowWithHostileTarget && Enemy) ||
                                (Config.AlwaysShowInDuties && _utilities.InDuty());
            HPBarWindow.IsOpen = shouldShowHPBar || (_currentHP != _maxHP);
            MPBarWindow.IsOpen = shouldShowMPBar || (_currentMP != _maxMP);
            if ((_lastHPValue < _currentHP && !HPBarWindow.FastTick) || (_lastMPValue < _currentMP && !MPBarWindow.FastTick))
            {
                _syncValue = now;
            }
            UpdateHPTick(now, Regen);
            UpdateMPTick(now, LucidDream);
            if (_syncValue + _actorTickInterval <= now)
            {
                _syncValue += _actorTickInterval;
            }
        }

        private void UpdateHPTick(double currentTime, bool regen)
        {
            HPBarWindow.FastTick = (regen && _currentHP != _maxHP);
            if (_currentHP == _maxHP)
            {
                HPBarWindow.LastTick = _syncValue;
            }
            else if (_lastHPValue < _currentHP)
            {
                HPBarWindow.LastTick = currentTime;
            }
            else if (HPBarWindow.LastTick + (HPBarWindow.FastTick ? _fastTickInterval : _actorTickInterval) <= currentTime)
            {
                HPBarWindow.LastTick += HPBarWindow.FastTick ? _fastTickInterval : _actorTickInterval;
            }
            _lastHPValue = (int)_currentHP;
            HPBarWindow.UpdateAvailable = true;
        }

        private void UpdateMPTick(double currentTime, bool lucid)
        {
            MPBarWindow.FastTick = (lucid && _currentMP != _maxMP);
            if (_currentMP == _maxMP)
            {
                MPBarWindow.LastTick = _syncValue;
            }
            else if (_lastMPValue < _currentMP)
            {
                MPBarWindow.LastTick = currentTime;
            }
            else if (MPBarWindow.LastTick + (MPBarWindow.FastTick ? _fastTickInterval : _actorTickInterval) <= currentTime)
            {
                MPBarWindow.LastTick += MPBarWindow.FastTick ? _fastTickInterval : _actorTickInterval;
            }
            _lastMPValue = (int)_currentMP;
            MPBarWindow.UpdateAvailable = true;
        }

        private void TerritoryChanged(object? sender, ushort e)
        {
            _lastHPValue = -1;
            _lastMPValue = -1;
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();
            ConfigWindow.Dispose();
            DebugWindow.Dispose();
            _pluginInterface.UiBuilder.Draw -= DrawUI;
            _pluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            _commandManager.RemoveHandler(_commandName);
            _framework.Update -= OnFrameworkUpdate;
            _clientState.TerritoryChanged -= TerritoryChanged;
        }

        private void OnCommand(string command, string args)
        {
            ConfigWindow.Toggle();
        }

        private void DrawUI()
        {
            WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.Toggle();
        }
    }
}
