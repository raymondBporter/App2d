using App2d.Core.Mathematics.Vectors;

namespace App2d.Core.Mathematics;

// C# cannot infer TValue and TReal from a constraint such as `where TCurve : ICurve<TValue, TReal>`, so one entry
// point is declared per value type. Each sits in its own class so overload resolution drops the ones whose
// constraint does not hold. Every operation itself is written once, on Curve<TCurve, TValue, TReal>.

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
