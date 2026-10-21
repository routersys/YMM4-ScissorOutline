namespace ScissorOutline.Tests;

public sealed class ContourTracerTests
{
    public static readonly TheoryData<int, int, int, int> Masks = new()
    {
        { 5, 5, 1, 40 },
        { 12, 9, 2, 50 },
        { 24, 24, 3, 30 },
        { 40, 17, 4, 65 },
        { 31, 31, 5, 15 },
    };

    static ContourLoops Trace(string[] rows)
    {
        var height = rows.Length;
        var width = rows[0].Length;
        var distance = new float[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                distance[y * width + x] = rows[y][x] == '#' ? 0f : 100f;
        }

        var loops = new ContourLoops();
        new ContourTracer().Trace(distance, width, height, 1f, loops);
        return loops;
    }

    static (int X, int Y)[] Loop(ContourLoops loops, int index)
    {
        var start = loops.Starts[index];
        var end = loops.Starts[index + 1];
        var points = loops.Points.ToArray();
        return Enumerable.Range(start, end - start).Select(i => (points[i * 2], points[i * 2 + 1])).ToArray();
    }

    [Fact]
    public void ASingleCellIsAClockwiseSquare()
    {
        var loops = Trace(["...", ".#.", "..."]);

        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(2, loops.GetDoubleArea(0));
        Assert.Equal([(1, 1), (1, 2), (2, 1), (2, 2)], Loop(loops, 0).OrderBy(p => p.X).ThenBy(p => p.Y).ToArray());
    }

    [Fact]
    public void ARectangleHasFourVerticesAndItsDoubledArea()
    {
        var loops = Trace(["....", ".###", ".###", "...."]);

        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(4, loops.PointCount);
        Assert.Equal(12, loops.GetDoubleArea(0));
    }

    [Fact]
    public void AnLShapeHasSixVertices()
    {
        var loops = Trace(["....", ".#..", ".#..", ".###", "...."]);

        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(6, loops.PointCount);
        Assert.Equal(10, loops.GetDoubleArea(0));
    }

    [Fact]
    public void ARingHasAnOuterLoopAndAnInnerLoopWithTheOppositeSign()
    {
        var loops = Trace([".....", ".###.", ".#.#.", ".###.", "....."]);

        Assert.Equal(2, loops.LoopCount);
        var areas = Enumerable.Range(0, 2).Select(loops.GetDoubleArea).OrderBy(a => a).ToArray();
        Assert.Equal([-2L, 18L], areas);
        Assert.Equal(8, loops.PointCount);
    }

    [Fact]
    public void ACellOnTheBorderOfTheGridStillClosesItsLoop()
    {
        var loops = Trace(["##", "##"]);

        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(4, loops.PointCount);
        Assert.Equal(8, loops.GetDoubleArea(0));
    }

    [Fact]
    public void ADistanceEqualToTheLimitIsInside()
    {
        var distance = new float[] { 1f, 2f, 1f };
        var loops = new ContourLoops();

        new ContourTracer().Trace(distance, 3, 1, 1f, loops);

        Assert.Equal(2, loops.LoopCount);
        Assert.Equal(2, loops.GetDoubleArea(0));
        Assert.Equal(2, loops.GetDoubleArea(1));
    }

    [Fact]
    public void NothingInsideGivesNoLoops()
    {
        var loops = Trace(["...", "...", "..."]);

        Assert.Equal(0, loops.LoopCount);
        Assert.Equal(0, loops.PointCount);
    }

    [Theory]
    [MemberData(nameof(Masks))]
    public void TheSignedAreasAddUpToTheNumberOfInsideCells(int width, int height, int seed, int percent)
    {
        var mask = DistanceTransformTests.RandomMask(width, height, seed, percent);
        var distance = mask.Select(v => v != 0 ? 0f : 9f).ToArray();
        var loops = new ContourLoops();

        new ContourTracer().Trace(distance, width, height, 1f, loops);

        long total = 0;
        for (var loop = 0; loop < loops.LoopCount; loop++)
            total += loops.GetDoubleArea(loop);
        Assert.Equal(2L * mask.Count(v => v != 0), total);
    }

    [Theory]
    [MemberData(nameof(Masks))]
    public void EveryEdgeIsAxisAlignedAndEveryVertexIsATurn(int width, int height, int seed, int percent)
    {
        var mask = DistanceTransformTests.RandomMask(width, height, seed, percent);
        var distance = mask.Select(v => v != 0 ? 0f : 9f).ToArray();
        var loops = new ContourLoops();

        new ContourTracer().Trace(distance, width, height, 1f, loops);

        for (var loop = 0; loop < loops.LoopCount; loop++)
        {
            var points = Loop(loops, loop);
            for (var i = 0; i < points.Length; i++)
            {
                var previous = points[(i + points.Length - 1) % points.Length];
                var current = points[i];
                var next = points[(i + 1) % points.Length];
                Assert.True(previous.X == current.X ^ previous.Y == current.Y);
                Assert.True(current.X == next.X ^ current.Y == next.Y);
                Assert.True((previous.X == current.X) != (current.X == next.X));
            }
        }
    }

    [Fact]
    public void TheTracerCanBeReusedOnADifferentGridSize()
    {
        var tracer = new ContourTracer();
        var loops = new ContourLoops();
        var large = Enumerable.Repeat(0f, 30 * 30).ToArray();
        var small = new float[] { 5f, 0f, 5f };

        tracer.Trace(large, 30, 30, 1f, loops);
        var largeCount = loops.PointCount;
        tracer.Trace(small, 3, 1, 1f, loops);

        Assert.Equal(4, largeCount);
        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(2, loops.GetDoubleArea(0));
    }
}
