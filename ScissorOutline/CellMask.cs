using System.Numerics;
using System.Runtime.Intrinsics;

namespace ScissorOutline;

internal static class CellMask
{
    private const int Lanes = 16;
    private const int LaneShift = 4;

    public static void Build(ReadOnlySpan<byte> alpha, int width, int height, int stride, int cell, int gridWidth, Span<ulong> cells)
    {
        cells.Clear();
        var wordsPerRow = BitGrid.WordsPerRow(gridWidth);
        var shift = BitOperations.Log2((uint)cell);
        var threshold = Vector128.Create(ScissorOutlineSettings.AlphaThreshold);
        for (var y = 0; y < height; y++)
        {
            var row = alpha.Slice(y * stride, width);
            var cellRow = cells.Slice(((y >> shift) + ScissorOutlineSettings.PaddingCells) * wordsPerRow, wordsPerRow);
            var x = 0;
            for (; x + Lanes <= row.Length; x += Lanes)
            {
                var mask = Vector128.GreaterThan(Vector128.Create(row.Slice(x, Lanes)), threshold).ExtractMostSignificantBits();
                if (mask != 0)
                    Set(cellRow, (x >> shift) + ScissorOutlineSettings.PaddingCells, Fold(mask, shift));
            }

            for (; x < row.Length; x++)
            {
                if (row[x] > ScissorOutlineSettings.AlphaThreshold)
                    Set(cellRow, (x >> shift) + ScissorOutlineSettings.PaddingCells, 1UL);
            }
        }
    }

    private static ulong Fold(uint mask, int shift)
    {
        for (var step = Math.Min(shift, LaneShift); step > 0; step--)
        {
            var pairs = (mask | (mask >> 1)) & 0x5555u;
            pairs = (pairs | (pairs >> 1)) & 0x3333u;
            pairs = (pairs | (pairs >> 2)) & 0x0F0Fu;
            mask = (pairs | (pairs >> 4)) & 0x00FFu;
        }

        return mask;
    }

    private static void Set(Span<ulong> cellRow, int position, ulong bits)
    {
        var word = position >> 6;
        var offset = position & 63;
        cellRow[word] |= bits << offset;
        if (offset != 0)
        {
            var carry = bits >> (64 - offset);
            if (carry != 0)
                cellRow[word + 1] |= carry;
        }
    }
}
