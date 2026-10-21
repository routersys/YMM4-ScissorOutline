using System.Runtime.CompilerServices;

namespace ScissorOutline.Tests;

internal static class PolygonGeometry
{
    const double SampleSpacing = 0.5;
    const double SideOffset = 0.02;

    static readonly ConditionalWeakTable<ScissorOutlineShape, VisibleBoundary> Cache = new();

    public static double DistanceToVisibleBoundary(ScissorOutlineShape shape, double x, double y)
    {
        var cached = Cache.GetValue(shape, _ => new VisibleBoundary());
        if (!shape.Vertices.SequenceEqual(cached.Vertices) || !shape.Starts.SequenceEqual(cached.Starts))
        {
            cached.Vertices = shape.Vertices.ToArray();
            cached.Starts = shape.Starts.ToArray();
            cached.Points = SampleVisibleBoundary(shape);
        }

        return DistanceToNearest(cached.Points, x, y);
    }

    static List<(double X, double Y)> SampleVisibleBoundary(ScissorOutlineShape shape)
    {
        var points = new List<(double X, double Y)>();
        var vertices = shape.Vertices;
        var starts = shape.Starts;
        for (var loop = 0; loop < shape.LoopCount; loop++)
        {
            var start = starts[loop];
            var count = starts[loop + 1] - start;
            for (var i = 0; i < count; i++)
            {
                var a = vertices[start + i];
                var b = vertices[start + (i + 1) % count];
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length == 0.0)
                    continue;

                var steps = (int)Math.Ceiling(length / SampleSpacing);
                for (var step = 0; step <= steps; step++)
                {
                    var x = a.X + dx * step / steps;
                    var y = a.Y + dy * step / steps;
                    var nx = dy / length * SideOffset;
                    var ny = -dx / length * SideOffset;
                    if ((Winding(shape, x + nx, y + ny) == 0) != (Winding(shape, x - nx, y - ny) == 0))
                        points.Add((x, y));
                }
            }
        }

        return points;
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

    sealed class VisibleBoundary
    {
        public System.Numerics.Vector2[] Vertices = [];
        public int[] Starts = [];
        public List<(double X, double Y)> Points = [];
    }
}
