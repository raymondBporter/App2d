using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Rotation in the XY plane, in radians, without constructing a transform or shape.</summary>
public static class Rotation2D
{
    private const double MinimumHalfAngleSine = 1e-6;

    public static Vector2 Apply(Vector2 value, float radians)
    {
        if (radians == 0f) return value;
        var (sin, cos) = MathF.SinCos(radians);
        return new(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
    }

    /// <summary>Rotates a point about a pivot in the XY plane.</summary>
    public static Vector2 ApplyAround(Vector2 point, Vector2 pivot, float radians) =>
        pivot + Apply(point - pivot, radians);

    /// <summary>Rotates XY while preserving depth exactly.</summary>
    public static Vector3 ApplyXY(Vector3 value, float radians) =>
        new(Apply(value.XY, radians), value.Z);

    /// <summary>Rotates XY about a pivot while preserving depth exactly.</summary>
    public static Vector3 ApplyXYAround(Vector3 value, Vector2 pivot, float radians) =>
        new(ApplyAround(value.XY, pivot, radians), value.Z);

    /// <summary>
    /// Finds the pivot carrying from to to through radians. Returns false when the turn is too close
    /// to a whole revolution to determine a stable pivot from float coordinates.
    /// </summary>
    public static bool TryFindPivot(Vector2 from, Vector2 to, float radians, out Vector2 pivot)
    {
        ValidateArc(from, to, radians);
        var half = (double)radians * 0.5d;
        var sinHalf = Math.Sin(half);
        if (Math.Abs(sinHalf) < MinimumHalfAngleSine)
        {
            pivot = default;
            return false;
        }
        var cotHalf = Math.Cos(half) / sinHalf;
        var dx = (double)to.X - from.X;
        var dy = (double)to.Y - from.Y;
        var x = ((double)from.X + to.X - cotHalf * dy) * 0.5d;
        var y = ((double)from.Y + to.Y + cotHalf * dx) * 0.5d;
        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            Math.Abs(x) > float.MaxValue || Math.Abs(y) > float.MaxValue)
        {
            pivot = default;
            return false;
        }
        pivot = new((float)x, (float)y);
        return true;
    }

    /// <summary>
    /// Interpolates the circular motion carrying from to to through radians, including long turns.
    /// Fractions outside [0, 1] return the nearest endpoint. Near a whole revolution the pivot
    /// is ill-conditioned, so this uses linear interpolation.
    /// </summary>
    public static Vector2 InterpolateArc(Vector2 from, Vector2 to, float radians, float fraction)
    {
        ValidateArc(from, to, radians);
        ArgGuard.ThrowIfNotFinite(fraction);
        if (fraction <= 0f) return from;
        if (fraction >= 1f) return to;
        var half = (double)radians * 0.5d;
        var sinHalf = Math.Sin(half);
        if (Math.Abs(sinHalf) < MinimumHalfAngleSine) return Vector2.Lerp(from, to, fraction);

        // (1 - exp(i*fraction*angle)) / (1 - exp(i*angle)) applied to the endpoint displacement.
        var scale = Math.Sin(fraction * half) / sinHalf;
        var phase = (fraction - 1d) * half;
        var (sinPhase, cosPhase) = Math.SinCos(phase);
        var real = scale * cosPhase;
        var imaginary = scale * sinPhase;
        var dx = (double)to.X - from.X;
        var dy = (double)to.Y - from.Y;
        return new((float)(from.X + real * dx - imaginary * dy),
            (float)(from.Y + imaginary * dx + real * dy));
    }

    private static void ValidateArc(Vector2 from, Vector2 to, float radians)
    {
        if (!float.IsFinite(from.X) || !float.IsFinite(from.Y)) throw new ArgumentOutOfRangeException(nameof(from));
        if (!float.IsFinite(to.X) || !float.IsFinite(to.Y)) throw new ArgumentOutOfRangeException(nameof(to));
        if (!float.IsFinite(radians)) throw new ArgumentOutOfRangeException(nameof(radians));
    }
}
