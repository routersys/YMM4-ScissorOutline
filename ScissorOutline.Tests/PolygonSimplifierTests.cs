namespace ScissorOutline.Tests;

public sealed class PolygonSimplifierTests
{
    public static readonly TheoryData<double> Tolerances = [1.0, 2.5, 6.0];

    static ContourLoops LoopOf(params (int X, int Y)[] points)
    {
        var loops = new ContourLoops();
        foreach (var (x, y) in points)
            loops.AddPoint(x, y);
        loops.CloseLoop();
        return loops;
    }

    static (int X, int Y)[] Points(ContourLoops loops, int index)
    {
        var start = loops.Starts[index];
        var end = loops.Starts[index + 1];
        var points = loops.Points.ToArray();
        return Enumerable.Range(start, end - start).Select(i => (points[i * 2], points[i * 2 + 1])).ToArray();
    }

    static ContourLoops TraceDisc(double radius, double scaleX = 1.0)
    {
        const int size = 80;
        var mask = new byte[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5 - size / 2.0) / scaleX;
                var dy = y + 0.5 - size / 2.0;
                mask[y * size + x] = Math.Sqrt(dx * dx + dy * dy) <= radius ? (byte)1 : (byte)0;
            }
        }

        var loops = new ContourLoops();
        new ContourTracer().Trace(Masks.ToBits(mask, size, size), size, size, loops);
        return loops;
    }

    static (int X, int Y)[] Staircase()
    {
        var points = new List<(int X, int Y)> { (0, 0), (10, 0), (10, 10) };
        for (var i = 9; i >= 1; i--)
        {
            points.Add((i, i + 1));
            points.Add((i, i));
        }

        points.Add((0, 1));
        return points.ToArray();
    }

    static double DistanceToLine((int X, int Y) a, (int X, int Y) b, (int X, int Y) p)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Abs(dx * (p.Y - a.Y) - dy * (p.X - a.X)) / Math.Sqrt(dx * dx + dy * dy);
    }

    [Fact]
    public void AStaircaseCollapsesIntoItsDiagonalWithinOneCell()
    {
        var source = LoopOf(Staircase());
        var result = new ContourLoops();

        var kept = new PolygonSimplifier().Simplify(source, 0, 1.0, result);

        Assert.True(kept);
        Assert.Equal([(0, 0), (10, 0), (10, 10)], Points(result, 0));
    }

    [Fact]
    public void AToleranceOfZeroKeepsEveryVertex()
    {
        var points = Staircase();
        var result = new ContourLoops();

        new PolygonSimplifier().Simplify(LoopOf(points), 0, 0.0, result);

        Assert.Equal(points.Length, result.PointCount);
    }

    [Theory]
    [MemberData(nameof(Tolerances))]
    public void EveryDroppedVertexLiesWithinTheToleranceOfTheEdgeThatReplacesIt(double epsilon)
    {
        var source = TraceDisc(24.0, 1.5);
        var original = Points(source, 0);
        var result = new ContourLoops();

        new PolygonSimplifier().Simplify(source, 0, epsilon, result);

        var kept = Points(result, 0);
        var indices = kept.Select(p => Array.IndexOf(original, p)).ToArray();
        Assert.DoesNotContain(-1, indices);
        for (var k = 0; k < indices.Length; k++)
        {
            var from = indices[k];
            var to = indices[(k + 1) % indices.Length];
            var span = (to - from + original.Length) % original.Length;
            for (var step = 1; step < span; step++)
                Assert.True(DistanceToLine(original[from], original[to], original[(from + step) % original.Length]) <= epsilon + 1e-9);
        }
    }

    [Fact]
    public void ALargerToleranceKeepsASubsetOfTheVerticesOfASmallerOne()
    {
        var source = TraceDisc(24.0, 1.5);
        var fine = new ContourLoops();
        var coarse = new ContourLoops();
        var simplifier = new PolygonSimplifier();

        simplifier.Simplify(source, 0, 1.0, fine);
        simplifier.Simplify(source, 0, 4.0, coarse);

        Assert.True(coarse.PointCount < fine.PointCount);
        Assert.Subset(Points(fine, 0).ToHashSet(), Points(coarse, 0).ToHashSet());
        Assert.Subset(Points(source, 0).ToHashSet(), Points(fine, 0).ToHashSet());
    }

    [Fact]
    public void TheLoopStartsAtTheTopmostThenLeftmostVertex()
    {
        var source = TraceDisc(25.0);
        var result = new ContourLoops();

        new PolygonSimplifier().Simplify(source, 0, 2.0, result);

        var expected = Points(source, 0).OrderBy(p => p.Y).ThenBy(p => p.X).First();
        Assert.Equal(expected, Points(result, 0)[0]);
    }

    [Fact]
    public void SimplifyingTheSameLoopTwiceGivesTheSameVertices()
    {
        var source = TraceDisc(28.0, 0.7);
        var simplifier = new PolygonSimplifier();
        var first = new ContourLoops();
        var second = new ContourLoops();

        simplifier.Simplify(source, 0, 2.0, first);
        simplifier.Simplify(source, 0, 2.0, second);

        Assert.Equal(Points(first, 0), Points(second, 0));
    }

    [Fact]
    public void ALoopOfFewerThanThreeVerticesIsRejected()
    {
        var result = new ContourLoops();

        var kept = new PolygonSimplifier().Simplify(LoopOf((0, 0), (5, 0)), 0, 1.0, result);

        Assert.False(kept);
        Assert.Equal(0, result.LoopCount);
        Assert.Equal(0, result.PointCount);
    }

    [Fact]
    public void ALoopThatCollapsesIntoALineIsRejectedWithoutLeavingVertices()
    {
        var result = new ContourLoops();
        result.AddPoint(7, 7);
        result.AddPoint(8, 7);
        result.AddPoint(8, 8);
        result.CloseLoop();

        var kept = new PolygonSimplifier().Simplify(LoopOf((0, 0), (10, 0), (20, 1), (10, 1)), 0, 5.0, result);

        Assert.False(kept);
        Assert.Equal(1, result.LoopCount);
        Assert.Equal(3, result.PointCount);
    }

    [Fact]
    public void SeveralLoopsAreAppendedInOrder()
    {
        var source = new ContourLoops();
        foreach (var (x, y) in new[] { (0, 0), (4, 0), (4, 4), (0, 4) })
            source.AddPoint(x, y);
        source.CloseLoop();
        foreach (var (x, y) in new[] { (10, 10), (14, 10), (14, 14), (10, 14) })
            source.AddPoint(x, y);
        source.CloseLoop();
        var result = new ContourLoops();
        var simplifier = new PolygonSimplifier();

        simplifier.Simplify(source, 1, 0.5, result);
        simplifier.Simplify(source, 0, 0.5, result);

        Assert.Equal(2, result.LoopCount);
        Assert.Equal((10, 10), Points(result, 0)[0]);
        Assert.Equal((0, 0), Points(result, 1)[0]);
    }

    static double EdgeLengthSpread(ContourLoops loops)
    {
        var points = Points(loops, 0);
        var lengths = points.Select((point, i) =>
        {
            var next = points[(i + 1) % points.Length];
            return Math.Sqrt(Math.Pow(point.X - next.X, 2) + Math.Pow(point.Y - next.Y, 2));
        }).ToArray();
        var mean = lengths.Average();
        return Math.Sqrt(lengths.Select(length => (length - mean) * (length - mean)).Average()) / mean;
    }

    static ContourLoops Simplify(ContourLoops source, double epsilon, double spread, uint variation)
    {
        var result = new ContourLoops();
        new PolygonSimplifier().Simplify(source, 0, epsilon, result, spread, variation);
        return result;
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void EveryDroppedVertexLiesWithinTheBaseToleranceOfTheEdgeThatReplacesIt(double spread)
    {
        var source = TraceDisc(24.0, 1.5);
        var original = Points(source, 0);
        const double limit = 2.0;

        foreach (var variation in new[] { 1u, 2u, 3u, 4u })
        {
            var kept = Points(Simplify(source, 2.0, spread, variation), 0);
            var indices = kept.Select(p => Array.IndexOf(original, p)).ToArray();
            Assert.DoesNotContain(-1, indices);
            for (var k = 0; k < indices.Length; k++)
            {
                var from = indices[k];
                var to = indices[(k + 1) % indices.Length];
                var span = (to - from + original.Length) % original.Length;
                for (var step = 1; step < span; step++)
                    Assert.True(DistanceToLine(original[from], original[to], original[(from + step) % original.Length]) <= limit + 1e-9);
            }
        }
    }

    [Fact]
    public void WithoutSpreadTheVariationHasNoEffect()
    {
        var source = TraceDisc(24.0, 1.5);

        var first = Simplify(source, 3.0, 0.0, 1u);
        var second = Simplify(source, 3.0, 0.0, 2u);

        Assert.Equal(Points(first, 0), Points(second, 0));
        Assert.Equal(Points(first, 0), Points(Simplify(source, 3.0, 0.0, 0u), 0));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ASpreadMakesTheEdgeLengthsMoreUneven(double spread)
    {
        var source = TraceDisc(24.0, 1.5);
        var even = EdgeLengthSpread(Simplify(source, 3.0, 0.0, 1u));

        foreach (var variation in new[] { 1u, 2u, 3u })
            Assert.True(EdgeLengthSpread(Simplify(source, 3.0, spread, variation)) > even, $"{spread} {variation}");
    }

    [Fact]
    public void AFullSpreadMakesTheEdgesClearlyUneven()
    {
        var source = TraceDisc(24.0, 1.5);

        foreach (var variation in new[] { 1u, 2u, 3u })
            Assert.True(EdgeLengthSpread(Simplify(source, 3.0, 1.0, variation)) > 0.5);
    }

    [Fact]
    public void AnotherVariationGivesOtherVerticesAndTheSameVariationTheSameOnes()
    {
        var source = TraceDisc(24.0, 1.5);

        var first = Points(Simplify(source, 3.0, 1.0, 1u), 0);
        var again = Points(Simplify(source, 3.0, 1.0, 1u), 0);
        var other = Points(Simplify(source, 3.0, 1.0, 2u), 0);

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void TheRandomSplitStillKeepsOnlyOriginalVerticesAndTheTopmostStart()
    {
        var source = TraceDisc(24.0, 1.5);

        var kept = Points(Simplify(source, 3.0, 1.0, 5u), 0);

        Assert.Subset(Points(source, 0).ToHashSet(), kept.ToHashSet());
        Assert.Equal(Points(source, 0).OrderBy(p => p.Y).ThenBy(p => p.X).First(), kept[0]);
        Assert.True(kept.Length >= 3);
    }
}
