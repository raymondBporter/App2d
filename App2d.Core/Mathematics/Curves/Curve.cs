using App2d.Core.Mathematics.Curves;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Shared operations on a differentiable curve. Construct with explicit type arguments or use <c>curve.Ops</c>
/// for common value types. Composition returns typed wrappers and uses constrained calls on the source.
/// With a concrete struct source this avoids boxing. An interface-typed source still uses interface dispatch.
/// Composition validates each operation as it is built.
/// <para>
/// Every operation works over the curve's own <see cref="Domain"/>: [0, 1] for geometry, and the input span for a
/// progress map built with FromRange.
/// </para>
/// </summary>
public readonly record struct Curve<TCurve, TValue, TReal>(TCurve Source) : IDifferentiableCurve<TValue, TReal>
    where TCurve : IDifferentiableCurve<TValue, TReal>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>, IAdditiveIdentity<TValue, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public Interval<TReal> Domain => Source.Domain;
    public TValue Evaluate(TReal t)
    {
        return Source.Evaluate(t);
    }

    public TValue Derivative(TReal t)
    {
        return Source.Derivative(t);
    }

    /// <summary>Evaluates once with the parameter clamped to the domain. Use <see cref="Clamped"/> when the policy should travel with the curve.</summary>
    public TValue EvaluateClamped(TReal t)
    {
        return Source.Evaluate(Source.Domain.Clamp(t));
    }

    /// <summary>Samples both ends of the domain and equal parameter steps between them into caller-owned storage, without allocating.</summary>
    public void Sample(Span<TValue> points)
    {
        Ensure.ThrowIfTooShort(points, 2);
        var domain = Source.Domain;
        var last = TReal.CreateChecked(points.Length - 1);
        for (var i = 0; i < points.Length - 1; i++) points[i] = Source.Evaluate(domain.Min + (domain.Max - domain.Min) * (TReal.CreateChecked(i) / last));
        points[^1] = Source.Evaluate(domain.Max);
    }

    /// <summary>Samples both ends of the domain and the requested number of equal parameter segments.</summary>
    public TValue[] Sample(int segmentCount)
    {
        var points = new TValue[Ensure.Positive(segmentCount) + 1];
        Sample(points);
        return points;
    }

    /// <summary>Maps [0, 1] onto [from, to] of this curve's parameter. The interval may be reversed or empty.</summary>
    public Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Slice(TReal from, TReal to)
    {
        return new(new Sliced<TCurve, TValue, TReal>(Source, from, to).Validated());
    }

    /// <summary>The whole curve with its domain remapped to [0, 1].</summary>
    public Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Normalized()
    {
        var domain = Source.Domain;
        return Slice(domain.Min, domain.Max);
    }

    /// <summary>The whole curve traversed from its end to its beginning, over [0, 1].</summary>
    public Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Reverse()
    {
        var domain = Source.Domain;
        return Slice(domain.Max, domain.Min);
    }

    /// <summary>Both halves at a parameter inside the domain, each remapped to [0, 1].</summary>
    public (Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Left, Curve<Sliced<TCurve, TValue, TReal>, TValue, TReal> Right) Split(TReal t)
    {
        var domain = Source.Domain;
        Ensure.InRange(t, domain.Min, domain.Max);
        return (Slice(domain.Min, t), Slice(t, domain.Max));
    }

    /// <summary>Traverses this curve through a scalar map: curve(map(t)). Nothing is clamped; wrap either side to choose.</summary>
    public Curve<Reparameterized<TCurve, TMap, TValue, TReal>, TValue, TReal> Reparameterize<TMap>(TMap map) where TMap : IDifferentiableCurve<TReal, TReal>
    {
        return new(new Reparameterized<TCurve, TMap, TValue, TReal>(Source, map).Validated());
    }

    /// <summary>Maps points through the transform and derivatives through its linear part.</summary>
    public Curve<Transformed<TCurve, TTransform, TValue, TReal>, TValue, TReal> Transform<TTransform>(TTransform transform) where TTransform : ITransform<TValue>
    {
        return new(new Transformed<TCurve, TTransform, TValue, TReal>(Source, transform).Validated());
    }

    /// <summary>
    /// This curve over its domain, then the next one, shifted to begin where this one ends. The result's domain is
    /// the two lengths added. The values are not required to meet at the join.
    /// </summary>
    public Curve<Concatenated<TCurve, TNext, TValue, TReal>, TValue, TReal> Concatenate<TNext>(TNext next) where TNext : IDifferentiableCurve<TValue, TReal>
    {
        return new(new Concatenated<TCurve, TNext, TValue, TReal>(Source, next).Validated());
    }

    /// <summary>Holds the end values outside the domain.</summary>
    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Clamped()
    {
        return Extrapolated(Extrapolation.Clamp, Extrapolation.Clamp);
    }

    /// <summary>Continues straight along the end tangents outside the domain.</summary>
    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Extended()
    {
        return Extrapolated(Extrapolation.Extend, Extrapolation.Extend);
    }

    public Curve<Extrapolated<TCurve, TValue, TReal>, TValue, TReal> Extrapolated(Extrapolation before, Extrapolation after)
    {
        return new(new Extrapolated<TCurve, TValue, TReal>(Source, before, after).Validated());
    }
}
