using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;

namespace ScissorOutline.Tests;

[Collection("Direct2D")]
public sealed class AlphaReadbackTests
{
    static readonly byte[] Alphas = [0, 1, 8, 9, 255];

    static byte AlphaOf(int x, int y, int seed, int sparsity)
    {
        var state = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
        state = state * 1664525u + 1013904223u;
        state ^= state >> 15;
        state *= 2246822519u;
        state ^= state >> 13;
        return state % (uint)sparsity != 0 ? (byte)0 : Alphas[(state >> 8) % (uint)Alphas.Length];
    }

    static bool[] Expected(Func<int, int, byte> alphaOf, int width, int height, int level, out int pooledWidth, out int pooledHeight)
    {
        var block = 1 << level;
        pooledWidth = (width + block - 1) / block;
        pooledHeight = (height + block - 1) / block;
        var expected = new bool[pooledWidth * pooledHeight];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (alphaOf(x, y) > ScissorOutlineSettings.AlphaThreshold)
                    expected[(y >> level) * pooledWidth + (x >> level)] = true;
            }
        }

        return expected;
    }

    static bool[] Read(AlphaReadback readback, Vortice.RawRectF bounds, int width, int height, int level, out int pooledWidth, out int pooledHeight)
    {
        var values = readback.Read(bounds, width, height, level, out pooledWidth, out pooledHeight, out var stride);
        var result = new bool[pooledWidth * pooledHeight];
        for (var y = 0; y < pooledHeight; y++)
        {
            for (var x = 0; x < pooledWidth; x++)
                result[y * pooledWidth + x] = level == 0 ? values[y * stride + x] > ScissorOutlineSettings.AlphaThreshold : values[y * stride + x] != 0;
        }

        readback.Unmap();
        return result;
    }

    public static readonly TheoryData<int, int, int, int> Sizes = new()
    {
        { 97, 61, 300, 1 },
        { 130, 66, 200, 2 },
        { 1, 1, 1, 3 },
        { 33, 17, 1, 4 },
        { 256, 130, 900, 5 },
    };

    [Theory]
    [MemberData(nameof(Sizes))]
    public void EveryLevelMarksTheBlocksThatHoldAVisiblePixel(int width, int height, int sparsity, int seed)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        byte AlphaAt(int x, int y) => AlphaOf(x, y, seed, sparsity);
        using var source = new SourceImage(context, width, height, (x, y) => new Bgra(AlphaAt(x, y), AlphaAt(x, y), AlphaAt(x, y), AlphaAt(x, y)));
        using var readback = new AlphaReadback(context);
        readback.SetInput(source.Bitmap);
        var bounds = new Vortice.RawRectF(0, 0, width, height);

        foreach (var level in new[] { 0, 1, 2, 3, 4, 2, 0, 4, 1 })
        {
            var expected = Expected(AlphaAt, width, height, level, out var expectedWidth, out var expectedHeight);

            var actual = Read(readback, bounds, width, height, level, out var pooledWidth, out var pooledHeight);

            Assert.Equal((expectedWidth, expectedHeight), (pooledWidth, pooledHeight));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void ChangingTheSizeOfTheImageReadsTheNewSize()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var readback = new AlphaReadback(context);

        foreach (var (width, height) in new[] { (97, 61), (130, 66), (97, 61), (33, 90) })
        {
            byte AlphaAt(int x, int y) => AlphaOf(x, y, width, 120);
            using var source = new SourceImage(context, width, height, (x, y) => new Bgra(AlphaAt(x, y), AlphaAt(x, y), AlphaAt(x, y), AlphaAt(x, y)));
            readback.SetInput(source.Bitmap);

            foreach (var level in new[] { 3, 0, 2 })
            {
                var expected = Expected(AlphaAt, width, height, level, out var expectedWidth, out var expectedHeight);

                var actual = Read(readback, new Vortice.RawRectF(0, 0, width, height), width, height, level, out var pooledWidth, out var pooledHeight);

                Assert.Equal((expectedWidth, expectedHeight), (pooledWidth, pooledHeight));
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void ASinglePixelInEveryBlockSurvivesTheLargestLevel()
    {
        const int width = 64;
        const int height = 48;
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        byte AlphaAt(int x, int y) => (x % 16 == 5 && y % 16 == 9) ? (byte)(ScissorOutlineSettings.AlphaThreshold + 1) : (byte)0;
        using var source = new SourceImage(context, width, height, (x, y) => new Bgra(AlphaAt(x, y), AlphaAt(x, y), AlphaAt(x, y), AlphaAt(x, y)));
        using var readback = new AlphaReadback(context);
        readback.SetInput(source.Bitmap);

        var actual = Read(readback, new Vortice.RawRectF(0, 0, width, height), width, height, AlphaReadback.MaximumLevel, out var pooledWidth, out var pooledHeight);

        Assert.Equal((4, 3), (pooledWidth, pooledHeight));
        Assert.All(actual, set => Assert.True(set));
    }

    [Fact]
    public void ADenseImageStaysSetAtEveryLevel()
    {
        const int width = 50;
        const int height = 37;
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, width, height, (_, _) => new Bgra(255, 255, 255, 255));
        using var readback = new AlphaReadback(context);
        readback.SetInput(source.Bitmap);

        foreach (var level in Enumerable.Range(1, AlphaReadback.MaximumLevel))
            Assert.All(Read(readback, new Vortice.RawRectF(0, 0, width, height), width, height, level, out _, out _), set => Assert.True(set));
    }

    [Fact]
    public void AnImageWithoutVisiblePixelsReadsAsEmpty()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 40, 30, (x, y) => new Bgra(8, 8, 8, ScissorOutlineSettings.AlphaThreshold));
        using var readback = new AlphaReadback(context);
        readback.SetInput(source.Bitmap);

        foreach (var level in Enumerable.Range(0, AlphaReadback.MaximumLevel + 1))
            Assert.All(Read(readback, new Vortice.RawRectF(0, 0, 40, 30), 40, 30, level, out _, out _), set => Assert.False(set));
    }

    [Fact]
    public void TheBoundsOfTheInputDecideWhichPixelsAreRead()
    {
        const int width = 61;
        const int height = 45;
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        byte AlphaAt(int x, int y) => AlphaOf(x, y, 9, 40);
        using var source = new SourceImage(context, width, height, (x, y) => new Bgra(AlphaAt(x, y), AlphaAt(x, y), AlphaAt(x, y), AlphaAt(x, y)));
        using var moved = new AffineTransform2D(context.DeviceContext) { TransformMatrix = Matrix3x2.CreateTranslation(-37f, 11f) };
        moved.SetInput(0, source.Bitmap, true);
        using var output = moved.Output;
        var bounds = context.DeviceContext.GetImageLocalBounds(output);
        using var readback = new AlphaReadback(context);
        readback.SetInput(output);

        foreach (var level in new[] { 0, 3 })
        {
            var expected = Expected(AlphaAt, width, height, level, out _, out _);

            var actual = Read(readback, bounds, width, height, level, out _, out _);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void ALevelBeyondTheMaximumIsRefused()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var readback = new AlphaReadback(context);

        Assert.Throws<ArgumentOutOfRangeException>(() => readback.Read(new Vortice.RawRectF(0, 0, 8, 8), 8, 8, AlphaReadback.MaximumLevel + 1, out _, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => readback.Read(new Vortice.RawRectF(0, 0, 8, 8), 8, 8, -1, out _, out _, out _));
    }
}
