namespace TickTracker;

public class TickTrackerSystem
{
    public static Configuration config = null!;
    public TickTrackerSystem()
    {
        config = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    }
}
