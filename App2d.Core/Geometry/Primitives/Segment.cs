using App2d.Core.Mathematics.Vectors;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>A lightweight directed pair of floating-point vectors. Validity belongs to the owning geometry.</summary>
public readonly record struct Segment<TVector, TReal>(TVector Start, TVector End) : ISegment<Segment<TVector, TReal>, TVector>
    where TVector : unmanaged, IVector<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public static Segment<TVector, TReal> Create(TVector start, TVector end) => new(start, end);

    [System.Text.Json.Serialization.JsonIgnore]
    public TVector Midpoint => TVector.Midpoint(Start, End);
    public TReal Length() => TVector.Distance(Start, End);

    /// <summary>Samples the segment, clamping the amount to [0, 1].</summary>
    public TVector PointAt(TReal amount) => TVector.Lerp(Start, End, Real<TReal>.Clamp01(amount));

    public Segment<TVector, TReal> Reversed() => new(End, Start);
}
