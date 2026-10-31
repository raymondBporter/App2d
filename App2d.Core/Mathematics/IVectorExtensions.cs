using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics;

/// <summary>Dimension-independent math on concrete vector structs. Arithmetic follows IEEE 754.</summary>
public static class IVectorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TReal LengthSquared<TVector, TReal>(TVector value)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => TVector.Dot(value, value);

    /// <summary>The square root of the dot product. Intermediate squares may overflow or underflow.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TReal Length<TVector, TReal>(TVector value)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => TReal.Sqrt(LengthSquared<TVector, TReal>(value));

    /// <summary>Divides by the length. Zero or nonfinite inputs follow ordinary IEEE arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TVector Normalize<TVector, TReal>(TVector value)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => value / Length<TVector, TReal>(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TReal DistanceSquared<TVector, TReal>(TVector first, TVector second)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => LengthSquared<TVector, TReal>(first - second);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TReal Distance<TVector, TReal>(TVector first, TVector second)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => Length<TVector, TReal>(first - second);

    /// <summary>Linear interpolation, or extrapolation when amount lies outside [0, 1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TVector Lerp<TVector, TReal>(TVector first, TVector second, TReal amount)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => first * (TReal.One - amount) + second * amount;

    /// <summary>The component parallel to a unit normal. The normal must already be normalized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TVector Project<TVector, TReal>(TVector value, TVector unitNormal)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => unitNormal * TVector.Dot(value, unitNormal);

    /// <summary>Removes the component parallel to a unit normal. The normal must already be normalized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TVector Reject<TVector, TReal>(TVector value, TVector unitNormal)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => value - Project<TVector, TReal>(value, unitNormal);

    /// <summary>Reflects in a plane with a unit normal. The normal must already be normalized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TVector Reflect<TVector, TReal>(TVector value, TVector unitNormal)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> =>
        value - unitNormal * ((TReal.One + TReal.One) * TVector.Dot(value, unitNormal));

    /// <summary>Clamps each component using the implementation's Min and Max behavior.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TVector Clamp<TVector, TReal>(TVector value, TVector minimum, TVector maximum)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => TVector.Min(TVector.Max(value, minimum), maximum);
}

// C# does not infer a scalar type from a constraint on TVector. These two extension groups
// fix the scalar type, preserving both value.Length() and Vector3d.Distance(a, b) without boxing.
public static class FloatVectorExtensions
{
    extension<TVector>(TVector value) where TVector : unmanaged, IVector<TVector, float>
    {
        public float Dot(TVector other) => TVector.Dot(value, other);
        public bool IsFinite() => TVector.IsFinite(value);
        public float LengthSquared() => IVectorExtensions.LengthSquared<TVector, float>(value);
        public float Length() => IVectorExtensions.Length<TVector, float>(value);
        public TVector Normalized() => IVectorExtensions.Normalize<TVector, float>(value);
        public static TVector Normalize(TVector vector) => IVectorExtensions.Normalize<TVector, float>(vector);
        public static float DistanceSquared(TVector first, TVector second) => IVectorExtensions.DistanceSquared<TVector, float>(first, second);
        public static float Distance(TVector first, TVector second) => IVectorExtensions.Distance<TVector, float>(first, second);
        public static TVector Lerp(TVector first, TVector second, float amount) => IVectorExtensions.Lerp<TVector, float>(first, second, amount);
        public static TVector Project(TVector vector, TVector unitNormal) => IVectorExtensions.Project<TVector, float>(vector, unitNormal);
        public static TVector Reject(TVector vector, TVector unitNormal) => IVectorExtensions.Reject<TVector, float>(vector, unitNormal);
        public static TVector Reflect(TVector vector, TVector unitNormal) => IVectorExtensions.Reflect<TVector, float>(vector, unitNormal);
        public static TVector Clamp(TVector vector, TVector minimum, TVector maximum) => IVectorExtensions.Clamp<TVector, float>(vector, minimum, maximum);
    }
}

public static class DoubleVectorExtensions
{
    extension<TVector>(TVector value) where TVector : unmanaged, IVector<TVector, double>
    {
        public double Dot(TVector other) => TVector.Dot(value, other);
        public bool IsFinite() => TVector.IsFinite(value);
        public double LengthSquared() => IVectorExtensions.LengthSquared<TVector, double>(value);
        public double Length() => IVectorExtensions.Length<TVector, double>(value);
        public TVector Normalized() => IVectorExtensions.Normalize<TVector, double>(value);
        public static TVector Normalize(TVector vector) => IVectorExtensions.Normalize<TVector, double>(vector);
        public static double DistanceSquared(TVector first, TVector second) => IVectorExtensions.DistanceSquared<TVector, double>(first, second);
        public static double Distance(TVector first, TVector second) => IVectorExtensions.Distance<TVector, double>(first, second);
        public static TVector Lerp(TVector first, TVector second, double amount) => IVectorExtensions.Lerp<TVector, double>(first, second, amount);
        public static TVector Project(TVector vector, TVector unitNormal) => IVectorExtensions.Project<TVector, double>(vector, unitNormal);
        public static TVector Reject(TVector vector, TVector unitNormal) => IVectorExtensions.Reject<TVector, double>(vector, unitNormal);
        public static TVector Reflect(TVector vector, TVector unitNormal) => IVectorExtensions.Reflect<TVector, double>(vector, unitNormal);
        public static TVector Clamp(TVector vector, TVector minimum, TVector maximum) => IVectorExtensions.Clamp<TVector, double>(vector, minimum, maximum);
    }
}
