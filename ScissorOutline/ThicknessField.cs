namespace ScissorOutline;

internal sealed class ThicknessField
{
    private double[] _nodes = [];
    private int[] _columns = [];
    private double[] _columnFades = [];
    private int[] _rows = [];
    private double[] _rowFades = [];
    private int _nodeStride;

    public void Prepare(int gridWidth, int gridHeight, int cell, double wavelength, uint fixedVariation, uint frameVariation, double change)
    {
        Ensure(ref _columns, ref _columnFades, gridWidth);
        Ensure(ref _rows, ref _rowFades, gridHeight);
        var firstColumn = Axis(gridWidth, cell, wavelength, _columns, _columnFades);
        var firstRow = Axis(gridHeight, cell, wavelength, _rows, _rowFades);
        _nodeStride = _columns[gridWidth - 1] + 2;
        var nodeRows = _rows[gridHeight - 1] + 2;
        if (_nodes.Length < _nodeStride * nodeRows)
            _nodes = new double[_nodeStride * nodeRows];

        var angle = change * (Math.PI / 2.0);
        var fixedWeight = Math.Cos(angle);
        var frameWeight = Math.Sin(angle);
        for (var row = 0; row < nodeRows; row++)
        {
            for (var column = 0; column < _nodeStride; column++)
            {
                var fixedValue = ScissorOutlineJitter.GetThicknessLattice(firstColumn + column, firstRow + row, fixedVariation);
                var frameValue = ScissorOutlineJitter.GetThicknessLattice(firstColumn + column, firstRow + row, frameVariation);
                _nodes[row * _nodeStride + column] = fixedValue * fixedWeight + frameValue * frameWeight;
            }
        }
    }

    public void FillRow(int y, Span<double> row)
    {
        var upper = _nodes.AsSpan(_rows[y] * _nodeStride, _nodeStride);
        var lower = _nodes.AsSpan((_rows[y] + 1) * _nodeStride, _nodeStride);
        var fadeY = _rowFades[y];
        for (var x = 0; x < row.Length; x++)
        {
            var column = _columns[x];
            var fadeX = _columnFades[x];
            var top = upper[column] + (upper[column + 1] - upper[column]) * fadeX;
            var bottom = lower[column] + (lower[column + 1] - lower[column]) * fadeX;
            var value = top + (bottom - top) * fadeY;
            row[x] = Math.Clamp(0.5 + ScissorOutlineSettings.ThicknessContrast * value, 0.0, 1.0);
        }
    }

    private static void Ensure(ref int[] indexes, ref double[] fades, int length)
    {
        if (indexes.Length < length)
        {
            indexes = new int[length];
            fades = new double[length];
        }
    }

    private static int Axis(int length, int cell, double wavelength, int[] indexes, double[] fades)
    {
        var first = (int)Math.Floor(Position(0, cell) / wavelength);
        for (var i = 0; i < length; i++)
        {
            var unit = Position(i, cell) / wavelength;
            var index = (int)Math.Floor(unit);
            indexes[i] = index - first;
            fades[i] = ScissorOutlineJitter.Fade(unit - index);
        }

        return first;
    }

    private static double Position(int index, int cell) => (index - ScissorOutlineSettings.PaddingCells + 0.5) * cell;
}
