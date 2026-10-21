namespace ScissorOutline.Tests;

public sealed class ThicknessFieldTests
{
    const int Width = 160;
    const int Height = 90;

    static double[] Field(int cell, double wavelength, uint fixedVariation, uint frameVariation, double change, int width = Width, int height = Height)
    {
        var field = new ThicknessField();
        field.Prepare(width, height, cell, wavelength, fixedVariation, frameVariation, change);
        var values = new double[width * height];
        for (var y = 0; y < height; y++)
            field.FillRow(y, values.AsSpan(y * width, width));
        return values;
    }

    [Fact]
    public void EveryValueIsBetweenZeroAndOne()
    {
        foreach (var change in new[] { 0.0, 0.5, 1.0 })
            Assert.All(Field(4, 100, 3, 17, change), value => Assert.InRange(value, 0.0, 1.0));
    }

    [Fact]
    public void TheValuesReachBothEndsOfTheRange()
    {
        var values = Field(4, 60, 5, 9, 0.5);

        Assert.True(values.Min() < 0.05);
        Assert.True(values.Max() > 0.95);
        Assert.InRange(values.Average(), 0.4, 0.6);
    }

    [Fact]
    public void TheSameArgumentsGiveTheSameField()
    {
        Assert.Equal(Field(4, 100, 3, 17, 0.5), Field(4, 100, 3, 17, 0.5));
    }

    [Fact]
    public void WithoutChangeTheFrameDoesNotMatter()
    {
        Assert.Equal(Field(4, 100, 3, 17, 0.0), Field(4, 100, 3, 99, 0.0));
        Assert.NotEqual(Field(4, 100, 3, 17, 0.0), Field(4, 100, 4, 17, 0.0));
    }

    [Fact]
    public void WithFullChangeOnlyTheFrameMatters()
    {
        var first = Field(4, 100, 3, 17, 1.0);
        var second = Field(4, 100, 8, 17, 1.0);

        Assert.All(first.Zip(second), pair => Assert.Equal(pair.First, pair.Second, 1e-9));
        Assert.NotEqual(first, Field(4, 100, 3, 18, 1.0));
    }

    [Fact]
    public void ADifferentFrameChangesTheFieldMoreAsTheChangeGrows()
    {
        double Difference(double change)
        {
            var before = Field(4, 100, 3, 17, change);
            var after = Field(4, 100, 3, 18, change);
            return before.Zip(after, (a, b) => Math.Abs(a - b)).Average();
        }

        Assert.Equal(0.0, Difference(0.0));
        Assert.True(Difference(0.3) > 0.0);
        Assert.True(Difference(0.3) < Difference(0.7));
        Assert.True(Difference(0.7) < Difference(1.0));
    }

    [Theory]
    [InlineData(2, 40.0)]
    [InlineData(4, 100.0)]
    [InlineData(8, 400.0)]
    public void NeighbouringCellsDifferByLessThanTheSlopeOfTheInterpolationAllows(int cell, double wavelength)
    {
        var values = Field(cell, wavelength, 3, 17, 0.5);
        var limit = 2.0 * ScissorOutlineSettings.ThicknessContrast * 1.5 * 1.5 * cell / wavelength;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 1; x < Width; x++)
                Assert.True(Math.Abs(values[y * Width + x] - values[y * Width + x - 1]) <= limit + 1e-9);
        }

        for (var y = 1; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
                Assert.True(Math.Abs(values[y * Width + x] - values[(y - 1) * Width + x]) <= limit + 1e-9);
        }
    }

    [Fact]
    public void ALongerWavelengthGivesSlowerChange()
    {
        double Slope(double wavelength)
        {
            var values = Field(4, wavelength, 3, 17, 0.5);
            double total = 0;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 1; x < Width; x++)
                    total += Math.Abs(values[y * Width + x] - values[y * Width + x - 1]);
            }

            return total;
        }

        Assert.True(Slope(40.0) > Slope(160.0));
        Assert.True(Slope(160.0) > Slope(640.0));
    }

    [Fact]
    public void TheValuesInterpolateTheLatticeWithTheSmoothFadeAtTheCenterOfEachCell()
    {
        const int cell = 4;
        const double wavelength = 100.0;
        const uint fixedVariation = 3;
        const uint frameVariation = 17;
        const double change = 0.5;
        var values = Field(cell, wavelength, fixedVariation, frameVariation, change);

        double Node(int x, int y)
            => ScissorOutlineJitter.GetThicknessLattice(x, y, fixedVariation) * Math.Cos(change * Math.PI / 2.0)
                + ScissorOutlineJitter.GetThicknessLattice(x, y, frameVariation) * Math.Sin(change * Math.PI / 2.0);

        foreach (var (x, y) in new[] { (0, 0), (21, 5), (22, 22), (40, 33), (100, 60), (159, 89) })
        {
            var u = (x - ScissorOutlineSettings.PaddingCells + 0.5) * cell / wavelength;
            var v = (y - ScissorOutlineSettings.PaddingCells + 0.5) * cell / wavelength;
            var column = (int)Math.Floor(u);
            var row = (int)Math.Floor(v);
            var fadeX = ScissorOutlineJitter.Fade(u - column);
            var fadeY = ScissorOutlineJitter.Fade(v - row);
            var top = Node(column, row) + (Node(column + 1, row) - Node(column, row)) * fadeX;
            var bottom = Node(column, row + 1) + (Node(column + 1, row + 1) - Node(column, row + 1)) * fadeX;
            var expected = Math.Clamp(0.5 + ScissorOutlineSettings.ThicknessContrast * (top + (bottom - top) * fadeY), 0.0, 1.0);

            Assert.Equal(expected, values[y * Width + x], 1e-9);
        }
    }

    [Fact]
    public void TheSameInstanceFillsDifferentSizesCorrectly()
    {
        var field = new ThicknessField();
        var large = new double[Width * Height];
        field.Prepare(Width, Height, 4, 100, 3, 17, 0.5);
        for (var y = 0; y < Height; y++)
            field.FillRow(y, large.AsSpan(y * Width, Width));

        var small = new double[30 * 20];
        field.Prepare(30, 20, 8, 64, 1, 2, 0.2);
        for (var y = 0; y < 20; y++)
            field.FillRow(y, small.AsSpan(y * 30, 30));

        var again = new double[Width * Height];
        field.Prepare(Width, Height, 4, 100, 3, 17, 0.5);
        for (var y = 0; y < Height; y++)
            field.FillRow(y, again.AsSpan(y * Width, Width));

        Assert.Equal(large, again);
        Assert.Equal(Field(8, 64, 1, 2, 0.2, 30, 20), small);
    }
}
