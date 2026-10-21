namespace ScissorOutline.Tests;

internal static class PolygonGeometry
{
    public static double DistanceToBoundary(ScissorOutlineShape shape, double x, double y)
    {
        var vertices = shape.Vertices;
        var starts = shape.Starts;
        var best = double.PositiveInfinity;
        for (var loop = 0; loop < shape.LoopCount; loop++)
        {
            var start = starts[loop];
            var count = starts[loop + 1] - start;
            for (var i = 0; i < count; i++)
            {
                var a = vertices[start + i];
                var b = vertices[start + (i + 1) % count];
                best = Math.Min(best, DistanceToSegment(x, y, a.X, a.Y, b.X, b.Y));
            }
        }

        return best;
    }

    public static int Winding(ScissorOutlineShape shape, double x, double y)
    {
        var vertices = shape.Vertices;
        var starts = shape.Starts;
        var winding = 0;
        for (var loop = 0; loop < shape.LoopCount; loop++)
        {
            var start = starts[loop];
            var count = starts[loop + 1] - start;
            for (var i = 0; i < count; i++)
            {
                var a = vertices[start + i];
                var b = vertices[start + (i + 1) % count];
                if (a.Y <= y)
                {
                    if (b.Y > y && Side(a.X, a.Y, b.X, b.Y, x, y) > 0)
                        winding++;
                }
                else if (b.Y <= y && Side(a.X, a.Y, b.X, b.Y, x, y) < 0)
                {
                    winding--;
                }
            }
        }

        return winding;
    }

    public static double DistanceToNearest(IReadOnlyList<(double X, double Y)> points, double x, double y)
    {
        var best = double.PositiveInfinity;
        foreach (var (px, py) in points)
            best = Math.Min(best, Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y)));
        return best;
    }

    static double Side(double ax, double ay, double bx, double by, double x, double y)
        => (bx - ax) * (y - ay) - (x - ax) * (by - ay);

    static double DistanceToSegment(double x, double y, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0.0 ? 0.0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / lengthSquared, 0.0, 1.0);
        var px = ax + t * dx - x;
        var py = ay + t * dy - y;
        return Math.Sqrt(px * px + py * py);
    }
}
