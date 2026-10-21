namespace ScissorOutline;

internal sealed class DistanceTransform
{
    private const int Far = 1 << 14;

    private int[] _row = [];
    private int[] _starts = [];
    private int[] _bounds = [];

    public void Squared(ReadOnlySpan<ulong> source, int width, int height, Span<int> result)
    {
        var wordsPerRow = BitGrid.WordsPerRow(width);
        var distances = result.Slice(0, width * height);
        ScanColumns(source, width, height, wordsPerRow, distances);
        if (_row.Length < width)
        {
            _row = new int[width];
            _starts = new int[width];
            _bounds = new int[width];
        }

        for (var y = 0; y < height; y++)
            ScanRow(distances.Slice(y * width, width));
    }

    private static void ScanColumns(ReadOnlySpan<ulong> source, int width, int height, int wordsPerRow, Span<int> distances)
    {
        for (var y = 0; y < height; y++)
        {
            var bits = source.Slice(y * wordsPerRow, wordsPerRow);
            var row = distances.Slice(y * width, width);
            for (var x = 0; x < width; x++)
            {
                if (((bits[x >> 6] >> (x & 63)) & 1UL) != 0)
                    row[x] = 0;
                else
                    row[x] = y > 0 ? Math.Min(distances[(y - 1) * width + x] + 1, Far) : Far;
            }
        }

        for (var y = height - 2; y >= 0; y--)
        {
            var row = distances.Slice(y * width, width);
            var below = distances.Slice((y + 1) * width, width);
            for (var x = 0; x < width; x++)
                row[x] = Math.Min(row[x], below[x] + 1);
        }
    }

    private void ScanRow(Span<int> row)
    {
        var width = row.Length;
        var g = _row.AsSpan(0, width);
        row.CopyTo(g);
        var s = _starts;
        var t = _bounds;
        var q = 0;
        s[0] = 0;
        t[0] = 0;
        for (var u = 1; u < width; u++)
        {
            while (q >= 0 && Cost(t[q], s[q], g) > Cost(t[q], u, g))
                q--;
            if (q < 0)
            {
                q = 0;
                s[0] = u;
            }
            else
            {
                var boundary = 1 + Separation(s[q], u, g);
                if (boundary < width)
                {
                    q++;
                    s[q] = u;
                    t[q] = boundary;
                }
            }
        }

        for (var u = width - 1; u >= 0; u--)
        {
            row[u] = Cost(u, s[q], g);
            if (u == t[q])
                q--;
        }
    }

    private static int Cost(int x, int i, ReadOnlySpan<int> g)
    {
        var dx = x - i;
        return dx * dx + g[i] * g[i];
    }

    private static int Separation(int i, int u, ReadOnlySpan<int> g)
    {
        long numerator = (long)u * u - (long)i * i + (long)g[u] * g[u] - (long)g[i] * g[i];
        long denominator = 2L * (u - i);
        return (int)Math.DivRem(numerator, denominator, out var remainder) - (remainder < 0 ? 1 : 0);
    }
}
