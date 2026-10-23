using App2d.Core.Geometry.Operations.HeightFields;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.HeightFields;

/// <summary>
/// An analytic/procedural heightfield evaluated directly, without constructing vertices.
/// Delegates must be deterministic; the caller supplies bounds and keeps the function above YMin.
/// </summary>
public sealed class FunctionHeightField2D : IHeightField2D
{
    private readonly Func<float, float> _height;
    private readonly Func<float, float>? _slope;

    public FunctionHeightField2D(float xMin, float xMax, float yMin, Func<float, float> height,
        Func<float, float>? slope = null, float yMax = float.PositiveInfinity)
    {
        HeightField2D.ValidateDomain(xMin, xMax, yMin, yMax);
        _height = ArgGuard.RequireNotNull(height);
        _slope = slope;
        XMin = xMin;
        XMax = xMax;
        YMin = yMin;
        YMax = yMax;
        _ = HeightAt(xMin);
        _ = HeightAt(xMax);
    }

    public float XMin { get; }
    public float XMax { get; }
    public float YMin { get; }
    public float YMax { get; }

    public float HeightAt(float x)
    {
        HeightField2D.ValidateX(this, x);
        return HeightField2D.ValidateHeight(this, _height(x));
    }

    public float SlopeAt(float x)
    {
        HeightField2D.ValidateX(this, x);
        return _slope is null ? HeightField2D.EstimateSlope(this, x) : ArgGuard.Finite(_slope(x));
    }
}
