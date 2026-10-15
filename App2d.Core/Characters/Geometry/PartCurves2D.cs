using App2d.Core.Geometry.Authoring;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Bézier drawing handles use the same rest-offset attachments as cutout points.</summary>
public static class PartCurves2D
{
    /// <summary>Point indices follow curve order: start, handle(s), end.</summary>
    public static IReadOnlyList<Vector2> ControlPoints(CurveDefinition2D curve) => curve switch
    {
        QuadraticBezierCurveDefinition2D q => [q.Start, q.Control, q.End],
        CubicBezierCurveDefinition2D c => [c.Start, c.Control1, c.Control2, c.End],
        _ => throw new InvalidDataException("Curve point attachments require a quadratic or cubic Bézier.")
    };

    public static CurveDefinition2D WithControlPoints(CurveDefinition2D curve, IReadOnlyList<Vector2> points) => curve switch
    {
        QuadraticBezierCurveDefinition2D q when points.Count == 3 => q with { Start = points[0], Control = points[1], End = points[2] },
        CubicBezierCurveDefinition2D c when points.Count == 4 => c with { Start = points[0], Control1 = points[1], Control2 = points[2], End = points[3] },
        _ => throw new InvalidDataException("Bézier point count must match its curve definition.")
    };

    internal static List<Vector3> BoundContour(PuppetPart part, CurveDefinition2D curve, Func<string, Vector3> world, Func<string, Vector3>? rest)
    {
        if (rest is null) throw new InvalidOperationException("A bound curve needs the model's rest pose.");
        var points = ControlPoints(curve);
        var a = world(part.A); var b = world(part.B!);
        var coordinates = Frame2D.FromYAxis(a.XY, b.XY - a.XY);
        var restA = rest(part.A); var restB = rest(part.B!);
        var restCoordinates = Frame2D.FromYAxis(restA.XY, restB.XY - restA.XY);
        var placed = new Vector3[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            placed[i] = new(coordinates.TransformPoint(p), a.Z + (b.Z - a.Z) * p.Y + part.Depth);
        }
        foreach (var binding in part.Bindings!)
        {
            var p = points[binding.Point];
            var drawnRest = new Vector3(restCoordinates.TransformPoint(p), restA.Z + (restB.Z - restA.Z) * p.Y + part.Depth);
            placed[binding.Point] = PartAttachment2D.BoundPoint(binding, drawnRest, world, rest);
        }
        var xy = WithControlPoints(curve, placed.Select(p => p.XY).ToArray()).Build();
        // Layer order follows the same Bézier weights, independently of its XY geometry.
        var layers = WithControlPoints(curve, placed.Select(p => new Vector2(p.Z, 0)).ToArray()).Build();
        var result = new List<Vector3>(33);
        for (var i = 0; i < 33; i++)
        {
            var t = i / 32f;
            result.Add(new(xy.Evaluate(t), layers.Evaluate(t).X));
        }
        return result;
    }
}
