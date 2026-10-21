namespace ScissorOutline.Tests;

public sealed class ScissorOutlineSettingsTests
{
    public static readonly TheoryData<double, double, double, int, int, ScissorOutlineQuality, int> Cells = new()
    {
        { 24, 0, 0, 1920, 1080, ScissorOutlineQuality.High, 2 },
        { 200, 0, 0, 1920, 1080, ScissorOutlineQuality.High, 16 },
        { 200, 1, 0, 1920, 1080, ScissorOutlineQuality.High, 32 },
        { 24, 0, 0, 4096, 4096, ScissorOutlineQuality.Balanced, 8 },
        { 0, 0, 0, 100, 100, ScissorOutlineQuality.High, 1 },
        { 24, 0, 0, 4096, 4096, ScissorOutlineQuality.Ultra, 2 },
    };

    public static readonly TheoryData<ScissorOutlineQuality, int> Limits = new()
    {
        { ScissorOutlineQuality.Balanced, 512 },
        { ScissorOutlineQuality.High, 1024 },
        { ScissorOutlineQuality.Ultra, 2048 },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public void TheCellIsTheLargerOfThePowerOfTwoBelowAnEighthOfTheRadiusAndTheGridLimit(double thickness, double coarseness, double tremor, int width, int height, ScissorOutlineQuality quality, int expected)
    {
        var parameters = new ScissorOutlineParameters(thickness, coarseness, tremor, 0, true, quality);

        var plan = ScissorOutlineSettings.CreatePlan(parameters, width, height);

        Assert.Equal(expected, plan.Cell);
    }

    [Theory]
    [MemberData(nameof(Limits))]
    public void TheGridNeverExceedsTheLimitOfTheQuality(ScissorOutlineQuality quality, int limit)
    {
        Assert.Equal(limit, ScissorOutlineSettings.GetMaximumGridSide(quality));
        foreach (var side in new[] { 1, 100, 1920, 4095, 4096 })
        {
            foreach (var thickness in new[] { 0.0, 1.0, 24.0, 200.0, 500.0 })
            {
                var plan = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(thickness, 0.5, 0.5, 0, true, quality), side, side);

                Assert.True((side + plan.Cell - 1) / plan.Cell <= limit);
                Assert.Equal((side + plan.Cell - 1) / plan.Cell + 2 * ScissorOutlineSettings.PaddingCells, plan.GridWidth);
            }
        }
    }

    [Fact]
    public void TheRadiusAddsTheEpsilonTheTremorAndTheSlackToTheThickness()
    {
        var parameters = new ScissorOutlineParameters(40, 0.5, 0.4, 0, true, ScissorOutlineQuality.High);

        var plan = ScissorOutlineSettings.CreatePlan(parameters, 1920, 1080);

        Assert.Equal(4, plan.Cell);
        Assert.Equal(10.0, plan.Epsilon);
        Assert.Equal(4.0, plan.Tremor);
        Assert.Equal(60.0, plan.Radius);
        Assert.Equal(225, plan.RadiusSquaredCells);
    }

    [Fact]
    public void TheIrregularityAddsTheLargestToleranceAndTheDistanceToTheRadius()
    {
        var parameters = new ScissorOutlineParameters(40, 0.5, 0.4, 0, true, ScissorOutlineQuality.High, 0.5);

        var plan = ScissorOutlineSettings.CreatePlan(parameters, 1920, 1080);

        Assert.Equal(8, plan.Cell);
        Assert.Equal(10.0, plan.Epsilon);
        Assert.Equal(15.0, plan.EpsilonLimit);
        Assert.Equal(0.5, plan.EpsilonSpread);
        Assert.Equal(4.0, plan.Tremor);
        Assert.Equal(10.0, plan.Distance);
        Assert.Equal(80.0, plan.Wavelength);
        Assert.Equal(81.0, plan.Radius);
        Assert.Equal(102, plan.RadiusSquaredCells);
    }

    [Fact]
    public void WithoutIrregularityThePlanIsTheSameAsBefore()
    {
        var plain = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(40, 0.5, 0.4, 0, true, ScissorOutlineQuality.High), 1920, 1080);
        var zero = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(40, 0.5, 0.4, 0, true, ScissorOutlineQuality.High, 0.0, 17), 1920, 1080);

        Assert.Equal(plain, zero);
        Assert.Equal(60.0, plain.Radius);
        Assert.Equal(plain.Epsilon, plain.EpsilonLimit);
        Assert.Equal(0.0, plain.Distance);
    }

    [Fact]
    public void TheWavelengthHasAFloorAndGrowsWithTheThickness()
    {
        var thin = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(2, 0, 0, 0, true, ScissorOutlineQuality.High), 100, 100);
        var thick = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(100, 0, 0, 0, true, ScissorOutlineQuality.High), 100, 100);

        Assert.Equal(ScissorOutlineSettings.MinimumWavelength, thin.Wavelength);
        Assert.Equal(200.0, thick.Wavelength);
    }

    [Fact]
    public void TheEpsilonIsNeverBelowOneCell()
    {
        var parameters = new ScissorOutlineParameters(200, 0, 0, 0, true, ScissorOutlineQuality.High);

        var plan = ScissorOutlineSettings.CreatePlan(parameters, 1920, 1080);

        Assert.Equal(16.0, plan.Epsilon);
        Assert.Equal(plan.Cell, plan.Epsilon);
    }

    [Fact]
    public void TheRadiusInCellsStaysWithinThePadding()
    {
        foreach (var quality in Enum.GetValues<ScissorOutlineQuality>())
        {
            foreach (var side in new[] { 1, 64, 1920, 4096 })
            {
                for (var thickness = 0.0; thickness <= ScissorOutlineSettings.MaximumThickness; thickness += 3.7)
                {
                    foreach (var ratio in new[] { 0.0, 0.5, 1.0 })
                    {
                        foreach (var irregularity in new[] { 0.0, 0.5, 1.0 })
                        {
                            var plan = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(thickness, ratio, 1.0 - ratio, 0, true, quality, irregularity), side, side);

                            Assert.True(Math.Ceiling(plan.Radius / plan.Cell) + 2 <= ScissorOutlineSettings.PaddingCells);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TheCellOnlyChangesWhenThePowerOfTwoChanges()
    {
        var before = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(24, 0, 0, 0, true, ScissorOutlineQuality.High), 1920, 1080);
        var within = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(30, 0, 0, 0, true, ScissorOutlineQuality.High), 1920, 1080);
        var beyond = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(32, 0, 0, 0, true, ScissorOutlineQuality.High), 1920, 1080);

        Assert.Equal(before.Cell, within.Cell);
        Assert.Equal(before.GridWidth, within.GridWidth);
        Assert.NotEqual(before.Cell, beyond.Cell);
    }

    [Fact]
    public void ParametersOutsideTheirRangeAreClamped()
    {
        var clamped = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(-5, -1, -1, 0, true, ScissorOutlineQuality.High, -3), 100, 100);
        var zero = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(0, 0, 0, 0, true, ScissorOutlineQuality.High), 100, 100);
        var large = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(1e6, 5, 5, 0, true, ScissorOutlineQuality.High, 7), 100, 100);
        var maximum = ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(ScissorOutlineSettings.MaximumThickness, 1, 1, 0, true, ScissorOutlineQuality.High, 1), 100, 100);

        Assert.Equal(zero, clamped);
        Assert.Equal(maximum, large);
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(1.0, 1)]
    [InlineData(1.9, 1)]
    [InlineData(2.0, 2)]
    [InlineData(7.9, 4)]
    [InlineData(8.0, 8)]
    [InlineData(109.0, 64)]
    public void FloorPowerOfTwoRoundsDownAndNeverReturnsLessThanOne(double value, int expected)
    {
        Assert.Equal(expected, ScissorOutlineSettings.FloorPowerOfTwo(value));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 8)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    public void CeilingPowerOfTwoRoundsUp(int value, int expected)
    {
        Assert.Equal(expected, ScissorOutlineSettings.CeilingPowerOfTwo(value));
    }
}
