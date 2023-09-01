using Lumina.Excel.GeneratedSheets;
using System.Runtime.InteropServices;
using System;

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
        Knockback = 33,
        Mount = 40,
        VFX = 59,
        JobGauge = 61,
    };
}
// DamageInfo structs
#pragma warning disable MA0048 // File name must match type name
[StructLayout(LayoutKind.Explicit)]
public struct EffectHeader
{
    [FieldOffset(8)] public uint ActionId;
    [FieldOffset(28)] public ushort AnimationId;
    [FieldOffset(33)] public byte TargetCount;
}

[StructLayout(LayoutKind.Auto)]
public struct EffectEntry
{
    public Enum.ActionEffectType type;
    public byte param0;
    public byte param1;
    public byte param2;
    public byte mult;
    public byte flags;
    public ushort value;

    public byte AttackType => (byte)(param1 & 0xF);

    public override string ToString()
    {
        return
            $"Type: {type}, p0: {param0:D3}, p1: {param1:D3}, p2: {param2:D3} 0x{param2:X2} '{Convert.ToString(param2, 2).PadLeft(8, '0')}', mult: {mult:D3}, flags: {flags:D3} | {Convert.ToString(flags, 2).PadLeft(8, '0')}, value: {value:D6} ATTACK TYPE: {AttackType}";
    }
#pragma warning restore MA0048 // File name must match type name
}
