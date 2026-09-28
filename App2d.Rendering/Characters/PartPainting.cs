using App2d.Core.Geometry.Functions;
using App2d.Core.Characters;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Rendering.Characters;

/// <summary>Paint is mapped through the same frame and dimensions as the body, then clipped to its convex contour.</summary>
internal static class PartPainting
{
    public static void Add(CharacterMesh mesh, PuppetPart part, PartGeometry.Frame frame, IReadOnlyList<Vector3> contour)
    {
        if (part.Paint is null) return;
        for (var layer = 0; layer < part.Paint.Count; layer++)
        {
            var patch = part.Paint[layer];
            var points = patch.Points.Select(p => frame.At(new(p.X * part.Width, p.Y * part.Height))).ToList();
            points = PolygonClipping2D.ClipConvexXY(points, contour);
            // Tiny raster bias only: paint remains on the torso rather than becoming another garment plane.
            var bias = new Vector3(0, 0, .00001f * (layer + 1));
            mesh.Polygon(points.Select(p => p - bias).ToArray(), CharacterJson.Color(patch.Fill), null, 0);
        }
    }
}
