using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry;

public static partial class Area2D
{
    private const double ConicCoincidenceTolerance = 8d * 2.2204460492503131e-16;

    // Each convex part has its interior on the left of its boundary. Integrating only the
    // uncovered pieces counts every point once, including when several parts overlap or enclose a hole.
    internal static float Union(ReadOnlySpan<UnionPart> parts)
    {
        var origin = parts[0].Origin;
        foreach (var part in parts) part.Shift(origin);
        var covered = new List<Interval<double>>();
        var angles = new List<double>();
        var area = 0d;
        var compensation = 0d;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.IsCurve)
            {
                covered.Clear();
                for (var j = 0; j < parts.Length; j++)
                {
                    if (j == i || !part.Overlaps(parts[j])) continue;
                    var other = parts[j];
                    if (part.IsCircle && other.IsCircle) CoverCircle(part, other, j < i, covered);
                    else if (other.IsCurve) CoverCurveWithCurve(part, other, j < i, covered, angles);
                    else CoverCurveWithPolygon(part, other, covered, angles);
                }
                AddArea(IntegrateExposed(covered, Math.Tau, (from, to) => ArcArea(part, from, to)));
            }
            else
            {
                for (var edge = 0; edge < part.Vertices.Length; edge++)
                {
                    var start = part.Vertices[edge];
                    var direction = part.Vertices[(edge + 1) % part.Vertices.Length] - start;
                    if (direction == default) continue;
                    covered.Clear();
                    for (var j = 0; j < parts.Length; j++)
                    {
                        if (j == i || !part.Overlaps(parts[j])) continue;
                        var relativeStart = part.Origin - parts[j].Origin + start;
                        if (parts[j].IsCurve) CoverSegmentWithCurve(relativeStart, direction, parts[j], covered);
                        else CoverSegmentWithPolygon(relativeStart, direction, parts[j], j < i, covered);
                    }
                    AddArea(.5d * (start.Cross(direction) + part.Origin.Cross(direction)) * ExposedLength(covered, 1d));
                }
            }
        }
        return (float)Math.Max(0d, area);

        void AddArea(double value)
        {
            var adjusted = value - compensation;
            var sum = area + adjusted;
            compensation = sum - area - adjusted;
            area = sum;
        }
    }

    private static void CoverCircle(UnionPart circle, UnionPart other, bool preferOther, List<Interval<double>> covered)
    {
        var delta = other.Center - circle.Center;
        var distance = delta.Length();
        if (circle.Radius == other.Radius && distance <= ConicCoincidenceTolerance * circle.Radius)
        {
            if (preferOther) covered.Add(new(0d, Math.Tau));
            return;
        }
        if (distance + circle.Radius <= other.Radius)
        {
            covered.Add(new(0d, Math.Tau));
            return;
        }
        if (distance >= circle.Radius + other.Radius || distance + other.Radius <= circle.Radius) return;
        var cosine = (distance * distance + circle.Radius * circle.Radius - other.Radius * other.Radius) / (2d * distance * circle.Radius);
        var halfAngle = Math.Acos(Math.Clamp(cosine, -1d, 1d));
        var start = PositiveAngle(Math.Atan2(delta.Y, delta.X) - halfAngle);
        var end = start + 2d * halfAngle;
        covered.Add(new(start, Math.Min(end, Math.Tau)));
        if (end > Math.Tau) covered.Add(new(0d, end - Math.Tau));
    }

    private static void CoverCurveWithCurve(UnionPart curve, UnionPart other, bool preferOther, List<Interval<double>> covered, List<double> angles)
    {
        var offset = other.NormalizeRelative(curve.Center - other.Center);
        // Treat a displacement below conic evaluation precision as a coincident boundary.
        if (curve.Radii == other.Radii && Math.Abs(offset.X) <= ConicCoincidenceTolerance && Math.Abs(offset.Y) <= ConicCoincidenceTolerance)
        {
            if (preferOther) covered.Add(new(0d, Math.Tau));
            return;
        }
        angles.Clear();
        angles.Add(0d);
        angles.Add(Math.Tau);
        var sx = curve.Radii.X / other.Radii.X;
        var sy = curve.Radii.Y / other.Radii.Y;
        Span<double> coefficients = stackalloc double[5];
        Span<double> roots = stackalloc double[4];
        for (var chart = 0; chart < 2; chart++)
        {
            // x = cx + rx*cos(theta), y = cy + ry*sin(theta). Substituting
            // t = tan(theta/2) in the other ellipse's implicit equation gives a quartic.
            // Two charts, each with t in [-1,1], cover the perimeter without an infinite root at PI.
            var sign = chart == 0 ? 1d : -1d;
            var x0 = offset.X + sign * sx;
            var x2 = offset.X - sign * sx;
            coefficients[0] = x0 * x0 + offset.Y * offset.Y - 1d;
            coefficients[1] = 4d * offset.Y * sign * sy;
            coefficients[2] = 2d * x0 * x2 + 2d * offset.Y * offset.Y + 4d * sy * sy - 2d;
            coefficients[3] = coefficients[1];
            coefficients[4] = x2 * x2 + offset.Y * offset.Y - 1d;
            var count = PolynomialRoots.FindRealRoots(coefficients, -1d, 1d, roots);
            for (var i = 0; i < count; i++) angles.Add(PositiveAngle(chart * Math.PI + 2d * Math.Atan(roots[i])));
        }
        CoverArcsInside(curve, other, covered, angles);
    }

    private static void CoverCurveWithPolygon(UnionPart curve, UnionPart polygon, List<Interval<double>> covered, List<double> angles)
    {
        angles.Clear();
        angles.Add(0d);
        angles.Add(Math.Tau);
        var offset = polygon.Origin - curve.Origin;
        for (var i = 0; i < polygon.Vertices.Length; i++)
        {
            var vertex = polygon.Vertices[i];
            var start = offset + vertex;
            var direction = polygon.Vertices[(i + 1) % polygon.Vertices.Length] - vertex;
            var normalized = curve.NormalizeRelative(start);
            var magnitude = 1d + (Math.Abs(offset.X) + Math.Abs(vertex.X)) / curve.Radii.X
                + (Math.Abs(offset.Y) + Math.Abs(vertex.Y)) / curve.Radii.Y;
            // Preserve corner contacts when a tangent's discriminant or endpoint parameter rounds
            // just outside the edge. This is especially important at a capsule's arc/edge seams.
            if (Math.Abs(normalized.Dot(normalized) - 1d) <= 8d * ConicCoincidenceTolerance * magnitude)
                angles.Add(PositiveAngle(Math.Atan2(normalized.Y, normalized.X)));
            if (!CurveRoots(start, direction, curve, out var low, out var high)) continue;
            AddAngle(low);
            AddAngle(high);

            void AddAngle(double parameter)
            {
                if (parameter < 0d || parameter > 1d) return;
                var point = curve.NormalizeRelative(start + direction * parameter);
                angles.Add(PositiveAngle(Math.Atan2(point.Y, point.X)));
            }
        }
        CoverArcsInside(curve, polygon, covered, angles);
    }

    private static void CoverArcsInside(UnionPart curve, UnionPart other, List<Interval<double>> covered, List<double> angles)
    {
        angles.Sort();
        for (var i = 1; i < angles.Count; i++)
        {
            var low = angles[i - 1];
            var high = angles[i];
            if (high <= low) continue;
            var point = curve.Origin - other.Origin + curve.PointAt((low + high) * .5d);
            if (other.ContainsInterior(point)) covered.Add(new(low, high));
        }
    }

    private static void CoverSegmentWithCurve(Vector2d start, Vector2d direction, UnionPart curve, List<Interval<double>> covered)
    {
        if (CurveRoots(start, direction, curve, out var low, out var high))
            AddInterval(covered, Math.Max(0d, low), Math.Min(1d, high));
    }

    private static bool CurveRoots(Vector2d start, Vector2d direction, UnionPart curve, out double low, out double high)
    {
        low = high = 0d;
        var relative = curve.NormalizeRelative(start);
        direction = new(direction.X / curve.Radii.X, direction.Y / curve.Radii.Y);
        var lengthSquared = direction.LengthSquared();
        if (lengthSquared == 0d) return false;
        var cross = relative.Cross(direction);
        var remaining = 1d - cross * cross / lengthSquared;
        if (remaining < 0d) return false;
        var midpoint = -relative.Dot(direction) / lengthSquared;
        var halfWidth = Math.Sqrt(remaining / lengthSquared);
        low = midpoint - halfWidth;
        high = midpoint + halfWidth;
        return true;
    }

    private static void CoverSegmentWithPolygon(Vector2d start, Vector2d direction, UnionPart polygon, bool preferOther, List<Interval<double>> covered)
    {
        var low = 0d;
        var high = 1d;
        for (var i = 0; i < polygon.Vertices.Length; i++)
        {
            var edgeStart = polygon.Vertices[i];
            var edge = polygon.Vertices[(i + 1) % polygon.Vertices.Length] - edgeStart;
            if (edge == default) continue;
            var side = edge.Cross(start - edgeStart);
            var slope = edge.Cross(direction);
            if (slope == 0d)
            {
                if (side < 0d) return;
                // Same-side coincident edges contribute once. Opposite-side edges are
                // internal to the union, so both owners remove their shared interval.
                if (side == 0d && edge.Dot(direction) > 0d && !preferOther) return;
            }
            else if (slope > 0d)
            {
                low = Math.Max(low, -side / slope);
            }
            else
            {
                high = Math.Min(high, -side / slope);
            }

            if (low >= high) return;
        }
        AddInterval(covered, low, high);
    }

    private static void AddInterval(List<Interval<double>> covered, double low, double high)
    {
        if (low < high) covered.Add(new(low, high));
    }

    private static double ExposedLength(List<Interval<double>> covered, double end) =>
        IntegrateExposed(covered, end, static (low, high) => high - low);

    private static double IntegrateExposed(List<Interval<double>> covered, double end, Func<double, double, double> integrate)
    {
        covered.Sort(static (a, b) => a.Min.CompareTo(b.Min));
        var position = 0d;
        var result = 0d;
        foreach (var interval in covered)
        {
            if (interval.Min > position) result += integrate(position, interval.Min);
            position = Math.Max(position, interval.Max);
        }
        if (position < end) result += integrate(position, end);
        return result;
    }

    private static double ArcArea(UnionPart curve, double from, double to)
    {
        var (sinFrom, cosFrom) = EndpointSinCos(from);
        var (sinTo, cosTo) = EndpointSinCos(to);
        return .5d * (curve.Radii.X * curve.Radii.Y * (to - from)
            + curve.Radii.Y * curve.Center.X * (sinTo - sinFrom)
            + curve.Radii.X * curve.Center.Y * (cosFrom - cosTo));
    }

    private static (double Sin, double Cos) EndpointSinCos(double angle) =>
        angle == 0d || angle == Math.Tau ? (0d, 1d) : Math.SinCos(angle);

    private static double PositiveAngle(double angle)
    {
        angle %= Math.Tau;
        return angle < 0d ? angle + Math.Tau : angle;
    }

    internal sealed class UnionPart
    {
        internal Vector2d Center { get; private set; }
        internal Vector2d Radii { get; }
        internal double Radius => Radii.X;
        internal Vector2d[] Vertices { get; }
        internal Vector2d Origin => IsCurve ? Center : _vertexOrigin;
        internal bool IsCurve => Radii.X > 0d;
        internal bool IsCircle => IsCurve && Radii.X == Radii.Y;
        private readonly Vector2d _min;
        private readonly Vector2d _max;
        private readonly bool _hasArea;
        private Vector2d _vertexOrigin;

        internal UnionPart(Vector2 center, float radius) : this(center, new Vector2(radius)) { }

        internal UnionPart(Vector2 center, Vector2 radii)
        {
            Center = new(center);
            Radii = new(radii);
            Vertices = [];
            _hasArea = true;
            _min = Radii * -1d;
            _max = Radii;
        }

        internal UnionPart(ReadOnlySpan<Vector2> vertices) : this(ToPoints(vertices), new Vector2d(vertices[0])) { }

        internal UnionPart(Vector2d[] vertices, Vector2d origin = default)
        {
            Vertices = vertices;
            _vertexOrigin = origin;
            var areaTwice = 0d;
            var first = Vertices[0];
            _min = _max = first;
            for (var i = 0; i < Vertices.Length; i++)
            {
                var point = Vertices[i];
                areaTwice += (point - first).Cross(Vertices[(i + 1) % Vertices.Length] - first);
                _min = new(Math.Min(_min.X, point.X), Math.Min(_min.Y, point.Y));
                _max = new(Math.Max(_max.X, point.X), Math.Max(_max.Y, point.Y));
            }
            if (areaTwice < 0d) Array.Reverse(Vertices);
            _hasArea = areaTwice != 0d;
        }

        internal void Shift(Vector2d origin)
        {
            if (IsCurve) Center -= origin;
            else _vertexOrigin -= origin;
        }

        internal bool Overlaps(UnionPart other)
        {
            var offset = other.Origin - Origin;
            return _hasArea && other._hasArea &&
                _min.X < offset.X + other._max.X && _max.X > offset.X + other._min.X &&
                _min.Y < offset.Y + other._max.Y && _max.Y > offset.Y + other._min.Y;
        }

        internal bool ContainsInterior(Vector2d point)
        {
            if (!_hasArea) return false;
            if (IsCurve)
            {
                var normalized = NormalizeRelative(point);
                return normalized.LengthSquared() < 1d;
            }
            for (var i = 0; i < Vertices.Length; i++)
            {
                var edge = Vertices[(i + 1) % Vertices.Length] - Vertices[i];
                if (edge != default && edge.Cross(point - Vertices[i]) <= 0d) return false;
            }
            return true;
        }

        internal Vector2d NormalizeRelative(Vector2d point) => new(point.X / Radii.X, point.Y / Radii.Y);

        internal Vector2d PointAt(double angle)
        {
            var (sin, cos) = EndpointSinCos(angle);
            return new Vector2d(Radii.X * cos, Radii.Y * sin);
        }

        private static Vector2d[] ToPoints(ReadOnlySpan<Vector2> vertices)
        {
            var points = new Vector2d[vertices.Length];
            var origin = new Vector2d(vertices[0]);
            for (var i = 0; i < points.Length; i++) points[i] = new Vector2d(vertices[i]) - origin;
            return points;
        }
    }
}
