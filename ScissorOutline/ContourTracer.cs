namespace ScissorOutline;

internal sealed class ContourTracer
{
    private static readonly int[] StepX = [1, 0, -1, 0];
    private static readonly int[] StepY = [0, 1, 0, -1];

    private byte[] _corners = [];

    public void Trace(ReadOnlySpan<float> squaredDistance, int width, int height, float limit, ContourLoops loops)
    {
        loops.Clear();
        var stride = width + 1;
        var size = stride * (height + 1);
        if (_corners.Length < size)
            _corners = new byte[size];
        var corners = _corners.AsSpan(0, size);
        corners.Clear();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (squaredDistance[y * width + x] > limit)
                    continue;

                if (y == 0 || squaredDistance[(y - 1) * width + x] > limit)
                    corners[y * stride + x] |= 1 << 0;
                if (x == width - 1 || squaredDistance[y * width + x + 1] > limit)
                    corners[y * stride + x + 1] |= 1 << 1;
                if (y == height - 1 || squaredDistance[(y + 1) * width + x] > limit)
                    corners[(y + 1) * stride + x + 1] |= 1 << 2;
                if (x == 0 || squaredDistance[y * width + x - 1] > limit)
                    corners[(y + 1) * stride + x] |= 1 << 3;
            }
        }

        for (var start = 0; start < size; start++)
        {
            while (corners[start] != 0)
                TraceLoop(corners, stride, start, loops);
        }
    }

    private static void TraceLoop(Span<byte> corners, int stride, int start, ContourLoops loops)
    {
        var firstDirection = System.Numerics.BitOperations.TrailingZeroCount((int)corners[start]);
        corners[start] &= (byte)~(1 << firstDirection);
        var x = start % stride;
        var y = start / stride;
        var direction = firstDirection;
        while (true)
        {
            x += StepX[direction];
            y += StepY[direction];
            var index = y * stride + x;
            var next = Pick(corners[index], direction);
            if (next < 0)
            {
                if (direction != firstDirection)
                    loops.AddPoint(x, y);
                break;
            }

            if (next != direction)
                loops.AddPoint(x, y);
            corners[index] &= (byte)~(1 << next);
            direction = next;
        }

        loops.CloseLoop();
    }

    private static int Pick(byte mask, int direction)
    {
        var right = (direction + 1) & 3;
        if ((mask & (1 << right)) != 0)
            return right;
        if ((mask & (1 << direction)) != 0)
            return direction;
        var left = (direction + 3) & 3;
        if ((mask & (1 << left)) != 0)
            return left;
        return -1;
    }
}
