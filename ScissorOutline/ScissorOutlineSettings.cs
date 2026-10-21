namespace ScissorOutline;

internal static class ScissorOutlineSettings
{
    public const double MaximumThickness = 500.0;
    public const double SliderMaximumThickness = 200.0;
    public const double DefaultThickness = 24.0;
    public const double DefaultCoarseness = 50.0;
    public const double DefaultTremor = 50.0;
    public const double DefaultOpacity = 100.0;
    public const double EpsilonPerThickness = 1.0;
    public const double TremorPerThickness = 1.0;
    public const double DefaultIrregularity = 50.0;
    public const int DefaultHoldFrames = 3;
    public const int HoldFramesSliderMaximum = 12;
    public const double IrregularEpsilonShrink = 0.75;
    public const double IrregularDistancePerThickness = 1.0;
    public const double WavelengthPerThickness = 2.0;
    public const double MinimumWavelength = 16.0;
    public const double CellsPerRadius = 8.0;
    public const double SlackCells = 1.5;
    public const int PaddingCells = 22;
    public const int MaximumInputSide = 4096;
    public const byte AlphaThreshold = 8;
    public const int SeedSliderMaximum = 10000;
    public const int BalancedGridSide = 512;
    public const int HighGridSide = 1024;
    public const int UltraGridSide = 2048;

    public static int GetMaximumGridSide(ScissorOutlineQuality quality) => quality switch
    {
        ScissorOutlineQuality.Balanced => BalancedGridSide,
        ScissorOutlineQuality.Ultra => UltraGridSide,
        _ => HighGridSide,
    };

    public static int FloorPowerOfTwo(double value)
    {
        var power = 1;
        while (power < int.MaxValue / 2 && power * 2.0 <= value)
            power *= 2;
        return power;
    }

    public static int CeilingPowerOfTwo(int value)
    {
        var power = 1;
        while (power < value)
            power *= 2;
        return power;
    }

    public static ScissorOutlinePlan CreatePlan(in ScissorOutlineParameters parameters, int width, int height)
    {
        var thickness = Math.Clamp(parameters.Thickness, 0.0, MaximumThickness);
        var requestedEpsilon = EpsilonPerThickness * Math.Clamp(parameters.Coarseness, 0.0, 1.0) * thickness;
        var tremor = TremorPerThickness * Math.Clamp(parameters.Tremor, 0.0, 1.0) * thickness;
        var irregularity = Math.Clamp(parameters.Irregularity, 0.0, 1.0);
        var distance = IrregularDistancePerThickness * irregularity * thickness;
        var longSide = Math.Max(Math.Max(width, height), 1);
        var gridCell = CeilingPowerOfTwo((longSide + GetMaximumGridSide(parameters.Quality) - 1) / GetMaximumGridSide(parameters.Quality));
        var radiusCell = FloorPowerOfTwo((thickness + requestedEpsilon + tremor + distance) / CellsPerRadius);
        var cell = Math.Max(gridCell, radiusCell);
        var epsilon = Math.Max(requestedEpsilon, cell);
        var radius = thickness + epsilon + tremor + distance + SlackCells * cell;
        var radiusCells = radius / cell;
        return new ScissorOutlinePlan(
            cell,
            (width + cell - 1) / cell + 2 * PaddingCells,
            (height + cell - 1) / cell + 2 * PaddingCells,
            radius,
            (int)Math.Floor(radiusCells * radiusCells),
            epsilon,
            irregularity,
            tremor,
            distance,
            Math.Max(MinimumWavelength, WavelengthPerThickness * thickness));
    }
}
