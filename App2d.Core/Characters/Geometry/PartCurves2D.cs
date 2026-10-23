using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Attached curve handles and sampling, applying a curve's <see cref="CurveWave"/> modifier when it has one.</summary>
public static class PartCurves2D
{
    /// <summary>Point indices follow curve order: start, handle(s), end.</summary>
    public static IReadOnlyList<Vector2> ControlPoints(ICurve2D curve) => curve switch
    {
        QuadraticBezier2D q => [q.Start, q.Control, q.End],
        CubicBezier2D c => [c.Start, c.Control1, c.Control2, c.End],
        RoundedPolylineCurve2D r => r.Points.ToArray(),
        _ => throw new InvalidDataException("Curve point attachments require a quadratic or cubic Bézier or a rounded polyline.")
    };

    public static ICurve2D WithControlPoints(ICurve2D curve, IReadOnlyList<Vector2> points) => curve switch
    {
        QuadraticBezier2D q when points.Count == 3 => q with { Start = points[0], Control = points[1], End = points[2] },
        CubicBezier2D c when points.Count == 4 => c with { Start = points[0], Control1 = points[1], Control2 = points[2], End = points[3] },
        RoundedPolylineCurve2D r when points.Count == r.Points.Length => r with { Points = [.. points] },
        _ => throw new InvalidDataException("Curve point count must match its curve definition.")
    };

    internal static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, Vector3>? rest)
    {
        var curve = (ICurve2D)part.Geometry!;
        var a = world(part.A);
        var b = world(part.B!);
        var coordinates = PartAttachment2D.FrameOf(part, world).Frame;
        var source = curve;
        ICurve2D? layers = null;
        // A rounded path pins each bound point to its control, so its depth comes from the same fillet weights as XY.
        RoundedPolylineCurve2D? pinned = null;
        float[]? pinnedDepths = null;
        if (part.Bindings is { Count: > 0 })
        {
            var points = ControlPoints(curve);
            var placed = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                placed[i] = new(coordinates.TransformPoint(p), a.Z + (b.Z - a.Z) * p.Y + part.Depth);
            }
            if (curve is RoundedPolylineCurve2D rounded)
            {
                foreach (var binding in part.Bindings)
                {
                    placed[binding.Point] = world(binding.Control) + new Vector3(0, 0, part.Depth);
                }

                var path = (rounded with { Points = [.. placed.Select(p => p.XY)] }).Validated();
                source = path;
                pinned = path;
                pinnedDepths = [.. placed.Select(p => p.Z)];
            }
            else
            {
                if (rest is null)
                {
                    throw new InvalidOperationException("A bound curve needs the model's rest pose.");
                }

                var restA = rest(part.A);
                var restB = rest(part.B!);
                var restCoordinates = PartAttachment2D.FrameOf(part, rest).Frame;
                foreach (var binding in part.Bindings)
                {
                    var p = points[binding.Point];
                    var drawnRest = new Vector3(restCoordinates.TransformPoint(p), restA.Z + (restB.Z - restA.Z) * p.Y + part.Depth);
                    placed[binding.Point] = PartAttachment2D.BoundPoint(binding, drawnRest, world, rest);
                }
                source = WithControlPoints(curve, [.. placed.Select(p => p.XY)]).Validated();
                // Layer order follows the same Bézier weights, independently of its XY geometry.
                layers = WithControlPoints(curve, [.. placed.Select(p => new Vector2(p.Z, 0))]).Validated();
            }
        }
        var worldSpace = layers is not null || pinned is not null;
        var count = source switch
        {
            LineSegmentCurve2D => 2,
            PolylineCurve2D polyline => polyline.Points.Length,
            _ => 33
        };
        ICurve2D? wave = null;
        if (part.Wave is { Deforms: true } modifier)
        {
            wave = modifier.Apply(source, modifier.Phase(coordinates, world), worldSpace ? Vector2.Distance(a.XY, b.XY) : 1);
            count = Math.Max(count, modifier.Samples);
        }
        var parameters = source is RoundedPolylineCurve2D sampled
            ? sampled.SampleParameters(Math.Max(part.CurveSections, wave is null ? 0 : count - 1))
            : Enumerable.Range(0, count).Select(i => (float)i / (count - 1)).ToArray();
        var result = new List<Vector3>(parameters.Length);
        foreach (var t in parameters)
        {
            var p = source.Evaluate(t);
            var xy = wave?.Evaluate(t) ?? p;
            result.Add(pinned is { } path
                ? new(xy, path.Interpolate(pinnedDepths!, t))
                : layers is null
                    ? new(coordinates.TransformPoint(xy), a.Z + (b.Z - a.Z) * p.Y + part.Depth)
                    : new(xy, layers.Evaluate(t).X));
        }
        return result;
    }
}
