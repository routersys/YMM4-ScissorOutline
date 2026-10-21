namespace ScissorOutline;

internal sealed class ContourLoops
{
    private int[] _points = new int[512];
    private double[] _shifts = new double[256];
    private int[] _starts = new int[17];

    public int PointCount { get; private set; }

    public int LoopCount { get; private set; }

    public ReadOnlySpan<int> Points => _points.AsSpan(0, PointCount * 2);

    public ReadOnlySpan<double> Shifts => _shifts.AsSpan(0, PointCount);

    public ReadOnlySpan<int> Starts => _starts.AsSpan(0, LoopCount + 1);

    public void Clear()
    {
        PointCount = 0;
        LoopCount = 0;
        _starts[0] = 0;
    }

    public void AddPoint(int x, int y, double shift = 0.0)
    {
        if (PointCount * 2 + 2 > _points.Length)
        {
            Array.Resize(ref _points, _points.Length * 2);
            Array.Resize(ref _shifts, _shifts.Length * 2);
        }

        _points[PointCount * 2] = x;
        _points[PointCount * 2 + 1] = y;
        _shifts[PointCount] = shift;
        PointCount++;
    }

    public void CloseLoop()
    {
        if (LoopCount + 2 > _starts.Length)
            Array.Resize(ref _starts, _starts.Length * 2);
        LoopCount++;
        _starts[LoopCount] = PointCount;
    }

    public void DiscardOpenLoop()
    {
        PointCount = _starts[LoopCount];
    }

    public long GetDoubleArea(int loop)
    {
        var start = _starts[loop];
        var count = _starts[loop + 1] - start;
        long area = 0;
        for (var i = 0; i < count; i++)
        {
            var current = (start + i) * 2;
            var next = (start + (i + 1) % count) * 2;
            area += (long)_points[current] * _points[next + 1] - (long)_points[next] * _points[current + 1];
        }

        return area;
    }
}
