using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry;

public static partial class Area2D
{
    // Each convex part has its interior on the left of its boundary. Integrating only the
    // uncovered pieces counts every point once, including when several parts overlap or enclose a hole.
    internal static float Union(ReadOnlySpan<UnionPart> parts)
    {
        var origin = parts[0].IsCircle ? parts[0].Center : parts[0].Vertices[0];
        foreach (var part in parts) part.Shift(origin);
        var covered = new List<Interval>();
        var angles = new List<double>();
        var area = 0d;
        var compensation = 0d;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.IsCircle)
            {
                covered.Clear();
                for (var j = 0; j < parts.Length; j++)
                {
                    if (j == i || !part.Overlaps(parts[j])) continue;
                    if (parts[j].IsCircle) CoverCircle(part, parts[j], j < i, covered);
                    else CoverCircleWithPolygon(part, parts[j], covered, angles);
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
                        if (parts[j].IsCircle) CoverSegmentWithCircle(start, direction, parts[j], covered);
                        else CoverSegmentWithPolygon(start, direction, parts[j], j < i, covered);
                    }
                    AddArea(.5d * start.Cross(direction) * ExposedLength(covered, 1d));
                }
            }
        }
        return (float)Math.Max(0d, area);

        void AddArea(double value)
        {
            var adjusted = value - compensation;
            var sum = area + adjusted;
            compensation = (sum - area) - adjusted;
            area = sum;
        }
    }

    private static void CoverCircle(UnionPart circle, UnionPart other, bool preferOther, List<Interval> covered)
    {
        var delta = other.Center - circle.Center;
        var distance = Math.Sqrt(delta.Dot(delta));
        if (distance == 0d && circle.Radius == other.Radius)
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

    private static void CoverCircleWithPolygon(UnionPart circle, UnionPart polygon, List<Interval> covered, List<double> angles)
    {
        angles.Clear();
        angles.Add(0d);
        angles.Add(Math.Tau);
        for (var i = 0; i < polygon.Vertices.Length; i++)
        {
            var start = polygon.Vertices[i];
            var direction = polygon.Vertices[(i + 1) % polygon.Vertices.Length] - start;
            if (!CircleRoots(start, direction, circle, out var low, out var high)) continue;
            AddAngle(low);
            AddAngle(high);

            void AddAngle(double parameter)
            {
                if (parameter < 0d || parameter > 1d) return;
                var point = start + direction * parameter - circle.Center;
                angles.Add(PositiveAngle(Math.Atan2(point.Y, point.X)));
            }
        }
        angles.Sort();
        for (var i = 1; i < angles.Count; i++)
        {
            var low = angles[i - 1];
            var high = angles[i];
            if (high <= low) continue;
            var (sin, cos) = Math.SinCos((low + high) * .5d);
            if (polygon.ContainsInterior(circle.Center + new Point(cos, sin) * circle.Radius)) covered.Add(new(low, high));
        }
    }

    private static void CoverSegmentWithCircle(Point start, Point direction, UnionPart circle, List<Interval> covered)
    {
        if (CircleRoots(start, direction, circle, out var low, out var high))
            AddInterval(covered, Math.Max(0d, low), Math.Min(1d, high));
    }

    private static bool CircleRoots(Point start, Point direction, UnionPart circle, out double low, out double high)
    {
        low = high = 0d;
        var lengthSquared = direction.Dot(direction);
        if (lengthSquared == 0d) return false;
        var relative = start - circle.Center;
        var cross = relative.Cross(direction);
        var remaining = circle.Radius * circle.Radius - cross * cross / lengthSquared;
        if (remaining < 0d) return false;
        var midpoint = -relative.Dot(direction) / lengthSquared;
        var halfWidth = Math.Sqrt(remaining / lengthSquared);
        low = midpoint - halfWidth;
        high = midpoint + halfWidth;
        return true;
    }

    private static void CoverSegmentWithPolygon(Point start, Point direction, UnionPart polygon, bool preferOther, List<Interval> covered)
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
            else if (slope > 0d) low = Math.Max(low, -side / slope);
            else high = Math.Min(high, -side / slope);
            if (low >= high) return;
        }
        AddInterval(covered, low, high);
    }

    private static void AddInterval(List<Interval> covered, double low, double high)
    {
        if (low < high) covered.Add(new(low, high));
    }

    private static double ExposedLength(List<Interval> covered, double end) =>
        IntegrateExposed(covered, end, static (low, high) => high - low);

    private static double IntegrateExposed(List<Interval> covered, double end, Func<double, double, double> integrate)
    {
        covered.Sort(static (a, b) => a.Low.CompareTo(b.Low));
        var position = 0d;
        var result = 0d;
        foreach (var interval in covered)
        {
            if (interval.Low > position) result += integrate(position, interval.Low);
            position = Math.Max(position, interval.High);
        }
        if (position < end) result += integrate(position, end);
        return result;
    }

    private static double ArcArea(UnionPart circle, double from, double to)
    {
        var (sinFrom, cosFrom) = EndpointSinCos(from);
        var (sinTo, cosTo) = EndpointSinCos(to);
        return .5d * (circle.Radius * circle.Radius * (to - from)
            + circle.Radius * (circle.Center.X * (sinTo - sinFrom) + circle.Center.Y * (cosFrom - cosTo)));
    }

    private static (double Sin, double Cos) EndpointSinCos(double angle) =>
        angle == 0d || angle == Math.Tau ? (0d, 1d) : Math.SinCos(angle);

    private static double PositiveAngle(double angle)
    {
        angle %= Math.Tau;
        return angle < 0d ? angle + Math.Tau : angle;
    }

    private readonly record struct Interval(double Low, double High);

    internal readonly record struct Point(double X, double Y)
    {
        internal Point(Vector2 value) : this(value.X, value.Y) { }
        public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
        public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
        public static Point operator *(Point a, double scale) => new(a.X * scale, a.Y * scale);
        internal double Dot(Point other) => X * other.X + Y * other.Y;
        internal double Cross(Point other) => Vector2Extensions.CrossDouble(X, Y, other.X, other.Y);
    }

    internal sealed class UnionPart
    {
        internal Point Center { get; private set; }
        internal double Radius { get; }
        internal Point[] Vertices { get; }
        internal bool IsCircle => Radius > 0d;
        private Point _min;
        private Point _max;
        private readonly bool _hasArea;

        internal UnionPart(Vector2 center, float radius)
        {
            Center = new(center);
            Radius = radius;
            Vertices = [];
            _hasArea = true;
            _min = Center - new Point(radius, radius);
            _max = Center + new Point(radius, radius);
        }

        internal UnionPart(ReadOnlySpan<Vector2> vertices)
        {
            Vertices = new Point[vertices.Length];
            for (var i = 0; i < vertices.Length; i++) Vertices[i] = new(vertices[i]);
            var areaTwice = 0d;
            var origin = Vertices[0];
            _min = _max = origin;
            for (var i = 0; i < Vertices.Length; i++)
            {
                var point = Vertices[i];
                areaTwice += (point - origin).Cross(Vertices[(i + 1) % Vertices.Length] - origin);
                _min = new(Math.Min(_min.X, point.X), Math.Min(_min.Y, point.Y));
                _max = new(Math.Max(_max.X, point.X), Math.Max(_max.Y, point.Y));
            }
            if (areaTwice < 0d) Array.Reverse(Vertices);
            _hasArea = areaTwice != 0d;
        }

        internal void Shift(Point origin)
        {
            Center -= origin;
            _min -= origin;
            _max -= origin;
            for (var i = 0; i < Vertices.Length; i++) Vertices[i] -= origin;
        }

        internal bool Overlaps(UnionPart other) =>
            _hasArea && other._hasArea &&
            _min.X < other._max.X && _max.X > other._min.X && _min.Y < other._max.Y && _max.Y > other._min.Y;

        internal bool ContainsInterior(Point point)
        {
            if (!_hasArea) return false;
            for (var i = 0; i < Vertices.Length; i++)
            {
                var edge = Vertices[(i + 1) % Vertices.Length] - Vertices[i];
                if (edge != default && edge.Cross(point - Vertices[i]) <= 0d) return false;
            }
            return true;
        }
    }
}
