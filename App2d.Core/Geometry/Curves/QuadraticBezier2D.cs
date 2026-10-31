using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>A quadratic Bezier curve over normalized progress [0, 1].</summary>
public readonly partial record struct QuadraticBezier2D : ICurve2D
{
    public string Kind => CurveKinds2D.QuadraticBezier;

    public QuadraticBezier2D(Vector2 start, Vector2 control, Vector2 end)
    {
        Start = start;
        Control = control;
        End = end;
    }

    public Vector2 Start { get; init; }
    public Vector2 Control { get; init; }
    public Vector2 End { get; init; }

    public Vector2 Evaluate(float amount) =>
        BezierMath.Quadratic<Vector2f, float>(new(Start), new(Control), new(End), amount).ToVector2();

    public Vector2 EvaluateDerivative(float amount) =>
        BezierMath.QuadraticDerivative<Vector2f, float>(new(Start), new(Control), new(End), amount).ToVector2();

    public float Distance(Vector2 point) => CurveDistance2D.SampledDistance(point, this, 64);
}
