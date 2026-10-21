using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace ScissorOutline;

[PluginDetails(AuthorName = "routersys")]
[VideoEffect(nameof(Texts.ScissorOutline), [VideoEffectCategories.Decoration, VideoEffectCategories.Filtering], [nameof(Texts.TagOutline), nameof(Texts.TagScissors), nameof(Texts.TagPolygon)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class ScissorOutlineEffect : VideoEffectBase
{
    public override string Label => Texts.ScissorOutline;

    public ScissorOutlineEffect()
    {
        ScissorOutlineTelemetry.EnsureStartedOnce();
        ScissorOutlineUpdateNotifier.EnsureCheckedOnce();
    }

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Thickness), Description = nameof(Texts.ThicknessDescription), Order = 0, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", 0, ScissorOutlineSettings.SliderMaximumThickness)]
    public Animation Thickness { get; } = new Animation(ScissorOutlineSettings.DefaultThickness, 0, ScissorOutlineSettings.MaximumThickness);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.ThicknessSpread), Description = nameof(Texts.ThicknessSpreadDescription), Order = 1, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", 0, ScissorOutlineSettings.SliderMaximumThicknessSpread)]
    public Animation ThicknessSpread { get; } = new Animation(ScissorOutlineSettings.DefaultThicknessSpread, 0, ScissorOutlineSettings.MaximumThicknessSpread);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.SpreadWavelength), Description = nameof(Texts.SpreadWavelengthDescription), Order = 2, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", ScissorOutlineSettings.MinimumSpreadWavelength, ScissorOutlineSettings.SliderMaximumSpreadWavelength)]
    public Animation SpreadWavelength { get; } = new Animation(ScissorOutlineSettings.DefaultSpreadWavelength, ScissorOutlineSettings.MinimumSpreadWavelength, ScissorOutlineSettings.MaximumSpreadWavelength);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.SpreadChange), Description = nameof(Texts.SpreadChangeDescription), Order = 3, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation SpreadChange { get; } = new Animation(ScissorOutlineSettings.DefaultSpreadChange, 0, 100);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Coarseness), Description = nameof(Texts.CoarsenessDescription), Order = 4, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Coarseness { get; } = new Animation(ScissorOutlineSettings.DefaultCoarseness, 0, 100);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Irregularity), Description = nameof(Texts.IrregularityDescription), Order = 5, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Irregularity { get; } = new Animation(ScissorOutlineSettings.DefaultIrregularity, 0, 100);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Tremor), Description = nameof(Texts.TremorDescription), Order = 6, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Tremor { get; } = new Animation(ScissorOutlineSettings.DefaultTremor, 0, 100);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.HoldFrames), Description = nameof(Texts.HoldFramesDescription), Order = 7, ResourceType = typeof(Texts))]
    [Range(1, int.MaxValue)]
    [DefaultValue(ScissorOutlineSettings.DefaultHoldFrames)]
    [TextBoxSlider("F0", nameof(Texts.FrameUnit), 1, ScissorOutlineSettings.HoldFramesSliderMaximum, ResourceType = typeof(Texts))]
    public int HoldFrames
    {
        get => _holdFrames;
        set => Set(ref _holdFrames, Math.Max(value, 1));
    }
    private int _holdFrames = ScissorOutlineSettings.DefaultHoldFrames;

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Seed), Description = nameof(Texts.SeedDescription), Order = 8, ResourceType = typeof(Texts))]
    [Range(0, int.MaxValue)]
    [DefaultValue(0)]
    [TextBoxSlider("F0", "", 0, ScissorOutlineSettings.SeedSliderMaximum)]
    public int Seed
    {
        get => _seed;
        set => Set(ref _seed, Math.Max(value, 0));
    }
    private int _seed;

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.FillHoles), Description = nameof(Texts.FillHolesDescription), Order = 9, ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool FillHoles { get => _fillHoles; set => Set(ref _fillHoles, value); }
    private bool _fillHoles = true;

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Quality), Description = nameof(Texts.QualityDescription), Order = 10, ResourceType = typeof(Texts))]
    [EnumComboBox]
    public ScissorOutlineQuality Quality { get => _quality; set => Set(ref _quality, value); }
    private ScissorOutlineQuality _quality = ScissorOutlineQuality.High;

    [Display(GroupName = nameof(Texts.PaperGroup), Name = nameof(Texts.PaperColor), Description = nameof(Texts.PaperColorDescription), Order = 10, ResourceType = typeof(Texts))]
    [ColorPicker]
    public Color PaperColor { get => _paperColor; set => Set(ref _paperColor, value); }
    private Color _paperColor = Colors.White;

    [Display(GroupName = nameof(Texts.PaperGroup), Name = nameof(Texts.Opacity), Description = nameof(Texts.OpacityDescription), Order = 11, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Opacity { get; } = new Animation(ScissorOutlineSettings.DefaultOpacity, 0, 100);

    private IAnimatable[]? _animatables;

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        try
        {
            return new ScissorOutlineEffectProcessor(devices, this);
        }
        catch (Exception exception)
        {
            ScissorOutlineTelemetry.Report(exception);
            throw;
        }
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => _animatables ??= [Thickness, ThicknessSpread, SpreadWavelength, SpreadChange, Coarseness, Irregularity, Tremor, Opacity];
}
