using System;
using System.Runtime.InteropServices;

namespace TickTracker
{
#pragma warning disable MA0048 // File name must match type name
    // DamageInfo structs
    [StructLayout(LayoutKind.Explicit)]
    public struct EffectHeader
    {
        [FieldOffset(8)] public uint ActionId;
        [FieldOffset(28)] public ushort AnimationId;
        [FieldOffset(33)] public byte TargetCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EffectEntry
    {
        public ActionEffectType type;
        public byte param0;
        public byte param1;
        public byte param2;
        public byte mult;
        public byte flags;
        public ushort value;

        public readonly byte AttackType => (byte)(param1 & 0xF);

        public override readonly string ToString()
        {
            return
                $"Type: {type}, p0: {param0:D3}, p1: {param1:D3}, p2: {param2:D3} 0x{param2:X2} '{Convert.ToString(param2, 2).PadLeft(8, '0')}', mult: {mult:D3}, flags: {flags:D3} | {Convert.ToString(flags, 2).PadLeft(8, '0')}, value: {value:D6} ATTACK TYPE: {AttackType}";
        }
    }
#pragma warning restore MA0048 // File name must match type name
}
