using System.Numerics;

namespace ScissorOutline;

internal static class CellMask
{
    public static void Build(ReadOnlySpan<byte> alpha, int width, int height, int stride, int cell, int gridWidth, Span<byte> cells)
    {
        cells.Clear();
        var shift = BitOperations.Log2((uint)cell);
        for (var y = 0; y < height; y++)
        {
            var row = alpha.Slice(y * stride, width);
            var cellRow = cells.Slice(((y >> shift) + ScissorOutlineSettings.PaddingCells) * gridWidth + ScissorOutlineSettings.PaddingCells);
            for (var x = 0; x < row.Length; x++)
            {
                if (row[x] > ScissorOutlineSettings.AlphaThreshold)
                    cellRow[x >> shift] = 1;
            }
        }
    }
}
