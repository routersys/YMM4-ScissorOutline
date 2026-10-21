namespace ScissorOutline;

internal sealed class ThicknessDilation
{
    private readonly DistanceTransform _distances = new();
    private readonly ThicknessField _field = new();
    private int[] _squared = [];
    private double[] _row = [];

    public void Dilate(ReadOnlySpan<ulong> source, int width, int height, in ScissorOutlinePlan plan, uint fixedVariation, uint frameVariation, Span<ulong> result)
    {
        var wordsPerRow = BitGrid.WordsPerRow(width);
        var size = width * height;
        if (_squared.Length < size)
            _squared = new int[size];
        if (_row.Length < width)
            _row = new double[width];

        var squared = _squared.AsSpan(0, size);
        _distances.Squared(source, width, height, squared);
        _field.Prepare(width, height, plan.Cell, plan.SpreadWavelength, fixedVariation, frameVariation, plan.SpreadChange);
        result.Slice(0, wordsPerRow * height).Clear();
        var row = _row.AsSpan(0, width);
        for (var y = 0; y < height; y++)
        {
            _field.FillRow(y, row);
            var bits = result.Slice(y * wordsPerRow, wordsPerRow);
            var distances = squared.Slice(y * width, width);
            for (var x = 0; x < width; x++)
            {
                var radius = (plan.Radius + plan.ThicknessSpread * row[x]) / plan.Cell;
                if (distances[x] <= radius * radius)
                    bits[x >> 6] |= 1UL << (x & 63);
            }
        }
    }
}
