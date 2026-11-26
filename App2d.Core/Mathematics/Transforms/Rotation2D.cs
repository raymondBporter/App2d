using App2d.Core.Validation;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>Pivot solving and endpoint arc interpolation in the XY plane.</summary>
public static class Rotation2D
{
    /// <summary>
    /// Finds the pivot carrying from -> to through radians. Returns false when the turn is too close
    /// to a whole revolution to determine a stable pivot from float coordinates.
    /// </summary>
    public static bool TryFindPivot(Vector2f from, Vector2f to, float radians, out Vector2f pivot)
    {
        ValidateArc(from, to, radians);
        var half = (double)radians * 0.5d;
        var sinHalf = Math.Sin(half);
        if (Math.Abs(sinHalf) < Angles.MinimumHalfAngleSine)
        {
            pivot = default;
            return false;
        }
        var cotHalf = Math.Cos(half) / sinHalf;
        var start = from.ToVector2d();
        var end = to.ToVector2d();
        var center = (start + end + (end - start).PerpCcw * cotHalf) * 0.5d;
        if (!center.IsFinite() || Math.Abs(center.X) > float.MaxValue || Math.Abs(center.Y) > float.MaxValue)
        {
            pivot = default;
            return false;
        }
        pivot = center.ToVector2f();
        return true;
    }

    /// <summary>
    /// Interpolates the circular motion carrying from -> to through radians, including long turns.
    /// Fractions outside [0, 1] return the nearest endpoint. Near a whole revolution the pivot
    /// is ill-conditioned, so this uses linear interpolation.
    /// </summary>
    public static Vector2f InterpolateArc(Vector2f from, Vector2f to, float radians, float fraction)
    {
        ValidateArc(from, to, radians);
        Ensure.Finite(fraction);
        if (fraction <= 0f)
        {
            return from;
        }

        if (fraction >= 1f)
        {
            return to;
        }

        var half = (double)radians * 0.5d;
        var sinHalf = Math.Sin(half);
        if (Math.Abs(sinHalf) < Angles.MinimumHalfAngleSine)
        {
            return Vector2f.Lerp(from, to, fraction);
        }

        // (1 - exp(i*fraction*angle)) / (1 - exp(i*angle)) applied to the endpoint displacement.
        var scale = Math.Sin(fraction * half) / sinHalf;
        var phase = (fraction - 1d) * half;
        var (sinPhase, cosPhase) = Math.SinCos(phase);
        var real = scale * cosPhase;
        var imaginary = scale * sinPhase;
        var delta = to.ToVector2d() - from.ToVector2d();
        return new Vector2d(from.X + real * delta.X - imaginary * delta.Y, from.Y + imaginary * delta.X + real * delta.Y).ToVector2f();
    }

    private static void ValidateArc(Vector2f from, Vector2f to, float radians)
    {
        Ensure.Finite(from);
        Ensure.Finite(to);
        Ensure.Finite(radians);
    }
}
