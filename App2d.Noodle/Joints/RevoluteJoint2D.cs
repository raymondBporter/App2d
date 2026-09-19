using System.Numerics;

namespace App2d.Noodle.Joints;

/// <summary>A fixed-length link whose relative angle may rotate within an interval.</summary>
internal sealed class RevoluteJoint2D : IJoint2D
{
    public RevoluteJoint2D(float length, float minimumAngle, float maximumAngle)
    {
        if (!float.IsFinite(length) || length <= 0f)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (!float.IsFinite(minimumAngle) || !float.IsFinite(maximumAngle) || minimumAngle > maximumAngle)
            throw new ArgumentException("The angular limits must be finite and ordered.");

        Length = length;
        MinimumAngle = minimumAngle;
        MaximumAngle = maximumAngle;
    }

    public string Name => "REVOLUTE";
    public string Constraint => "r = L,  min <= angle <= max";
    public float Length { get; }
    public float MinimumAngle { get; }
    public float MaximumAngle { get; }

    public Vector2 Constrain(Vector2 parentAnchor, Vector2 requestedChildAnchor)
    {
        var delta = requestedChildAnchor - parentAnchor;
        var angle = delta.LengthSquared() > float.Epsilon ? MathF.Atan2(delta.Y, delta.X) : 0f;
        angle = Math.Clamp(angle, MinimumAngle, MaximumAngle);
        return parentAnchor + Length * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}
