namespace App2d.Core.Mathematics;

/// <summary>
/// A curve defined by an ordered list of control points, which can be read and replaced as a whole.
/// <typeparamref name="TCurve"/> is what replacement returns: the curve's own type for the generic curves, and the
/// erased geometry interface for the 2D geometry curves, which are edited while held as that interface.
/// </summary>
public interface IControlPointCurve<TPoint, TCurve>
{
    int ControlPointCount { get; }
    TPoint GetControlPoint(int index);

    /// <summary>Returns this curve with every control point replaced. The count must match.</summary>
    TCurve WithControlPoints(ReadOnlySpan<TPoint> points);
}
