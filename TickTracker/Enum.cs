using Lumina.Excel.GeneratedSheets;

namespace TickTracker;

public class Enum
{
    /// <summary>
    ///     An enum of expected <see cref="WindowType" />.<paramref name="window"/>
    /// </summary>
    public enum WindowType
    {
        HpWindow,
        MpWindow,
    }

    /// <summary>
    ///     A mapping of <see cref="TerritoryType.TerritoryIntendedUse" /> IDs to their names.
    /// </summary>
    public enum TerritoryIntendedUseType : byte
    {
        City = 0,
        OpenWorld = 1,
        Inn = 2,
        Dungeon = 3,
        AllianceRaid = 8,
        Trial = 10,
        Housing1 = 13,
        Housing2 = 14,
        InstancedOpenZone = 15,
        Raid1 = 16,
        Raid2 = 17,
        GoldSaucer = 23,
        GrandCompany = 30,
        DeepDungeons = 31,
        Eureka = 41,
        Diadem = 47,
        BozjaZadnor = 48,
        IslandSanctuary = 49,
    }
}
