using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Rendering.Characters;

/// <summary>Plain primitives over an evaluated pose; independent of source clips and anatomy names.</summary>
public sealed class PuppetDrawing
{
    public CharacterMesh Mesh { get; } = new();
    public void Build(PuppetDefinition definition, PuppetPose pose) => Build(definition.Ink, definition.LineWidth, definition.Parts, pose.World);
    /// <summary>Faces come from the pose's evaluated expressions, never from the parts' stored defaults.</summary>
    public void Build(ResolvedModel model, EvaluatedPose pose) =>
        Build(model.Base.Ink, model.Base.LineWidth, model.Parts, pose.World, part => pose.Expressions.GetValueOrDefault(part.Id, "none"));

    /// <summary>Plain primitives from parts and a world-position lookup. The only drawing path for both prototype and authored models.</summary>
    public void Build(string inkColor, float lineWidth, IEnumerable<PuppetPart> parts, Func<string, Vector3> world, Func<PuppetPart, string>? expression = null)
    {
        Mesh.Clear(); var ink = CharacterJson.Color(inkColor);
        foreach (var part in parts)
        {
            if (part.Hidden) continue;
            var face = expression?.Invoke(part) ?? part.Face;
            if (part.Kind == "stroke")
            {
                var ends = PartGeometry.Contour(part, world); Mesh.Line(ends[0], ends[1], part.Width, ink); continue;
            }
            Mesh.Polygon(PartGeometry.Contour(part, world), CharacterJson.Color(part.Fill), ink, lineWidth);
            var frame = PartGeometry.FrameOf(part, world);
            if (face != "none") FaceDrawing.Build(Mesh, FaceExpressions.Get(face),
                p => frame.At(new(p.X * part.Width + part.FaceX, -p.Y * part.Height)) - new Vector3(0, 0, .002f), lineWidth * .6f, ink);
        }
    }
}
