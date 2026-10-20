using System.Numerics;

namespace ScissorOutline;

internal static class ScissorOutlineJitter
{
    private const double InverseSquareRootOfTwo = 0.70710678118654752;

    public static Vector2 GetOffset(int x, int y, int seed, double amplitude)
    {
        var hash = Mix(unchecked((uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA77u ^ (uint)seed * 0xC2B2AE3Du));
        var horizontal = (hash & 0xFFFF) / 32767.5 - 1.0;
        var vertical = (hash >> 16) / 32767.5 - 1.0;
        var scale = amplitude * InverseSquareRootOfTwo;
        return new Vector2((float)(horizontal * scale), (float)(vertical * scale));
    }

    private static uint Mix(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x85EBCA6Bu;
            value ^= value >> 13;
            value *= 0xC2B2AE35u;
            value ^= value >> 16;
            return value;
        }
    }
}
