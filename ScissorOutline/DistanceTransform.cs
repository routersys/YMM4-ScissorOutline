namespace ScissorOutline;

internal sealed class DistanceTransform
{
    public const float Far = 1e9f;

    private int[] _vertices = [];
    private double[] _boundaries = [];
    private float[] _line = [];
    private float[] _result = [];

    public void Compute(ReadOnlySpan<byte> subject, int width, int height, Span<float> squaredDistance)
    {
        var longSide = Math.Max(width, height);
        if (_vertices.Length < longSide)
        {
            _vertices = new int[longSide];
            _boundaries = new double[longSide + 1];
            _line = new float[longSide];
            _result = new float[longSide];
        }

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
                _line[y] = subject[y * width + x] != 0 ? 0f : Far;

            Envelope(height);
            for (var y = 0; y < height; y++)
                squaredDistance[y * width + x] = _result[y];
        }

        for (var y = 0; y < height; y++)
        {
            var row = squaredDistance.Slice(y * width, width);
            row.CopyTo(_line);
            Envelope(width);
            _result.AsSpan(0, width).CopyTo(row);
        }
    }

    private void Envelope(int length)
    {
        var vertices = _vertices;
        var boundaries = _boundaries;
        var k = 0;
        vertices[0] = 0;
        boundaries[0] = double.NegativeInfinity;
        boundaries[1] = double.PositiveInfinity;
        for (var q = 1; q < length; q++)
        {
            double intersection;
            while (true)
            {
                var p = vertices[k];
                intersection = ((double)_line[q] + (double)q * q - ((double)_line[p] + (double)p * p)) / (2.0 * q - 2.0 * p);
                if (intersection > boundaries[k])
                    break;
                k--;
            }

            k++;
            vertices[k] = q;
            boundaries[k] = intersection;
            boundaries[k + 1] = double.PositiveInfinity;
        }

        k = 0;
        for (var q = 0; q < length; q++)
        {
            while (boundaries[k + 1] < q)
                k++;
            var difference = q - vertices[k];
            _result[q] = difference * difference + _line[vertices[k]];
        }
    }
}
