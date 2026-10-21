namespace ScissorOutline.Tests;

public sealed class CellMaskTests
{
    const int Padding = ScissorOutlineSettings.PaddingCells;

    static int IndexOf(int gridWidth, int cellX, int cellY) => (cellY + Padding) * gridWidth + cellX + Padding;

    static byte[] Build(byte[] alpha, int width, int height, int stride, int cell)
    {
        var gridWidth = (width + cell - 1) / cell + 2 * Padding;
        var gridHeight = (height + cell - 1) / cell + 2 * Padding;
        var bits = new ulong[BitGrid.WordsPerRow(gridWidth) * gridHeight];
        CellMask.Build(alpha, width, height, stride, cell, gridWidth, bits);
        return Masks.ToBytes(bits, gridWidth, gridHeight);
    }

    public static readonly TheoryData<int, int, int, int> Randoms = new()
    {
        { 1, 1, 1, 1 },
        { 15, 9, 1, 2 },
        { 16, 9, 2, 3 },
        { 17, 11, 2, 4 },
        { 31, 13, 1, 5 },
        { 33, 21, 2, 6 },
        { 130, 17, 1, 7 },
        { 131, 18, 2, 8 },
        { 200, 70, 4, 9 },
        { 201, 71, 8, 10 },
        { 300, 100, 16, 11 },
        { 301, 99, 32, 12 },
        { 257, 65, 64, 13 },
    };

    static ulong[] Scalar(byte[] alpha, int width, int height, int stride, int cell)
    {
        var gridWidth = (width + cell - 1) / cell + 2 * Padding;
        var gridHeight = (height + cell - 1) / cell + 2 * Padding;
        var wordsPerRow = BitGrid.WordsPerRow(gridWidth);
        var bits = new ulong[wordsPerRow * gridHeight];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (alpha[y * stride + x] > ScissorOutlineSettings.AlphaThreshold)
                    BitGrid.Set(bits, wordsPerRow, x / cell + Padding, y / cell + Padding);
            }
        }

        return bits;
    }

    [Theory]
    [MemberData(nameof(Randoms))]
    public void TheMaskMatchesAPerPixelReferenceForRandomImages(int width, int height, int cell, int seed)
    {
        var stride = width + 5;
        var alpha = new byte[stride * height];
        var state = (uint)seed * 2654435761u + 5u;
        for (var i = 0; i < alpha.Length; i++)
        {
            state = state * 1664525u + 1013904223u;
            var roll = (state >> 8) % 100;
            alpha[i] = roll < 6 ? (byte)((state >> 16) % 256) : roll < 8 ? (byte)(ScissorOutlineSettings.AlphaThreshold + (state >> 16) % 3) : (byte)0;
        }

        var gridWidth = (width + cell - 1) / cell + 2 * Padding;
        var gridHeight = (height + cell - 1) / cell + 2 * Padding;
        var bits = new ulong[BitGrid.WordsPerRow(gridWidth) * gridHeight];

        CellMask.Build(alpha, width, height, stride, cell, gridWidth, bits);

        Assert.Equal(Scalar(alpha, width, height, stride, cell), bits);
    }

    [Fact]
    public void ACellIsASubjectWhenAnyPixelOfItsBlockIsVisible()
    {
        const int width = 10;
        const int height = 9;
        const int stride = 12;
        var alpha = new byte[stride * height];
        alpha[6 * stride + 5] = 255;
        alpha[8 * stride + 9] = ScissorOutlineSettings.AlphaThreshold + 1;

        var cells = Build(alpha, width, height, stride, 4);

        var gridWidth = (width + 3) / 4 + 2 * Padding;
        var expected = new HashSet<int> { IndexOf(gridWidth, 1, 1), IndexOf(gridWidth, 2, 2) };
        Assert.Equal(expected, Enumerable.Range(0, cells.Length).Where(i => cells[i] != 0).ToHashSet());
    }

    [Fact]
    public void AnIsolatedPixelAwayFromTheBlockCornerIsNotLost()
    {
        var alpha = new byte[64 * 64];
        alpha[37 * 64 + 51] = 255;

        var cells = Build(alpha, 64, 64, 64, 8);

        var gridWidth = 8 + 2 * Padding;
        Assert.Equal(1, cells.Count(c => c != 0));
        Assert.Equal(1, cells[IndexOf(gridWidth, 6, 4)]);
    }

    [Fact]
    public void APixelAtTheThresholdOrBelowIsNotASubject()
    {
        var alpha = new byte[8 * 8];
        alpha[0] = ScissorOutlineSettings.AlphaThreshold;
        alpha[9] = 1;

        var cells = Build(alpha, 8, 8, 8, 2);

        Assert.All(cells, cell => Assert.Equal(0, cell));
    }

    [Fact]
    public void BytesBeyondTheWidthOfARowAreIgnored()
    {
        const int width = 5;
        const int stride = 9;
        var alpha = new byte[stride * 4];
        for (var y = 0; y < 4; y++)
            alpha[y * stride + 7] = 255;

        var cells = Build(alpha, width, 4, stride, 1);

        Assert.All(cells, cell => Assert.Equal(0, cell));
    }

    [Fact]
    public void WithOnePixelPerCellTheMaskIsTheVisibilityOfEachPixel()
    {
        const int width = 6;
        const int height = 5;
        var alpha = Silhouettes.Fill(width, height, (x, y) => (x + y) % 3 == 0);

        var cells = Build(alpha, width, height, width, 1);

        var gridWidth = width + 2 * Padding;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                Assert.Equal(alpha[y * width + x] != 0, cells[IndexOf(gridWidth, x, y)] != 0);
        }
    }

    [Fact]
    public void ThePaddingAroundTheImageStaysEmpty()
    {
        var alpha = Silhouettes.Fill(16, 16, (_, _) => true);

        var cells = Build(alpha, 16, 16, 16, 4);

        var gridWidth = 4 + 2 * Padding;
        Assert.Equal(16, cells.Count(c => c != 0));
        for (var y = 0; y < gridWidth; y++)
        {
            for (var x = 0; x < gridWidth; x++)
            {
                var inside = x >= Padding && x < Padding + 4 && y >= Padding && y < Padding + 4;
                Assert.Equal(inside, cells[y * gridWidth + x] != 0);
            }
        }
    }
}
