using App2d.Core.Geometry.Authoring;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Attached curve handles, sampling and optional bumps along the curve normal.</summary>
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

    internal static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, Vector3>? rest)
    {
        var curve = (CurveDefinition2D)part.Geometry!;
        var a = world(part.A); var b = world(part.B!);
        var coordinates = Frame2D.FromYAxis(a.XY, b.XY - a.XY);
        var source = GeometryDefinitionCache2D.CurveOf(curve);
        ICurve2D? layers = null;
        if (part.Bindings is { Count: > 0 })
        {
            if (rest is null) throw new InvalidOperationException("A bound curve needs the model's rest pose.");
            var points = ControlPoints(curve);
            var restA = rest(part.A); var restB = rest(part.B!);
            var restCoordinates = Frame2D.FromYAxis(restA.XY, restB.XY - restA.XY);
            var placed = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                placed[i] = new(coordinates.TransformPoint(p), a.Z + (b.Z - a.Z) * p.Y + part.Depth);
            }
            foreach (var binding in part.Bindings)
            {
                var p = points[binding.Point];
                var drawnRest = new Vector3(restCoordinates.TransformPoint(p), restA.Z + (restB.Z - restA.Z) * p.Y + part.Depth);
                placed[binding.Point] = PartAttachment2D.BoundPoint(binding, drawnRest, world, rest);
            }
            source = WithControlPoints(curve, placed.Select(p => p.XY).ToArray()).Build();
            // Layer order follows the same Bézier weights, independently of its XY geometry.
            layers = WithControlPoints(curve, placed.Select(p => new Vector2(p.Z, 0)).ToArray()).Build();
        }
        var count = source switch { LineSegmentCurve2D => 2, PolylineCurve2D polyline => polyline.Points.Length, _ => 33 };
        NormalOffsetCurve2D? wave = null;
        if (part.WaveAmplitude != 0)
        {
            var phase = part.WavePhaseControl is { } control && coordinates.TryToLocal(world(control).XY, out var local) ? local.Y * part.WaveCycles : 0;
            var amplitude = part.WaveAmplitude * (layers is null ? 1 : Vector2.Distance(a.XY, b.XY));
            wave = new(source, t => amplitude * .5f * (1 + MathF.Sin(MathF.Tau * (part.WaveCycles * t - phase))));
            count = Math.Max(count, Math.Max(33, (int)MathF.Ceiling(part.WaveCycles * 16) + 1));
        }
        var result = new List<Vector3>(count);
        for (var i = 0; i < count; i++)
        {
            var t = (float)i / (count - 1); var p = source.Evaluate(t); var xy = wave?.Evaluate(t) ?? p;
            result.Add(layers is null
                ? new(coordinates.TransformPoint(xy), a.Z + (b.Z - a.Z) * p.Y + part.Depth)
                : new(xy, layers.Evaluate(t).X));
        }
        return result;
    }
}
