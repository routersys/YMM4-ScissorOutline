namespace ScissorOutline;

internal sealed class PolygonSimplifier
{
    private bool[] _keep = [];
    private int[] _order = [];
    private int[] _stack = new int[64];

    public bool Simplify(ContourLoops source, int loop, double epsilon, ContourLoops destination, double spread = 0.0, uint variation = 0u)
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

        Reduce(points, count, first, second > first ? second : second + count, epsilon, spread, variation);
        Reduce(points, count, second, first > second ? first : first + count, epsilon, spread, variation);

        if (_order.Length < count)
            _order = new int[count];
        var kept = 0;
        for (var offset = 0; offset < count; offset++)
        {
            if (_keep[(first + offset) % count])
                _order[kept++] = offset;
        }

        if (kept < 3)
            return false;

        for (var i = 0; i < kept; i++)
        {
            var from = first + _order[i];
            var to = first + (i + 1 < kept ? _order[i + 1] : count);
            destination.AddPoint(points[(from % count) * 2], points[(from % count) * 2 + 1], GetOutwardDeviation(points, count, from, to));
        }

        destination.CloseLoop();
        return true;
    }

    internal static double GetOutwardDeviation(ReadOnlySpan<int> points, int count, int begin, int end)
    {
        double px = points[(begin % count) * 2];
        double py = points[(begin % count) * 2 + 1];
        var dx = points[(end % count) * 2] - px;
        var dy = points[(end % count) * 2 + 1] - py;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0.0)
            return 0.0;

        var best = 0.0;
        for (var i = begin + 1; i < end; i++)
        {
            var rx = points[(i % count) * 2] - px;
            var ry = points[(i % count) * 2 + 1] - py;
            best = Math.Max(best, (dy * rx - dx * ry) / length);
        }

        return best;
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

    private void Reduce(ReadOnlySpan<int> points, int count, int from, int to, double epsilon, double spread, uint variation)
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
            var tolerance = spread > 0.0
                ? epsilon * ScissorOutlineJitter.GetToleranceFactor(points[(begin % count) * 2], points[(begin % count) * 2 + 1], points[(end % count) * 2], points[(end % count) * 2 + 1], variation, spread)
                : epsilon;
            if (squaredDistance <= tolerance * tolerance)
                continue;

            if (spread > 0.0)
                farthest = ChooseSplit(points, count, begin, end, squaredDistance, spread, variation);

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

    internal static int ChooseSplit(ReadOnlySpan<int> points, int count, int begin, int end, double squaredDistance, double spread, uint variation)
    {
        var keep = 1.0 - ScissorOutlineSettings.IrregularEpsilonShrink * spread;
        var limit = squaredDistance * keep * keep;
        var candidates = 0;
        for (var i = begin + 1; i < end; i++)
        {
            if (GetSquaredDeviation(points, count, begin, end, i) >= limit)
                candidates++;
        }

        var unit = ScissorOutlineJitter.GetSplitUnit(points[(begin % count) * 2], points[(begin % count) * 2 + 1], points[(end % count) * 2], points[(end % count) * 2 + 1], variation);
        var pick = Math.Min((int)(unit * candidates), candidates - 1);
        for (var i = begin + 1; i < end; i++)
        {
            if (GetSquaredDeviation(points, count, begin, end, i) < limit)
                continue;
            if (pick == 0)
                return i;
            pick--;
        }

        return begin + 1;
    }

    private static double GetSquaredDeviation(ReadOnlySpan<int> points, int count, int begin, int end, int index)
    {
        double px = points[(begin % count) * 2];
        double py = points[(begin % count) * 2 + 1];
        var dx = points[(end % count) * 2] - px;
        var dy = points[(end % count) * 2 + 1] - py;
        var rx = points[(index % count) * 2] - px;
        var ry = points[(index % count) * 2 + 1] - py;
        var squaredLength = dx * dx + dy * dy;
        if (squaredLength == 0.0)
            return rx * rx + ry * ry;

        var cross = dx * ry - dy * rx;
        return cross * cross / squaredLength;
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
