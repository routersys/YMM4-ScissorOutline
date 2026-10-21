using System.Numerics;

namespace ScissorOutline;

internal enum ScissorOutlineShapeChange
{
    Unchanged,
    Rebuilt,
}

internal sealed class ScissorOutlineShape
{
    private readonly DiscDilation _dilation = new();
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
    private int _tracedRadius;
    private ShapeKey _shapeKey;
    private bool _hasShape;
    private Vector2[] _vertices = new Vector2[256];
    private int[] _starts = new int[17];

    public int LoopCount { get; private set; }

    public int VertexCount { get; private set; }

    public ReadOnlySpan<Vector2> Vertices => _vertices.AsSpan(0, VertexCount);

    public ReadOnlySpan<int> Starts => _starts.AsSpan(0, LoopCount + 1);

    public ScissorOutlineShapeChange Build(ReadOnlySpan<byte> alpha, int width, int height, int stride, float originX, float originY, in ScissorOutlineParameters parameters)
    {
        var plan = ScissorOutlineSettings.CreatePlan(parameters, width, height);
        var cellKey = new CellKey(width, height, plan.Cell);
        var size = BitGrid.WordsPerRow(plan.GridWidth) * plan.GridHeight;
        if (_nextCells.Length < size)
            _nextCells = new ulong[size];

        var next = _nextCells.AsSpan(0, size);
        CellMask.Build(alpha, width, height, stride, plan.Cell, plan.GridWidth, next);
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
        var shapeKey = new ShapeKey(cellKey, plan.RadiusSquaredCells, plan.Epsilon, plan.EpsilonSpread, plan.Tremor, plan.Distance, seed, frame, parameters.FillHoles, originX, originY);
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

        if (!_hasTraced || _tracedRadius != plan.RadiusSquaredCells)
        {
            if (_inside.Length < size)
                _inside = new ulong[size];
            _dilation.Dilate(_cells.AsSpan(0, size), plan.GridWidth, plan.GridHeight, plan.RadiusSquaredCells, _inside.AsSpan(0, size));
            _tracer.Trace(_inside.AsSpan(0, size), plan.GridWidth, plan.GridHeight, _traced);
            _tracedRadius = plan.RadiusSquaredCells;
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
        if (_vertices.Length < _simplified.PointCount)
            _vertices = new Vector2[Math.Max(_simplified.PointCount, _vertices.Length * 2)];
        if (_starts.Length < _simplified.LoopCount + 1)
            _starts = new int[Math.Max(_simplified.LoopCount + 1, _starts.Length * 2)];

        var points = _simplified.Points;
        var starts = _simplified.Starts;
        for (var loop = 0; loop < _simplified.LoopCount; loop++)
        {
            var start = starts[loop];
            var count = starts[loop + 1] - start;
            for (var k = 0; k < count; k++)
            {
                var index = start + k;
                var x = (points[index * 2] - ScissorOutlineSettings.PaddingCells) * plan.Cell;
                var y = (points[index * 2 + 1] - ScissorOutlineSettings.PaddingCells) * plan.Cell;
                var vertex = new Vector2(originX + x, originY + y);
                if (plan.Distance > 0.0)
                {
                    var previous = start + (k + count - 1) % count;
                    var next = start + (k + 1) % count;
                    vertex += GetOutwardNormal(points, previous, index, next) * (float)(plan.Distance * ScissorOutlineJitter.GetWave(x, y, plan.Wavelength, variation));
                }

                if (plan.Tremor > 0.0)
                    vertex += ScissorOutlineJitter.GetOffset(x, y, variation, plan.Tremor);
                _vertices[index] = vertex;
            }
        }

        _simplified.Starts.CopyTo(_starts);
        LoopCount = _simplified.LoopCount;
        VertexCount = _simplified.PointCount;
    }

    private static Vector2 GetOutwardNormal(ReadOnlySpan<int> points, int previous, int index, int next)
    {
        double incomingX = points[index * 2] - points[previous * 2];
        double incomingY = points[index * 2 + 1] - points[previous * 2 + 1];
        double outgoingX = points[next * 2] - points[index * 2];
        double outgoingY = points[next * 2 + 1] - points[index * 2 + 1];
        var incomingLength = Math.Sqrt(incomingX * incomingX + incomingY * incomingY);
        var outgoingLength = Math.Sqrt(outgoingX * outgoingX + outgoingY * outgoingY);
        var normalX = (incomingLength > 0.0 ? incomingY / incomingLength : 0.0) + (outgoingLength > 0.0 ? outgoingY / outgoingLength : 0.0);
        var normalY = (incomingLength > 0.0 ? -incomingX / incomingLength : 0.0) + (outgoingLength > 0.0 ? -outgoingX / outgoingLength : 0.0);
        var length = Math.Sqrt(normalX * normalX + normalY * normalY);
        if (length < 1e-9)
        {
            var fallback = outgoingLength > 0.0 ? outgoingLength : incomingLength;
            return fallback > 0.0 ? new Vector2((float)((outgoingLength > 0.0 ? outgoingY : incomingY) / fallback), (float)((outgoingLength > 0.0 ? -outgoingX : -incomingX) / fallback)) : Vector2.Zero;
        }

        return new Vector2((float)(normalX / length), (float)(normalY / length));
    }

    private readonly record struct CellKey(int Width, int Height, int Cell);

    private readonly record struct ShapeKey(CellKey Cells, int RadiusSquaredCells, double Epsilon, double EpsilonSpread, double Tremor, double Distance, int Seed, int Frame, bool FillHoles, float OriginX, float OriginY);
}
