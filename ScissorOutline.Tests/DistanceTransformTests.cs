namespace ScissorOutline.Tests;

public sealed class DistanceTransformTests
{
    public static readonly TheoryData<int, int, int, int> Masks = new()
    {
        { 1, 1, 1, 100 },
        { 1, 40, 2, 20 },
        { 40, 1, 3, 20 },
        { 17, 9, 4, 5 },
        { 33, 33, 5, 2 },
        { 64, 48, 6, 10 },
        { 100, 70, 7, 1 },
        { 31, 53, 8, 60 },
    };

    public static byte[] RandomMask(int width, int height, int seed, int percent)
    {
        var state = (uint)seed * 2654435761u + 12345u;
        var mask = new byte[width * height];
        for (var i = 0; i < mask.Length; i++)
        {
            state = state * 1664525u + 1013904223u;
            mask[i] = (byte)((state >> 24) % 100 < percent ? 1 : 0);
        }

        return mask;
    }

    static float Nearest(byte[] mask, int width, int height, int x, int y)
    {
        var best = float.PositiveInfinity;
        for (var sy = 0; sy < height; sy++)
        {
            for (var sx = 0; sx < width; sx++)
            {
                if (mask[sy * width + sx] == 0)
                    continue;
                best = Math.Min(best, (sx - x) * (sx - x) + (sy - y) * (sy - y));
            }
        }

        return best;
    }

    [Theory]
    [MemberData(nameof(Masks))]
    public void EachCellHoldsTheSquaredDistanceToTheNearestSubjectCell(int width, int height, int seed, int percent)
    {
        var mask = RandomMask(width, height, seed, percent);
        mask[(height / 2) * width + width / 2] = 1;
        var distance = new float[width * height];

        new DistanceTransform().Compute(mask, width, height, distance);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                Assert.Equal(Nearest(mask, width, height, x, y), distance[y * width + x]);
        }
    }

    [Fact]
    public void SubjectCellsAreAtDistanceZero()
    {
        var mask = RandomMask(30, 20, 9, 30);
        var distance = new float[mask.Length];

        new DistanceTransform().Compute(mask, 30, 20, distance);

        for (var i = 0; i < mask.Length; i++)
            Assert.Equal(mask[i] != 0, distance[i] == 0f);
    }

    [Fact]
    public void AMaskWithoutSubjectStaysFar()
    {
        var distance = new float[12 * 7];

        new DistanceTransform().Compute(new byte[12 * 7], 12, 7, distance);

        Assert.All(distance, value => Assert.True(value >= DistanceTransform.Far));
    }

    [Fact]
    public void TheSameInstanceComputesDifferentSizesCorrectly()
    {
        var transform = new DistanceTransform();
        var large = RandomMask(80, 60, 11, 3);
        var small = RandomMask(9, 14, 12, 20);
        var largeDistance = new float[large.Length];
        var smallDistance = new float[small.Length];
        var again = new float[large.Length];

        transform.Compute(large, 80, 60, largeDistance);
        transform.Compute(small, 9, 14, smallDistance);
        transform.Compute(large, 80, 60, again);

        Assert.Equal(largeDistance, again);
        for (var y = 0; y < 14; y++)
        {
            for (var x = 0; x < 9; x++)
                Assert.Equal(Nearest(small, 9, 14, x, y), smallDistance[y * 9 + x]);
        }
    }
}
