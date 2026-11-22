using System.Numerics;

namespace App2d.Core.Mathematics;

// These blocks are what give every float vector struct its members without the struct writing
// them: v.Length(), a.Dot(b), Vector3f.Distance(a, b). Each line only forwards to the interface
// static, which resolves to the struct's own version when it has one. A member a struct declares
// itself always wins over the one here.
//
// C# cannot infer the scalar type from "where TSelf : IVector<TSelf, TReal>", so the scalar is fixed
// per file. VectorExtensions.Double.cs is this file with float replaced by double; keep them in step
// (VectorExtensionParityTests fails if they drift). Instance and static members sit in separate
// classes because a.Dot(b) and T.Dot(a, b) would otherwise compile to the same method.

/// <summary>Instance members shared by every float vector: v.Length(), a.Dot(b).</summary>
public static class FloatVectorMembers
{
    extension<TSelf>(TSelf value) where TSelf : unmanaged, IVector<TSelf, float>
    {
        public float Dot(TSelf other) => TSelf.Dot(value, other);
        public TSelf Min(TSelf other) => TSelf.Min(value, other);
        public TSelf Max(TSelf other) => TSelf.Max(value, other);
        public TSelf Abs() => TSelf.Abs(value);
        public bool IsFinite() => TSelf.IsFinite(value);
        public float LengthSquared() => TSelf.LengthSquared(value);
        public float Length() => TSelf.Length(value);
        public TSelf Normalized() => TSelf.Normalize(value);
        public float DistanceSquared(TSelf other) => TSelf.DistanceSquared(value, other);
        public float Distance(TSelf other) => TSelf.Distance(value, other);
        public TSelf Lerp(TSelf other, float amount) => TSelf.Lerp(value, other, amount);
        public TSelf Midpoint(TSelf other) => TSelf.Midpoint(value, other);
        public TSelf Project(TSelf unitNormal) => TSelf.Project(value, unitNormal);
        public TSelf Reject(TSelf unitNormal) => TSelf.Reject(value, unitNormal);
        public TSelf Reflect(TSelf unitNormal) => TSelf.Reflect(value, unitNormal);
        public TSelf Clamp(TSelf minimum, TSelf maximum) => TSelf.Clamp(value, minimum, maximum);
    }

    extension<TSelf>(TSelf value) where TSelf : unmanaged, IVector2<TSelf, float>
    {
        public float Cross(TSelf other) => TSelf.Cross(value, other);
        public float Orientation(TSelf second, TSelf third) => TSelf.Orientation(value, second, third);
        public float AngleRadians => TSelf.AngleRadians(value);
        public TSelf PerpCcw => TSelf.PerpCcw(value);
        public TSelf PerpCw => TSelf.PerpCw(value);
        public TSelf Rotate(float radians) => TSelf.Rotate(value, radians);
        public TSelf Rotate(float radians, TSelf pivot) => TSelf.Rotate(value, radians, pivot);
        public Polar<float> ToPolar() => TSelf.ToPolar(value);
        public Vector2 ToVector2() => TSelf.ToVector2(value);
        public Vector2f ToVector2f() => TSelf.ToVector2f(value);
        public Vector2d ToVector2d() => TSelf.ToVector2d(value);
    }
}

/// <summary>Static members shared by every float vector: Vector3f.Distance(a, b), Vector2f.FromAngle(r).</summary>
public static class FloatVectorStatics
{
    extension<TSelf>(TSelf) where TSelf : unmanaged, IVector<TSelf, float>
    {
        public static VectorChannels<TSelf, float> Channels => VectorChannels<TSelf, float>.Instance;
        public static float Dot(TSelf left, TSelf right) => TSelf.Dot(left, right);
        public static TSelf Min(TSelf left, TSelf right) => TSelf.Min(left, right);
        public static TSelf Max(TSelf left, TSelf right) => TSelf.Max(left, right);
        public static TSelf Abs(TSelf value) => TSelf.Abs(value);
        public static bool IsFinite(TSelf value) => TSelf.IsFinite(value);
        public static float LengthSquared(TSelf value) => TSelf.LengthSquared(value);
        public static float Length(TSelf value) => TSelf.Length(value);
        public static TSelf Normalize(TSelf value) => TSelf.Normalize(value);
        public static float DistanceSquared(TSelf first, TSelf second) => TSelf.DistanceSquared(first, second);
        public static float Distance(TSelf first, TSelf second) => TSelf.Distance(first, second);
        public static TSelf Lerp(TSelf first, TSelf second, float amount) => TSelf.Lerp(first, second, amount);
        public static TSelf Midpoint(TSelf first, TSelf second) => TSelf.Midpoint(first, second);
        public static TSelf Project(TSelf value, TSelf unitNormal) => TSelf.Project(value, unitNormal);
        public static TSelf Reject(TSelf value, TSelf unitNormal) => TSelf.Reject(value, unitNormal);
        public static TSelf Reflect(TSelf value, TSelf unitNormal) => TSelf.Reflect(value, unitNormal);
        public static TSelf Clamp(TSelf value, TSelf minimum, TSelf maximum) => TSelf.Clamp(value, minimum, maximum);
    }

    extension<TSelf>(TSelf) where TSelf : unmanaged, IVector2<TSelf, float>
    {
        public static float Cross(TSelf first, TSelf second) => TSelf.Cross(first, second);
        public static float Orientation(TSelf first, TSelf second, TSelf third) => TSelf.Orientation(first, second, third);
        public static TSelf Direction(float radians) => TSelf.Direction(radians);
        public static TSelf FromAngle(float radians) => TSelf.FromAngle(radians);
        public static TSelf FromAngleDegrees(float degrees) => TSelf.FromAngleDegrees(degrees);
        public static TSelf FromPolar(float radius, float radians) => TSelf.FromPolar(radius, radians);
        public static TSelf Rotate(TSelf value, float radians) => TSelf.Rotate(value, radians);
        public static TSelf Rotate(TSelf value, float radians, TSelf pivot) => TSelf.Rotate(value, radians, pivot);
        public static bool TryRefract(TSelf direction, TSelf normal, float fromIndex, float toIndex, out TSelf transmitted) => TSelf.TryRefract(direction, normal, fromIndex, toIndex, out transmitted);
        public static TSelf Transform(TSelf point, Matrix3x2 matrix) => TSelf.Transform(point, matrix);
        public static TSelf TransformNormal(TSelf direction, Matrix3x2 matrix) => TSelf.TransformNormal(direction, matrix);
    }
}

/// <summary>Instance members of every float 3D vector: a.Cross(b). A class of its own because this Cross would collide with the 2D one above.</summary>
public static class FloatVector3Members
{
    extension<TSelf>(TSelf value) where TSelf : unmanaged, IVector3<TSelf, float>
    {
        public TSelf Cross(TSelf other) => TSelf.Cross(value, other);
    }
}

/// <summary>Static members of every float 3D vector: Vector3d.Cross(a, b).</summary>
public static class FloatVector3Statics
{
    extension<TSelf>(TSelf) where TSelf : unmanaged, IVector3<TSelf, float>
    {
        public static TSelf Cross(TSelf first, TSelf second) => TSelf.Cross(first, second);
    }
}
