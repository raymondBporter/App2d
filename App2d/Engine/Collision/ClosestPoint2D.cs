using System.Numerics;

namespace App2d.Engine.Collision;

public readonly record struct SegmentClosestPoints2D(
    Vector2 First,
    Vector2 Second,
    float FirstParameter,
    float SecondParameter);

public static class ClosestPoint2D
{
    public static Vector2 OnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
            return start;

        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return start + segment * t;
    }

    public static SegmentClosestPoints2D BetweenSegments(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd)
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
        else
        {
            var firstDotDelta = Vector2.Dot(firstDirection, startDelta);
            if (secondLengthSquared <= float.Epsilon)
            {
                secondParameter = 0f;
                firstParameter = Math.Clamp(-firstDotDelta / firstLengthSquared, 0f, 1f);
            }
            else
            {
                var directionsDot = Vector2.Dot(firstDirection, secondDirection);
                var secondDotDelta = Vector2.Dot(secondDirection, startDelta);
                var denominator = firstLengthSquared * secondLengthSquared - directionsDot * directionsDot;
                var parallelTolerance = 0.000001f * firstLengthSquared * secondLengthSquared;

                firstParameter = MathF.Abs(denominator) > parallelTolerance
                    ? Math.Clamp((directionsDot * secondDotDelta - firstDotDelta * secondLengthSquared) / denominator, 0f, 1f)
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
        }

        return new SegmentClosestPoints2D(firstStart + firstDirection * firstParameter, secondStart + secondDirection * secondParameter, firstParameter, secondParameter);
    }
}
