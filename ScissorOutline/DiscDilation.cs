namespace ScissorOutline;

internal sealed class DiscDilation
{
    private ulong[] _spread = [];
    private ulong[] _row = [];
    private int[] _halfWidths = [];

    public void Dilate(ReadOnlySpan<ulong> source, int width, int height, int squaredRadius, Span<ulong> result)
    {
        var wordsPerRow = BitGrid.WordsPerRow(width);
        result.Slice(0, wordsPerRow * height).Clear();
        var radius = IntegerRoot(squaredRadius);
        var spreadLength = wordsPerRow + (radius >> 6) + 2;
        if (_spread.Length < spreadLength)
            _spread = new ulong[spreadLength];
        if (_row.Length < wordsPerRow)
            _row = new ulong[wordsPerRow];
        if (_halfWidths.Length < radius + 1)
            _halfWidths = new int[radius + 1];
        for (var dy = 0; dy <= radius; dy++)
            _halfWidths[dy] = IntegerRoot(squaredRadius - dy * dy);

        var lastMask = (width & 63) == 0 ? ulong.MaxValue : (1UL << (width & 63)) - 1UL;
        for (var y = 0; y < height; y++)
        {
            var sourceRow = source.Slice(y * wordsPerRow, wordsPerRow);
            if (sourceRow.IndexOfAnyExcept(0UL) < 0)
                continue;

            var halfWidth = -1;
            for (var dy = radius; dy >= 0; dy--)
            {
                if (_halfWidths[dy] != halfWidth)
                {
                    halfWidth = _halfWidths[dy];
                    Spread(sourceRow, halfWidth, spreadLength, lastMask);
                }

                Merge(result, y + dy, height, wordsPerRow);
                if (dy > 0)
                    Merge(result, y - dy, height, wordsPerRow);
            }
        }
    }

    private void Merge(Span<ulong> result, int y, int height, int wordsPerRow)
    {
        if ((uint)y >= (uint)height)
            return;

        var target = result.Slice(y * wordsPerRow, wordsPerRow);
        for (var i = 0; i < wordsPerRow; i++)
            target[i] |= _row[i];
    }

    private void Spread(ReadOnlySpan<ulong> sourceRow, int halfWidth, int spreadLength, ulong lastMask)
    {
        var spread = _spread.AsSpan(0, spreadLength);
        spread.Clear();
        sourceRow.CopyTo(spread);
        var length = 2 * halfWidth + 1;
        var covered = 1;
        while (covered * 2 <= length)
        {
            ShiftUpOr(spread, covered);
            covered *= 2;
        }

        if (covered < length)
            ShiftUpOr(spread, length - covered);

        var row = _row.AsSpan(0, sourceRow.Length);
        ShiftDown(spread, halfWidth, row);
        row[^1] &= lastMask;
    }

    private static void ShiftUpOr(Span<ulong> bits, int shift)
    {
        var wordShift = shift >> 6;
        var bitShift = shift & 63;
        for (var i = bits.Length - 1; i >= wordShift; i--)
        {
            var value = bits[i - wordShift] << bitShift;
            if (bitShift != 0 && i - wordShift - 1 >= 0)
                value |= bits[i - wordShift - 1] >> (64 - bitShift);
            bits[i] |= value;
        }
    }

    private static void ShiftDown(ReadOnlySpan<ulong> bits, int shift, Span<ulong> result)
    {
        var wordShift = shift >> 6;
        var bitShift = shift & 63;
        for (var i = 0; i < result.Length; i++)
        {
            var from = i + wordShift;
            var value = from < bits.Length ? bits[from] >> bitShift : 0UL;
            if (bitShift != 0 && from + 1 < bits.Length)
                value |= bits[from + 1] << (64 - bitShift);
            result[i] = value;
        }
    }

    private static int IntegerRoot(int value)
    {
        var root = (int)Math.Sqrt(value);
        while (root * root > value)
            root--;
        while ((root + 1) * (root + 1) <= value)
            root++;
        return root;
    }
}
