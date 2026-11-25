using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Operations shared by every curve, written once. The wrapper carries the value and parameter types so the
/// compiler can infer them; reach it through <c>curve.Ops</c>. Each operation returns another wrapper, so calls
/// chain, and a chain of struct curves is itself a struct: nothing is boxed and no call is virtual.
/// This is the validated construction path: each operation builds its struct and calls Validate on it.
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
    public Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Slice(TReal from, TReal to) => new(new Sliced<TCurve, TValue, TReal>(Source, from, to).Validated());

    public Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Reverse() => Slice(TReal.One, TReal.Zero);

    /// <summary>Traverses this curve through a scalar map: curve(map(t)). Nothing is clamped; wrap either side to choose.</summary>
    public Curve<Reparameterized<TCurve, TMap, TValue, TReal>, TValue, TReal> Reparameterize<TMap>(TMap map) where TMap : IDifferentiableCurve<TReal, TReal> => new(new Reparameterized<TCurve, TMap, TValue, TReal>(Source, map).Validated());

    /// <summary>Maps points through the transform and derivatives through its linear part.</summary>
    public Curve<Transformed<TCurve, TTransform, TValue, TReal>, TValue, TReal> Transform<TTransform>(TTransform transform) where TTransform : ITransform<TValue> => new(new Transformed<TCurve, TTransform, TValue, TReal>(Source, transform).Validated());

    /// <summary>Holds the endpoint values outside [0, 1].</summary>
    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Clamped() => Extrapolated(Extrapolation.Clamp, Extrapolation.Clamp);

    /// <summary>Continues straight along the endpoint tangents outside [0, 1].</summary>
    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Extended() => Extrapolated(Extrapolation.Extend, Extrapolation.Extend);

    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Extrapolated(Extrapolation before, Extrapolation after) => new(new Extrapolated<TCurve, TValue, TReal>(Source, before, after).Validated());
}
