namespace ScissorOutline.Tests;

public sealed class AllocationTests
{
    const int Width = 200;
    const int Height = 160;

    [Fact]
    public void BuildingTheSameShapeAgainAllocatesNothing()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 3, 7);
        var parameters = new ScissorOutlineParameters(24, 0.5, 0.3, 0, true, ScissorOutlineQuality.High);
        var shape = new ScissorOutlineShape();
        shape.Build(alpha, Width, Height, Width, 0f, 0f, parameters);

        var allocated = Allocation.Of(() => shape.Build(alpha, Width, Height, Width, 0f, 0f, parameters));

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void RebuildingTheShapeWhenTheSettingsChangeAllocatesNothing()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 3, 7);
        var shape = new ScissorOutlineShape();
        var step = 0;
        for (var warm = 0; warm < 8; warm++)
            shape.Build(alpha, Width, Height, Width, 0f, 0f, Parameters(warm));

        var allocated = Allocation.Of(() => shape.Build(alpha, Width, Height, Width, 0f, 0f, Parameters(step++)));

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void BuildingAfterTheImageChangesAllocatesNothing()
    {
        var first = Silhouettes.Blobs(Width, Height, 3, 7);
        var second = Silhouettes.Blobs(Width, Height, 4, 7);
        var parameters = new ScissorOutlineParameters(24, 0.5, 0.3, 0, true, ScissorOutlineQuality.High);
        var shape = new ScissorOutlineShape();
        for (var warm = 0; warm < 4; warm++)
        {
            shape.Build(first, Width, Height, Width, 0f, 0f, parameters);
            shape.Build(second, Width, Height, Width, 0f, 0f, parameters);
        }

        var flip = false;
        var allocated = Allocation.Of(() => shape.Build((flip = !flip) ? first : second, Width, Height, Width, 0f, 0f, parameters));

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void RebuildingAnIrregularShapeForEveryFrameAllocatesNothing()
    {
        var alpha = Silhouettes.Blobs(Width, Height, 3, 7);
        var shape = new ScissorOutlineShape();
        for (var warm = 0; warm < 64; warm++)
            shape.Build(alpha, Width, Height, Width, 0f, 0f, new ScissorOutlineParameters(24, 0.5, 0.5, 0, true, ScissorOutlineQuality.High, 1.0, warm));
        var frame = 64;

        var allocated = Allocation.Of(() => shape.Build(alpha, Width, Height, Width, 0f, 0f, new ScissorOutlineParameters(24, 0.5, 0.5, 0, true, ScissorOutlineQuality.High, 1.0, frame++ % 64)));

        Assert.Equal(0, allocated);
    }

    static ScissorOutlineParameters Parameters(int step)
        => new(8 + step % 6 * 5, step % 5 / 4.0, step % 3 / 2.0, step % 4, step % 2 == 0, ScissorOutlineQuality.High);
}
