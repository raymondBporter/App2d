using System.Numerics;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>
/// The rigid turn carrying one segment (a base and a tip, such as a blade's guard and tip) onto the next: <see cref="Angle"/>
/// radians about <see cref="Axis"/>. A turn in the XY plane uses the Z axis and a signed angle; one through depth has its own
/// axis and a positive angle. Interpolating along the turn keeps the tip on its arc instead of cutting a chord, which matters
/// when two samples are far apart (a fast swing between frames).
/// </summary>
public readonly record struct SegmentTurn3D(float Angle, Vector3 Axis)
{
    /// <summary>No turn: the first sample of a sequence.</summary>
    public static SegmentTurn3D None { get; } = new(0, Vector3.UnitZ);

    public bool Planar => Axis == Vector3.UnitZ;

    /// <summary>
    /// The turn from one segment to the next. In the XY plane it is signed about Z, and the shorter way round is right unless the
    /// step goes against the motion: past 150 degrees against <paramref name="previous"/> (a fast swing can pass that in one step),
    /// or, with no previous planar turn, past a quarter turn against <paramref name="sweep"/> (-1 clockwise, +1 counter-clockwise,
    /// 0 to infer). Through depth it turns about the axis the two segments share, with the same 150 degree rule against the
    /// previous turn's axis.
    /// </summary>
    public static SegmentTurn3D Between(Vector3 fromBase, Vector3 fromTip, Vector3 toBase, Vector3 toTip, SegmentTurn3D previous, int sweep = 0)
    {
        var a = fromTip - fromBase;
        var b = toTip - toBase;
        if (a.LengthSquared() < 1e-12f || b.LengthSquared() < 1e-12f)
        {
            return None;
        }

        var cross = Vector3.Cross(Vector3.Normalize(a), Vector3.Normalize(b));
        var planar = cross.LengthSquared() < 1e-10f || MathF.Abs(cross.Z) > .97f * cross.Length();
        if (planar)
        {
            var d0 = a.XY;
            var d1 = b.XY;
            var turn = MathF.Atan2(d0.Cross(d1), Vector2.Dot(d0, d1));
            var (expected, limit) = previous.Angle != 0 && previous.Planar ? (MathF.Sign(previous.Angle), 2.6f) : (Math.Sign(sweep), MathF.PI / 2);
            if (expected != 0 && MathF.Sign(turn) != expected && MathF.Abs(turn) > limit)
            {
                turn -= MathF.Sign(turn) * MathF.Tau;
            }

            return new(turn, Vector3.UnitZ);
        }
        var axis = Vector3.Normalize(cross);
        var angle = MathF.Atan2(cross.Length(), Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)));
        if (previous.Angle != 0 && !previous.Planar && Vector3.Dot(axis, previous.Axis) < 0 && angle > 2.6f)
        {
            (axis, angle) = (-axis, MathF.Tau - angle);
        }

        return new(angle, axis);
    }

    /// <summary>The length of the tip's arc from <paramref name="fromTip"/> to <paramref name="toTip"/> under this turn.</summary>
    public float ArcLength(Vector3 fromTip, Vector3 toTip)
    {
        var chord = Vector3.Distance(fromTip, toTip);
        var half = MathF.Abs(Angle) / 2;
        return half < 5e-4f ? chord : chord * half / MathF.Sin(half);
    }

    /// <summary>
    /// The segment a fraction <paramref name="u"/> (clamped to 0..1) of the way through this turn. In the plane the base follows
    /// the arc about the shared pivot and depths blend; through depth the base moves straight and the segment turns about the axis.
    /// Lengths blend linearly.
    /// </summary>
    public (Vector3 Base, Vector3 Tip) Interpolate(Vector3 fromBase, Vector3 fromTip, Vector3 toBase, Vector3 toTip, float u)
    {
        u = Math.Clamp(u, 0, 1);
        if (!Planar)
        {
            var along = fromTip - fromBase;
            var length = float.Lerp(along.Length(), (toTip - toBase).Length(), u);
            var direction = Vector3.Transform(Vector3.Normalize(along), Quaternion.CreateFromAxisAngle(Axis, Angle * u));
            var at = Vector3.Lerp(fromBase, toBase, u);
            return (at, at + direction * length);
        }
        var baseZ = float.Lerp(fromBase.Z, toBase.Z, u);
        var tipZ = float.Lerp(fromTip.Z, toTip.Z, u);
        var d0 = (fromTip - fromBase).XY;
        var d1 = (toTip - toBase).XY;
        var flatLength = float.Lerp(d0.Length(), d1.Length(), u);
        var position = Rotation2D.InterpolateArc(fromBase.XY, toBase.XY, Angle, u);
        var flat = Rotation2D.Apply(d0.Length() > 1e-6f ? Vector2.Normalize(d0) : Vector2.UnitX, Angle * u);
        return (new(position, baseZ), new(position + flat * flatLength, tipZ));
    }
}
