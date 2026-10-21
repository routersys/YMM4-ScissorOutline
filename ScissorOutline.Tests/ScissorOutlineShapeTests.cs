using System.Numerics;

namespace ScissorOutline.Tests;

public sealed class ScissorOutlineShapeTests
{
    public const int Width = 200;
    public const int Height = 160;

    public static readonly TheoryData<double, double, double, ScissorOutlineQuality> Settings = new()
    {
        { 6, 0, 0, ScissorOutlineQuality.High },
        { 6, 1, 1, ScissorOutlineQuality.Balanced },
        { 12, 0.5, 0.3, ScissorOutlineQuality.High },
        { 24, 0, 1, ScissorOutlineQuality.Ultra },
        { 24, 1, 0, ScissorOutlineQuality.High },
        { 40, 0.5, 0.5, ScissorOutlineQuality.Balanced },
        { 1, 0.5, 0.5, ScissorOutlineQuality.High },
    };

    public static IEnumerable<(string Name, byte[] Alpha)> Subjects()
    {
        yield return ("disc", Silhouettes.Disc(Width, Height, 90, 80, 30));
        yield return ("ring", Silhouettes.Ring(Width, Height, 100, 80, 50, 25));
        yield return ("strokes", Silhouettes.Strokes(Width, Height));
        yield return ("blobs", Silhouettes.Blobs(Width, Height, 3, 7));
        yield return ("dots", Silhouettes.Blobs(Width, Height, 8, 25));
        yield return ("specks", Silhouettes.Fill(Width, Height, (x, y) => (x == 51 && y == 37) || (x == 151 && y == 101) || (x == 13 && y == 77) || (x == 101 && y == 141) || (x == 175 && y == 29)));
    }

    static ScissorOutlineShape Build(byte[] alpha, ScissorOutlineParameters parameters, int width = Width, int height = Height, float originX = 0f, float originY = 0f)
    {
        var shape = new ScissorOutlineShape();
        shape.Build(alpha, width, height, width, originX, originY, parameters);
        return shape;
    }

    static ScissorOutlineParameters Parameters(double thickness, double coarseness, double tremor, ScissorOutlineQuality quality = ScissorOutlineQuality.High, int seed = 0, bool fillHoles = true)
        => new(thickness, coarseness, tremor, seed, fillHoles, quality);

    [Theory]
    [MemberData(nameof(Settings))]
    public void TheCutNeverComesCloserToTheSubjectThanTheThickness(double thickness, double coarseness, double tremor, ScissorOutlineQuality quality)
    {
        foreach (var (name, alpha) in Subjects())
        {
            var shape = Build(alpha, Parameters(thickness, coarseness, tremor, quality, 5));

            foreach (var (x, y) in Silhouettes.Centers(alpha, Width, Height))
                Assert.True(PolygonGeometry.DistanceToBoundary(shape, x, y) >= thickness - 0.01, $"{name} ({x}, {y})");
        }
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void EverySubjectPixelStaysInsideThePaper(double thickness, double coarseness, double tremor, ScissorOutlineQuality quality)
    {
        foreach (var (name, alpha) in Subjects())
        {
            var shape = Build(alpha, Parameters(thickness, coarseness, tremor, quality, 5));

            foreach (var (x, y) in Silhouettes.Centers(alpha, Width, Height))
                Assert.NotEqual(0, PolygonGeometry.Winding(shape, x, y));
        }
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void TheCutStaysWithinTheBoundOfTheEpsilonTheTremorAndTheCell(double thickness, double coarseness, double tremor, ScissorOutlineQuality quality)
    {
        var plan = ScissorOutlineSettings.CreatePlan(Parameters(thickness, coarseness, tremor, quality), Width, Height);
        var limit = thickness + 2 * plan.Epsilon + 2 * plan.Tremor + 3 * plan.Cell;
        foreach (var (name, alpha) in Subjects())
        {
            var shape = Build(alpha, Parameters(thickness, coarseness, tremor, quality, 5));
            var centers = Silhouettes.Centers(alpha, Width, Height).ToArray();

            for (var i = 0; i < shape.VertexCount; i++)
                Assert.True(PolygonGeometry.DistanceToNearest(centers, shape.Vertices[i].X, shape.Vertices[i].Y) <= limit, name);
        }
    }

    [Fact]
    public void AGuaranteeHoldsWhenTheGridLimitForcesCoarseCells()
    {
        const int width = 1600;
        const int height = 1000;
        var alpha = Silhouettes.Fill(width, height, (x, y) => (x - 700) * (x - 700) + (y - 500) * (y - 500) <= 150 * 150 || (x >= 1200 && x < 1203 && y >= 300 && y < 700));
        var parameters = Parameters(10, 0.5, 0.5, ScissorOutlineQuality.Balanced);

        var shape = Build(alpha, parameters, width, height);

        Assert.Equal(4, ScissorOutlineSettings.CreatePlan(parameters, width, height).Cell);
        foreach (var (x, y) in Silhouettes.Centers(alpha, width, height).Where((_, i) => i % 7 == 0))
        {
            Assert.True(PolygonGeometry.DistanceToBoundary(shape, x, y) >= 10 - 0.01);
            Assert.NotEqual(0, PolygonGeometry.Winding(shape, x, y));
        }
    }

    [Fact]
    public void ADiscIsCutIntoAFewStraightEdges()
    {
        var alpha = Silhouettes.Disc(Width, Height, 100, 80, 30);

        var shape = Build(alpha, Parameters(20, 1, 0));

        Assert.Equal(1, shape.LoopCount);
        Assert.InRange(shape.VertexCount, 6, 20);
    }

    [Fact]
    public void ACoarserCutHasFewerVertices()
    {
        var alpha = Silhouettes.Disc(Width, Height, 100, 80, 40);

        var fine = Build(alpha, Parameters(30, 0, 0));
        var medium = Build(alpha, Parameters(30, 0.5, 0));
        var coarse = Build(alpha, Parameters(30, 1, 0));

        Assert.True(medium.VertexCount < fine.VertexCount);
        Assert.True(coarse.VertexCount <= medium.VertexCount);
    }

    [Fact]
    public void WithoutTremorTheSeedHasNoEffect()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 6);

        var first = Build(alpha, Parameters(20, 0.5, 0, seed: 1));
        var second = Build(alpha, Parameters(20, 0.5, 0, seed: 2));

        Assert.True(first.Vertices.SequenceEqual(second.Vertices));
    }

    [Fact]
    public void WithTremorTheSeedMovesTheVerticesButNotTheirCount()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 6);

        var first = Build(alpha, Parameters(20, 0.5, 1, seed: 1));
        var second = Build(alpha, Parameters(20, 0.5, 1, seed: 2));

        Assert.Equal(first.VertexCount, second.VertexCount);
        Assert.False(first.Vertices.SequenceEqual(second.Vertices));
    }

    [Fact]
    public void TwoSeedsMoveEachVertexByAtMostTwiceTheAmplitude()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 4, 6);
        var plan = ScissorOutlineSettings.CreatePlan(Parameters(30, 0.5, 1), Width, Height);

        var first = Build(alpha, Parameters(30, 0.5, 1, seed: 9));
        var second = Build(alpha, Parameters(30, 0.5, 1, seed: 10));

        Assert.Equal(first.VertexCount, second.VertexCount);
        for (var i = 0; i < first.VertexCount; i++)
            Assert.True(Vector2.Distance(first.Vertices[i], second.Vertices[i]) <= 2 * plan.Tremor + 1e-3);
    }

    [Fact]
    public void TheSameBuildTwiceGivesTheSameVerticesFromADifferentInstance()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 6, 9);

        var first = Build(alpha, Parameters(18, 0.4, 0.6, seed: 3));
        var second = Build(alpha, Parameters(18, 0.4, 0.6, seed: 3));

        Assert.True(first.Vertices.SequenceEqual(second.Vertices));
        Assert.True(first.Starts.SequenceEqual(second.Starts));
    }

    public static readonly TheoryData<double> PooledThicknesses = new() { 6, 24, 40, 60, 160, 320 };

    static byte[] Pool(byte[] alpha, int level, out int pooledWidth, out int pooledHeight)
    {
        var block = 1 << level;
        pooledWidth = (Width + block - 1) / block;
        pooledHeight = (Height + block - 1) / block;
        var pooled = new byte[pooledWidth * pooledHeight];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (alpha[y * Width + x] > ScissorOutlineSettings.AlphaThreshold)
                    pooled[(y >> level) * pooledWidth + (x >> level)] = 255;
            }
        }

        return pooled;
    }

    static int PoolLevel(ScissorOutlineParameters parameters)
        => Math.Min(System.Numerics.BitOperations.Log2((uint)ScissorOutlineSettings.CreatePlan(parameters, Width, Height).Cell), 4);

    [Theory]
    [MemberData(nameof(PooledThicknesses))]
    public void APooledImageGivesTheSameShapeAsTheFullImage(double thickness)
    {
        foreach (var (name, alpha) in Subjects())
        {
            var parameters = Parameters(thickness, 0.5, 0.3, ScissorOutlineQuality.High, 5);
            var level = PoolLevel(parameters);
            var pooled = Pool(alpha, level, out var pooledWidth, out var pooledHeight);

            var full = Build(alpha, parameters);
            var fromPooled = new ScissorOutlineShape();
            fromPooled.BuildPooled(pooled, pooledWidth, pooledHeight, pooledWidth, level, Width, Height, 0f, 0f, parameters);

            Assert.True(full.VertexCount > 0, name);
            Assert.True(full.Vertices.SequenceEqual(fromPooled.Vertices), name);
            Assert.True(full.Starts.SequenceEqual(fromPooled.Starts), name);
        }
    }

    [Fact]
    public void APooledBuildAndAFullBuildOfTheSameImageAreTheSameInput()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 3, 7);
        var parameters = Parameters(40, 0.5, 0.3, ScissorOutlineQuality.High, 5);
        var level = PoolLevel(parameters);
        var pooled = Pool(alpha, level, out var pooledWidth, out var pooledHeight);
        var shape = new ScissorOutlineShape();

        var first = shape.BuildPooled(pooled, pooledWidth, pooledHeight, pooledWidth, level, Width, Height, 0f, 0f, parameters);
        var second = shape.Build(alpha, Width, Height, Width, 0f, 0f, parameters);

        Assert.Equal(3, level);
        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, first);
        Assert.Equal(ScissorOutlineShapeChange.Unchanged, second);
    }

    [Fact]
    public void APoolLevelBeyondTheCellIsRefused()
    {
        var pooled = new byte[100 * 80];
        var shape = new ScissorOutlineShape();

        Assert.Throws<ArgumentOutOfRangeException>(() => shape.BuildPooled(pooled, 100, 80, 100, 1, Width, Height, 0f, 0f, Parameters(6, 0.5, 0.3)));
    }

    [Fact]
    public void ARepeatedBuildOfTheSameInputIsReportedAsUnchanged()
    {
        var alpha = Silhouettes.Disc(Width, Height, 100, 80, 30);
        var parameters = Parameters(20, 0.5, 0.5, seed: 4);
        var shape = new ScissorOutlineShape();

        var first = shape.Build(alpha, Width, Height, Width, 0f, 0f, parameters);
        var second = shape.Build(alpha, Width, Height, Width, 0f, 0f, parameters);

        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, first);
        Assert.Equal(ScissorOutlineShapeChange.Unchanged, second);
    }

    [Fact]
    public void ChangingAnyInputRebuildsTheShape()
    {
        var alpha = Silhouettes.Disc(Width, Height, 100, 80, 30);
        var moved = Silhouettes.Disc(Width, Height, 101, 80, 30);
        var parameters = Parameters(20, 0.5, 0.5, seed: 4);
        var shape = new ScissorOutlineShape();
        shape.Build(alpha, Width, Height, Width, 0f, 0f, parameters);

        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, shape.Build(moved, Width, Height, Width, 0f, 0f, parameters));
        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, shape.Build(moved, Width, Height, Width, 0f, 0f, parameters with { Thickness = 21 }));
        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, shape.Build(moved, Width, Height, Width, 0f, 0f, parameters with { Thickness = 21, Coarseness = 0.6 }));
        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, shape.Build(moved, Width, Height, Width, 0f, 0f, parameters with { Thickness = 21, Coarseness = 0.6, Tremor = 0.4 }));
        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, shape.Build(moved, Width, Height, Width, 0f, 0f, parameters with { Thickness = 21, Coarseness = 0.6, Tremor = 0.4, Seed = 5 }));
        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, shape.Build(moved, Width, Height, Width, 0f, 0f, parameters with { Thickness = 21, Coarseness = 0.6, Tremor = 0.4, Seed = 5, FillHoles = false }));
        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, shape.Build(moved, Width, Height, Width, 3f, 0f, parameters with { Thickness = 21, Coarseness = 0.6, Tremor = 0.4, Seed = 5, FillHoles = false }));
        Assert.Equal(ScissorOutlineShapeChange.Rebuilt, shape.Build(moved, Width, Height, Width, 3f, 2f, parameters with { Thickness = 21, Coarseness = 0.6, Tremor = 0.4, Seed = 5, FillHoles = false }));
    }

    [Fact]
    public void AReusedShapeBuildsLikeAFreshOneAfterTheInputChanges()
    {
        var first = Silhouettes.Blobs(Width, Height, 1, 8);
        var second = Silhouettes.Blobs(Width, Height, 2, 5);
        var parameters = Parameters(16, 0.5, 0.5, seed: 7);
        var reused = new ScissorOutlineShape();
        reused.Build(first, Width, Height, Width, 0f, 0f, Parameters(40, 1, 1));
        reused.Build(first, Width, Height, Width, 0f, 0f, parameters);

        reused.Build(second, Width, Height, Width, 0f, 0f, parameters);

        var fresh = Build(second, parameters);
        Assert.True(reused.Vertices.SequenceEqual(fresh.Vertices));
        Assert.True(reused.Starts.SequenceEqual(fresh.Starts));
    }

    [Fact]
    public void AnImageWithoutSubjectHasNoLoops()
    {
        var shape = Build(Silhouettes.Empty(Width, Height), Parameters(20, 0.5, 0.5));

        Assert.Equal(0, shape.LoopCount);
        Assert.Equal(0, shape.VertexCount);
    }

    [Fact]
    public void ADimPixelBelowTheThresholdIsNotASubject()
    {
        var alpha = new byte[Width * Height];
        alpha[80 * Width + 100] = ScissorOutlineSettings.AlphaThreshold;
        var dim = Build(alpha, Parameters(20, 0.5, 0.5));
        alpha[80 * Width + 100] = ScissorOutlineSettings.AlphaThreshold + 1;
        var visible = Build(alpha, Parameters(20, 0.5, 0.5));

        Assert.Equal(0, dim.LoopCount);
        Assert.Equal(1, visible.LoopCount);
    }

    [Fact]
    public void TheShapeFollowsTheOriginOfTheImage()
    {
        var alpha = Silhouettes.Disc(Width, Height, 100, 80, 30);
        var parameters = Parameters(20, 0.5, 0.5, seed: 2);

        var origin = Build(alpha, parameters);
        var moved = Build(alpha, parameters, originX: -37f, originY: 12.5f);

        Assert.Equal(origin.VertexCount, moved.VertexCount);
        for (var i = 0; i < origin.VertexCount; i++)
            Assert.True(Vector2.Distance(origin.Vertices[i] + new Vector2(-37f, 12.5f), moved.Vertices[i]) < 1e-3f);
    }

    [Fact]
    public void NearbySubjectsShareOnePaperWhileDistantOnesDoNot()
    {
        var near = Silhouettes.Fill(Width, Height, (x, y) => (Math.Abs(x - 70) <= 12 && Math.Abs(y - 80) <= 12) || (Math.Abs(x - 110) <= 12 && Math.Abs(y - 80) <= 12));
        var far = Silhouettes.Fill(Width, Height, (x, y) => (Math.Abs(x - 30) <= 12 && Math.Abs(y - 80) <= 12) || (Math.Abs(x - 170) <= 12 && Math.Abs(y - 80) <= 12));
        var parameters = Parameters(20, 0.2, 0);

        Assert.Equal(1, Build(near, parameters).LoopCount);
        Assert.Equal(2, Build(far, parameters).LoopCount);
    }

    [Fact]
    public void FillingHolesKeepsOnlyTheOuterLoopOfARing()
    {
        var alpha = Silhouettes.Ring(Width, Height, 100, 80, 60, 40);

        var filled = Build(alpha, Parameters(8, 0.3, 0, fillHoles: true));
        var open = Build(alpha, Parameters(8, 0.3, 0, fillHoles: false));

        Assert.Equal(1, filled.LoopCount);
        Assert.Equal(2, open.LoopCount);
        Assert.NotEqual(0, PolygonGeometry.Winding(filled, 100, 80));
        Assert.Equal(0, PolygonGeometry.Winding(open, 100, 80));
    }

    [Fact]
    public void TheEdgeOfAHoleThatStaysOpenKeepsTheThicknessFromTheSubject()
    {
        var alpha = Silhouettes.Ring(Width, Height, 100, 80, 60, 40);

        var open = Build(alpha, Parameters(8, 0.3, 0, fillHoles: false));

        Assert.All(Silhouettes.Centers(alpha, Width, Height).Take(500), center => Assert.NotEqual(0, PolygonGeometry.Winding(open, center.X, center.Y)));
        Assert.InRange(PolygonGeometry.DistanceToBoundary(open, 100, 80), 1, 32.01);
    }

    [Fact]
    public void AHoleSmallerThanTheMarginIsClosedWhetherOrNotHolesAreFilled()
    {
        var alpha = Silhouettes.Ring(Width, Height, 100, 80, 40, 6);

        var open = Build(alpha, Parameters(20, 0.3, 0, fillHoles: false));

        Assert.Equal(1, open.LoopCount);
    }

    [Fact]
    public void TheLoopsOfSeveralSubjectsAreIndexedByTheirStarts()
    {
        var alpha = Silhouettes.Fill(Width, Height, (x, y) => (Math.Abs(x - 30) <= 8 && Math.Abs(y - 80) <= 8) || (Math.Abs(x - 170) <= 8 && Math.Abs(y - 80) <= 8));

        var shape = Build(alpha, Parameters(10, 0.3, 0.2, seed: 1));

        Assert.Equal(2, shape.LoopCount);
        Assert.Equal(0, shape.Starts[0]);
        Assert.Equal(shape.VertexCount, shape.Starts[2]);
        Assert.True(shape.Starts[1] > 2 && shape.Starts[1] < shape.Starts[2] - 2);
    }

    [Fact]
    public void EveryLoopHasAtLeastThreeVerticesAndNoDuplicatedNeighbours()
    {
        foreach (var (name, alpha) in Subjects())
        {
            var shape = Build(alpha, Parameters(14, 0.7, 0.2, seed: 2));

            for (var loop = 0; loop < shape.LoopCount; loop++)
            {
                var start = shape.Starts[loop];
                var count = shape.Starts[loop + 1] - start;
                Assert.True(count >= 3, name);
                for (var i = 0; i < count; i++)
                    Assert.NotEqual(shape.Vertices[start + i], shape.Vertices[start + (i + 1) % count]);
            }
        }
    }
}
