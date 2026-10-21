namespace ScissorOutline.Tests;

public sealed class ScissorOutlineJitterTests
{
    [Fact]
    public void TheShiftUnitStaysInTheHalfOpenIntervalAndDependsOnEveryArgument()
    {
        var sum = 0.0;
        const int count = 20000;
        for (var i = 0; i < count; i++)
        {
            var value = ScissorOutlineJitter.GetShiftUnit(i, i * 3 - 7, i % 13, -i, 11u);

            Assert.InRange(value, 0.0, 1.0 - 1e-9);
            sum += value;
        }

        var baseline = ScissorOutlineJitter.GetShiftUnit(3, 5, 7, 9, 1u);
        Assert.InRange(sum / count, 0.48, 0.52);
        Assert.NotEqual(baseline, ScissorOutlineJitter.GetShiftUnit(4, 5, 7, 9, 1u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.GetShiftUnit(3, 6, 7, 9, 1u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.GetShiftUnit(3, 5, 8, 9, 1u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.GetShiftUnit(3, 5, 7, 10, 1u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.GetShiftUnit(3, 5, 7, 9, 2u));
        Assert.Equal(baseline, ScissorOutlineJitter.GetShiftUnit(3, 5, 7, 9, 1u));
    }

    [Fact]
    public void TheShiftUnitDiffersFromTheOtherRandomValuesOfTheSameArguments()
    {
        Assert.NotEqual(ScissorOutlineJitter.GetShiftUnit(3, 5, 7, 9, 1u), ScissorOutlineJitter.GetSplitUnit(3, 5, 7, 9, 1u));
        Assert.NotEqual(ScissorOutlineJitter.GetShiftUnit(3, 5, 7, 9, 1u), ScissorOutlineJitter.Unit(3, 5, 7, 9, 1u));
    }

    [Fact]
    public void TheFirstFrameUsesTheSeedAloneAndOtherFramesDifferFromIt()
    {
        Assert.Equal(5u, ScissorOutlineJitter.Variation(5, 0));
        Assert.NotEqual(ScissorOutlineJitter.Variation(5, 0), ScissorOutlineJitter.Variation(5, 1));
        Assert.NotEqual(ScissorOutlineJitter.Variation(5, 1), ScissorOutlineJitter.Variation(5, 2));
        Assert.NotEqual(ScissorOutlineJitter.Variation(5, 1), ScissorOutlineJitter.Variation(6, 1));
        Assert.Equal(ScissorOutlineJitter.Variation(5, 3), ScissorOutlineJitter.Variation(5, 3));
    }

    [Fact]
    public void TheUnitValueStaysInTheHalfOpenIntervalWithAMeanNearOneHalf()
    {
        var sum = 0.0;
        const int count = 20000;
        for (var i = 0; i < count; i++)
        {
            var value = ScissorOutlineJitter.Unit(i, i * 3 - 7, i % 13, -i, 11u);

            Assert.InRange(value, 0.0, 1.0 - 1e-9);
            sum += value;
        }

        Assert.InRange(sum / count, 0.48, 0.52);
    }

    [Fact]
    public void EveryArgumentOfTheUnitValueMattersAndOrderMatters()
    {
        var baseline = ScissorOutlineJitter.Unit(3, 5, 7, 9, 1u);

        Assert.NotEqual(baseline, ScissorOutlineJitter.Unit(4, 5, 7, 9, 1u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.Unit(3, 6, 7, 9, 1u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.Unit(3, 5, 8, 9, 1u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.Unit(3, 5, 7, 10, 1u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.Unit(3, 5, 7, 9, 2u));
        Assert.NotEqual(baseline, ScissorOutlineJitter.Unit(7, 9, 3, 5, 1u));
        Assert.Equal(baseline, ScissorOutlineJitter.Unit(3, 5, 7, 9, 1u));
    }

    [Theory]
    [InlineData(0.0, 1.0, 1.0)]
    [InlineData(0.5, 0.625, 1.0)]
    [InlineData(1.0, 0.25, 1.0)]
    public void TheToleranceFactorStaysWithinItsRange(double spread, double minimum, double maximum)
    {
        for (var i = 0; i < 5000; i++)
            Assert.InRange(ScissorOutlineJitter.GetToleranceFactor(i, i * 2, i * 5, -i, 3u, spread), minimum - 1e-12, maximum + 1e-12);
    }

    [Fact]
    public void WithoutSpreadTheToleranceFactorIsExactlyOne()
    {
        for (var i = 0; i < 2000; i++)
            Assert.Equal(1.0, ScissorOutlineJitter.GetToleranceFactor(i, -i, i * 3, 7, (uint)i, 0.0));
    }

    [Fact]
    public void TheToleranceFactorReachesBothEndsAndAveragesBelowOne()
    {
        var minimum = double.MaxValue;
        var maximum = double.MinValue;
        var sum = 0.0;
        const int count = 20000;
        for (var i = 0; i < count; i++)
        {
            var factor = ScissorOutlineJitter.GetToleranceFactor(i, i % 97, i % 31, -i, 9u, 1.0);
            minimum = Math.Min(minimum, factor);
            maximum = Math.Max(maximum, factor);
            sum += factor;
        }

        Assert.InRange(minimum, 0.25, 0.3);
        Assert.InRange(maximum, 0.99, 1.0);
        Assert.InRange(sum / count, 0.60, 0.65);
    }

    [Fact]
    public void TheWaveStaysWithinMinusOneAndOneAndVariesAcrossSpace()
    {
        var minimum = double.MaxValue;
        var maximum = double.MinValue;
        var sum = 0.0;
        var count = 0;
        for (var x = -400.0; x <= 400.0; x += 3.7)
        {
            for (var y = -400.0; y <= 400.0; y += 3.1)
            {
                var wave = ScissorOutlineJitter.GetWave(x, y, 40.0, 5u);
                minimum = Math.Min(minimum, wave);
                maximum = Math.Max(maximum, wave);
                sum += wave;
                count++;
            }
        }

        Assert.InRange(minimum, -1.0, -0.8);
        Assert.InRange(maximum, 0.8, 1.0);
        Assert.InRange(sum / count, -0.1, 0.1);
    }

    [Fact]
    public void TheWaveChangesSmoothlyWithinAFractionOfTheWavelength()
    {
        for (var x = -200.0; x <= 200.0; x += 1.3)
        {
            for (var y = -200.0; y <= 200.0; y += 2.9)
            {
                var here = ScissorOutlineJitter.GetWave(x, y, 50.0, 8u);

                Assert.True(Math.Abs(ScissorOutlineJitter.GetWave(x + 0.1, y, 50.0, 8u) - here) <= 0.02);
                Assert.True(Math.Abs(ScissorOutlineJitter.GetWave(x, y + 0.1, 50.0, 8u) - here) <= 0.02);
            }
        }
    }

    [Fact]
    public void ALongerWavelengthStretchesTheWaveAndAnotherVariationReplacesIt()
    {
        var narrow = Enumerable.Range(0, 200).Select(i => ScissorOutlineJitter.GetWave(i, 17, 20.0, 1u)).ToArray();
        var wide = Enumerable.Range(0, 200).Select(i => ScissorOutlineJitter.GetWave(i, 17, 80.0, 1u)).ToArray();
        var other = Enumerable.Range(0, 200).Select(i => ScissorOutlineJitter.GetWave(i, 17, 20.0, 2u)).ToArray();

        double Travel(double[] values) => values.Zip(values.Skip(1), (a, b) => Math.Abs(a - b)).Sum();

        Assert.True(Travel(wide) < Travel(narrow) / 2);
        Assert.NotEqual(narrow, other);
        Assert.Equal(narrow, Enumerable.Range(0, 200).Select(i => ScissorOutlineJitter.GetWave(i, 17, 20.0, 1u)).ToArray());
    }
}
