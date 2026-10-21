using System.Numerics;

namespace ScissorOutline;

internal static class ScissorOutlineJitter
{
    private const double InverseSquareRootOfTwo = 0.70710678118654752;
    private const uint ToleranceSalt = 0x68E31DA4u;
    private const uint WaveSalt = 0xB5297A4Du;
    private const uint SplitSalt = 0x2545F491u;

    public static uint Variation(int seed, int frame)
        => unchecked((uint)seed + (uint)frame * 0x9E3779B9u);

    public static Vector2 GetOffset(int x, int y, uint variation, double amplitude)
    {
        var hash = Mix(unchecked((uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA77u ^ variation * 0xC2B2AE3Du));
        var horizontal = (hash & 0xFFFF) / 32767.5 - 1.0;
        var vertical = (hash >> 16) / 32767.5 - 1.0;
        var scale = amplitude * InverseSquareRootOfTwo;
        return new Vector2((float)(horizontal * scale), (float)(vertical * scale));
    }

    public static double GetToleranceFactor(int fromX, int fromY, int toX, int toY, uint variation, double spread)
    {
        var unit = Unit(fromX, fromY, toX, toY, variation ^ ToleranceSalt);
        return 1.0 - ScissorOutlineSettings.IrregularEpsilonShrink * spread
            + (ScissorOutlineSettings.IrregularEpsilonShrink + ScissorOutlineSettings.IrregularEpsilonGrowth) * spread * unit;
    }

    public static double GetSplitUnit(int fromX, int fromY, int toX, int toY, uint variation)
        => Unit(fromX, fromY, toX, toY, variation ^ SplitSalt);

    public static double GetWave(double x, double y, double wavelength, uint variation)
    {
        var u = x / wavelength;
        var v = y / wavelength;
        var cellX = (int)Math.Floor(u);
        var cellY = (int)Math.Floor(v);
        var fadeX = Fade(u - cellX);
        var fadeY = Fade(v - cellY);
        var salted = variation ^ WaveSalt;
        var topLeft = Signed(cellX, cellY, salted);
        var topRight = Signed(cellX + 1, cellY, salted);
        var bottomLeft = Signed(cellX, cellY + 1, salted);
        var bottomRight = Signed(cellX + 1, cellY + 1, salted);
        var top = topLeft + (topRight - topLeft) * fadeX;
        var bottom = bottomLeft + (bottomRight - bottomLeft) * fadeX;
        return top + (bottom - top) * fadeY;
    }

    public static double Unit(int first, int second, int third, int fourth, uint variation)
    {
        unchecked
        {
            var hash = Mix(Mix((uint)first * 0x9E3779B1u ^ (uint)second * 0x85EBCA77u) ^ ((uint)third * 0xC2B2AE3Du ^ (uint)fourth * 0x27D4EB2Fu) ^ variation);
            return hash / 4294967296.0;
        }
    }

    private static double Signed(int x, int y, uint variation) => Unit(x, y, 0, 0, variation) * 2.0 - 1.0;

    private static double Fade(double value) => value * value * (3.0 - 2.0 * value);

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
