using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Mathematics;

public static class Vector2Extensions
{
    public static readonly Vector2 NegativeInfinity = new(float.NegativeInfinity, float.NegativeInfinity);
    public static readonly Vector2 PositiveInfinity = new(float.PositiveInfinity, float.PositiveInfinity);

    extension(Vector2 value)
    {
        /// <summary>Unit vector at a finite angle in radians, counter-clockwise from +X in Y-up space.</summary>
        public static Vector2 FromAngle(float angleRadians) => Polar2D.Direction(angleRadians);

        /// <summary>Unit vector at a finite angle in degrees, counter-clockwise from +X in Y-up space.</summary>
        public static Vector2 FromAngleDegrees(float angleDegrees) => Polar2D.Direction(float.DegreesToRadians(angleDegrees));

        /// <summary>Removes the component parallel to a unit normal. The normal must already be normalized.</summary>
        public static Vector2 Reject(Vector2 vector, Vector2 unitNormal) =>
            Vector2f.Reject(new(vector), new(unitNormal)).ToVector2();

        /// <summary>
        /// Snell's law for unit directions and unit normals pointing toward the incident medium.
        /// Indices must be finite and positive. False means total internal reflection; the output is then zero.
        /// </summary>
        public static bool TryRefract(Vector2 direction, Vector2 incidentNormal, float fromIndex, float toIndex, out Vector2 transmitted)
        {
            Validation.ArgGuard.Positive(fromIndex);
            Validation.ArgGuard.Positive(toIndex);
            // Rejection is shared vector math. Widen before applying the ratio so extreme valid indices
            // do not turn a normal-incidence zero tangent into 0 * infinity, or overflow its squared length.
            var tangent = Vector2.Reject(direction, incidentNormal).ToVector2d() * ((double)fromIndex / toIndex);
            var normalSquared = 1d - tangent.LengthSquared();
            if (normalSquared < 0)
            {
                transmitted = default;
                return false;
            }
            transmitted = (tangent - incidentNormal.ToVector2d() * Math.Sqrt(normalSquared)).ToVector2();
            return true;
        }

        /// <summary>Vector from a finite, nonnegative radius and a finite angle in radians.</summary>
        public static Vector2 FromPolar(float radius, float angleRadians) => Polar2D.ToCartesian(radius, angleRadians);

        /// <summary>Widens both coordinates before double-precision arithmetic, including subtraction.</summary>
        public Vector2d ToVector2d() => new(value);

        public Vector2f ToVector2f() => new(value);

        /// <summary>True when every component is finite, regardless of the vector's length.</summary>
        public bool IsFinite() => Vector2f.IsFinite(new(value));

        /// <summary>Heading in radians from +X, counter-clockwise in Y-up space. Zero vectors return zero.</summary>
        public float AngleRadians => Polar2D.AngleOf(value);

        public Polar2D ToPolar() => Polar2D.FromCartesian(value);

        /// <summary>Rotates counter-clockwise by radians in Y-up coordinates.</summary>
        public Vector2 Rotate(float radians) => Rotation2D.Apply(value, radians);

        /// <summary>Rotates this point about a pivot.</summary>
        public Vector2 RotateAround(Vector2 pivot, float radians) => Rotation2D.ApplyAround(value, pivot, radians);

        /// <summary>The perpendicular dot product in float arithmetic; positive for a counter-clockwise turn in Y-up space.</summary>
        public float Cross(Vector2 right) => value.X * right.Y - value.Y * right.X;

        /// <summary>Twice the signed triangle area in float arithmetic; positive for a counter-clockwise turn in Y-up space.</summary>
        public float Orientation(Vector2 b, Vector2 c) => (b - value).Cross(c - value);

        /// <summary>Rotates 90° counter-clockwise (in +Y-up orientation).</summary>
        public Vector2 PerpCcw => new(-value.Y, value.X);

        /// <summary>Rotates 90° clockwise (in +Y-up orientation).</summary>
        public Vector2 PerpCw => new(value.Y, -value.X);
    }
}
