using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Vertices;

/// <summary>Backend-independent packed triangles for thick segments and their caps.</summary>
public static class StrokeTessellation2D
{
    public const int DefaultCapSegments = 10;
    /// <summary>Room for one segment with two round caps at the default resolution.</summary>
    public const int MaximumSegmentVertices = 6 + DefaultCapSegments * 6;

    /// <summary>Writes triangle-list vertices. A collapsed butt stroke is empty; square/round strokes become a square/disk.</summary>
    public static int WriteSegment(Span<Vector2> vertices, Vector2 start, Vector2 end, float width,
        LineCap2D startCap = LineCap2D.Butt, LineCap2D endCap = LineCap2D.Butt, int capSegments = DefaultCapSegments) =>
        WriteSegment(vertices, start, end, width, width, startCap, endCap, capSegments);

    /// <summary>Writes triangle-list vertices for a uniform or tapered segment. A collapsed butt stroke is empty; square/round strokes become a square/disk.</summary>
    public static int WriteSegment(Span<Vector2> vertices, Vector2 start, Vector2 end, float startWidth, float endWidth,
        LineCap2D startCap = LineCap2D.Butt, LineCap2D endCap = LineCap2D.Butt, int capSegments = DefaultCapSegments)
    {
        ArgGuard.Finite(start);
        ArgGuard.Finite(end);
        ArgGuard.Positive(startWidth);
        ArgGuard.Positive(endWidth);
        ArgGuard.Positive(capSegments);
        ValidateCap(startCap);
        ValidateCap(endCap);
        var axis = end - start;
        var collapsed = axis.LengthSquared() <= float.Epsilon;
        var count = collapsed
            ? startCap == LineCap2D.Round || endCap == LineCap2D.Round ? checked(capSegments * 6)
                : startCap == LineCap2D.Square || endCap == LineCap2D.Square ? 6 : 0
            : checked(6 + (startCap == LineCap2D.Round ? capSegments * 3 : 0) + (endCap == LineCap2D.Round ? capSegments * 3 : 0));
        ArgGuard.ThrowIfTooShort(vertices, count);
        var startRadius = startWidth * .5f;
        var endRadius = endWidth * .5f;
        if (collapsed)
        {
            var maxRadius = MathF.Max(startRadius, endRadius);
            if (startCap == LineCap2D.Round || endCap == LineCap2D.Round)
            {
                WriteDisk(vertices, start, maxRadius, checked(capSegments * 2));
            }
            else if (count != 0)
            {
                WriteQuad(vertices, start + new Vector2(-maxRadius, -maxRadius), start + new Vector2(maxRadius, -maxRadius),
                    start + new Vector2(maxRadius, maxRadius), start + new Vector2(-maxRadius, maxRadius));
            }

            return count;
        }

        var unit = Vector2.Normalize(axis);
        var perp = new Vector2(-unit.Y, unit.X);
        var startNormal = perp * startRadius;
        var endNormal = perp * endRadius;
        if (startCap == LineCap2D.Square) start -= unit * startRadius;
        if (endCap == LineCap2D.Square) end += unit * endRadius;
        WriteQuad(vertices, start + startNormal, end + endNormal, end - endNormal, start - startNormal);
        var written = 6;
        if (startCap == LineCap2D.Round)
        {
            WriteRoundCap(vertices[written..], start, -unit, startRadius, capSegments);
            written += capSegments * 3;
        }
        if (endCap == LineCap2D.Round)
            WriteRoundCap(vertices[written..], end, unit, endRadius, capSegments);
        return count;
    }

    private static void WriteQuad(Span<Vector2> vertices, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        vertices[0] = a; vertices[1] = b; vertices[2] = c;
        vertices[3] = a; vertices[4] = c; vertices[5] = d;
    }

    private static void WriteDisk(Span<Vector2> vertices, Vector2 center, float radius, int segments)
    {
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i * MathF.Tau / segments;
            var next = center + Vector2.FromPolar(radius, angle);
            var offset = (i - 1) * 3;
            vertices[offset] = center; vertices[offset + 1] = previous; vertices[offset + 2] = next;
            previous = next;
        }
    }

    private static void WriteRoundCap(Span<Vector2> vertices, Vector2 center, Vector2 outward, float radius, int segments)
    {
        var tangent = new Vector2(-outward.Y, outward.X);
        var previous = center + tangent * radius;
        for (var i = 1; i <= segments; i++)
        {
            var angle = i * MathF.PI / segments;
            var next = center + (tangent * MathF.Cos(angle) + outward * MathF.Sin(angle)) * radius;
            var offset = (i - 1) * 3;
            vertices[offset] = center; vertices[offset + 1] = previous; vertices[offset + 2] = next;
            previous = next;
        }
    }

    private static void ValidateCap(LineCap2D cap)
    {
        if (cap is < LineCap2D.Butt or > LineCap2D.Round) throw new ArgumentOutOfRangeException(nameof(cap));
    }
}
