using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Curves;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Resolves character bindings, then delegates curve and attribute evaluation to geometry.</summary>
public static class PartCurves2D
{
    internal static PartContour Contour(PartGeometry part, Func<string, Vector3> world, Func<string, Vector3>? rest)
    {
        var curve = (ICurve2D)part.Geometry!;
        var a = world(part.A);
        var b = world(part.B!);
        var coordinates = PartAttachment2D.FrameOf(part, world).Frame;
        var source = curve;
        Vector3f[]? controls = null;
        float[]? layers = null;
        RoundedPolylineCurve2D? pinned = null;
        if (part.Bindings is { Count: > 0 })
        {
            var points = CurveControlPoints2D.Get(curve);
            var placed = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
                placed[i] = new(coordinates.TransformPoint(points[i]), float.Lerp(a.Z, b.Z, points[i].Y) + part.Layer);
            if (curve is RoundedPolylineCurve2D rounded)
            {
                foreach (var binding in part.Bindings)
                    placed[binding.Point] = world(binding.Control) + Vector3.UnitZ * part.Layer;
                pinned = (rounded with { Points = [.. placed.Select(p => p.XY)] }).Validated();
                source = pinned.Value;
            }
            else
            {
                if (rest is null) throw new InvalidOperationException("A bound curve needs the model's rest pose.");
                var restA = rest(part.A);
                var restB = rest(part.B!);
                var restCoordinates = PartAttachment2D.FrameOf(part, rest).Frame;
                foreach (var binding in part.Bindings)
                {
                    var p = points[binding.Point];
                    var drawnRest = new Vector3(restCoordinates.TransformPoint(p), float.Lerp(restA.Z, restB.Z, p.Y) + part.Layer);
                    placed[binding.Point] = PartAttachment2D.BoundPoint(binding, drawnRest, world, rest);
                }
                // Rig points are planar positions plus rendering layers. Never feed their packed Z into spatial geometry.
                controls = [.. placed.Select(p => new Vector3f(p.X, p.Y, 0))];
            }
            layers = [.. placed.Select(p => p.Z)];
        }
        var count = source switch { LineSegmentCurve2D => 2, PolylineCurve2D p => p.Points.Length, _ => 33 };
        var parameters = source is RoundedPolylineCurve2D roundedPath ? roundedPath.SampleParameters(part.CurveSections)
            : [.. Enumerable.Range(0, count).Select(i => i / (float)(count - 1))];
        var positions = new Vector3[parameters.Length];
        var order = new float[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var t = parameters[i];
            if (pinned is { } path)
            {
                positions[i] = new(path.Evaluate(t), 0);
                order[i] = path.Interpolate(layers!, t);
            }
            else if (controls is not null)
            {
                positions[i] = controls.Length == 3
                    ? BezierMath.Quadratic<Vector3f, float>(controls[0], controls[1], controls[2], t).ToVector3()
                    : BezierMath.Cubic<Vector3f, float>(controls[0], controls[1], controls[2], controls[3], t).ToVector3();
                order[i] = controls.Length == 3
                    ? BezierMath.Quadratic<float, float>(layers![0], layers[1], layers[2], t)
                    : BezierMath.Cubic<float, float>(layers![0], layers[1], layers[2], layers[3], t);
            }
            else
            {
                var p = source.Evaluate(t);
                positions[i] = new(coordinates.TransformPoint(p), 0);
                order[i] = float.Lerp(a.Z, b.Z, p.Y) + part.Layer;
            }
        }
        return new(positions, layers: order);
    }
}
