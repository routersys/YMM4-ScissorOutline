using System.Numerics;

namespace ScissorOutline;

internal enum ScissorOutlineShapeChange
{
    Unchanged,
    Rebuilt,
}

internal sealed class ScissorOutlineShape
{
    private const double MiterLimit = 4.0;
    private const double MinimumEdgeRatio = 0.25;

    private readonly DiscDilation _dilation = new();
    private readonly ThicknessDilation _thickness = new();
    private readonly ContourTracer _tracer = new();
    private readonly PolygonSimplifier _simplifier = new();
    private readonly ContourLoops _traced = new();
    private readonly ContourLoops _simplified = new();

    private ulong[] _cells = [];
    private ulong[] _nextCells = [];
    private ulong[] _inside = [];
    private CellKey _cellKey;
    private bool _hasCells;
    private bool _hasSubject;
    private bool _hasTraced;
    private TraceKey _traceKey;
    private ShapeKey _shapeKey;
    private bool _hasShape;
    private Vector2[] _vertices = new Vector2[256];
    private int[] _starts = new int[17];
    private Corner[] _corners = new Corner[64];

    public int LoopCount { get; private set; }

    public int VertexCount { get; private set; }

    public ReadOnlySpan<Vector2> Vertices => _vertices.AsSpan(0, VertexCount);

    public ReadOnlySpan<int> Starts => _starts.AsSpan(0, LoopCount + 1);

    public ScissorOutlineShapeChange Build(ReadOnlySpan<byte> alpha, int width, int height, int stride, float originX, float originY, in ScissorOutlineParameters parameters)
    {
        var plan = ScissorOutlineSettings.CreatePlan(parameters, width, height);
        var next = PrepareCells(plan);
        CellMask.Build(alpha, width, height, stride, plan.Cell, plan.GridWidth, next);
        return Update(plan, width, height, next, originX, originY, parameters);
    }

    public ScissorOutlineShapeChange BuildPooled(ReadOnlySpan<byte> pooled, int pooledWidth, int pooledHeight, int pooledStride, int level, int width, int height, float originX, float originY, in ScissorOutlineParameters parameters)
    {
        var plan = ScissorOutlineSettings.CreatePlan(parameters, width, height);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(1 << level, plan.Cell, nameof(level));
        var next = PrepareCells(plan);
        CellMask.Build(pooled, pooledWidth, pooledHeight, pooledStride, plan.Cell >> level, plan.GridWidth, next, 0);
        return Update(plan, width, height, next, originX, originY, parameters);
    }

    private Span<ulong> PrepareCells(in ScissorOutlinePlan plan)
    {
        var size = BitGrid.WordsPerRow(plan.GridWidth) * plan.GridHeight;
        if (_nextCells.Length < size)
            _nextCells = new ulong[size];

        return _nextCells.AsSpan(0, size);
    }

    private ScissorOutlineShapeChange Update(in ScissorOutlinePlan plan, int width, int height, Span<ulong> next, float originX, float originY, in ScissorOutlineParameters parameters)
    {
        var cellKey = new CellKey(width, height, plan.Cell);
        var size = next.Length;
        var cellsChanged = !_hasCells || cellKey != _cellKey || !next.SequenceEqual(_cells.AsSpan(0, size));
        if (cellsChanged)
        {
            (_cells, _nextCells) = (_nextCells, _cells);
            _cellKey = cellKey;
            _hasCells = true;
            _hasTraced = false;
            _hasSubject = _cells.AsSpan(0, size).IndexOfAnyExcept(0UL) >= 0;
        }

        var randomized = plan.Tremor > 0.0 || plan.EpsilonSpread > 0.0 || plan.Distance > 0.0;
        var seed = randomized ? parameters.Seed : 0;
        var frame = randomized ? parameters.Frame : 0;
        var uneven = plan.ThicknessSpread > 0.0;
        var traceKey = uneven
            ? new TraceKey(plan.RadiusSquaredCells, plan.Radius, plan.ThicknessSpread, plan.SpreadWavelength, plan.SpreadChange, parameters.Seed, plan.SpreadChange > 0.0 ? parameters.Frame : 0)
            : new TraceKey(plan.RadiusSquaredCells, 0.0, 0.0, 0.0, 0.0, 0, 0);
        var shapeKey = new ShapeKey(cellKey, traceKey, plan.Epsilon, plan.EpsilonSpread, plan.Tremor, plan.Distance, seed, frame, parameters.FillHoles, originX, originY);
        if (!cellsChanged && _hasShape && shapeKey == _shapeKey)
            return ScissorOutlineShapeChange.Unchanged;

        _shapeKey = shapeKey;
        _hasShape = true;
        if (!_hasSubject)
        {
            LoopCount = 0;
            VertexCount = 0;
            return ScissorOutlineShapeChange.Rebuilt;
        }

        if (!_hasTraced || _traceKey != traceKey)
        {
            if (_inside.Length < size)
                _inside = new ulong[size];
            if (uneven)
                _thickness.Dilate(_cells.AsSpan(0, size), plan.GridWidth, plan.GridHeight, plan, ScissorOutlineJitter.Variation(traceKey.Seed, 0), ScissorOutlineJitter.Variation(traceKey.Seed, traceKey.Frame), _inside.AsSpan(0, size));
            else
                _dilation.Dilate(_cells.AsSpan(0, size), plan.GridWidth, plan.GridHeight, plan.RadiusSquaredCells, _inside.AsSpan(0, size));
            _tracer.Trace(_inside.AsSpan(0, size), plan.GridWidth, plan.GridHeight, _traced);
            _traceKey = traceKey;
            _hasTraced = true;
        }

        var variation = ScissorOutlineJitter.Variation(seed, frame);
        _simplified.Clear();
        var epsilonCells = plan.Epsilon / plan.Cell;
        for (var loop = 0; loop < _traced.LoopCount; loop++)
        {
            if (parameters.FillHoles && _traced.GetDoubleArea(loop) < 0)
                continue;
            _simplifier.Simplify(_traced, loop, epsilonCells, _simplified, plan.EpsilonSpread, variation);
        }

        Emit(plan, originX, originY, variation);
        return ScissorOutlineShapeChange.Rebuilt;
    }

    private void Emit(in ScissorOutlinePlan plan, float originX, float originY, uint variation)
    {
        var capacity = _simplified.PointCount * 2;
        if (_vertices.Length < capacity)
            _vertices = new Vector2[Math.Max(capacity, _vertices.Length * 2)];
        if (_starts.Length < _simplified.LoopCount + 1)
            _starts = new int[Math.Max(_simplified.LoopCount + 1, _starts.Length * 2)];

        var starts = _simplified.Starts;
        var written = 0;
        for (var loop = 0; loop < _simplified.LoopCount; loop++)
        {
            _starts[loop] = written;
            written = EmitLoop(plan, starts[loop], starts[loop + 1] - starts[loop], originX, originY, variation, written);
        }

        _starts[_simplified.LoopCount] = written;
        LoopCount = _simplified.LoopCount;
        VertexCount = written;
    }

    private int EmitLoop(in ScissorOutlinePlan plan, int start, int count, float originX, float originY, uint variation, int written)
    {
        if (_corners.Length < count)
            _corners = new Corner[Math.Max(count, _corners.Length * 2)];

        var points = _simplified.Points;
        var shifts = _simplified.Shifts;
        var corners = _corners.AsSpan(0, count);
        double cell = plan.Cell;
        var padding = ScissorOutlineSettings.PaddingCells;
        for (var k = 0; k < count; k++)
        {
            var index = start + k;
            var next = start + (k + 1) % count;
            var x = (points[index * 2] - padding) * cell;
            var y = (points[index * 2 + 1] - padding) * cell;
            var nextX = (points[next * 2] - padding) * cell;
            var nextY = (points[next * 2 + 1] - padding) * cell;
            var length = Math.Sqrt((nextX - x) * (nextX - x) + (nextY - y) * (nextY - y));
            var shift = shifts[index] * cell;
            if (plan.Tremor > 0.0)
                shift += plan.Tremor * ScissorOutlineJitter.GetShiftUnit(points[index * 2], points[index * 2 + 1], points[next * 2], points[next * 2 + 1], variation);
            if (plan.Distance > 0.0)
                shift += plan.Distance * 0.5 * (1.0 + ScissorOutlineJitter.GetWave((x + nextX) / 2.0, (y + nextY) / 2.0, plan.Wavelength, variation));
            corners[k] = new Corner(x, y, length, length > 0.0 ? (nextY - y) / length : 0.0, length > 0.0 ? (x - nextX) / length : 0.0, shift);
        }

        for (var k = 0; k < count; k++)
        {
            ref var corner = ref corners[k];
            var previous = corners[(k + count - 1) % count];
            var determinant = previous.NormalX * corner.NormalY - previous.NormalY * corner.NormalX;
            corner.Bevel = Math.Abs(determinant) <= 1e-9;
            if (corner.Bevel)
                continue;

            corner.OffsetX = (previous.Shift * corner.NormalY - previous.NormalY * corner.Shift) / determinant;
            corner.OffsetY = (previous.NormalX * corner.Shift - previous.Shift * corner.NormalX) / determinant;
            var limit = MiterLimit * Math.Max(previous.Shift, corner.Shift);
            corner.Bevel = corner.OffsetX * corner.OffsetX + corner.OffsetY * corner.OffsetY > limit * limit;
        }

        bool changed;
        do
        {
            changed = false;
            for (var k = 0; k < count; k++)
            {
                ref var corner = ref corners[k];
                ref var next = ref corners[(k + 1) % count];
                if (corner.Bevel && next.Bevel)
                    continue;

                var startOffsetX = corner.Bevel ? corner.NormalX * corner.Shift : corner.OffsetX;
                var startOffsetY = corner.Bevel ? corner.NormalY * corner.Shift : corner.OffsetY;
                var endOffsetX = next.Bevel ? corner.NormalX * corner.Shift : next.OffsetX;
                var endOffsetY = next.Bevel ? corner.NormalY * corner.Shift : next.OffsetY;
                var along = corner.Length > 0.0 ? (-corner.NormalY * (endOffsetX - startOffsetX) + corner.NormalX * (endOffsetY - startOffsetY)) : 0.0;
                if (corner.Length + along >= MinimumEdgeRatio * corner.Length)
                    continue;

                corner.Bevel = true;
                next.Bevel = true;
                changed = true;
            }
        }
        while (changed);

        for (var k = 0; k < count; k++)
        {
            var corner = corners[k];
            var previous = corners[(k + count - 1) % count];
            if (!corner.Bevel)
            {
                _vertices[written++] = new Vector2((float)(originX + corner.X + corner.OffsetX), (float)(originY + corner.Y + corner.OffsetY));
                continue;
            }

            if (previous.Shift == 0.0 && corner.Shift == 0.0)
            {
                _vertices[written++] = new Vector2((float)(originX + corner.X), (float)(originY + corner.Y));
                continue;
            }

            _vertices[written++] = new Vector2((float)(originX + corner.X + previous.NormalX * previous.Shift), (float)(originY + corner.Y + previous.NormalY * previous.Shift));
            _vertices[written++] = new Vector2((float)(originX + corner.X + corner.NormalX * corner.Shift), (float)(originY + corner.Y + corner.NormalY * corner.Shift));
        }

        return written;
    }

    private struct Corner(double x, double y, double length, double normalX, double normalY, double shift)
    {
        public double X = x;
        public double Y = y;
        public double Length = length;
        public double NormalX = normalX;
        public double NormalY = normalY;
        public double Shift = shift;
        public double OffsetX;
        public double OffsetY;
        public bool Bevel;
    }

    private readonly record struct CellKey(int Width, int Height, int Cell);

    private readonly record struct TraceKey(int RadiusSquaredCells, double Radius, double Spread, double Wavelength, double Change, int Seed, int Frame);

    private readonly record struct ShapeKey(CellKey Cells, TraceKey Trace, double Epsilon, double EpsilonSpread, double Tremor, double Distance, int Seed, int Frame, bool FillHoles, float OriginX, float OriginY);
}
