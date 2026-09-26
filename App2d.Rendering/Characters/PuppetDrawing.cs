using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Rendering.Characters;

/// <summary>Plain primitives over an evaluated pose; independent of source clips and anatomy names.</summary>
public sealed class PuppetDrawing
{
    public CharacterMesh Mesh { get; } = new();
    public void Build(PuppetDefinition definition, PuppetPose pose) => Build(definition.Ink, definition.LineWidth, definition.Parts, pose.World);
    /// <summary>
    /// Faces come from the pose's evaluated expressions, never from the parts' stored defaults. <paramref name="face"/>, when
    /// given, replaces the drawn expression with a gameplay-blended face on every part that shows one; a clip that hides the
    /// face ("none", for a back view) still hides it.
    /// </summary>
    public void Build(ResolvedModel model, EvaluatedPose pose, FacePose? face = null) =>
        Build(model.Base.Ink, model.Base.LineWidth, model.Parts, pose.World, part => pose.Expressions.GetValueOrDefault(part.Id, "none"), face);

    /// <summary>
    /// An entity's final pose in actor-local units, with its equipped props placed by the same socket transform that hit
    /// regions use. Facing and position belong to the caller's world matrix.
    /// </summary>
    public void Build(ResolvedEntity entity, EvaluatedPose local)
    {
        Build(entity.Model, local);
        var placed = new ActorPose(local, Vector2.Zero, 1);
        foreach (var equipment in entity.Equipment) AddProp(equipment.Prop, placed.Socket(equipment.Socket));
    }

    /// <summary>Appends a prop with its grip on the given frame. Strokes get an ink outline just behind them.</summary>
    public void AddProp(PropAsset prop, SocketFrame frame)
    {
        var ink = CharacterJson.Color(prop.Ink);
        foreach (var shape in prop.Shapes)
        {
            var points = shape.Points.Select(p => ActorPose.PropPoint(frame, prop, p)).ToList(); var fill = CharacterJson.Color(shape.Fill);
            if (shape.Kind == "polygon") { Mesh.Polygon(points, fill, ink, prop.LineWidth); continue; }
            for (var i = 1; i < points.Count; i++)
            {
                Mesh.Line(points[i - 1] + new Vector3(0, 0, .001f), points[i] + new Vector3(0, 0, .001f), shape.Width + prop.LineWidth * 2, ink);
                Mesh.Line(points[i - 1], points[i], shape.Width, fill);
            }
        }
    }

    /// <summary>Plain primitives from parts and a world-position lookup. The only drawing path for both prototype and authored models.</summary>
    public void Build(string inkColor, float lineWidth, IEnumerable<PuppetPart> parts, Func<string, Vector3> world, Func<PuppetPart, string>? expression = null, FacePose? facePose = null)
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
            if (face != "none") FaceDrawing.Build(Mesh, facePose ?? FaceExpressions.Get(face),
                p => frame.At(new(p.X * part.Width + part.FaceX, -p.Y * part.Height)) - new Vector3(0, 0, .002f), lineWidth * .6f, ink);
        }
    }
}
