using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Mathematics;

public static class Vector2Extensions
{
    public static readonly Vector2f NegativeInfinity = new(float.NegativeInfinity, float.NegativeInfinity);
    public static readonly Vector2f PositiveInfinity = new(float.PositiveInfinity, float.PositiveInfinity);

    extension(Vector2f value)
    {
        /// <summary>Unit vector at a finite angle in degrees, counter-clockwise from +X in Y-up space.</summary>
        public static Vector2f  FromAngleDegrees(float angleDegrees) => Polar2D.Direction(float.DegreesToRadians(angleDegrees));

        /// <summary>
        /// Snell's law for unit directions and unit normals pointing toward the incident medium.
        /// Indices must be finite and positive. False means total internal reflection; the output is then zero.
        /// </summary>
        public static bool TryRefract(Vector2f direction, Vector2f incidentNormal, float fromIndex, float toIndex, out Vector2f transmitted)
        {
            Validation.Ensure.Positive(fromIndex);
            Validation.Ensure.Positive(toIndex);
            // Rejection is shared vector math. Widen before applying the ratio so extreme valid indices
            // do not turn a normal-incidence zero tangent into 0 * infinity, or overflow its squared length.
            var tangent = Vector2f.Reject(direction, incidentNormal).ToVector2d() * ((double)fromIndex / toIndex);
            var normalSquared = 1d - tangent.LengthSquared();
            if (normalSquared < 0)
            {
                transmitted = default;
                return false;
            }
            transmitted = (tangent - incidentNormal.ToVector2d() * Math.Sqrt(normalSquared)).ToVector2f();
            return true;
        }

        /// <summary>Vector from a finite, nonnegative radius and a finite angle in radians.</summary>
        public static Vector2f  FromPolar(float radius, float angleRadians) => Polar2D.ToCartesian(radius, angleRadians);

        /// <summary>Heading in radians from +X, counter-clockwise in Y-up space. Zero vectors return zero.</summary>
        public float AngleRadians => Polar2D.AngleOf(value);

        public Polar2D ToPolar() => Polar2D.FromCartesian(value);

        /// <summary>Rotates counter-clockwise by radians in Y-up coordinates.</summary>
        public Vector2f Rotate(float radians) => Rotation2D.Apply(value, radians);

        /// <summary>Rotates this point about a pivot.</summary>
        public Vector2f RotateAround(Vector2f pivot, float radians) => Rotation2D.ApplyAround(value, pivot, radians);

        /// <summary>The perpendicular dot product in float arithmetic; positive for a counter-clockwise turn in Y-up space.</summary>
        public float Cross(Vector2f right) => value.X * right.Y - value.Y * right.X;

        /// <summary>Twice the signed triangle area in float arithmetic; positive for a counter-clockwise turn in Y-up space.</summary>
        public float Orientation(Vector2f b, Vector2f c) => (b - value).Cross(c - value);

        /// <summary>Rotates 90° counter-clockwise (in +Y-up orientation).</summary>
        public Vector2f PerpCcw => new(-value.Y, value.X);

        /// <summary>Rotates 90° clockwise (in +Y-up orientation).</summary>
        public Vector2f PerpCw => new(value.Y, -value.X);
    }
}
