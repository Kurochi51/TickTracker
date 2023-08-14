using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.JobGauge;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace TickTracker;

public class Service
{
    [PluginService] public static DalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] public static IClientState ClientState { get; set; } = null!;
    [PluginService] public static Framework Framework { get; set; } = null!;
    [PluginService] public static IGameGui GameGui { get; set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] public static Condition Condition { get; set; } = null!;
    [PluginService] public static IDataManager DataManager { get; set; } = null!;
    [PluginService] public static JobGauges JobGauges { get; set; } = null!;
}
