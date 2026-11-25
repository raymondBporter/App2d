using System.Numerics;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>
/// The rigid turn carrying one segment (a base and a tip, such as a blade's guard and tip) onto the next: <see cref="Angle"/>
/// radians about <see cref="Axis"/>. A turn in the XY plane uses the Z axis and a signed angle; one through depth has its own
/// axis and a positive angle. Interpolating along the turn keeps the tip on its arc instead of cutting a chord, which matters
/// when two samples are far apart (a fast swing between frames).
/// </summary>
public readonly record struct SegmentTurn3D(float Angle, Vector3f Axis)
{
    /// <summary>No turn: the first sample of a sequence.</summary>
    public static SegmentTurn3D None { get; } = new(0, Vector3f.UnitZ);

    public bool Planar => Axis == Vector3f.UnitZ;

    /// <summary>
    /// The turn from one segment to the next. In the XY plane it is signed about Z, and the shorter way round is right unless the
    /// step goes against the motion: past 150 degrees against <paramref name="previous"/> (a fast swing can pass that in one step),
    /// or, with no previous planar turn, past a quarter turn against <paramref name="sweep"/> (-1 clockwise, +1 counter-clockwise,
    /// 0 to infer). Through depth it turns about the axis the two segments share, with the same 150 degree rule against the
    /// previous turn's axis.
    /// </summary>
    public static SegmentTurn3D Between(Vector3f fromBase, Vector3f fromTip, Vector3f toBase, Vector3f toTip, SegmentTurn3D previous, int sweep = 0)
    {
        var a = fromTip - fromBase;
        var b = toTip - toBase;
        if (a.LengthSquared() < 1e-12f || b.LengthSquared() < 1e-12f)
        {
            return None;
        }

        var cross = Vector3f.Cross(Vector3f.Normalize(a), Vector3f.Normalize(b));
        var planar = cross.LengthSquared() < 1e-10f || MathF.Abs(cross.Z) > .97f * cross.Length();
        if (planar)
        {
            var d0 = a.XY;
            var d1 = b.XY;
            var turn = MathF.Atan2(d0.Cross(d1), Vector2f.Dot(d0, d1));
            var (expected, limit) = previous.Angle != 0 && previous.Planar ? (MathF.Sign(previous.Angle), 2.6f) : (Math.Sign(sweep), MathF.PI / 2);
            if (expected != 0 && MathF.Sign(turn) != expected && MathF.Abs(turn) > limit)
            {
                turn -= MathF.Sign(turn) * MathF.Tau;
            }

            return new(turn, Vector3f.UnitZ);
        }
        var axis = Vector3f.Normalize(cross);
        var angle = MathF.Atan2(cross.Length(), Vector3f.Dot(Vector3f.Normalize(a), Vector3f.Normalize(b)));
        if (previous.Angle != 0 && !previous.Planar && Vector3f.Dot(axis, previous.Axis) < 0 && angle > 2.6f)
        {
            (axis, angle) = (-axis, MathF.Tau - angle);
        }

        return new(angle, axis);
    }

    /// <summary>The length of the tip's arc from <paramref name="fromTip"/> to <paramref name="toTip"/> under this turn.</summary>
    public float ArcLength(Vector3f fromTip, Vector3f toTip)
    {
        var chord = Vector3f.Distance(fromTip, toTip);
        var half = MathF.Abs(Angle) / 2;
        return half < 5e-4f ? chord : chord * half / MathF.Sin(half);
    }

    /// <summary>
    /// The segment a fraction <paramref name="u"/> (clamped to 0..1) of the way through this turn. In the plane the base follows
    /// the arc about the shared pivot and depths blend; through depth the base moves straight and the segment turns about the axis.
    /// Lengths blend linearly.
    /// </summary>
    public (Vector3f Base, Vector3f Tip) Interpolate(Vector3f fromBase, Vector3f fromTip, Vector3f toBase, Vector3f toTip, float u)
    {
        u = Realf.Clamp01(u);
        if (!Planar)
        {
            var along = fromTip - fromBase;
            var length = float.Lerp(along.Length(), (toTip - toBase).Length(), u);
            var direction = Vector3f.Transform(Vector3f.Normalize(along), Quaternion.CreateFromAxisAngle(Axis.ToVector3(), Angle * u));
            var at = Vector3f.Lerp(fromBase, toBase, u);
            return (at, at + direction * length);
        }
        var baseZ = float.Lerp(fromBase.Z, toBase.Z, u);
        var tipZ = float.Lerp(fromTip.Z, toTip.Z, u);
        var d0 = (fromTip - fromBase).XY;
        var d1 = (toTip - toBase).XY;
        var flatLength = float.Lerp(d0.Length(), d1.Length(), u);
        var position = Rotation2D.InterpolateArc(fromBase.XY, toBase.XY, Angle, u);
        var flat = (d0.Length() > 1e-6f ? Vector2f.Normalize(d0) : Vector2f.UnitX).Rotate(Angle * u);
        return (new(position, baseZ), new(position + flat * flatLength, tipZ));
    }
}
