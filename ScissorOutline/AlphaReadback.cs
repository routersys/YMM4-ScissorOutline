using System.Numerics;
using SharpGen.Runtime;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace ScissorOutline;

internal sealed class AlphaReadback(IGraphicsDevicesAndContext devices) : IDisposable
{
    public const int MaximumLevel = 4;

    private const float Steepness = 10f;
    private const float ThresholdSlope = Steepness * byte.MaxValue;
    private const float ThresholdIntercept = -Steepness * (ScissorOutlineSettings.AlphaThreshold + 0.5f);
    private const float SaturationSlope = 4f;

    private readonly DisposeCollector _persistent = new();
    private DisposeCollector _chain = new();
    private ID2D1Image? _input;
    private LinearTransfer? _threshold;
    private ID2D1Image? _thresholdOutput;
    private ID2D1Bitmap1? _full;
    private ID2D1Bitmap1[] _levels = [];
    private ID2D1Image[] _steps = [];
    private ID2D1Bitmap1? _staging;
    private int _width;
    private int _height;
    private int _level = -1;

    public void SetInput(ID2D1Image? input)
    {
        _input = input;
        _threshold?.SetInput(0, input, true);
    }

    public unsafe ReadOnlySpan<byte> Read(Vortice.RawRectF bounds, int width, int height, int level, out int readWidth, out int readHeight, out int stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, MaximumLevel);
        EnsureChain(width, height, level);
        var dc = devices.DeviceContext;
        Draw(dc, _full!, level == 0 ? _input! : _thresholdOutput!, new Vector2(-bounds.Left, -bounds.Top));
        for (var step = 0; step < level; step++)
            Draw(dc, _levels[step], _steps[step], Vector2.Zero);
        dc.Target = null;
        _staging!.CopyFromBitmap(level == 0 ? _full! : _levels[level - 1]);
        var mapped = _staging.Map(MapOptions.Read);
        readWidth = _staging.PixelSize.Width;
        readHeight = _staging.PixelSize.Height;
        stride = (int)mapped.Pitch;
        return new ReadOnlySpan<byte>((void*)mapped.Bits, stride * readHeight);
    }

    public void Unmap() => _staging?.Unmap();

    public void Dispose()
    {
        _chain.Dispose();
        _persistent.Dispose();
    }

    private static T Keep<T>(DisposeCollector collector, T item)
        where T : IDisposable
    {
        collector.Collect(item);
        return item;
    }

    private static void Draw(ID2D1DeviceContext dc, ID2D1Bitmap1 target, ID2D1Image source, Vector2 offset)
    {
        dc.Target = target;
        dc.BeginDraw();
        dc.Clear(new Color4(0f, 0f, 0f, 0f));
        dc.DrawImage(source, offset, null, InterpolationMode.NearestNeighbor, CompositeMode.SourceCopy);
        dc.EndDraw().CheckError();
    }

    private void EnsureChain(int width, int height, int level)
    {
        var dc = devices.DeviceContext;
        if (level > 0)
            EnsureThreshold(dc);
        if (_full is not null && _width == width && _height == height && _level == level)
            return;

        _chain.Dispose();
        _chain = new DisposeCollector();
        var format = new PixelFormat(Format.A8_UNorm, Vortice.DCommon.AlphaMode.Straight);
        ID2D1Bitmap1 Create(int bitmapWidth, int bitmapHeight, BitmapOptions options)
            => Keep(_chain, dc.CreateBitmap(new SizeI(bitmapWidth, bitmapHeight), new BitmapProperties1(format, dc.Dpi.Width, dc.Dpi.Height, options)));

        _full = Create(width, height, BitmapOptions.Target);
        _levels = new ID2D1Bitmap1[level];
        _steps = new ID2D1Image[level];
        var levelWidth = width;
        var levelHeight = height;
        for (var step = 0; step < level; step++)
        {
            levelWidth = (levelWidth + 1) / 2;
            levelHeight = (levelHeight + 1) / 2;
            _levels[step] = Create(levelWidth, levelHeight, BitmapOptions.Target);
            var half = Keep(_chain, new AffineTransform2D(dc)
            {
                TransformMatrix = Matrix3x2.CreateScale(0.5f),
                InterPolationMode = AffineTransform2DInterpolationMode.Linear,
            });
            half.SetInput(0, step == 0 ? _full : _levels[step - 1], true);
            var halfOutput = Keep(_chain, half.Output);
            var saturate = Keep(_chain, new LinearTransfer(dc)
            {
                AlphaSlope = SaturationSlope,
                AlphaYIntercept = 0f,
                RedDisable = true,
                GreenDisable = true,
                BlueDisable = true,
                ClampOutput = true,
            });
            saturate.SetInput(0, halfOutput, true);
            _steps[step] = Keep(_chain, saturate.Output);
        }

        _staging = Create(level == 0 ? width : levelWidth, level == 0 ? height : levelHeight, BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
        _width = width;
        _height = height;
        _level = level;
    }

    private void EnsureThreshold(ID2D1DeviceContext dc)
    {
        if (_threshold is not null)
            return;

        var threshold = Keep(_persistent, new LinearTransfer(dc)
        {
            AlphaSlope = ThresholdSlope,
            AlphaYIntercept = ThresholdIntercept,
            RedDisable = true,
            GreenDisable = true,
            BlueDisable = true,
            ClampOutput = true,
        });
        threshold.SetInput(0, _input, true);
        _thresholdOutput = Keep(_persistent, threshold.Output);
        _threshold = threshold;
    }
}
