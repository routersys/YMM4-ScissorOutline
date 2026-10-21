using System.Numerics;

namespace ScissorOutline.Tests;

public sealed class ScissorOutlineIrregularShapeTests
{
    const int Width = ScissorOutlineShapeTests.Width;
    const int Height = ScissorOutlineShapeTests.Height;

    public static readonly TheoryData<double, double, double, double, ScissorOutlineQuality, int> Settings = new()
    {
        { 6, 0.5, 0.5, 1.0, ScissorOutlineQuality.High, 0 },
        { 12, 0, 0, 1.0, ScissorOutlineQuality.High, 3 },
        { 24, 1, 1, 1.0, ScissorOutlineQuality.Balanced, 7 },
        { 24, 0.5, 0.3, 0.5, ScissorOutlineQuality.Ultra, 1 },
        { 40, 0.5, 0.5, 1.0, ScissorOutlineQuality.High, 2 },
        { 1, 0.5, 0.5, 1.0, ScissorOutlineQuality.High, 5 },
        { 18, 0.2, 0, 0.7, ScissorOutlineQuality.High, 11 },
    };

    static ScissorOutlineParameters Parameters(double thickness, double coarseness, double tremor, double irregularity, ScissorOutlineQuality quality = ScissorOutlineQuality.High, int frame = 0, int seed = 3)
        => new(thickness, coarseness, tremor, seed, true, quality, irregularity, frame);

    static ScissorOutlineShape Build(byte[] alpha, ScissorOutlineParameters parameters)
    {
        var shape = new ScissorOutlineShape();
        shape.Build(alpha, Width, Height, Width, 0f, 0f, parameters);
        return shape;
    }

    static double[] Distances(ScissorOutlineShape shape, (double X, double Y)[] centers)
    {
        var distances = new List<double>();
        for (var loop = 0; loop < shape.LoopCount; loop++)
        {
            var start = shape.Starts[loop];
            var count = shape.Starts[loop + 1] - start;
            for (var i = 0; i < count; i++)
            {
                var a = shape.Vertices[start + i];
                var b = shape.Vertices[start + (i + 1) % count];
                for (var step = 0; step < 4; step++)
                    distances.Add(PolygonGeometry.DistanceToNearest(centers, a.X + (b.X - a.X) * step / 4.0, a.Y + (b.Y - a.Y) * step / 4.0));
            }
        }

        return distances.ToArray();
    }

    static double Spread(double[] values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Select(value => (value - mean) * (value - mean)).Average());
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void TheIrregularCutNeverComesCloserToTheSubjectThanTheThickness(double thickness, double coarseness, double tremor, double irregularity, ScissorOutlineQuality quality, int frame)
    {
        foreach (var (name, alpha) in ScissorOutlineShapeTests.Subjects())
        {
            var shape = Build(alpha, Parameters(thickness, coarseness, tremor, irregularity, quality, frame));

            foreach (var (x, y) in Silhouettes.Centers(alpha, Width, Height))
                Assert.True(PolygonGeometry.DistanceToVisibleBoundary(shape, x, y) >= thickness - 0.01, $"{name} ({x}, {y})");
        }
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void EverySubjectPixelStaysInsideTheIrregularPaper(double thickness, double coarseness, double tremor, double irregularity, ScissorOutlineQuality quality, int frame)
    {
        foreach (var (name, alpha) in ScissorOutlineShapeTests.Subjects())
        {
            var shape = Build(alpha, Parameters(thickness, coarseness, tremor, irregularity, quality, frame));

            foreach (var (x, y) in Silhouettes.Centers(alpha, Width, Height))
                Assert.NotEqual(0, PolygonGeometry.Winding(shape, x, y));
        }
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void TheIrregularCutStaysWithinTheBoundOfTheEpsilonTheShiftsAndTheCell(double thickness, double coarseness, double tremor, double irregularity, ScissorOutlineQuality quality, int frame)
    {
        var parameters = Parameters(thickness, coarseness, tremor, irregularity, quality, frame);
        var plan = ScissorOutlineSettings.CreatePlan(parameters, Width, Height);
        var limit = thickness + plan.ThicknessSpread + 3 * plan.Cell + 4 * (plan.Epsilon + plan.Tremor + plan.Distance);
        foreach (var (name, alpha) in ScissorOutlineShapeTests.Subjects())
        {
            var shape = Build(alpha, parameters);
            var centers = Silhouettes.Centers(alpha, Width, Height).ToArray();

            for (var i = 0; i < shape.VertexCount; i++)
                Assert.True(PolygonGeometry.DistanceToNearest(centers, shape.Vertices[i].X, shape.Vertices[i].Y) <= limit, name);
        }
    }

    [Fact]
    public void TheGuaranteeHoldsForManyFramesAndSeedsAtFullIrregularity()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 7);
        var centers = Silhouettes.Centers(alpha, Width, Height).ToArray();

        for (var seed = 0; seed < 3; seed++)
        {
            for (var frame = 0; frame < 12; frame++)
            {
                var shape = Build(alpha, Parameters(16, 0.4, 1, 1, ScissorOutlineQuality.High, frame, seed));

                foreach (var (x, y) in centers.Where((_, i) => i % 5 == 0))
                {
                    Assert.True(PolygonGeometry.DistanceToVisibleBoundary(shape, x, y) >= 16 - 0.01, $"seed {seed} frame {frame}");
                    Assert.NotEqual(0, PolygonGeometry.Winding(shape, x, y));
                }
            }
        }
    }

    [Fact]
    public void TheIrregularityMakesTheDistanceToTheSubjectVary()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 5);
        var centers = Silhouettes.Centers(alpha, Width, Height).ToArray();
        var even = Spread(Distances(Build(alpha, Parameters(20, 0.3, 0, 0)), centers));

        foreach (var frame in new[] { 0, 1, 2 })
        {
            var uneven = Spread(Distances(Build(alpha, Parameters(20, 0.3, 0, 1, ScissorOutlineQuality.High, frame)), centers));

            Assert.True(uneven > 2 * even, $"{frame} {even} {uneven}");
        }
    }

    [Fact]
    public void ALargerIrregularityVariesTheDistanceMore()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 5);
        var centers = Silhouettes.Centers(alpha, Width, Height).ToArray();

        var none = Spread(Distances(Build(alpha, Parameters(20, 0.3, 0, 0)), centers));
        var half = Spread(Distances(Build(alpha, Parameters(20, 0.3, 0, 0.5)), centers));
        var full = Spread(Distances(Build(alpha, Parameters(20, 0.3, 0, 1)), centers));

        Assert.True(none < half);
        Assert.True(half < full);
    }

    [Fact]
    public void WithoutAnyRandomnessTheFrameDoesNotRebuildTheShape()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 6);
        var shape = new ScissorOutlineShape();
        shape.Build(alpha, Width, Height, Width, 0f, 0f, Parameters(20, 0.4, 0, 0, frame: 0));
        var before = shape.Vertices.ToArray();

        var change = shape.Build(alpha, Width, Height, Width, 0f, 0f, Parameters(20, 0.4, 0, 0, frame: 7, seed: 9));

        Assert.Equal(ScissorOutlineShapeChange.Unchanged, change);
        Assert.Equal(before, shape.Vertices.ToArray());
    }

    [Theory]
    [InlineData(0.5, 0.0)]
    [InlineData(0.0, 0.8)]
    [InlineData(0.5, 0.8)]
    public void ChangingTheFrameRebuildsAndMovesTheShapeWhenSomethingIsRandom(double tremor, double irregularity)
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 6);
        var shape = new ScissorOutlineShape();
        shape.Build(alpha, Width, Height, Width, 0f, 0f, Parameters(20, 0.4, tremor, irregularity, frame: 0));
        var before = shape.Vertices.ToArray();

        var change = shape.Build(alpha, Width, Height, Width, 0f, 0f, Parameters(20, 0.4, tremor, irregularity, frame: 1));

        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, change);
        Assert.NotEqual(before, shape.Vertices.ToArray());
    }

    [Fact]
    public void EveryFrameGivesItsOwnShapeAndReturningToAFrameGivesTheSameShape()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 6);
        var reused = new ScissorOutlineShape();
        var shapes = new List<Vector2[]>();
        for (var frame = 0; frame < 6; frame++)
        {
            reused.Build(alpha, Width, Height, Width, 0f, 0f, Parameters(20, 0.4, 0.5, 0.8, frame: frame));
            shapes.Add(reused.Vertices.ToArray());
        }

        reused.Build(alpha, Width, Height, Width, 0f, 0f, Parameters(20, 0.4, 0.5, 0.8, frame: 2));

        for (var first = 0; first < shapes.Count; first++)
        {
            for (var second = first + 1; second < shapes.Count; second++)
                Assert.NotEqual(shapes[first], shapes[second]);
        }

        Assert.Equal(shapes[2], reused.Vertices.ToArray());
        Assert.Equal(shapes[2], Build(alpha, Parameters(20, 0.4, 0.5, 0.8, frame: 2)).Vertices.ToArray());
    }

    [Fact]
    public void TheSeedAndTheFrameBothSelectTheShape()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 6);

        var origin = Build(alpha, Parameters(20, 0.4, 0.5, 0.8, frame: 0, seed: 0)).Vertices.ToArray();
        var otherSeed = Build(alpha, Parameters(20, 0.4, 0.5, 0.8, frame: 0, seed: 1)).Vertices.ToArray();
        var otherFrame = Build(alpha, Parameters(20, 0.4, 0.5, 0.8, frame: 1, seed: 0)).Vertices.ToArray();

        Assert.NotEqual(origin, otherSeed);
        Assert.NotEqual(origin, otherFrame);
        Assert.NotEqual(otherSeed, otherFrame);
    }

    [Fact]
    public void TheShapeFollowsTheSubjectWhileTheRandomPartIsHeld()
    {
        var first = Silhouettes.Disc(Width, Height, 90, 80, 30);
        var second = Silhouettes.Disc(Width, Height, 100, 80, 30);
        var shape = new ScissorOutlineShape();
        shape.Build(first, Width, Height, Width, 0f, 0f, Parameters(20, 0.4, 0.5, 0.8, frame: 4));

        shape.Build(second, Width, Height, Width, 0f, 0f, Parameters(20, 0.4, 0.5, 0.8, frame: 4));

        foreach (var (x, y) in Silhouettes.Centers(second, Width, Height))
            Assert.True(PolygonGeometry.DistanceToVisibleBoundary(shape, x, y) >= 20 - 0.01);
    }

    [Fact]
    public void TheIrregularityChangesTheNumberOfEdges()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 5);

        var even = Build(alpha, Parameters(20, 0.3, 0, 0));
        var uneven = Build(alpha, Parameters(20, 0.3, 0, 1));

        Assert.True(uneven.VertexCount > even.VertexCount * 1.3, $"{even.VertexCount} {uneven.VertexCount}");
    }
}
