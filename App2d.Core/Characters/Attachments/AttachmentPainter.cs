using App2d.Core.Rendering;
using System.Collections.Immutable;

namespace App2d.Core.Characters;

/// <summary>Paints attachments onto a <see cref="LayeredMesh"/>. A smaller layer is nearer the viewer.</summary>
public static class AttachmentPainter
{
    /// <summary>Adds every attachment to the mesh for a posed skeleton.</summary>
    public static void Paint(LayeredMesh mesh, PosedSkeleton2D pose, ImmutableArray<Attachment> attachments)
    {
        foreach (var attachment in attachments) attachment.Paint(mesh, pose);
        mesh.Layer = 0;
    }

    /// <summary>The colour a material fills with, or strokes a path in.</summary>
    internal static Color4b? Fill(RenderMaterialDefinition2D material) => material.Fill is { } hex ? ColorExtensions.FromHexRgb(hex) : null;

    /// <summary>The colour and width a material edges with, when it has an edge.</summary>
    internal static (Color4b Color, float Width)? Edge(RenderMaterialDefinition2D material) =>
        material.Outline is { Color: { } hex, Width: > 0 and var width } ? (ColorExtensions.FromHexRgb(hex), width) : null;

    /// <summary>
    /// One straight stroke: the edge underneath, wider by its width on each side, then the stroke itself. Each end may sit at
    /// its own layer, on top of the mesh's.
    /// </summary>
    internal static void Segment(LayeredMesh mesh, Vector3f from, Vector3f to, RenderMaterialDefinition2D material, float fromLayer = 0, float toLayer = 0)
    {
        var width = material.StrokeWidth ?? 0;
        if (Edge(material) is { } edge) mesh.Line(from, to, width + edge.Width * 2, edge.Color, fromLayer, toLayer);
        if (Fill(material) is { } ink && width > 0) mesh.Line(from, to, width, ink, fromLayer, toLayer);
    }
}
