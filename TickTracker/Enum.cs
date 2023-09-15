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
        GpWindow,
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

    public enum ActionEffectType : byte
    {
        Nothing = 0,
        Miss = 1,
        FullResist = 2,
        Damage = 3,
        Heal = 4,
        BlockedDamage = 5,
        ParriedDamage = 6,
        Invulnerable = 7,
        NoEffectText = 8,
        Unknown_0 = 9,
        MpLoss = 10,
        MpGain = 11,
        TpLoss = 12,
        TpGain = 13,
        GpGain = 14,
        ApplyStatusEffectTarget = 15,
        ApplyStatusEffectSource = 16,
        StatusNoEffect = 20,
        Unknown0 = 27,
        Unknown1 = 28,
        Ukknown2 = 32,
        Knockback = 33,
        Mount = 40,
        VFX = 59,
        JobGauge = 61,
    };
}
