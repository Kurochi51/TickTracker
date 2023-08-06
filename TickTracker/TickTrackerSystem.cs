namespace TickTracker;

public class TickTrackerSystem
{
    /// <summary>
    ///     A <see cref="Configuration"/> instance to be referenced across the plugin.
    /// </summary>
    public static Configuration config = null!;
    public TickTrackerSystem()
    {
        config = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    }
}
