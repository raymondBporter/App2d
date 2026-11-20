using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.RootFinding;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Intersection;

/// <summary>First contact of closed segments under linear translation and rational half-angle rotation.</summary>
/// <remarks>
/// Rotations interpolate z = (cos(angle/2), sin(angle/2)) linearly, then use z²/|z|².
/// This has nonconstant angular speed. It agrees with the supplied endpoint poses, but differs from
/// linearly interpolating their angles. Results use double arithmetic, not certified exact predicates.
/// </remarks>
public static class SegmentCcd2D
{
    private const double EvaluationTolerance = 64d * 2.2204460492503131e-16;

    /// <summary>
    /// Returns the earliest touching fraction in [0, 1], zero for initial overlap, or null for a miss.
    /// Segments are local to their respective transforms; collapsed segments are treated as points.
    /// </summary>
    /// <remarks>
    /// Transform translations describe the moving rotation pivots. For an offset p carried by the
    /// rotation, add p to the local endpoints. Split turns approaching a full revolution before querying.
    /// </remarks>
    public static double? FirstContact(Segment<Vector2f, float> first, RigidTransform2D firstStart, RigidTransform2D firstEnd,
        Segment<Vector2f, float> second, RigidTransform2D secondStart, RigidTransform2D secondEnd)
    {
        Ensure.Finite(first.Start);
        Ensure.Finite(first.End);
        Ensure.Finite(second.Start);
        Ensure.Finite(second.End);
        var a = new Motion(firstStart, firstEnd);
        var b = new Motion(secondStart, secondEnd);
        var a0 = first.Start.ToVector2d();
        var a1 = first.End.ToVector2d();
        var b0 = second.Start.ToVector2d();
        var b1 = second.End.ToVector2d();
        if (InitiallyOverlapping(a, a0, a1, b, b0, b1)) return 0d;

        var best = double.PositiveInfinity;
        best = EndpointContact(a, a0, b, b0, b1, best);
        best = EndpointContact(a, a1, b, b0, b1, best);
        best = EndpointContact(b, b0, a, a0, a1, best);
        best = EndpointContact(b, b1, a, a0, a1, best);
        return double.IsFinite(best) ? best : null;
    }

    /// <summary>Samples the same rational motion used by FirstContact, retaining double precision.</summary>
    public static Vector2d TransformPoint(Vector2f localPoint, RigidTransform2D start, RigidTransform2D end, double time)
    {
        Ensure.Finite(localPoint);
        Ensure.Finite(time);
        Ensure.GreaterThanOrEqual(time, 0d);
        Ensure.LessThanOrEqual(time, 1d);
        var motion = new Motion(start, end);
        return motion.Position + motion.Displacement * time + Multiply(motion.RotationAt(time), localPoint.ToVector2d());
    }

    private static double EndpointContact(Motion a, Vector2d point, Motion b, Vector2d start, Vector2d end, double best)
    {
        Span<Vector2d> numerator = stackalloc Vector2d[6];
        Span<double> denominator = stackalloc double[5];
        var bounds = BuildRelativeNumerator(a, point, b, start, numerator, denominator);
        Span<double> cross = stackalloc double[6];
        Span<double> along = stackalloc double[6];
        Span<double> remaining = stackalloc double[6];
        var direction = end - start;
        if (direction == Vector2d.Zero)
        {
            // Point/point contact: one coordinate supplies candidate times, the other verifies them.
            for (var i = 0; i < numerator.Length; i++)
            {
                cross[i] = numerator[i].X;
                along[i] = numerator[i].Y;
                remaining[i] = -numerator[i].Y;
            }
            if (SumAbs(cross) <= bounds.X * EvaluationTolerance)
            {
                for (var i = 0; i < numerator.Length; i++)
                {
                    cross[i] = numerator[i].Y;
                    along[i] = numerator[i].X;
                    remaining[i] = -numerator[i].X;
                }
                bounds = new(bounds.Y, bounds.X);
            }
            return FindContact(cross, cross, bounds.X * EvaluationTolerance, along, remaining,
                bounds.Y * EvaluationTolerance, best);
        }

        var lengthSquared = direction.LengthSquared();
        var denominatorBound = 0d;
        for (var i = 0; i < numerator.Length; i++)
        {
            cross[i] = numerator[i].Cross(direction);
            along[i] = numerator[i].Dot(direction);
            var limit = i < denominator.Length ? denominator[i] * lengthSquared : 0d;
            remaining[i] = limit - along[i];
            denominatorBound += Math.Abs(limit);
        }
        var crossTolerance = EvaluationTolerance * (bounds.X * Math.Abs(direction.Y) + bounds.Y * Math.Abs(direction.X));
        var alongTolerance = EvaluationTolerance * (bounds.Dot(direction.Abs()) + denominatorBound);
        if (SumAbs(cross) > crossTolerance)
        {
            return FindContact(cross, cross, crossTolerance, along, remaining, alongTolerance, best);
        }

        // An endpoint stays on the supporting line. Its first entry onto the finite segment must
        // occur at an end of the projection interval. Test those events directly, including t=1
        // and isolated touches; there is no midpoint sampling or overlap-predicate bisection.
        best = FindContact(along, cross, crossTolerance, along, remaining, alongTolerance, best);
        return FindContact(remaining, cross, crossTolerance, along, remaining, alongTolerance, best);
    }

    private static double FindContact(ReadOnlySpan<double> equation, ReadOnlySpan<double> cross, double crossTolerance,
        ReadOnlySpan<double> along, ReadOnlySpan<double> remaining, double alongTolerance, double best)
    {
        if (IsContact(0d, cross, crossTolerance, along, remaining, alongTolerance)) return 0d;
        if (IsContact(1d, cross, crossTolerance, along, remaining, alongTolerance)) best = Math.Min(best, 1d);
        if (MaxAbs(equation) == 0d) return best;
        Span<double> roots = stackalloc double[5];
        var count = PolynomialRoots.FindRealRoots(equation, 0d, Math.Min(1d, best), roots);
        for (var i = 0; i < count; i++)
        {
            var time = roots[i];
            if (IsContact(time, cross, crossTolerance, along, remaining, alongTolerance))
            {
                return Math.Min(best, time);
            }
        }
        return best;
    }

    private static bool IsContact(double time, ReadOnlySpan<double> cross, double crossTolerance,
        ReadOnlySpan<double> along, ReadOnlySpan<double> remaining, double alongTolerance) =>
        Math.Abs(PolynomialRoots.Evaluate(cross, time, false)) <= crossTolerance &&
        PolynomialRoots.Evaluate(along, time, false) >= -alongTolerance &&
        PolynomialRoots.Evaluate(remaining, time, false) >= -alongTolerance;

    // q = R_B^T(P_A - P_B) + R_B^T R_A point - start = numerator / (D_A D_B).
    // N_B^T(P_A-P_B) D_A has degree 5; N_B^T N_A and D_A D_B have degree 4.
    // Taking cross/dot with B's fixed local direction gives the contact quintics directly.
    private static Vector2d BuildRelativeNumerator(Motion a, Vector2d point, Motion b, Vector2d start,
        Span<Vector2d> numerator, Span<double> denominator)
    {
        numerator.Clear();
        denominator.Clear();
        Span<double> da = stackalloc double[3];
        Span<double> db = stackalloc double[3];
        Span<Vector2d> na = stackalloc Vector2d[3];
        Span<Vector2d> nb = stackalloc Vector2d[3];
        a.WritePolynomials(da, na, false);
        b.WritePolynomials(db, nb, true);
        var position = a.Position - b.Position;
        var velocity = a.Displacement - b.Displacement;
        var bounds = Vector2d.Zero;
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                var factor = da[i] * db[j];
                denominator[i + j] += factor;
                AddTerm(numerator, i + j, -start * factor, ref bounds);
                AddTerm(numerator, i + j, Multiply(Multiply(nb[i], na[j]), point), ref bounds);
                AddTerm(numerator, i + j, Multiply(nb[i], position) * da[j], ref bounds);
                AddTerm(numerator, i + j + 1, Multiply(nb[i], velocity) * da[j], ref bounds);
            }
        }
        return bounds;
    }

    private static void AddTerm(Span<Vector2d> coefficients, int index, Vector2d term, ref Vector2d bounds)
    {
        coefficients[index] += term;
        bounds += term.Abs();
    }

    private static bool InitiallyOverlapping(Motion a, Vector2d a0, Vector2d a1, Motion b, Vector2d b0, Vector2d b1)
    {
        var inverseB = Conjugate(b.RotationAt(0));
        var relativeRotation = Multiply(inverseB, a.RotationAt(0));
        var offset = Multiply(inverseB, a.Position - b.Position);
        var p = offset + Multiply(relativeRotation, a0);
        var r = Multiply(relativeRotation, a1 - a0);
        var s = b1 - b0;
        var w = b0 - p;
        if (r == Vector2d.Zero) return PointOnSegment(-w, s);
        if (s == Vector2d.Zero) return PointOnSegment(w, r);
        var determinant = r.Cross(s);
        if (determinant == 0d)
        {
            if (w.Cross(r) != 0d) return false;
            var first = w.Dot(r);
            var second = (w + s).Dot(r);
            return Math.Min(first, second) <= r.LengthSquared() && Math.Max(first, second) >= 0d;
        }
        var u = w.Cross(s) / determinant;
        var v = w.Cross(r) / determinant;
        return u >= 0d && u <= 1d && v >= 0d && v <= 1d;
    }

    private static bool PointOnSegment(Vector2d offset, Vector2d direction)
    {
        if (direction == Vector2d.Zero) return offset == Vector2d.Zero;
        var along = offset.Dot(direction);
        return offset.Cross(direction) == 0d && along >= 0d && along <= direction.LengthSquared();
    }

    private static double MaxAbs(ReadOnlySpan<double> coefficients)
    {
        var maximum = 0d;
        foreach (var coefficient in coefficients) maximum = Math.Max(maximum, Math.Abs(coefficient));
        return maximum;
    }

    private static double SumAbs(ReadOnlySpan<double> coefficients)
    {
        var sum = 0d;
        foreach (var coefficient in coefficients) sum += Math.Abs(coefficient);
        return sum;
    }

    // A complex product applies the rotation numerator using the existing vector operations.
    private static Vector2d Multiply(Vector2d a, Vector2d b) => b * a.X + b.PerpCcw * a.Y;
    private static Vector2d Conjugate(Vector2d value) => new(value.X, -value.Y);

    private readonly struct Motion
    {
        internal readonly Vector2d Position, Displacement;
        private readonly Vector2d halfStart, halfDelta;

        internal Motion(RigidTransform2D start, RigidTransform2D end)
        {
            Ensure.Finite(start.Translation);
            Ensure.Finite(end.Translation);
            Ensure.Finite(start.Rotation);
            Ensure.Finite(end.Rotation);
            Ensure.ThrowIf(Math.Abs((double)end.Rotation - start.Rotation) >= Math.Tau,
                "Split rotations of a full revolution or more into smaller sweeps.", nameof(end));
            Position = start.Translation.ToVector2d();
            Displacement = end.Translation.ToVector2d() - Position;
            var (sinStart, cosStart) = Math.SinCos(.5d * start.Rotation);
            var (sinEnd, cosEnd) = Math.SinCos(.5d * end.Rotation);
            halfStart = new(cosStart, sinStart);
            halfDelta = new Vector2d(cosEnd, sinEnd) - halfStart;
            var lengthSquared = halfDelta.LengthSquared();
            var closest = lengthSquared == 0 ? 0 : Math.Clamp(-halfStart.Dot(halfDelta) / lengthSquared, 0, 1);
            Ensure.ThrowIf((halfStart + halfDelta * closest).LengthSquared() <= 1e-12,
                "The half-angle interpolation approaches zero; split the rotation into smaller sweeps.", nameof(end));
        }

        internal Vector2d RotationAt(double time)
        {
            var half = halfStart + halfDelta * time;
            return Multiply(half, half) / half.LengthSquared();
        }

        internal void WritePolynomials(Span<double> denominator, Span<Vector2d> rotation, bool inverse)
        {
            denominator[0] = halfStart.LengthSquared();
            denominator[1] = 2d * halfStart.Dot(halfDelta);
            denominator[2] = halfDelta.LengthSquared();
            rotation[0] = Multiply(halfStart, halfStart);
            rotation[1] = 2d * Multiply(halfStart, halfDelta);
            rotation[2] = Multiply(halfDelta, halfDelta);
            if (inverse)
                for (var i = 0; i < rotation.Length; i++) rotation[i] = Conjugate(rotation[i]);
        }
    }
}
