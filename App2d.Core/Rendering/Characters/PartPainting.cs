using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Core.Meshes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Rendering.Characters;

/// <summary>Paint is mapped through the same frame and dimensions as the body, then clipped to its contour; concave cutouts are clipped triangle by triangle.</summary>
internal static class PartPainting
{
    public static void Add(CharacterMesh mesh, PuppetPart part, PartGeometry.Frame frame, IReadOnlyList<Vector3> contour)
    {
        if (part.Paint is null) return;
        var triangles = part.Geometry is SimplePolygonShapeDefinition2D
            ? TriangleMesh2D.TriangulateSimplePolygon(contour.Select(p => new Vector2(p.X, p.Y)), 1e-8) : null;
        for (var layer = 0; layer < part.Paint.Count; layer++)
        {
            var patch = part.Paint[layer];
            var points = patch.Points.ConvertAll(p => frame.At(new(p.X * part.Width, p.Y * part.Height)));
            var regions = new List<IReadOnlyList<Vector3>>();
            if (triangles is null)
            {
                regions.Add(contour);
            }
            else
            {
                for (var i = 0; i < triangles.TriangleCount; i++)
                {
                    var (a, b, c) = triangles.TriangleAt(i);
                    regions.Add([new Vector3(a, contour[0].Z), new Vector3(b, contour[0].Z), new Vector3(c, contour[0].Z)]);
                }
            }
            // Tiny raster bias only: paint remains on the torso rather than becoming another garment plane.
            var bias = new Vector3(0, 0, .00001f * (layer + 1));
            foreach (var region in regions)
            {
                var clipped = PolygonClipping2D.ClipConvexXY(points, region);
                mesh.Polygon([.. clipped.Select(p => p - bias)], ColorExtensions.FromHexRgb(patch.Material!.Fill!), null, 0);
            }
        }
    }
}
