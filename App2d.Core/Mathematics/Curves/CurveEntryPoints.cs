using App2d.Core.Mathematics.Curves;
using App2d.Core.Mathematics.Vectors;

namespace App2d.Core.Mathematics;

// Use new Curve<TCurve, TValue, TReal>(curve) to supply the types explicitly. C# infers TCurve from
// the receiver but cannot infer TValue or TReal from a where constraint. Ops supplies those two
// types and keeps the concrete receiver type. Separate extension classes let overload resolution
// select the applicable value/parameter pair.

public static class FloatCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<float, float>
    {
        public Curve<TCurve, float, float> Ops => new(curve);
    }
}

public static class DoubleCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<double, double>
    {
        public Curve<TCurve, double, double> Ops => new(curve);
    }
}

public static class Vector2fCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<Vector2f, float>
    {
        public Curve<TCurve, Vector2f, float> Ops => new(curve);
    }
}

public static class Vector2dCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<Vector2d, double>
    {
        public Curve<TCurve, Vector2d, double> Ops => new(curve);
    }
}

public static class Vector3fCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<Vector3f, float>
    {
        public Curve<TCurve, Vector3f, float> Ops => new(curve);
    }
}

public static class Vector3dCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<Vector3d, double>
    {
        public Curve<TCurve, Vector3d, double> Ops => new(curve);
    }
}

public static class Vector4fCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<Vector4f, float>
    {
        public Curve<TCurve, Vector4f, float> Ops => new(curve);
    }
}

public static class Vector4dCurves
{
    extension<TCurve>(TCurve curve) where TCurve : IDifferentiableCurve<Vector4d, double>
    {
        public Curve<TCurve, Vector4d, double> Ops => new(curve);
    }
}
