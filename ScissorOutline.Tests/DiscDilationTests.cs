namespace ScissorOutline.Tests;

public sealed class DiscDilationTests
{
    public static readonly TheoryData<int, int, int, int, int> Cases = new()
    {
        { 1, 1, 1, 100, 1 },
        { 7, 5, 2, 30, 2 },
        { 63, 20, 3, 4, 25 },
        { 64, 20, 4, 4, 25 },
        { 65, 20, 5, 4, 100 },
        { 130, 40, 6, 1, 289 },
        { 190, 31, 7, 2, 5 },
        { 128, 90, 8, 1, 400 },
        { 77, 77, 9, 50, 17 },
    };

    static byte[] Expected(byte[] mask, int width, int height, int squaredRadius)
    {
        var expected = new byte[mask.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                for (var sy = Math.Max(0, y - 25); sy < Math.Min(height, y + 26) && expected[y * width + x] == 0; sy++)
                {
                    for (var sx = Math.Max(0, x - 25); sx < Math.Min(width, x + 26); sx++)
                    {
                        if (mask[sy * width + sx] != 0 && (sx - x) * (sx - x) + (sy - y) * (sy - y) <= squaredRadius)
                        {
                            expected[y * width + x] = 1;
                            break;
                        }
                    }
                }
            }
        }

        return expected;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ACellIsInsideWhenASubjectCellIsWithinTheRadius(int width, int height, int seed, int percent, int squaredRadius)
    {
        var mask = Masks.Random(width, height, seed, percent);
        var wordsPerRow = BitGrid.WordsPerRow(width);
        var result = new ulong[wordsPerRow * height];

        new DiscDilation().Dilate(Masks.ToBits(mask, width, height), width, height, squaredRadius, result);

        Assert.Equal(Expected(mask, width, height, squaredRadius), Masks.ToBytes(result, width, height));
    }

    public static readonly TheoryData<int, int, int, int, int> Large = new()
    {
        { 300, 200, 1, 6, 4900 },
        { 257, 150, 2, 5, 16900 },
        { 64, 64, 3, 3, 4900 },
        { 130, 90, 4, 8, 1600 },
        { 193, 40, 5, 4, 4096 },
    };

    [Theory]
    [MemberData(nameof(Large))]
    public void ALargeDiscCrossesWordBoundariesLikeAStampedDisc(int width, int height, int seed, int count, int squaredRadius)
    {
        var mask = new byte[width * height];
        var expected = new byte[width * height];
        var state = (uint)seed * 2654435761u + 3u;
        var subjects = new List<(int X, int Y)> { (0, 0), (width - 1, height - 1), (Math.Min(63, width - 1), height / 2), (Math.Min(64, width - 1), height / 3) };
        for (var i = 0; i < count; i++)
        {
            state = state * 1664525u + 1013904223u;
            var x = (int)((state >> 8) % (uint)width);
            state = state * 1664525u + 1013904223u;
            subjects.Add((x, (int)((state >> 8) % (uint)height)));
        }

        var radius = (int)Math.Floor(Math.Sqrt(squaredRadius));
        foreach (var (x, y) in subjects)
        {
            mask[y * width + x] = 1;
            for (var dy = -radius; dy <= radius; dy++)
            {
                var half = (int)Math.Floor(Math.Sqrt(squaredRadius - dy * dy));
                if (y + dy < 0 || y + dy >= height)
                    continue;
                for (var dx = Math.Max(-half, -x); dx <= Math.Min(half, width - 1 - x); dx++)
                    expected[(y + dy) * width + x + dx] = 1;
            }
        }

        var result = new ulong[BitGrid.WordsPerRow(width) * height];

        new DiscDilation().Dilate(Masks.ToBits(mask, width, height), width, height, squaredRadius, result);

        Assert.Equal(expected, Masks.ToBytes(result, width, height));
    }

    [Fact]
    public void ASingleCellBecomesAnIntegerDisc()
    {
        var mask = new byte[41 * 41];
        mask[20 * 41 + 20] = 1;
        var result = new ulong[BitGrid.WordsPerRow(41) * 41];

        new DiscDilation().Dilate(Masks.ToBits(mask, 41, 41), 41, 41, 100, result);

        var inside = Masks.ToBytes(result, 41, 41);
        for (var y = 0; y < 41; y++)
        {
            for (var x = 0; x < 41; x++)
                Assert.Equal((x - 20) * (x - 20) + (y - 20) * (y - 20) <= 100, inside[y * 41 + x] != 0);
        }
    }

    [Fact]
    public void AMaskWithoutSubjectStaysEmpty()
    {
        var result = Enumerable.Repeat(ulong.MaxValue, BitGrid.WordsPerRow(90) * 30).ToArray();

        new DiscDilation().Dilate(new ulong[BitGrid.WordsPerRow(90) * 30], 90, 30, 49, result);

        Assert.All(result, word => Assert.Equal(0UL, word));
    }

    [Fact]
    public void NoBitBeyondTheWidthIsEverSet()
    {
        const int width = 70;
        const int height = 12;
        var mask = new byte[width * height];
        mask[5 * width + 69] = 1;
        mask[6 * width + 0] = 1;
        var result = new ulong[BitGrid.WordsPerRow(width) * height];

        new DiscDilation().Dilate(Masks.ToBits(mask, width, height), width, height, 64, result);

        for (var y = 0; y < height; y++)
            Assert.Equal(0UL, result[y * 2 + 1] >> (width - 64));
        Assert.True(BitGrid.Get(result, 2, 69, 5));
        Assert.True(BitGrid.Get(result, 2, 0, 6));
    }

    [Fact]
    public void TheSameInstanceDilatesDifferentSizesCorrectly()
    {
        var dilation = new DiscDilation();
        var large = Masks.Random(140, 60, 3, 2);
        var small = Masks.Random(9, 14, 4, 20);
        var largeResult = new ulong[BitGrid.WordsPerRow(140) * 60];
        var smallResult = new ulong[BitGrid.WordsPerRow(9) * 14];
        var again = new ulong[largeResult.Length];

        dilation.Dilate(Masks.ToBits(large, 140, 60), 140, 60, 144, largeResult);
        dilation.Dilate(Masks.ToBits(small, 9, 14), 9, 14, 9, smallResult);
        dilation.Dilate(Masks.ToBits(large, 140, 60), 140, 60, 144, again);

        Assert.Equal(largeResult, again);
        Assert.Equal(Expected(small, 9, 14, 9), Masks.ToBytes(smallResult, 9, 14));
    }
}
