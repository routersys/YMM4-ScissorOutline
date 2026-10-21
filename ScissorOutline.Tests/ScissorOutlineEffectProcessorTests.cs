using System.Globalization;
using System.Numerics;
using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Player.Video;

namespace ScissorOutline.Tests;

[Collection("Direct2D")]
public sealed class ScissorOutlineEffectProcessorTests
{
    const int Width = 128;
    const int Height = 96;
    const int Length = 30;
    const int Tolerance = 2;

    static readonly Bgra Subject = Bgra.Opaque(200, 80, 30);

    static Bgra Disc(int x, int y)
        => (x + 0.5 - 64) * (x + 0.5 - 64) + (y + 0.5 - 48) * (y + 0.5 - 48) <= 18 * 18 ? Subject : Bgra.Transparent;

    static Bgra MovedDisc(int x, int y)
        => (x + 0.5 - 40) * (x + 0.5 - 40) + (y + 0.5 - 30) * (y + 0.5 - 30) <= 12 * 12 ? Subject : Bgra.Transparent;

    static Bgra Nothing(int x, int y) => Bgra.Transparent;

    static Bgra Veil(int x, int y)
        => (x + 0.5 - 64) * (x + 0.5 - 64) + (y + 0.5 - 48) * (y + 0.5 - 48) <= 18 * 18 ? new Bgra(0, 0, 0, 128) : Bgra.Transparent;

    static ScissorOutlineEffect Cut(double thickness = 12d)
    {
        var effect = new ScissorOutlineEffect();
        effect.Thickness.Values[0].Value = thickness;
        return effect;
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static Rendering RenderFrame(IGraphicsDevicesAndContext devices, IVideoEffectProcessor processor, int frame)
    {
        processor.Update(EffectDescriptions.At(frame, Length));
        return Rendering.Capture(devices, processor.Output);
    }

    static Rendering RenderFresh(IGraphicsDevicesAndContext devices, ScissorOutlineEffect effect, SourceImage source, int frame)
    {
        using var processor = effect.CreateVideoEffect(devices);
        processor.SetInput(source.Bitmap);
        return RenderFrame(devices, processor, frame);
    }

    static (double X, double Y)[] SubjectCenters(Func<int, int, Bgra> pixelOf, int width, int height)
        => Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width).Where(x => pixelOf(x, y).Alpha > ScissorOutlineSettings.AlphaThreshold).Select(x => (x + 0.5, y + 0.5))).ToArray();

    static void AssertClose(Bgra expected, Bgra actual, string where)
    {
        Assert.True(Math.Abs(expected.Blue - actual.Blue) <= Tolerance, $"{where} blue {expected} {actual}");
        Assert.True(Math.Abs(expected.Green - actual.Green) <= Tolerance, $"{where} green {expected} {actual}");
        Assert.True(Math.Abs(expected.Red - actual.Red) <= Tolerance, $"{where} red {expected} {actual}");
        Assert.True(Math.Abs(expected.Alpha - actual.Alpha) <= Tolerance, $"{where} alpha {expected} {actual}");
    }

    [Fact]
    public void TheProcessorHandsTheDrawDescriptionBackUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);
        using var processor = Cut().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var description = EffectDescriptions.At(0, Length);

        var draw = processor.Update(description);

        Assert.Same(description.DrawDescription, draw);
    }

    [Fact]
    public void WithoutThicknessTheProcessorPassesTheImageThrough()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);
        using var processor = Cut(0d).CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal((0, 0, Width, Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Fact]
    public void WithoutOpacityOrWithATransparentColorTheProcessorPassesTheImageThrough()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);
        var hidden = Cut();
        hidden.Opacity.Values[0].Value = 0d;
        var clear = Cut();
        clear.PaperColor = Color.FromArgb(0, 255, 255, 255);

        foreach (var effect in new[] { hidden, clear })
        {
            var rendering = RenderFresh(context, effect, source, 0);

            Assert.Equal((0, 0, Width, Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
            Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
        }
    }

    [Fact]
    public void ThePaperCoversEveryPixelWithinTheThicknessOfTheSubject()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);
        var centers = SubjectCenters(Disc, Width, Height);

        var rendering = RenderFresh(context, Cut(12d), source, 0);

        Assert.True(rendering.Left <= 0 && rendering.Top <= 0 && rendering.Right >= Width && rendering.Bottom >= Height);
        Assert.All(rendering.Coordinates(), point =>
        {
            if (PolygonGeometry.DistanceToNearest(centers, point.X + 0.5, point.Y + 0.5) <= 12d - 1.5d)
                Assert.Equal(byte.MaxValue, rendering[point.X, point.Y].Alpha);
        });
    }

    [Fact]
    public void ThePaperStaysWithinTheBoundOfTheEpsilonTheTremorTheSpreadAndTheCell()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);
        var centers = SubjectCenters(Disc, Width, Height);
        var effect = Cut(12d);
        var plan = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(12d, ScissorOutlineSettings.DefaultCoarseness / 100d, ScissorOutlineSettings.DefaultTremor / 100d, 0, true, ScissorOutlineQuality.High, ScissorOutlineSettings.DefaultIrregularity / 100d, 0, ScissorOutlineSettings.DefaultThicknessSpread, ScissorOutlineSettings.DefaultSpreadWavelength, ScissorOutlineSettings.DefaultSpreadChange / 100d), Width, Height);
        var limit = 12d + plan.ThicknessSpread + 3 * plan.Cell + 4 * (plan.Epsilon + plan.Tremor + plan.Distance) + 2d;

        var rendering = RenderFresh(context, effect, source, 0);

        Assert.All(rendering.Coordinates(), point =>
        {
            if (PolygonGeometry.DistanceToNearest(centers, point.X + 0.5, point.Y + 0.5) > limit)
                Assert.Equal(0, rendering[point.X, point.Y].Alpha);
        });
        Assert.Contains(rendering.Coordinates(), point => PolygonGeometry.DistanceToNearest(centers, point.X + 0.5, point.Y + 0.5) > 12d && rendering[point.X, point.Y].Alpha == byte.MaxValue);
    }

    [Fact]
    public void TheSubjectKeepsItsColorOnTopOfThePaper()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);

        var rendering = RenderFresh(context, Cut(), source, 0);

        Assert.All(rendering.Coordinates().Where(point => source.Contains(point.X, point.Y) && source[point.X, point.Y].Alpha == byte.MaxValue), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Fact]
    public void ThePaperTakesTheChosenColorAndOpacity()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);
        var effect = Cut();
        effect.PaperColor = Colors.Red;
        effect.Opacity.Values[0].Value = 50d;

        var rendering = RenderFresh(context, effect, source, 0);

        AssertClose(new Bgra(0, 0, 128, 128), rendering[64 + 18 + 5, 48], "paper");
    }

    [Fact]
    public void TheOpacityOfTheColorMultipliesTheOpacityOfThePaper()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);
        var effect = Cut();
        effect.PaperColor = Color.FromArgb(128, 255, 255, 255);
        effect.Opacity.Values[0].Value = 50d;

        var rendering = RenderFresh(context, effect, source, 0);

        AssertClose(new Bgra(64, 64, 64, 64), rendering[64 + 18 + 5, 48], "paper");
    }

    [Fact]
    public void ASemiTransparentSubjectIsComposedOverThePaper()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Veil);

        var rendering = RenderFresh(context, Cut(), source, 0);

        AssertClose(new Bgra(127, 127, 127, 255), rendering[64, 48], "veil");
    }

    [Fact]
    public void ReturningToAFrameReproducesItExactly()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Cut();
        effect.Thickness.CopyFrom(Linear(6d, 30d));
        using var source = new SourceImage(context, Width, Height, Disc);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 5);
        var other = RenderFrame(context, processor, 20);
        var again = RenderFrame(context, processor, 5);

        Assert.False(first.SamePixelsAs(other));
        Assert.True(first.SamePixelsAs(again));
    }

    [Fact]
    public void ANewProcessorDrawsTheSamePaper()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);

        var first = RenderFresh(context, Cut(), source, 0);
        var second = RenderFresh(context, Cut(), source, 0);

        Assert.True(first.SamePixelsAs(second));
    }

    [Fact]
    public void AnimatedThicknessIsReadAtEachFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Cut();
        effect.Thickness.CopyFrom(Linear(6d, 30d));
        using var source = new SourceImage(context, Width, Height, Disc);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var start = RenderFrame(context, processor, 0);
        var end = RenderFrame(context, processor, Length - 1);

        Assert.True(end.Width > start.Width);
        Assert.True(end.Height > start.Height);
    }

    public static readonly TheoryData<string, Action<ScissorOutlineEffect>, bool> Changes = new()
    {
        { nameof(ScissorOutlineEffect.Thickness), effect => effect.Thickness.Values[0].Value = 30d, true },
        { nameof(ScissorOutlineEffect.ThicknessSpread), effect => effect.ThicknessSpread.Values[0].Value = 60d, true },
        { nameof(ScissorOutlineEffect.SpreadWavelength), effect => effect.SpreadWavelength.Values[0].Value = 40d, true },
        { nameof(ScissorOutlineEffect.SpreadChange), effect => effect.SpreadChange.Values[0].Value = 100d, true },
        { nameof(ScissorOutlineEffect.Coarseness), effect => effect.Coarseness.Values[0].Value = 100d, true },
        { nameof(ScissorOutlineEffect.Irregularity), effect => effect.Irregularity.Values[0].Value = 100d, true },
        { nameof(ScissorOutlineEffect.Tremor), effect => effect.Tremor.Values[0].Value = 100d, true },
        { nameof(ScissorOutlineEffect.HoldFrames), effect => effect.HoldFrames = 1, true },
        { nameof(ScissorOutlineEffect.Seed), effect => effect.Seed = 7, true },
        { nameof(ScissorOutlineEffect.PaperColor), effect => effect.PaperColor = Colors.Red, true },
        { nameof(ScissorOutlineEffect.Opacity), effect => effect.Opacity.Values[0].Value = 40d, true },
        { nameof(ScissorOutlineEffect.FillHoles), effect => effect.FillHoles = false, false },
        { nameof(ScissorOutlineEffect.Quality), effect => effect.Quality = ScissorOutlineQuality.Balanced, false },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void EverySettingChangedAfterTheFirstFrameReachesTheEffect(string setting, Action<ScissorOutlineEffect> change, bool visible)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Cut();
        using var source = new SourceImage(context, Width, Height, Disc);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var before = RenderFrame(context, processor, 3);
        change(effect);
        var after = RenderFrame(context, processor, 3);

        var fresh = Cut();
        change(fresh);
        Assert.Equal(visible, !before.SamePixelsAs(after));
        Assert.True(after.SamePixelsAs(RenderFresh(context, fresh, source, 3)), setting);
    }

    [Fact]
    public void ReducingTheThicknessToZeroRestoresTheSourceAndRaisingItDrawsThePaperAgain()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Cut();
        using var source = new SourceImage(context, Width, Height, Disc);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var shown = RenderFrame(context, processor, 0);
        effect.Thickness.Values[0].Value = 0d;
        var hidden = RenderFrame(context, processor, 0);
        effect.Thickness.Values[0].Value = 12d;
        var shownAgain = RenderFrame(context, processor, 0);

        Assert.False(shown.SamePixelsAs(hidden));
        Assert.All(hidden.Coordinates(), point => Assert.Equal(source[point.X, point.Y], hidden[point.X, point.Y]));
        Assert.True(shown.SamePixelsAs(shownAgain));
    }

    [Fact]
    public void ADifferentImageAfterTheFirstFrameIsCutAgain()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var first = new SourceImage(context, Width, Height, Disc);
        using var second = new SourceImage(context, Width, Height, MovedDisc);
        using var processor = Cut().CreateVideoEffect(context);
        processor.SetInput(first.Bitmap);
        RenderFrame(context, processor, 0);

        processor.SetInput(second.Bitmap);
        var rendering = RenderFrame(context, processor, 0);

        Assert.True(rendering.SamePixelsAs(RenderFresh(context, Cut(), second, 0)));
    }

    [Fact]
    public void PaperAppearsOnceATransparentSourceTakesShape()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var empty = new SourceImage(context, Width, Height, Nothing);
        using var disc = new SourceImage(context, Width, Height, Disc);
        using var processor = Cut().CreateVideoEffect(context);
        processor.SetInput(empty.Bitmap);

        var nothing = RenderFrame(context, processor, 0);
        processor.SetInput(disc.Bitmap);
        var something = RenderFrame(context, processor, 0);

        Assert.All(nothing.Coordinates(), point => Assert.Equal(Bgra.Transparent, nothing[point.X, point.Y]));
        Assert.True(something.SamePixelsAs(RenderFresh(context, Cut(), disc, 0)));
        Assert.Equal(byte.MaxValue, something[64 + 18 + 5, 48].Alpha);
    }

    [Fact]
    public void ThePaperExtendsTheImageWhenTheSubjectTouchesItsEdge()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, (x, y) => (x + 0.5 - 8) * (x + 0.5 - 8) + (y + 0.5 - 48) * (y + 0.5 - 48) <= 18 * 18 ? Subject : Bgra.Transparent);

        var rendering = RenderFresh(context, Cut(12d), source, 0);

        Assert.True(rendering.Left <= -12);
        Assert.Equal(byte.MaxValue, rendering[-6, 48].Alpha);
        Assert.Equal(0, rendering[rendering.Left, rendering.Top].Alpha);
    }

    [Theory]
    [InlineData(100, 50)]
    [InlineData(-37, 21)]
    public void ThePaperTravelsWithTheImage(int dx, int dy)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Disc);
        using var moved = new AffineTransform2D(context.DeviceContext)
        {
            InterPolationMode = AffineTransform2DInterpolationMode.NearestNeighbor,
            BorderMode = BorderMode.Hard,
            TransformMatrix = Matrix3x2.CreateTranslation(dx, dy),
        };
        moved.SetInput(0, source.Bitmap, true);
        using var movedOutput = moved.Output;
        var inPlace = RenderFresh(context, Cut(), source, 0);
        using var travelledProcessor = Cut().CreateVideoEffect(context);
        travelledProcessor.SetInput(movedOutput);

        var travelled = RenderFrame(context, travelledProcessor, 0);

        Assert.Equal((inPlace.Left + dx, inPlace.Top + dy, inPlace.Width, inPlace.Height), (travelled.Left, travelled.Top, travelled.Width, travelled.Height));
        Assert.All(inPlace.Coordinates(), point => Assert.True(inPlace[point.X, point.Y] == travelled[point.X + dx, point.Y + dy], $"({point.X}, {point.Y})"));
    }

    [Fact]
    public void AnImageBeyondTheInputLimitPassesThrough()
    {
        const int width = ScissorOutlineSettings.MaximumInputSide + 4;
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, width, 2, (x, _) => x % 64 < 32 ? Subject : Bgra.Transparent);

        var rendering = RenderFresh(context, Cut(), source, 0);

        Assert.Equal((0, 0, width, 2), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Fact]
    public void AFailureWhileUpdatingIsNotSwallowed()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = Cut().CreateVideoEffect(context);

        Assert.ThrowsAny<Exception>(() => processor.Update(null!));
    }

    [Fact]
    public void AFailureWhileCreatingTheProcessorIsNotSwallowed()
    {
        Assert.ThrowsAny<Exception>(() => Cut().CreateVideoEffect(null!));
    }
}
