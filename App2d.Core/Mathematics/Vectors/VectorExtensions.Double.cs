using App2d.Core.Mathematics.Vectors;
using System.Numerics;

namespace App2d.Core.Mathematics;

// The double twin of VectorExtensions.Float.cs: the same members with float replaced by double.
// See that file for why the scalar is fixed per file and why instance and static members are split.

/// <summary>Instance members shared by every double vector: v.Length(), a.Dot(b).</summary>
public static class DoubleVectorMembers
{
    extension<TSelf>(TSelf value) where TSelf : unmanaged, IVector<TSelf, double>
    {
        public double Dot(TSelf other) => TSelf.Dot(value, other);
        public TSelf Min(TSelf other) => TSelf.Min(value, other);
        public TSelf Max(TSelf other) => TSelf.Max(value, other);
        public TSelf Abs() => TSelf.Abs(value);
        public bool IsFinite() => TSelf.IsFinite(value);
        public double LengthSquared() => TSelf.LengthSquared(value);
        public double Length() => TSelf.Length(value);
        public TSelf Normalized() => TSelf.Normalize(value);
        public double DistanceSquared(TSelf other) => TSelf.DistanceSquared(value, other);
        public double Distance(TSelf other) => TSelf.Distance(value, other);
        public TSelf Lerp(TSelf other, double amount) => TSelf.Lerp(value, other, amount);
        public TSelf Midpoint(TSelf other) => TSelf.Midpoint(value, other);
        public TSelf Project(TSelf unitNormal) => TSelf.Project(value, unitNormal);
        public TSelf Reject(TSelf unitNormal) => TSelf.Reject(value, unitNormal);
        public TSelf Reflect(TSelf unitNormal) => TSelf.Reflect(value, unitNormal);
        public TSelf Clamp(TSelf minimum, TSelf maximum) => TSelf.Clamp(value, minimum, maximum);
    }

    extension<TSelf>(TSelf value) where TSelf : unmanaged, IVector2<TSelf, double>
    {
        public double Cross(TSelf other) => TSelf.Cross(value, other);
        public double Orientation(TSelf second, TSelf third) => TSelf.Orientation(value, second, third);
        public double AngleRadians => TSelf.AngleRadians(value);
        public TSelf PerpCcw => TSelf.PerpCcw(value);
        public TSelf PerpCw => TSelf.PerpCw(value);
        public TSelf Rotate(double radians) => TSelf.Rotate(value, radians);
        public TSelf Rotate(double radians, TSelf pivot) => TSelf.Rotate(value, radians, pivot);
        public Polar<double> ToPolar() => TSelf.ToPolar(value);
        public Vector2 ToVector2() => TSelf.ToVector2(value);
        public Vector2f ToVector2f() => TSelf.ToVector2f(value);
        public Vector2d ToVector2d() => TSelf.ToVector2d(value);
    }
}

/// <summary>Static members shared by every double vector: Vector3d.Distance(a, b), Vector2d.FromAngle(r).</summary>
public static class DoubleVectorStatics
{
    extension<TSelf>(TSelf) where TSelf : unmanaged, IVector<TSelf, double>
    {
        public static VectorChannels<TSelf, double> Channels => VectorChannels<TSelf, double>.Instance;
        public static double Dot(TSelf left, TSelf right) => TSelf.Dot(left, right);
        public static TSelf Min(TSelf left, TSelf right) => TSelf.Min(left, right);
        public static TSelf Max(TSelf left, TSelf right) => TSelf.Max(left, right);
        public static TSelf Abs(TSelf value) => TSelf.Abs(value);
        public static bool IsFinite(TSelf value) => TSelf.IsFinite(value);
        public static double LengthSquared(TSelf value) => TSelf.LengthSquared(value);
        public static double Length(TSelf value) => TSelf.Length(value);
        public static TSelf Normalize(TSelf value) => TSelf.Normalize(value);
        public static double DistanceSquared(TSelf first, TSelf second) => TSelf.DistanceSquared(first, second);
        public static double Distance(TSelf first, TSelf second) => TSelf.Distance(first, second);
        public static TSelf Lerp(TSelf first, TSelf second, double amount) => TSelf.Lerp(first, second, amount);
        public static TSelf Midpoint(TSelf first, TSelf second) => TSelf.Midpoint(first, second);
        public static TSelf Project(TSelf value, TSelf unitNormal) => TSelf.Project(value, unitNormal);
        public static TSelf Reject(TSelf value, TSelf unitNormal) => TSelf.Reject(value, unitNormal);
        public static TSelf Reflect(TSelf value, TSelf unitNormal) => TSelf.Reflect(value, unitNormal);
        public static TSelf Clamp(TSelf value, TSelf minimum, TSelf maximum) => TSelf.Clamp(value, minimum, maximum);
    }

    extension<TSelf>(TSelf) where TSelf : unmanaged, IVector2<TSelf, double>
    {
        public static double Cross(TSelf first, TSelf second) => TSelf.Cross(first, second);
        public static double Orientation(TSelf first, TSelf second, TSelf third) => TSelf.Orientation(first, second, third);
        public static TSelf Direction(double radians) => TSelf.Direction(radians);
        public static TSelf FromAngle(double radians) => TSelf.FromAngle(radians);
        public static TSelf FromAngleDegrees(double degrees) => TSelf.FromAngleDegrees(degrees);
        public static TSelf FromPolar(double radius, double radians) => TSelf.FromPolar(radius, radians);
        public static TSelf Rotate(TSelf value, double radians) => TSelf.Rotate(value, radians);
        public static TSelf Rotate(TSelf value, double radians, TSelf pivot) => TSelf.Rotate(value, radians, pivot);
        public static bool TryRefract(TSelf direction, TSelf normal, double fromIndex, double toIndex, out TSelf transmitted) => TSelf.TryRefract(direction, normal, fromIndex, toIndex, out transmitted);
        public static TSelf Transform(TSelf point, Matrix3x2 matrix) => TSelf.Transform(point, matrix);
        public static TSelf TransformNormal(TSelf direction, Matrix3x2 matrix) => TSelf.TransformNormal(direction, matrix);
    }
}

/// <summary>Instance members of every double 3D vector: a.Cross(b). A class of its own because this Cross would collide with the 2D one above.</summary>
public static class DoubleVector3Members
{
    extension<TSelf>(TSelf value) where TSelf : unmanaged, IVector3<TSelf, double>
    {
        public TSelf Cross(TSelf other) => TSelf.Cross(value, other);
    }
}

/// <summary>Static members of every double 3D vector: Vector3d.Cross(a, b).</summary>
public static class DoubleVector3Statics
{
    extension<TSelf>(TSelf) where TSelf : unmanaged, IVector3<TSelf, double>
    {
        public static TSelf Cross(TSelf first, TSelf second) => TSelf.Cross(first, second);
    }
}
