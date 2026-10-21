using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace ScissorOutline.Tests;

public sealed class ScissorOutlineEffectTests
{
    static PropertyInfo Property(string name) => typeof(ScissorOutlineEffect).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    [Theory]
    [InlineData(nameof(ScissorOutlineEffect.Thickness), 24d, 0d, 500d)]
    [InlineData(nameof(ScissorOutlineEffect.Coarseness), 50d, 0d, 100d)]
    [InlineData(nameof(ScissorOutlineEffect.Irregularity), 50d, 0d, 100d)]
    [InlineData(nameof(ScissorOutlineEffect.Tremor), 30d, 0d, 100d)]
    [InlineData(nameof(ScissorOutlineEffect.Opacity), 100d, 0d, 100d)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var effect = new ScissorOutlineEffect();

        var animation = (Animation)Property(name).GetValue(effect)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, EffectDescriptions.Fps));
    }

    [Fact]
    public void TheOtherParametersStartFromTheirDefaults()
    {
        var effect = new ScissorOutlineEffect();

        Assert.Equal(0, effect.Seed);
        Assert.Equal(3, effect.HoldFrames);
        Assert.True(effect.FillHoles);
        Assert.Equal(ScissorOutlineQuality.High, effect.Quality);
        Assert.Equal(Colors.White, effect.PaperColor);
    }

    [Theory]
    [InlineData(int.MinValue, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(10000, 10000)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void SeedNeverDropsBelowZero(int value, int expected)
    {
        var effect = new ScissorOutlineEffect { Seed = 5 };

        effect.Seed = value;

        Assert.Equal(expected, effect.Seed);
        Assert.False(effect.HasErrors);
    }

    [Theory]
    [InlineData(int.MinValue, 1)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(12, 12)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void HoldFramesNeverDropBelowOne(int value, int expected)
    {
        var effect = new ScissorOutlineEffect { HoldFrames = 5 };

        effect.HoldFrames = value;

        Assert.Equal(expected, effect.HoldFrames);
        Assert.False(effect.HasErrors);
    }

    [Fact]
    public void ChangingASettingNotifiesTheEditor()
    {
        var effect = new ScissorOutlineEffect();
        var changed = new List<string?>();
        effect.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        effect.Seed = 7;
        effect.HoldFrames = 2;
        effect.FillHoles = false;
        effect.Quality = ScissorOutlineQuality.Ultra;
        effect.PaperColor = Colors.Red;

        Assert.Equal([nameof(ScissorOutlineEffect.Seed), nameof(ScissorOutlineEffect.HoldFrames), nameof(ScissorOutlineEffect.FillHoles), nameof(ScissorOutlineEffect.Quality), nameof(ScissorOutlineEffect.PaperColor)], changed);
    }

    [Fact]
    public void AssigningAnUnchangedOrClampedValueDoesNotNotify()
    {
        var effect = new ScissorOutlineEffect { HoldFrames = 1 };
        var changed = new List<string?>();
        effect.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        effect.Seed = 0;
        effect.Seed = -1;
        effect.HoldFrames = 1;
        effect.HoldFrames = -4;
        effect.FillHoles = true;
        effect.Quality = ScissorOutlineQuality.High;
        effect.PaperColor = Colors.White;

        Assert.Empty(changed);
    }

    [Fact]
    public void TheLabelIsTheLocalizedEffectName()
    {
        var effect = new ScissorOutlineEffect();

        Assert.Equal(Texts.ScissorOutline, effect.Label);
    }

    [Fact]
    public void TheFiveNumericParametersReceiveTheAnimationParameters()
    {
        var effect = new ScissorOutlineEffect();

        effect.SetAnimationParameters(120, EffectDescriptions.Fps);

        Assert.All([effect.Thickness, effect.Coarseness, effect.Irregularity, effect.Tremor, effect.Opacity], animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var effect = new ScissorOutlineEffect();

        var description = new ExoOutputDescription(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

        Assert.Empty(effect.CreateExoVideoFilters(keyFrameIndex, description));
    }

    [Fact]
    public void TheEffectIsRegisteredForDecorationAndFilteringWithoutAviUtlSupport()
    {
        var attribute = typeof(ScissorOutlineEffect).GetCustomAttribute<VideoEffectAttribute>()!;

        Assert.Equal(nameof(Texts.ScissorOutline), attribute.Name);
        Assert.Equal([VideoEffectCategories.Decoration, VideoEffectCategories.Filtering], attribute.Categories);
        Assert.Equal([nameof(Texts.TagOutline), nameof(Texts.TagScissors), nameof(Texts.TagPolygon)], attribute.Keywords);
        Assert.False(attribute.IsAviUtlSupported);
        Assert.True(attribute.IsEffectItemSupported);
        Assert.Equal(typeof(Texts), attribute.ResourceType);
        Assert.Equal(Texts.ScissorOutline, attribute.GetName());
    }

    [Fact]
    public void TheAuthorIsDeclared()
    {
        var details = typeof(ScissorOutlineEffect).GetCustomAttribute<PluginDetailsAttribute>()!;

        Assert.Equal("routersys", details.AuthorName);
    }

    [Theory]
    [InlineData(nameof(ScissorOutlineEffect.Thickness), nameof(Texts.BasicGroup), nameof(Texts.Thickness), nameof(Texts.ThicknessDescription), 0)]
    [InlineData(nameof(ScissorOutlineEffect.Coarseness), nameof(Texts.BasicGroup), nameof(Texts.Coarseness), nameof(Texts.CoarsenessDescription), 1)]
    [InlineData(nameof(ScissorOutlineEffect.Irregularity), nameof(Texts.BasicGroup), nameof(Texts.Irregularity), nameof(Texts.IrregularityDescription), 2)]
    [InlineData(nameof(ScissorOutlineEffect.Tremor), nameof(Texts.BasicGroup), nameof(Texts.Tremor), nameof(Texts.TremorDescription), 3)]
    [InlineData(nameof(ScissorOutlineEffect.HoldFrames), nameof(Texts.BasicGroup), nameof(Texts.HoldFrames), nameof(Texts.HoldFramesDescription), 4)]
    [InlineData(nameof(ScissorOutlineEffect.Seed), nameof(Texts.BasicGroup), nameof(Texts.Seed), nameof(Texts.SeedDescription), 5)]
    [InlineData(nameof(ScissorOutlineEffect.FillHoles), nameof(Texts.BasicGroup), nameof(Texts.FillHoles), nameof(Texts.FillHolesDescription), 6)]
    [InlineData(nameof(ScissorOutlineEffect.Quality), nameof(Texts.BasicGroup), nameof(Texts.Quality), nameof(Texts.QualityDescription), 7)]
    [InlineData(nameof(ScissorOutlineEffect.PaperColor), nameof(Texts.PaperGroup), nameof(Texts.PaperColor), nameof(Texts.PaperColorDescription), 10)]
    [InlineData(nameof(ScissorOutlineEffect.Opacity), nameof(Texts.PaperGroup), nameof(Texts.Opacity), nameof(Texts.OpacityDescription), 11)]
    public void EveryParameterIsDisplayedInItsGroupInOrder(string property, string group, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Equal(group, display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(ScissorOutlineEffect.Thickness), "F1", "px", 0d, 200d)]
    [InlineData(nameof(ScissorOutlineEffect.Coarseness), "F1", "%", 0d, 100d)]
    [InlineData(nameof(ScissorOutlineEffect.Irregularity), "F1", "%", 0d, 100d)]
    [InlineData(nameof(ScissorOutlineEffect.Tremor), "F1", "%", 0d, 100d)]
    [InlineData(nameof(ScissorOutlineEffect.Opacity), "F1", "%", 0d, 100d)]
    public void AnimatedParametersAreEditedWithAnimationSliders(string property, string format, string unit, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal(format, slider.StringFormat);
        Assert.Equal(unit, slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Fact]
    public void HoldFramesAreEditedInFramesFromOneToTwelve()
    {
        var slider = Attribute<TextBoxSliderAttribute>(nameof(ScissorOutlineEffect.HoldFrames));
        var range = Attribute<RangeAttribute>(nameof(ScissorOutlineEffect.HoldFrames));

        Assert.Equal("F0", slider.StringFormat);
        Assert.Equal(nameof(Texts.FrameUnit), slider.UnitText);
        Assert.Equal(typeof(Texts), slider.ResourceType);
        Assert.Equal(1d, slider.DefaultMin);
        Assert.Equal(12d, slider.DefaultMax);
        Assert.Equal(1, range.Minimum);
        Assert.Equal(int.MaxValue, range.Maximum);
        Assert.Equal(3, Attribute<DefaultValueAttribute>(nameof(ScissorOutlineEffect.HoldFrames)).Value);
    }

    [Fact]
    public void TheSeedIsEditedWithoutAUnitFromZero()
    {
        var slider = Attribute<TextBoxSliderAttribute>(nameof(ScissorOutlineEffect.Seed));
        var range = Attribute<RangeAttribute>(nameof(ScissorOutlineEffect.Seed));

        Assert.Equal("F0", slider.StringFormat);
        Assert.Equal(string.Empty, slider.UnitText);
        Assert.Equal(0d, slider.DefaultMin);
        Assert.Equal(10000d, slider.DefaultMax);
        Assert.Equal(0, range.Minimum);
        Assert.Equal(int.MaxValue, range.Maximum);
        Assert.Equal(0, Attribute<DefaultValueAttribute>(nameof(ScissorOutlineEffect.Seed)).Value);
    }

    [Fact]
    public void TheOtherSettingsUseTheirOwnEditors()
    {
        Assert.NotNull(Attribute<ToggleSliderAttribute>(nameof(ScissorOutlineEffect.FillHoles)));
        Assert.NotNull(Attribute<EnumComboBoxAttribute>(nameof(ScissorOutlineEffect.Quality)));
        Assert.NotNull(Attribute<ColorPickerAttribute>(nameof(ScissorOutlineEffect.PaperColor)));
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var effect = new ScissorOutlineEffect
        {
            Seed = 42,
            HoldFrames = 6,
            FillHoles = false,
            Quality = ScissorOutlineQuality.Balanced,
            PaperColor = Color.FromArgb(200, 10, 20, 30),
        };
        effect.Thickness.Values[0].Value = 48d;
        effect.Coarseness.Values[0].Value = 80d;
        effect.Tremor.Values[0].Value = 5d;
        effect.Irregularity.Values[0].Value = 90d;
        effect.Opacity.Values[0].Value = 60d;

        var clone = Json.GetClone(effect)!;

        Assert.NotSame(effect, clone);
        Assert.Equal(42, clone.Seed);
        Assert.Equal(6, clone.HoldFrames);
        Assert.False(clone.FillHoles);
        Assert.Equal(ScissorOutlineQuality.Balanced, clone.Quality);
        Assert.Equal(Color.FromArgb(200, 10, 20, 30), clone.PaperColor);
        Assert.Equal(48d, clone.Thickness.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(80d, clone.Coarseness.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(5d, clone.Tremor.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(90d, clone.Irregularity.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(60d, clone.Opacity.GetValue(0, 1, EffectDescriptions.Fps));
    }
}
