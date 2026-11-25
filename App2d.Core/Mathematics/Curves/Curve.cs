using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Operations shared by every curve, written once. The wrapper carries the value and parameter types so the
/// compiler can infer them; reach it through <c>curve.Ops</c>. Each operation returns another wrapper, so calls
/// chain, and a chain of struct curves is itself a struct: nothing is boxed and no call is virtual.
/// <para>
/// Every operation here treats [0, 1] as the curve's domain. A <c>ProgressMap</c> built with FromRange has its own
/// input span, which its own EvaluateClamped honours and these operations do not.
/// </para>
/// </summary>
public readonly record struct Curve<TCurve, TValue, TReal>(TCurve Source) : IDifferentiableCurve<TValue, TReal>
    where TCurve : IDifferentiableCurve<TValue, TReal>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>, IAdditiveIdentity<TValue, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public TValue Evaluate(TReal t) => Source.Evaluate(t);
    public TValue Derivative(TReal t) => Source.Derivative(t);

    /// <summary>Evaluates once with the parameter clamped to [0, 1]. Use <see cref="Clamped"/> when the policy should travel with the curve.</summary>
    public TValue EvaluateClamped(TReal t) => Source.Evaluate(TReal.Clamp(t, TReal.Zero, TReal.One));

    /// <summary>Maps [0, 1] onto [from, to] of this curve. The interval may be reversed or empty.</summary>
    public Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Slice(TReal from, TReal to) => new(new(Source, Ensure.Finite(from), Ensure.Finite(to)));

    public Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Reverse() => Slice(TReal.One, TReal.Zero);

    /// <summary>Traverses this curve through a scalar map: curve(map(t)). Nothing is clamped; wrap either side to choose.</summary>
    public Curve<Reparameterized<TCurve, TMap, TValue, TReal>, TValue, TReal> Reparameterize<TMap>(TMap map) where TMap : IDifferentiableCurve<TReal, TReal>
    {
        // The type test keeps an unoptimized build from boxing a struct map just to compare it with null.
        if (!typeof(TMap).IsValueType && map is null) throw new ArgumentNullException(nameof(map));
        return new(new(Source, map));
    }

    /// <summary>Maps points through the transform and derivatives through its linear part.</summary>
    public Curve<Transformed<TCurve, TTransform, TValue, TReal>, TValue, TReal> Transform<TTransform>(TTransform transform) where TTransform : ITransform<TValue>
    {
        if (!typeof(TTransform).IsValueType && transform is null) throw new ArgumentNullException(nameof(transform));
        return new(new(Source, transform));
    }

    /// <summary>Holds the endpoint values outside [0, 1].</summary>
    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Clamped() => Extrapolated(Extrapolation.Clamp, Extrapolation.Clamp);

    /// <summary>Continues straight along the endpoint tangents outside [0, 1].</summary>
    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Extended() => Extrapolated(Extrapolation.Extend, Extrapolation.Extend);

    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Extrapolated(Extrapolation before, Extrapolation after) => new(new(Source, before, after));
}
