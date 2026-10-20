namespace ScissorOutline;

internal sealed class PolygonSimplifier
{
    private bool[] _keep = [];
    private int[] _stack = new int[64];

    public bool Simplify(ContourLoops source, int loop, double epsilon, ContourLoops destination)
    {
        var start = source.Starts[loop];
        var count = source.Starts[loop + 1] - start;
        if (count < 3)
            return false;

        var points = source.Points.Slice(start * 2, count * 2);
        var first = FindFirst(points, count);
        var second = FindFarthest(points, count, first);
        if (_keep.Length < count)
            _keep = new bool[count];
        Array.Clear(_keep, 0, count);
        _keep[first] = true;
        _keep[second] = true;

        var squaredEpsilon = epsilon * epsilon;
        Reduce(points, count, first, second > first ? second : second + count, squaredEpsilon);
        Reduce(points, count, second, first > second ? first : first + count, squaredEpsilon);

        var kept = 0;
        for (var offset = 0; offset < count; offset++)
        {
            var index = (first + offset) % count;
            if (!_keep[index])
                continue;
            destination.AddPoint(points[index * 2], points[index * 2 + 1]);
            kept++;
        }

        if (kept < 3)
        {
            destination.DiscardOpenLoop();
            return false;
        }

        destination.CloseLoop();
        return true;
    }

    private static int FindFirst(ReadOnlySpan<int> points, int count)
    {
        var best = 0;
        for (var i = 1; i < count; i++)
        {
            var y = points[i * 2 + 1];
            var bestY = points[best * 2 + 1];
            if (y < bestY || (y == bestY && points[i * 2] < points[best * 2]))
                best = i;
        }

        return best;
    }

    private static int FindFarthest(ReadOnlySpan<int> points, int count, int origin)
    {
        var best = origin;
        long bestDistance = -1;
        for (var i = 0; i < count; i++)
        {
            long dx = points[i * 2] - points[origin * 2];
            long dy = points[i * 2 + 1] - points[origin * 2 + 1];
            var distance = dx * dx + dy * dy;
            if (distance > bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void Reduce(ReadOnlySpan<int> points, int count, int from, int to, double squaredEpsilon)
    {
        var depth = 0;
        Push(ref depth, from, to);
        while (depth > 0)
        {
            var end = _stack[--depth];
            var begin = _stack[--depth];
            if (end - begin < 2)
                continue;

            var farthest = FindFarthestFromLine(points, count, begin, end, out var squaredDistance);
            if (squaredDistance <= squaredEpsilon)
                continue;

            _keep[farthest % count] = true;
            Push(ref depth, begin, farthest);
            Push(ref depth, farthest, end);
        }
    }

    private void Push(ref int depth, int begin, int end)
    {
        if (depth + 2 > _stack.Length)
            Array.Resize(ref _stack, _stack.Length * 2);
        _stack[depth++] = begin;
        _stack[depth++] = end;
    }

    private static int FindFarthestFromLine(ReadOnlySpan<int> points, int count, int begin, int end, out double squaredDistance)
    {
        double px = points[(begin % count) * 2];
        double py = points[(begin % count) * 2 + 1];
        var dx = points[(end % count) * 2] - px;
        var dy = points[(end % count) * 2 + 1] - py;
        var squaredLength = dx * dx + dy * dy;
        var best = begin + 1;
        squaredDistance = -1.0;
        for (var i = begin + 1; i < end; i++)
        {
            var rx = points[(i % count) * 2] - px;
            var ry = points[(i % count) * 2 + 1] - py;
            double candidate;
            if (squaredLength == 0.0)
            {
                candidate = rx * rx + ry * ry;
            }
            else
            {
                var cross = dx * ry - dy * rx;
                candidate = cross * cross / squaredLength;
            }

            if (candidate > squaredDistance)
            {
                best = i;
                squaredDistance = candidate;
            }
        }

        return best;
    }
}
