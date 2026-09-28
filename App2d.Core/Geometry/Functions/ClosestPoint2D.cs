using System.Numerics;

namespace App2d.Core.Geometry.Functions;

public readonly record struct SegmentClosestPoints2D(Vector2 First, Vector2 Second, float FirstParameter, float SecondParameter);

public static class ClosestPoint2D
{
    /// <summary>Closest point on an infinite line. Direction must be finite and nonzero; unit length is not required.</summary>
    public static Vector2 OnLine(Vector2 point, Vector2 origin, Vector2 direction)
    {
        var (x, y) = LinearGeometry2D.ClosestPoint(point, origin, direction, forwardOnly: false);
        return new((float)x, (float)y);
    }

    /// <summary>Closest point on a forward ray, clamped at its origin. Direction need not be unit length.</summary>
    public static Vector2 OnRay(Vector2 point, Vector2 origin, Vector2 direction)
    {
        var (x, y) = LinearGeometry2D.ClosestPoint(point, origin, direction, forwardOnly: true);
        return new((float)x, (float)y);
    }

    public static Vector2 OnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
            return start;

        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return start + segment * t;
    }

    public static SegmentClosestPoints2D BetweenSegments(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        var firstDirection = firstEnd - firstStart;
        var secondDirection = secondEnd - secondStart;
        var startDelta = firstStart - secondStart;
        var firstLengthSquared = firstDirection.LengthSquared();
        var secondLengthSquared = secondDirection.LengthSquared();
        float firstParameter;
        float secondParameter;

        if (firstLengthSquared <= float.Epsilon && secondLengthSquared <= float.Epsilon)
        {
            firstParameter = 0f;
            secondParameter = 0f;
        }
        else if (firstLengthSquared <= float.Epsilon)
        {
            firstParameter = 0f;
            secondParameter = Math.Clamp(Vector2.Dot(secondDirection, startDelta) / secondLengthSquared, 0f, 1f);
        }
        else if (secondLengthSquared <= float.Epsilon)
        {
            secondParameter = 0f;
            firstParameter = Math.Clamp(-Vector2.Dot(firstDirection, startDelta) / firstLengthSquared, 0f, 1f);
        }
        else
        {
            var firstDotDelta = Vector2.Dot(firstDirection, startDelta);
            var directionsDot = Vector2.Dot(firstDirection, secondDirection);
            var secondDotDelta = Vector2.Dot(secondDirection, startDelta);
            // In 2D, solve the line intersection with cross products. Subtracting squared dot
            // products loses precision near parallel, and an angle tolerance can miss crossings.
            var determinant = (double)firstDirection.X * secondDirection.Y - (double)firstDirection.Y * secondDirection.X;
            var numerator = (double)secondDirection.X * startDelta.Y - (double)secondDirection.Y * startDelta.X;
            firstParameter = determinant != 0d
                ? (float)Math.Clamp(numerator / determinant, 0d, 1d)
                : 0f;

            var secondNumerator = directionsDot * firstParameter + secondDotDelta;
            if (secondNumerator < 0f)
            {
                secondParameter = 0f;
                firstParameter = Math.Clamp(-firstDotDelta / firstLengthSquared, 0f, 1f);
            }
            else if (secondNumerator > secondLengthSquared)
            {
                secondParameter = 1f;
                firstParameter = Math.Clamp((directionsDot - firstDotDelta) / firstLengthSquared, 0f, 1f);
            }
            else
            {
                secondParameter = secondNumerator / secondLengthSquared;
            }
        }

        return new SegmentClosestPoints2D(firstStart + firstDirection * firstParameter, secondStart + secondDirection * secondParameter, firstParameter, secondParameter);
    }
}
