using System.Numerics;

namespace ScissorOutline;

internal static class CellMask
{
    public static void Build(ReadOnlySpan<byte> alpha, int width, int height, int stride, int cell, int gridWidth, Span<ulong> cells)
    {
        cells.Clear();
        var wordsPerRow = BitGrid.WordsPerRow(gridWidth);
        var shift = BitOperations.Log2((uint)cell);
        for (var y = 0; y < height; y++)
        {
            var row = alpha.Slice(y * stride, width);
            var cellRow = cells.Slice(((y >> shift) + ScissorOutlineSettings.PaddingCells) * wordsPerRow, wordsPerRow);
            for (var x = 0; x < row.Length; x++)
            {
                if (row[x] <= ScissorOutlineSettings.AlphaThreshold)
                    continue;

                var column = (x >> shift) + ScissorOutlineSettings.PaddingCells;
                cellRow[column >> 6] |= 1UL << (column & 63);
            }
        }
    }
}
