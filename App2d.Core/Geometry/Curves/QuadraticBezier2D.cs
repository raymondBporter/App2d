using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>A quadratic Bezier curve over normalized progress [0, 1].</summary>
public readonly partial record struct QuadraticBezier2D : ICurve2D
{
    public QuadraticBezier2D(Vector2f start, Vector2f control, Vector2f end)
    {
        Start = start;
        Control = control;
        End = end;
    }

    public Vector2f Start { get; init; }
    public Vector2f Control { get; init; }
    public Vector2f End { get; init; }

    public Vector2f Evaluate(float amount) =>
        BezierMath.Quadratic<Vector2f, float>(Start, Control, End, amount);

    public Vector2f EvaluateDerivative(float amount) =>
        BezierMath.QuadraticDerivative<Vector2f, float>(Start, Control, End, amount);

    public float Distance(Vector2f point) => CurveDistance2D.SampledDistance(point, this, 64);
}
