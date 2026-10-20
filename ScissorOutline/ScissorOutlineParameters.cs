namespace ScissorOutline;

internal readonly record struct ScissorOutlineParameters(
    double Thickness,
    double Coarseness,
    double Tremor,
    int Seed,
    bool FillHoles,
    ScissorOutlineQuality Quality);
