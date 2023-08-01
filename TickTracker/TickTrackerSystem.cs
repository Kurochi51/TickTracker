namespace TickTracker;

public class TickTrackerSystem
{
    public static Configuration config = null!;
    public TickTrackerSystem()
    {
        config = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    }
}
