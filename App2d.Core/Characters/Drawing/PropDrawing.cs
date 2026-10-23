using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using System.Numerics;
using System.Runtime.InteropServices;

namespace App2d.Core.Characters.Drawing;

/// <summary>Solid props with flat facet shading and visible silhouette/crease ink. Coplanar triangulation never becomes ink.</summary>
internal static class PropDrawing
{
    private static readonly Vector3 LightDirection = Vector3.Normalize(new(-.3f, .5f, -1));

    public static void Add(CharacterMesh mesh, PropAsset prop, Frame3D frame) =>
        Add(mesh, prop.Solids, prop.Ink, prop.LineWidth, point => PropPlacement3D.PropPoint(frame, prop, point), frame);

    /// <summary>Solids placed by <paramref name="place"/>: a prop's grip-relative points, or a library part's lifted local points (P5b).</summary>
    public static void Add(CharacterMesh mesh, IReadOnlyList<PropSolid> solids, string inkColor, float lineWidth, Func<PuppetPoint, Vector3> place, Frame3D? frame = null)
    {
        var handedness = 1;
        if (frame is { } placed && !Orientation3D.TryGetHandedness(placed.XAxis, placed.YAxis, placed.ZAxis, out handedness))
        {
            return;
        }

        if (frame is null && solids.Count > 0)
        {
            // Read the handedness from the placement of a unit frame, as a lifted mirrored instance reverses it.
            var origin = place(new(0, 0, 0));
            if (!Orientation3D.TryGetHandedness(place(new(1, 0, 0)) - origin, place(new(0, 1, 0)) - origin, place(new(0, 0, 1)) - origin, out handedness))
            {
                return;
            }
        }

        foreach (var solid in solids)
        {
            var vertices = solid.Vertices.Select(place).ToArray();
            var analysis = new TriangleMeshAnalysis3D(vertices, solid.Triangles, handedness);
            var material = solid.RenderMaterial;
            var fill = material.Fill is { } fillHex ? ColorExtensions.FromHexRgb(fillHex) : (Color4b?)null;
            var outline = material.Outline;
            var ink = outline?.Color is { } outlineHex ? ColorExtensions.FromHexRgb(outlineHex) : ColorExtensions.FromHexRgb(inkColor);
            var outlineWidth = outline?.Width ?? lineWidth;
            var face = 0;
            foreach (var (a, b, c) in MeshTopology.Triangles(CollectionsMarshal.AsSpan(solid.Triangles)))
            {
                if (!analysis.TryGetFaceNormal(face++, out var normal))
                {
                    continue;
                }

                var light = .72f + .28f * MathF.Max(0, Vector3.Dot(normal, LightDirection));
                if (fill is { } color)
                {
                    mesh.Triangle(vertices[a], vertices[b], vertices[c], color.ScaleRgb(light));
                }
            }
            if (outline is null || outlineWidth <= 0)
            {
                continue;
            }

            var bias = new Vector3(0, 0, .0005f);
            foreach (var edge in analysis.FeatureEdges(-Vector3.UnitZ, .7f, 1e-5f))
            {
                mesh.Line(vertices[edge.A] - bias, vertices[edge.B] - bias, outlineWidth, ink);
            }
        }
    }
}
