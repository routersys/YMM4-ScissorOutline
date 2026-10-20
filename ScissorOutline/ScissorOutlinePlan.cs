namespace ScissorOutline;

internal readonly record struct ScissorOutlinePlan(
    int Cell,
    int GridWidth,
    int GridHeight,
    double Radius,
    int RadiusSquaredCells,
    double Epsilon,
    double Tremor);
