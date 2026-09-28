using App2d.Core.Characters;
using System.Numerics;

namespace App2d.Rendering.Characters;

/// <summary>Paint is mapped through the same frame and dimensions as the body, then clipped to its convex contour.</summary>
internal static class PartPainting
{
    public static void Add(CharacterMesh mesh, PuppetPart part, PartGeometry.Frame frame, IReadOnlyList<Vector3> contour)
    {
        if (part.Paint is null) return;
        float Cross(Vector3 a, Vector3 b, Vector3 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        var area = 0f;
        for (var i = 0; i < contour.Count; i++)
        {
            var a = contour[i]; var b = contour[(i + 1) % contour.Count]; area += a.X * b.Y - b.X * a.Y;
        }
        var winding = MathF.Sign(area);
        for (var layer = 0; layer < part.Paint.Count; layer++)
        {
            var patch = part.Paint[layer];
            var points = patch.Points.Select(p => frame.At(new(p.X * part.Width, p.Y * part.Height))).ToList();
            // Sutherland-Hodgman: both the patch and the ellipse/rounded rectangle are convex.
            for (var edge = 0; edge < contour.Count && points.Count > 0; edge++)
            {
                var a = contour[edge]; var b = contour[(edge + 1) % contour.Count];
                if (Vector2.DistanceSquared(new(a.X, a.Y), new(b.X, b.Y)) < 1e-12f) continue;
                var clipped = new List<Vector3>(); var previous = points[^1]; var before = winding * Cross(a, b, previous);
                foreach (var current in points)
                {
                    var after = winding * Cross(a, b, current);
                    if ((before >= 0) != (after >= 0)) clipped.Add(Vector3.Lerp(previous, current, before / (before - after)));
                    if (after >= 0) clipped.Add(current);
                    previous = current; before = after;
                }
                points = clipped;
            }
            // Tiny raster bias only: paint remains on the torso rather than becoming another garment plane.
            var bias = new Vector3(0, 0, .00001f * (layer + 1));
            mesh.Polygon(points.Select(p => p - bias).ToArray(), CharacterJson.Color(patch.Fill), null, 0);
        }
    }
}
