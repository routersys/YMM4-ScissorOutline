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

        var shapeKey = new ShapeKey(cellKey, plan.RadiusSquaredCells, plan.Epsilon, plan.Tremor, parameters.Seed, parameters.FillHoles, originX, originY);
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

        _simplified.Clear();
        var epsilonCells = plan.Epsilon / plan.Cell;
        for (var loop = 0; loop < _traced.LoopCount; loop++)
        {
            if (parameters.FillHoles && _traced.GetDoubleArea(loop) < 0)
                continue;
            _simplifier.Simplify(_traced, loop, epsilonCells, _simplified);
        }

        Emit(plan, originX, originY, parameters.Seed);
        return ScissorOutlineShapeChange.Rebuilt;
    }

    private void Emit(in ScissorOutlinePlan plan, float originX, float originY, int seed)
    {
        if (_vertices.Length < _simplified.PointCount)
            _vertices = new Vector2[Math.Max(_simplified.PointCount, _vertices.Length * 2)];
        if (_starts.Length < _simplified.LoopCount + 1)
            _starts = new int[Math.Max(_simplified.LoopCount + 1, _starts.Length * 2)];

        var points = _simplified.Points;
        for (var i = 0; i < _simplified.PointCount; i++)
        {
            var x = (points[i * 2] - ScissorOutlineSettings.PaddingCells) * plan.Cell;
            var y = (points[i * 2 + 1] - ScissorOutlineSettings.PaddingCells) * plan.Cell;
            var vertex = new Vector2(originX + x, originY + y);
            if (plan.Tremor > 0.0)
                vertex += ScissorOutlineJitter.GetOffset(x, y, seed, plan.Tremor);
            _vertices[i] = vertex;
        }

        _simplified.Starts.CopyTo(_starts);
        LoopCount = _simplified.LoopCount;
        VertexCount = _simplified.PointCount;
    }

    private readonly record struct CellKey(int Width, int Height, int Cell);

    private readonly record struct ShapeKey(CellKey Cells, int RadiusSquaredCells, double Epsilon, double Tremor, int Seed, bool FillHoles, float OriginX, float OriginY);
}
