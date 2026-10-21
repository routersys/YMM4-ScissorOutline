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
        { 70, 23, 6, 50 },
        { 130, 20, 7, 35 },
    };

    static ContourLoops Trace(string[] rows)
    {
        var loops = new ContourLoops();
        new ContourTracer().Trace(Tests.Masks.FromRows(rows), rows[0].Length, rows.Length, loops);
        return loops;
    }

    static ContourLoops Trace(byte[] mask, int width, int height)
    {
        var loops = new ContourLoops();
        new ContourTracer().Trace(Tests.Masks.ToBits(mask, width, height), width, height, loops);
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
    public void CellsSeparatedByLongEmptyStretchesAreAllTraced()
    {
        const int width = 700;
        const int height = 400;
        var mask = new byte[width * height];
        var cells = new[] { (0, 0), (699, 0), (350, 200), (352, 202), (0, 399), (699, 399), (64, 100), (66, 100) };
        foreach (var (x, y) in cells)
            mask[y * width + x] = 1;

        var loops = Trace(mask, width, height);

        Assert.Equal(cells.Length, loops.LoopCount);
        Assert.Equal(cells.Length * 4, loops.PointCount);
        Assert.All(Enumerable.Range(0, loops.LoopCount), loop => Assert.Equal(2L, loops.GetDoubleArea(loop)));
    }

    [Fact]
    public void NothingInsideGivesNoLoops()
    {
        var loops = Trace(["...", "...", "..."]);

        Assert.Equal(0, loops.LoopCount);
        Assert.Equal(0, loops.PointCount);
    }

    [Theory]
    [InlineData(62, 66)]
    [InlineData(63, 64)]
    [InlineData(64, 65)]
    [InlineData(0, 129)]
    public void ARunAcrossAWordBoundaryIsOneLoop(int first, int last)
    {
        var mask = new byte[130 * 3];
        for (var x = first; x <= last; x++)
            mask[1 * 130 + x] = 1;

        var loops = Trace(mask, 130, 3);

        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(4, loops.PointCount);
        Assert.Equal(2L * (last - first + 1), loops.GetDoubleArea(0));
    }

    [Theory]
    [MemberData(nameof(Masks))]
    public void TheSignedAreasAddUpToTheNumberOfInsideCells(int width, int height, int seed, int percent)
    {
        var mask = Tests.Masks.Random(width, height, seed, percent);

        var loops = Trace(mask, width, height);

        long total = 0;
        for (var loop = 0; loop < loops.LoopCount; loop++)
            total += loops.GetDoubleArea(loop);
        Assert.Equal(2L * mask.Count(v => v != 0), total);
    }

    [Theory]
    [MemberData(nameof(Masks))]
    public void EveryEdgeIsAxisAlignedAndEveryVertexIsATurn(int width, int height, int seed, int percent)
    {
        var mask = Tests.Masks.Random(width, height, seed, percent);

        var loops = Trace(mask, width, height);

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
        var large = Enumerable.Repeat((byte)1, 30 * 30).ToArray();
        var small = new byte[] { 0, 1, 0 };

        tracer.Trace(Tests.Masks.ToBits(large, 30, 30), 30, 30, loops);
        var largeCount = loops.PointCount;
        tracer.Trace(Tests.Masks.ToBits(small, 3, 1), 3, 1, loops);

        Assert.Equal(4, largeCount);
        Assert.Equal(1, loops.LoopCount);
        Assert.Equal(2, loops.GetDoubleArea(0));
    }
}
