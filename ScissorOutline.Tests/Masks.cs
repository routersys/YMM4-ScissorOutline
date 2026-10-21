namespace ScissorOutline.Tests;

internal static class Masks
{
    public static byte[] Random(int width, int height, int seed, int percent)
    {
        var state = (uint)seed * 2654435761u + 12345u;
        var mask = new byte[width * height];
        for (var i = 0; i < mask.Length; i++)
        {
            state = state * 1664525u + 1013904223u;
            mask[i] = (byte)((state >> 24) % 100 < percent ? 1 : 0);
        }

        return mask;
    }

    public static ulong[] ToBits(byte[] mask, int width, int height)
    {
        var wordsPerRow = BitGrid.WordsPerRow(width);
        var bits = new ulong[wordsPerRow * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (mask[y * width + x] != 0)
                    BitGrid.Set(bits, wordsPerRow, x, y);
            }
        }

        return bits;
    }

    public static ulong[] FromRows(string[] rows)
    {
        var height = rows.Length;
        var width = rows[0].Length;
        var mask = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                mask[y * width + x] = rows[y][x] == '#' ? (byte)1 : (byte)0;
        }

        return ToBits(mask, width, height);
    }

    public static byte[] ToBytes(ReadOnlySpan<ulong> bits, int width, int height)
    {
        var wordsPerRow = BitGrid.WordsPerRow(width);
        var mask = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                mask[y * width + x] = BitGrid.Get(bits, wordsPerRow, x, y) ? (byte)1 : (byte)0;
        }

        return mask;
    }
}
