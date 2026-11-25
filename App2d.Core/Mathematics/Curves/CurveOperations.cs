using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

// Like the geometry types, these construct without checking, so data can be loaded first and validated after.
// The validated construction path is curve.Ops, which builds each one and calls Validate.

/// <summary>An affine remap of the parameter: [0, 1] becomes [From, To] of the source. Keeps its interval so exact queries can map through it.</summary>
public readonly record struct Sliced<TCurve, TValue, TReal>(TCurve Curve, TReal From, TReal To) : IDifferentiableCurve<TValue, TReal>
    where TCurve : IDifferentiableCurve<TValue, TReal>
    where TValue : IMultiplyOperators<TValue, TReal, TValue>, IAdditiveIdentity<TValue, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public void Validate()
    {
        CurveArgument.NotNull(Curve, "curve");
        Ensure.Finite(From, "from");
        Ensure.Finite(To, "to");
    }

    public Sliced<TCurve, TValue, TReal> Validated() { Validate(); return this; }

    // An empty interval is one point: it stays put for any finite or infinite t, and never asks the source for a tangent.
    public TValue Evaluate(TReal t) => Curve.Evaluate(IsPoint(t) ? From : From + (To - From) * t);
    public TValue Derivative(TReal t) => IsPoint(t) ? TValue.AdditiveIdentity : Curve.Derivative(From + (To - From) * t) * (To - From);

    private bool IsPoint(TReal t) => From == To && !TReal.IsNaN(t);
}

/// <summary>curve(map(t)), differentiated with the chain rule. Neither the map's input nor its output is clamped.</summary>
public readonly record struct Reparameterized<TCurve, TMap, TValue, TReal>(TCurve Curve, TMap Map) : IDifferentiableCurve<TValue, TReal>
    where TCurve : IDifferentiableCurve<TValue, TReal>
    where TMap : IDifferentiableCurve<TReal, TReal>
    where TValue : IMultiplyOperators<TValue, TReal, TValue>, IAdditiveIdentity<TValue, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public void Validate()
    {
        CurveArgument.NotNull(Curve, "curve");
        CurveArgument.NotNull(Map, "map");
    }

    public Reparameterized<TCurve, TMap, TValue, TReal> Validated() { Validate(); return this; }

    public TValue Evaluate(TReal t) => Curve.Evaluate(Map.Evaluate(t));

    /// <summary>A map that is not moving gives a zero tangent without asking the source, so a held parameter is stationary even at a corner.</summary>
    public TValue Derivative(TReal t)
    {
        var rate = Map.Derivative(t);
        return rate == TReal.Zero ? TValue.AdditiveIdentity : Curve.Derivative(Map.Evaluate(t)) * rate;
    }
}

/// <summary>The image of a curve under a transform. Translation moves points and never derivatives.</summary>
public readonly record struct Transformed<TCurve, TTransform, TValue, TReal>(TCurve Curve, TTransform Transform) : IDifferentiableCurve<TValue, TReal>
    where TCurve : IDifferentiableCurve<TValue, TReal>
    where TTransform : ITransform<TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public void Validate()
    {
        CurveArgument.NotNull(Curve, "curve");
        CurveArgument.NotNull(Transform, "transform");
    }

    public Transformed<TCurve, TTransform, TValue, TReal> Validated() { Validate(); return this; }

    public TValue Evaluate(TReal t) => Transform.TransformPoint(Curve.Evaluate(t));
    public TValue Derivative(TReal t) => Transform.TransformDirection(Curve.Derivative(t));
}

internal static class CurveArgument
{
    // The type test keeps an unoptimized build from boxing a struct just to compare it with null.
    internal static void NotNull<T>(T value, string name)
    {
        if (!typeof(T).IsValueType && value is null) throw new ArgumentNullException(name);
    }
}
