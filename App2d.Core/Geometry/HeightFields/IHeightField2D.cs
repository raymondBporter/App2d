using App2d.Core.Geometry.Operations.HeightFields;

namespace App2d.Core.Geometry.HeightFields;

/// <summary>
/// The solid XMin &lt;= x &lt;= XMax, YMin &lt;= y &lt;= HeightAt(x). The top is a continuous,
/// single-valued function; sides are vertical and the bottom is flat. No vertices are required.
/// </summary>
public interface IHeightField2D
{
    /// <summary>Finite, inclusive left endpoint.</summary>
    float XMin { get; }

    /// <summary>Finite, inclusive right endpoint, strictly greater than XMin.</summary>
    float XMax { get; }

    /// <summary>Supplied bottom; negative infinity means the solid extends downward indefinitely.</summary>
    float YMin { get; }

    /// <summary>A conservative upper bound, not an extremum search. Unknown bounds may be positive infinity.</summary>
    float YMax => float.PositiveInfinity;

    /// <summary>Finite surface height within the inclusive X domain, at least YMin and at most YMax.</summary>
    float HeightAt(float x);

    /// <summary>dy/dx. Override with an analytic derivative when available; otherwise finite differences are used.</summary>
    float SlopeAt(float x) => HeightField2D.EstimateSlope(this, x);
}
