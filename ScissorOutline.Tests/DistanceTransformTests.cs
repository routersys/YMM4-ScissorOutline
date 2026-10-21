namespace ScissorOutline.Tests;

public sealed class DistanceTransformTests
{
    public static readonly TheoryData<int, int, int, int> Cases = new()
    {
        { 1, 1, 1, 100 },
        { 7, 5, 2, 30 },
        { 63, 20, 3, 4 },
        { 64, 20, 4, 4 },
        { 65, 20, 5, 4 },
        { 130, 40, 6, 1 },
        { 77, 77, 7, 50 },
        { 40, 90, 8, 2 },
    };

    static int[] Expected(byte[] mask, int width, int height)
    {
        var subjects = new List<(int X, int Y)>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (mask[y * width + x] != 0)
                    subjects.Add((x, y));
            }
        }

        var expected = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                expected[y * width + x] = subjects.Min(s => (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y));
        }

        return expected;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryCellHoldsTheSquaredDistanceToTheNearestSubjectCell(int width, int height, int seed, int percent)
    {
        var mask = Masks.Random(width, height, seed, percent);
        mask[0] = 1;
        var result = new int[width * height];

        new DistanceTransform().Squared(Masks.ToBits(mask, width, height), width, height, result);

        Assert.Equal(Expected(mask, width, height), result);
    }

    [Fact]
    public void ASingleCellGivesTheSquaredDistanceToThatCell()
    {
        var mask = new byte[51 * 41];
        mask[17 * 51 + 30] = 1;
        var result = new int[mask.Length];

        new DistanceTransform().Squared(Masks.ToBits(mask, 51, 41), 51, 41, result);

        for (var y = 0; y < 41; y++)
        {
            for (var x = 0; x < 51; x++)
                Assert.Equal((x - 30) * (x - 30) + (y - 17) * (y - 17), result[y * 51 + x]);
        }
    }

    [Fact]
    public void WithoutSubjectEveryDistanceIsFarBeyondAnyRadius()
    {
        var result = new int[90 * 30];

        new DistanceTransform().Squared(new ulong[BitGrid.WordsPerRow(90) * 30], 90, 30, result);

        Assert.All(result, distance => Assert.True(distance > 100_000_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(50)]
    [InlineData(400)]
    [InlineData(1521)]
    public void TheCellsWithinASquaredRadiusAreTheDiscDilation(int squaredRadius)
    {
        const int width = 130;
        const int height = 70;
        var mask = Masks.Random(width, height, 11, 2);
        var source = Masks.ToBits(mask, width, height);
        var wordsPerRow = BitGrid.WordsPerRow(width);
        var dilated = new ulong[wordsPerRow * height];
        var distances = new int[width * height];

        new DiscDilation().Dilate(source, width, height, squaredRadius, dilated);
        new DistanceTransform().Squared(source, width, height, distances);

        var within = new byte[width * height];
        for (var i = 0; i < within.Length; i++)
            within[i] = distances[i] <= squaredRadius ? (byte)1 : (byte)0;
        Assert.Equal(Masks.ToBytes(dilated, width, height), within);
    }

    [Fact]
    public void TheSameInstanceTransformsDifferentSizesCorrectly()
    {
        var transform = new DistanceTransform();
        var large = Masks.Random(140, 60, 3, 2);
        var small = Masks.Random(9, 14, 4, 20);
        large[5] = 1;
        small[3] = 1;
        var largeResult = new int[140 * 60];
        var smallResult = new int[9 * 14];
        var again = new int[largeResult.Length];

        transform.Squared(Masks.ToBits(large, 140, 60), 140, 60, largeResult);
        transform.Squared(Masks.ToBits(small, 9, 14), 9, 14, smallResult);
        transform.Squared(Masks.ToBits(large, 140, 60), 140, 60, again);

        Assert.Equal(largeResult, again);
        Assert.Equal(Expected(small, 9, 14), smallResult);
    }
}
