using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Static 2D conveniences that retain the concrete float vector type.</summary>
public static class FloatVector2Extensions
{
    extension<TSelf>(TSelf value) where TSelf : unmanaged, IVector2<TSelf, float>
    {
        public static TSelf Direction(float radians) => Vector2Math<TSelf, float>.Direction(radians);
        public static TSelf FromAngle(float radians) => Vector2Math<TSelf, float>.Direction(radians);
        public static TSelf FromAngleDegrees(float degrees) => Vector2Math<TSelf, float>.FromAngleDegrees(degrees);
        public static TSelf FromPolar(float radius, float radians) => Vector2Math<TSelf, float>.FromPolar(radius, radians);
        public static bool TryRefract(TSelf direction, TSelf normal, float fromIndex, float toIndex, out TSelf transmitted) =>
            Vector2Math<TSelf, float>.TryRefract(direction, normal, fromIndex, toIndex, out transmitted);
        public static TSelf Transform(TSelf point, Matrix3x2 matrix) => Vector2Math<TSelf, float>.Transform(point, matrix);
        public static TSelf TransformNormal(TSelf direction, Matrix3x2 matrix) => Vector2Math<TSelf, float>.TransformNormal(direction, matrix);
    }
}

/// <summary>Static 2D conveniences that retain the concrete double vector type.</summary>
public static class DoubleVector2Extensions
{
    extension<TSelf>(TSelf value) where TSelf : unmanaged, IVector2<TSelf, double>
    {
        public static TSelf Direction(double radians) => Vector2Math<TSelf, double>.Direction(radians);
        public static TSelf FromAngle(double radians) => Vector2Math<TSelf, double>.FromAngle(radians);
        public static TSelf FromAngleDegrees(double degrees) => Vector2Math<TSelf, double>.FromAngleDegrees(degrees);
        public static TSelf FromPolar(double radius, double radians) => Vector2Math<TSelf, double>.FromPolar(radius, radians);
        public static bool TryRefract(TSelf direction, TSelf normal, double fromIndex, double toIndex, out TSelf transmitted) =>
            Vector2Math<TSelf, double>.TryRefract(direction, normal, fromIndex, toIndex, out transmitted);
        public static TSelf Transform(TSelf point, Matrix3x2 matrix) => Vector2Math<TSelf, double>.Transform(point, matrix);
        public static TSelf TransformNormal(TSelf direction, Matrix3x2 matrix) => Vector2Math<TSelf, double>.TransformNormal(direction, matrix);
    }
}
