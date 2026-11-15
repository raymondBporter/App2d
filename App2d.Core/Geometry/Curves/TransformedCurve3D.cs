using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Geometry.Curves;

/// <summary>An affine image of a curve. Translation affects positions, never derivatives.</summary>
public readonly record struct TransformedCurve3D<TCurve>(TCurve Curve, Frame3D Frame) : ICurve<Vector3f, float>
    where TCurve : ICurve<Vector3f, float>
{
    public Vector3f Evaluate(float amount) => Frame.TransformPoint(Curve.Evaluate(amount));
    public Vector3f EvaluateDerivative(float amount) => Frame.TransformDirection(Curve.EvaluateDerivative(amount));
}

/// <summary>Transforms double coordinates with widened frame coefficients, without narrowing the curve.</summary>
public readonly record struct TransformedCurve3d<TCurve>(TCurve Curve, Frame3D Frame) : ICurve<Vector3d, double>
    where TCurve : ICurve<Vector3d, double>
{
    private Vector3d Direction(Vector3d value) => Frame.XAxis.ToVector3d() * value.X + Frame.YAxis.ToVector3d() * value.Y + Frame.ZAxis.ToVector3d() * value.Z;
    public Vector3d Evaluate(double amount) => Frame.Origin.ToVector3d() + Direction(Curve.Evaluate(amount));
    public Vector3d EvaluateDerivative(double amount) => Direction(Curve.EvaluateDerivative(amount));
}

public static class FloatCurve3DExtensions
{
    extension<TCurve>(TCurve curve) where TCurve : ICurve<Vector3f, float>
    {
        public TransformedCurve3D<TCurve> TransformedBy(Frame3D frame) => new(curve, frame);
    }
}

public static class DoubleCurve3DExtensions
{
    extension<TCurve>(TCurve curve) where TCurve : ICurve<Vector3d, double>
    {
        public TransformedCurve3d<TCurve> TransformedBy(Frame3D frame) => new(curve, frame);
    }
}
