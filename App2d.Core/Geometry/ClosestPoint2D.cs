using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>The closest points of two segments and their parameters along each segment.</summary>
/// <param name="First">The closest point on the first segment.</param>
/// <param name="Second">The closest point on the second segment.</param>
/// <param name="FirstParameter">The position of <paramref name="First"/> along the first segment, in [0, 1].</param>
/// <param name="SecondParameter">The position of <paramref name="Second"/> along the second segment, in [0, 1].</param>
public readonly record struct SegmentClosestPoints2D(Vector2 First, Vector2 Second, float FirstParameter, float SecondParameter);

/// <summary>Closest points on raw primitives. Inputs are finite; a query point inside a filled primitive is returned unchanged only where documented.</summary>
public static class ClosestPoint2D
{
    /// <summary>The closest point on an infinite line.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="origin">A point on the line.</param>
    /// <param name="direction">A finite, nonzero direction; unit length is not required.</param>
    /// <returns>The perpendicular foot of the point on the line.</returns>
    public static Vector2 OnLine(Vector2 point, Vector2 origin, Vector2 direction)
    {
        var (x, y) = LinearGeometry2D.ClosestPoint(point, origin, direction, forwardOnly: false);
        return new((float)x, (float)y);
    }

    /// <summary>The closest point on a forward ray, clamped at its origin.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">A finite, nonzero direction; unit length is not required.</param>
    /// <returns>The nearest point on the ray.</returns>
    public static Vector2 OnRay(Vector2 point, Vector2 origin, Vector2 direction)
    {
        var (x, y) = LinearGeometry2D.ClosestPoint(point, origin, direction, forwardOnly: true);
        return new((float)x, (float)y);
    }

    /// <summary>The closest point on a segment, including its endpoints. A point-like segment returns its start.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="start">The segment start.</param>
    /// <param name="end">The segment end.</param>
    /// <returns>The nearest point on the segment.</returns>
    public static Vector2 OnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon) return start;
        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return start + segment * t;
    }

    /// <summary>The closest point in or on an ordered axis-aligned rectangle. Interior points are returned unchanged.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>The point clamped to the rectangle.</returns>
    public static Vector2 OnRectangle(Vector2 point, Vector2 min, Vector2 max) => Vector2.Clamp(point, min, max);

    /// <summary>The closest point on the perimeter of a circle. The center itself maps to the +X extreme.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The circle radius.</param>
    /// <returns>The nearest point on the circle boundary.</returns>
    public static Vector2 OnCirclePerimeter(Vector2 point, Vector2 center, float radius)
    {
        var offset = point - center;
        var length = offset.Length();
        return length <= float.Epsilon ? center + new Vector2(radius, 0f) : center + offset * (radius / length);
    }

    /// <summary>
    /// The closest point on the perimeter of an axis-aligned ellipse. Eberly's monotone root is bracketed
    /// and bisected in double precision, with explicit center and axis cases. No allocations or polygonizing.
    /// </summary>
    /// <param name="point">The query point.</param>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The positive half-extents along X and Y.</param>
    /// <returns>The nearest point on the ellipse boundary.</returns>
    public static Vector2 OnEllipsePerimeter(Vector2 point, Vector2 center, Vector2 radii)
    {
        var dx = (double)point.X - center.X;
        var dy = (double)point.Y - center.Y;
        var swapped = radii.X < radii.Y;
        var a = (double)(swapped ? radii.Y : radii.X);
        var b = (double)(swapped ? radii.X : radii.Y);
        var px = Math.Abs(swapped ? dy : dx);
        var py = Math.Abs(swapped ? dx : dy);
        double x, y;
        // 2D specialization of Geometric Tools DistPointHyperellipsoid, version 8.0.2025.05.10.
        // Copyright (c) 1998-2026 David Eberly, Boost Software License 1.0; see THIRD-PARTY-NOTICES.md.
        if (py == 0d)
        {
            var numerator = a * px;
            var denominator = a * a - b * b;
            if (numerator < denominator)
            {
                var ratio = numerator / denominator;
                x = a * ratio;
                y = b * Math.Sqrt(Math.Max(0d, 1d - ratio * ratio));
            }
            else if (px == 0d) { x = 0d; y = b; }
            else { x = a; y = 0d; }
        }
        else if (px == 0d) { x = 0d; y = b; }
        else
        {
            var zx = px / a;
            var zy = py / b;
            var normalizedSquared = zx * zx + zy * zy;
            var ratioSquared = (a / b) * (a / b);
            var numerator = ratioSquared * zx;
            // Shift Eberly's root by +1 so points very close to the major axis do not lose the minor term
            // through cancellation in s + 1. The bracket remains positive even for subnormal coordinates.
            var denominator = ratioSquared - 1d;
            var lower = zy;
            var largest = Math.Max(numerator, zy);
            var smallest = Math.Min(numerator, zy);
            var upper = normalizedSquared < 1d ? 1d : largest * Math.Sqrt(1d + (smallest / largest) * (smallest / largest));
            var root = 1d;
            if (normalizedSquared != 1d)
            {
                for (var i = 0; i < 2048; i++)
                {
                    root = (lower + upper) * .5d;
                    if (root == lower || root == upper) break;
                    var rx = numerator / (root + denominator);
                    var ry = zy / root;
                    var residual = rx * rx + ry * ry - 1d;
                    if (residual > 0d) lower = root;
                    else if (residual < 0d) upper = root;
                    else break;
                }
            }
            x = ratioSquared * px / (root + denominator);
            y = py / root;
        }
        var boundaryX = swapped ? y : x;
        var boundaryY = swapped ? x : y;
        return new((float)(center.X + (dx < 0d ? -boundaryX : boundaryX)), (float)(center.Y + (dy < 0d ? -boundaryY : boundaryY)));
    }

    /// <summary>The closest point on the perimeter of a polygon given in perimeter order, and the edge it lies on.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="vertices">At least one vertex; edges wrap from the last vertex to the first.</param>
    /// <param name="edgeIndex">The index of the edge (from vertex i to i + 1) holding the result.</param>
    /// <returns>The nearest point on the perimeter.</returns>
    public static Vector2 OnPolygonPerimeter(Vector2 point, ReadOnlySpan<Vector2> vertices, out int edgeIndex)
    {
        var closest = vertices[0];
        var bestDistanceSquared = float.PositiveInfinity;
        edgeIndex = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            var candidate = OnSegment(point, vertices[i], vertices[(i + 1) % vertices.Length]);
            var distanceSquared = Vector2.DistanceSquared(point, candidate);
            if (distanceSquared < bestDistanceSquared)
            {
                closest = candidate;
                bestDistanceSquared = distanceSquared;
                edgeIndex = i;
            }
        }
        return closest;
    }

    /// <summary>The closest point on the perimeter of a polygon given in perimeter order.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="vertices">At least one vertex; edges wrap from the last vertex to the first.</param>
    /// <returns>The nearest point on the perimeter.</returns>
    public static Vector2 OnPolygonPerimeter(Vector2 point, ReadOnlySpan<Vector2> vertices) => OnPolygonPerimeter(point, vertices, out _);

    /// <summary>The closest points between two segments, including parallel, crossing and point-like segments.</summary>
    /// <param name="firstStart">The first segment start.</param>
    /// <param name="firstEnd">The first segment end.</param>
    /// <param name="secondStart">The second segment start.</param>
    /// <param name="secondEnd">The second segment end.</param>
    /// <returns>Both closest points and their parameters; the points coincide when the segments cross.</returns>
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
            // In 2D the closest points of two non-parallel lines is their crossing; cross products keep precision near parallel.
            var determinant = (double)firstDirection.X * secondDirection.Y - (double)firstDirection.Y * secondDirection.X;
            var numerator = (double)secondDirection.X * startDelta.Y - (double)secondDirection.Y * startDelta.X;
            firstParameter = determinant != 0d ? (float)Math.Clamp(numerator / determinant, 0d, 1d) : 0f;

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
