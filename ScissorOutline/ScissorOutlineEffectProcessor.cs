using System.Numerics;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace ScissorOutline;

internal sealed class ScissorOutlineEffectProcessor(IGraphicsDevicesAndContext devices, ScissorOutlineEffect item) : VideoEffectProcessorBase(devices)
{
    private readonly IGraphicsDevicesAndContext _devices = devices;
    private readonly ScissorOutlineEffect _item = item;
    private readonly ScissorOutlineShape _shape = new();
    private readonly AlphaReadback _readback = new(devices);
    private AffineTransform2D? _output;
    private Composite? _composite;
    private ID2D1Image? _compositeOutput;
    private ID2D1CommandList? _paper;
    private PaperKey _paperKey;
    private bool _hasPaper;
    private Route _route;

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        try
        {
            return UpdateCore(effectDescription);
        }
        catch (Exception exception)
        {
            ScissorOutlineTelemetry.Report(exception);
            throw;
        }
    }

    private DrawDescription UpdateCore(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect || _output is null || _composite is null || input is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;
        var thickness = Math.Clamp(_item.Thickness.GetValue(frame, length, fps), 0.0, ScissorOutlineSettings.MaximumThickness);
        var color = _item.PaperColor;
        var opacity = Math.Clamp(_item.Opacity.GetValue(frame, length, fps) / 100.0, 0.0, 1.0) * color.A / byte.MaxValue;
        if (thickness <= 0.0 || opacity <= 0.0)
            return PassThrough(effectDescription);

        var dc = _devices.DeviceContext;
        var bounds = dc.GetImageLocalBounds(input);
        var boundsWidth = (double)bounds.Right - bounds.Left;
        var boundsHeight = (double)bounds.Bottom - bounds.Top;
        if (!(boundsWidth > 0.0 && boundsWidth <= ScissorOutlineSettings.MaximumInputSide && boundsHeight > 0.0 && boundsHeight <= ScissorOutlineSettings.MaximumInputSide))
            return PassThrough(effectDescription);

        var parameters = new ScissorOutlineParameters(
            thickness,
            Math.Clamp(_item.Coarseness.GetValue(frame, length, fps) / 100.0, 0.0, 1.0),
            Math.Clamp(_item.Tremor.GetValue(frame, length, fps) / 100.0, 0.0, 1.0),
            _item.Seed,
            _item.FillHoles,
            _item.Quality,
            Math.Clamp(_item.Irregularity.GetValue(frame, length, fps) / 100.0, 0.0, 1.0),
            (int)Math.Floor(frame / (double)Math.Max(_item.HoldFrames, 1)));
        var change = ReadAndBuild(bounds, (int)Math.Ceiling(boundsWidth), (int)Math.Ceiling(boundsHeight), parameters);
        if (_shape.LoopCount == 0)
            return PassThrough(effectDescription);

        var paperKey = new PaperKey(color.R, color.G, color.B, (float)opacity);
        if (change == ScissorOutlineShapeChange.Rebuilt || !_hasPaper || _paperKey != paperKey)
            RecordPaper(dc, paperKey);

        if (_route != Route.Paper)
        {
            _output.SetInput(0, _compositeOutput, true);
            _route = Route.Paper;
        }

        return effectDescription.DrawDescription;
    }

    private DrawDescription PassThrough(EffectDescription effectDescription)
    {
        if (_route != Route.PassThrough)
        {
            _output?.SetInput(0, input, true);
            _route = Route.PassThrough;
        }

        return effectDescription.DrawDescription;
    }

    private ScissorOutlineShapeChange ReadAndBuild(Vortice.RawRectF bounds, int width, int height, in ScissorOutlineParameters parameters)
    {
        var cell = ScissorOutlineSettings.CreatePlan(parameters, width, height).Cell;
        var level = Math.Min(BitOperations.Log2((uint)cell), AlphaReadback.MaximumLevel);
        var values = _readback.Read(bounds, width, height, level, out var readWidth, out var readHeight, out var stride);
        try
        {
            return level == 0
                ? _shape.Build(values, width, height, stride, bounds.Left, bounds.Top, parameters)
                : _shape.BuildPooled(values, readWidth, readHeight, stride, level, width, height, bounds.Left, bounds.Top, parameters);
        }
        finally
        {
            _readback.Unmap();
        }
    }

    private void RecordPaper(ID2D1DeviceContext dc, in PaperKey key)
    {
        using var geometry = _devices.D2D.Factory.CreatePathGeometry();
        using (var sink = geometry.Open())
        {
            sink.SetFillMode(FillMode.Winding);
            var vertices = _shape.Vertices;
            var starts = _shape.Starts;
            for (var loop = 0; loop < _shape.LoopCount; loop++)
            {
                var start = starts[loop];
                var end = starts[loop + 1];
                sink.BeginFigure(vertices[start], FigureBegin.Filled);
                for (var i = start + 1; i < end; i++)
                    sink.AddLine(vertices[i]);
                sink.EndFigure(FigureEnd.Closed);
            }

            sink.Close();
        }

        using var brush = dc.CreateSolidColorBrush(new Color4(key.Red / 255f, key.Green / 255f, key.Blue / 255f, key.Alpha));
        disposer.RemoveAndDispose(ref _paper);
        var paper = dc.CreateCommandList();
        disposer.Collect(paper);
        dc.Target = paper;
        dc.BeginDraw();
        dc.Clear(null);
        dc.FillGeometry(geometry, brush);
        dc.EndDraw().CheckError();
        dc.Target = null;
        paper.Close();
        _composite!.SetInput(0, paper, true);
        _paper = paper;
        _paperKey = key;
        _hasPaper = true;
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        disposer.Collect(_readback);
        var composite = new Composite(devices.DeviceContext) { InputCount = 2, Mode = CompositeMode.SourceOver };
        disposer.Collect(composite);
        var compositeOutput = composite.Output;
        disposer.Collect(compositeOutput);
        var output = new AffineTransform2D(devices.DeviceContext);
        disposer.Collect(output);
        var outputImage = output.Output;
        disposer.Collect(outputImage);
        _composite = composite;
        _compositeOutput = compositeOutput;
        _output = output;
        return outputImage;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _readback.SetInput(input);
        _composite?.SetInput(1, input, true);
        if (_route == Route.PassThrough)
            _output?.SetInput(0, input, true);
    }

    protected override void ClearEffectChain()
    {
        try
        {
            _readback.SetInput(null);
            _composite?.SetInput(0, null, true);
            _composite?.SetInput(1, null, true);
            _output?.SetInput(0, null, true);
            _route = Route.None;
            _hasPaper = false;
        }
        catch (Exception exception)
        {
            ScissorOutlineTelemetry.Report(exception);
            throw;
        }
    }

    private enum Route
    {
        None,
        PassThrough,
        Paper,
    }

    private readonly record struct PaperKey(byte Red, byte Green, byte Blue, float Alpha);
}
