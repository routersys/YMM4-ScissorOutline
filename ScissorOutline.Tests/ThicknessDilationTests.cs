namespace ScissorOutline.Tests;

public sealed class ThicknessDilationTests
{
    static ScissorOutlinePlan Plan(double thickness, double spread, double wavelength = 100.0, double change = 0.5, int width = 400, int height = 300)
        => ScissorOutlineSettings.CreatePlan(new ScissorOutlineParameters(thickness, 0.5, 0.3, 0, true, ScissorOutlineQuality.High, 0.0, 0, spread, wavelength, change), width, height);

    static byte[] Dilate(byte[] mask, in ScissorOutlinePlan plan, uint fixedVariation = 3, uint frameVariation = 17, ThicknessDilation? dilation = null)
    {
        var result = new ulong[BitGrid.WordsPerRow(plan.GridWidth) * plan.GridHeight];
        (dilation ?? new ThicknessDilation()).Dilate(Masks.ToBits(mask, plan.GridWidth, plan.GridHeight), plan.GridWidth, plan.GridHeight, plan, fixedVariation, frameVariation, result);
        return Masks.ToBytes(result, plan.GridWidth, plan.GridHeight);
    }

    static byte[] Single(in ScissorOutlinePlan plan)
    {
        var mask = new byte[plan.GridWidth * plan.GridHeight];
        mask[(plan.GridHeight / 2) * plan.GridWidth + plan.GridWidth / 2] = 1;
        return mask;
    }

    [Theory]
    [InlineData(6.0)]
    [InlineData(24.0)]
    [InlineData(80.0)]
    public void WithoutSpreadItIsTheDiscDilation(double thickness)
    {
        var plan = Plan(thickness, 0.0);
        var mask = Masks.Random(plan.GridWidth, plan.GridHeight, 7, 1);
        var expected = new ulong[BitGrid.WordsPerRow(plan.GridWidth) * plan.GridHeight];
        new DiscDilation().Dilate(Masks.ToBits(mask, plan.GridWidth, plan.GridHeight), plan.GridWidth, plan.GridHeight, plan.RadiusSquaredCells, expected);

        var actual = Dilate(mask, plan);

        Assert.Equal(Masks.ToBytes(expected, plan.GridWidth, plan.GridHeight), actual);
    }

    [Fact]
    public void EveryCellWithinTheMinimumRadiusIsInsideAndEveryCellBeyondTheMaximumIsOutside()
    {
        var plan = Plan(24.0, 40.0);
        var mask = Single(plan);
        var centerX = plan.GridWidth / 2;
        var centerY = plan.GridHeight / 2;
        var minimum = plan.Radius / plan.Cell;
        var maximum = (plan.Radius + plan.ThicknessSpread) / plan.Cell;

        var inside = Dilate(mask, plan);

        for (var y = 0; y < plan.GridHeight; y++)
        {
            for (var x = 0; x < plan.GridWidth; x++)
            {
                var distance = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
                if (distance <= minimum * minimum)
                    Assert.Equal(1, inside[y * plan.GridWidth + x]);
                if (distance > maximum * maximum)
                    Assert.Equal(0, inside[y * plan.GridWidth + x]);
            }
        }
    }

    [Fact]
    public void TheRadiusVariesBetweenThickAndThinPlacesAroundOneCell()
    {
        var plan = Plan(24.0, 60.0, 80.0);
        var inside = Dilate(Single(plan), plan);
        var centerX = plan.GridWidth / 2;
        var centerY = plan.GridHeight / 2;
        var reach = new List<double>();
        for (var step = 0; step < 72; step++)
        {
            var angle = step * Math.PI / 36.0;
            var farthest = 0.0;
            for (var distance = 0.0; distance < 40.0; distance += 0.25)
            {
                var x = (int)Math.Round(centerX + distance * Math.Cos(angle));
                var y = (int)Math.Round(centerY + distance * Math.Sin(angle));
                if (inside[y * plan.GridWidth + x] != 0)
                    farthest = distance;
            }

            reach.Add(farthest * plan.Cell);
        }

        Assert.True(reach.Max() - reach.Min() > 0.5 * plan.ThicknessSpread);
        Assert.True(reach.Min() >= plan.Radius - plan.Cell);
        Assert.True(reach.Max() <= plan.Radius + plan.ThicknessSpread + plan.Cell);
    }

    [Fact]
    public void ALargerSpreadOnlyAddsCells()
    {
        var thin = Plan(24.0, 20.0, 100.0, 0.5, 400, 300);
        var thick = thin with { ThicknessSpread = 60.0 };
        var mask = Masks.Random(thin.GridWidth, thin.GridHeight, 4, 1);

        var small = Dilate(mask, thin);
        var large = Dilate(mask, thick);

        Assert.All(small.Zip(large), pair => Assert.True(pair.First <= pair.Second));
        Assert.True(large.Count(v => v != 0) > small.Count(v => v != 0));
    }

    [Fact]
    public void WithoutChangeTheFrameDoesNotMatterAndWithChangeItDoes()
    {
        var plan = Plan(24.0, 40.0, 100.0, 0.0);
        var mask = Masks.Random(plan.GridWidth, plan.GridHeight, 5, 1);
        var changing = plan with { SpreadChange = 0.8 };

        Assert.Equal(Dilate(mask, plan, 3, 17), Dilate(mask, plan, 3, 99));
        Assert.NotEqual(Dilate(mask, changing, 3, 17), Dilate(mask, changing, 3, 99));
        Assert.NotEqual(Dilate(mask, plan, 3, 17), Dilate(mask, plan, 4, 17));
    }

    [Fact]
    public void TheSameInstanceDilatesDifferentSizesCorrectly()
    {
        var dilation = new ThicknessDilation();
        var large = Plan(24.0, 40.0, 100.0, 0.5, 600, 400);
        var small = Plan(6.0, 12.0, 40.0, 0.5, 90, 60);
        var largeMask = Masks.Random(large.GridWidth, large.GridHeight, 3, 1);
        var smallMask = Masks.Random(small.GridWidth, small.GridHeight, 4, 2);

        var first = Dilate(largeMask, large, 3, 17, dilation);
        var other = Dilate(smallMask, small, 3, 17, dilation);
        var again = Dilate(largeMask, large, 3, 17, dilation);

        Assert.Equal(first, again);
        Assert.Equal(Dilate(smallMask, small), other);
    }
}
