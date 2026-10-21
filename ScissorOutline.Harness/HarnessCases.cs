using System.Globalization;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;

namespace ScissorOutline.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, ScissorOutlineEffect Effect, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("thickness-animated-frames", Create(effect => effect.Thickness.CopyFrom(Linear(6d, 70d))), [0, 100, 200, 299]);
        yield return ("tremor-animated-frames", Create(effect => effect.Tremor.CopyFrom(Linear(0d, 100d))), [0, 100, 200, 299]);
        yield return ("thickness-0", Create(effect => effect.Thickness.Values[0].Value = 0), [0]);
        yield return ("thickness-8", Create(effect => effect.Thickness.Values[0].Value = 8), [0]);
        yield return ("thickness-60", Create(effect => effect.Thickness.Values[0].Value = 60), [0]);
        yield return ("thickness-160", Create(effect => effect.Thickness.Values[0].Value = 160), [0]);
        yield return ("coarseness-0", Create(effect => effect.Coarseness.Values[0].Value = 0), [0]);
        yield return ("coarseness-100", Create(effect => effect.Coarseness.Values[0].Value = 100), [0]);
        yield return ("tremor-0", Create(effect => effect.Tremor.Values[0].Value = 0), [0]);
        yield return ("tremor-100", Create(effect => effect.Tremor.Values[0].Value = 100), [0]);
        yield return ("seed-7", Create(effect => effect.Seed = 7), [0]);
        yield return ("quality-balanced-thickness-4", Create(effect => { effect.Thickness.Values[0].Value = 4; effect.Quality = ScissorOutlineQuality.Balanced; }), [0]);
        yield return ("quality-high-thickness-4", Create(effect => effect.Thickness.Values[0].Value = 4), [0]);
        yield return ("color-red", Create(effect => effect.PaperColor = Colors.Red), [0]);
        yield return ("color-translucent", Create(effect => effect.PaperColor = Color.FromArgb(128, 255, 255, 255)), [0]);
        yield return ("opacity-50", Create(effect => effect.Opacity.Values[0].Value = 50), [0]);
        yield return ("irregularity-0", Create(effect => effect.Irregularity.Values[0].Value = 0), [0]);
        yield return ("irregularity-100", Create(effect => effect.Irregularity.Values[0].Value = 100), [0]);
        yield return ("irregularity-animated-frames", Create(effect => effect.Irregularity.CopyFrom(Linear(0d, 100d))), [0, 100, 200, 299]);
        yield return ("hold-frames-3-frames", Create(), [0, 2, 3, 5, 6]);
        yield return ("hold-frames-1-frames", Create(effect => effect.HoldFrames = 1), [0, 1, 2, 3]);
    }

    public static IEnumerable<(string Name, Func<ScissorOutlineEffect> Create, Action<ScissorOutlineEffect> Change, int Frame)> Transitions()
    {
        yield return ("thickness-24-to-0", () => Create(), effect => effect.Thickness.Values[0].Value = 0, 0);
        yield return ("thickness-0-to-24", () => Create(effect => effect.Thickness.Values[0].Value = 0), effect => effect.Thickness.Values[0].Value = 24, 0);
        yield return ("thickness-24-to-60", () => Create(), effect => effect.Thickness.Values[0].Value = 60, 0);
        yield return ("thickness-24-to-25", () => Create(), effect => effect.Thickness.Values[0].Value = 25, 0);
        yield return ("coarseness-50-to-0", () => Create(), effect => effect.Coarseness.Values[0].Value = 0, 0);
        yield return ("tremor-30-to-100", () => Create(), effect => effect.Tremor.Values[0].Value = 100, 0);
        yield return ("seed-0-to-7", () => Create(), effect => effect.Seed = 7, 0);
        yield return ("quality-high-to-balanced-thickness-4", () => Create(effect => effect.Thickness.Values[0].Value = 4), effect => effect.Quality = ScissorOutlineQuality.Balanced, 0);
        yield return ("color-white-to-red", () => Create(), effect => effect.PaperColor = Colors.Red, 0);
        yield return ("opacity-100-to-50", () => Create(), effect => effect.Opacity.Values[0].Value = 50, 0);
        yield return ("opacity-100-to-0", () => Create(), effect => effect.Opacity.Values[0].Value = 0, 0);
        yield return ("opacity-0-to-100", () => Create(effect => effect.Opacity.Values[0].Value = 0), effect => effect.Opacity.Values[0].Value = 100, 0);
        yield return ("irregularity-50-to-0", () => Create(), effect => effect.Irregularity.Values[0].Value = 0, 0);
        yield return ("irregularity-50-to-100", () => Create(), effect => effect.Irregularity.Values[0].Value = 100, 0);
        yield return ("hold-frames-3-to-1", () => Create(), effect => effect.HoldFrames = 1, 7);
        yield return ("hold-frames-3-to-6", () => Create(), effect => effect.HoldFrames = 6, 7);
    }

    public static IEnumerable<(string Name, ScissorOutlineEffect Effect)> Benchmarks()
    {
        yield return ("default", Create());
        yield return ("thickness-animated", Create(effect => effect.Thickness.CopyFrom(Linear(6d, 70d))));
        yield return ("tremor-animated", Create(effect => effect.Tremor.CopyFrom(Linear(0d, 100d))));
        yield return ("quality-ultra", Create(effect => effect.Quality = ScissorOutlineQuality.Ultra));
        yield return ("thickness-0", Create(effect => effect.Thickness.Values[0].Value = 0));
        yield return ("irregularity-100", Create(effect => effect.Irregularity.Values[0].Value = 100));
        yield return ("hold-frames-1", Create(effect => effect.HoldFrames = 1));
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}""")) ?? throw new HarnessException("アニメーションを読み込めません。");

    public static ScissorOutlineEffect Create(Action<ScissorOutlineEffect>? configure = null)
    {
        var effect = new ScissorOutlineEffect();
        configure?.Invoke(effect);
        return effect;
    }
}
