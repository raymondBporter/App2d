using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Meshes;
using App2d.Core.Curves;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Rendering.Characters;

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
        Build(model.Base.Ink, model.Base.LineWidth, model.Parts, pose.World, part => pose.Expressions.GetValueOrDefault(part.Id, "none"), face, id => pose.Angles[id]);

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
        PropDrawing.Add(Mesh, prop, frame);
        var ink = ColorExtensions.FromHexRgb(prop.Ink);
        foreach (var shape in prop.Shapes)
        {
            var points = shape.Points.Select(p => ActorPose.PropPoint(frame, prop, p)).ToList();
            var fill = ColorExtensions.FromHexRgb(shape.Fill);
            if (shape.IsFilled)
            {
                var triangles = TriangleMesh2D.TriangulateSimplePolygon(shape.Points.Select(point => point.XY), 1e-8);
                var indices = triangles.Indices;
                for (var i = 0; i < indices.Length; i += 3)
                    Mesh.Triangle(points[indices[i]], points[indices[i + 1]], points[indices[i + 2]], fill);
                Mesh.Polygon(points, null, ink, prop.LineWidth);
                continue;
            }
            for (var i = 1; i < points.Count; i++)
            {
                Mesh.Line(points[i - 1] + new Vector3(0, 0, .001f), points[i] + new Vector3(0, 0, .001f), shape.Width + prop.LineWidth * 2, ink);
                Mesh.Line(points[i - 1], points[i], shape.Width, fill);
            }
        }
    }

    /// <summary>Plain primitives from parts and a world-position lookup. The only drawing path for both prototype and authored models.</summary>
    public void Build(string inkColor, float lineWidth, IEnumerable<PuppetPart> parts, Func<string, Vector3> world, Func<PuppetPart, string>? expression = null, FacePose? facePose = null, Func<string, float>? angle = null)
    {
        Mesh.Clear(); var ink = ColorExtensions.FromHexRgb(inkColor);
        foreach (var part in parts)
        {
            if (part.Hidden) continue;
            var face = expression?.Invoke(part) ?? part.Face;
            if (part.Geometry is CurveDefinition2D || part.Geometry is null && PuppetPartKinds.IsStroke(part.Kind))
            {
                var path = PartGeometry.Contour(part, world);
                for (var i = 1; i < path.Count; i++) Mesh.Line(path[i - 1], path[i], part.Width, ink);
                continue;
            }
            var contour = PartGeometry.Contour(part, world, angle);
            var frame = PartGeometry.FrameOf(part, world, angle);
            if (part.Geometry is ShapeDefinition2D typed && PartGeometry.ShapeOf(typed) is SimplePolygon2D polygon)
                Mesh.Add(polygon.Mesh, vertex => frame.At(new(vertex.X * part.Width, vertex.Y * part.Height)), ColorExtensions.FromHexRgb(part.Fill));
            else if (part.Kind == "polygon")
                Mesh.Add(TriangleMesh2D.TriangulateSimplePolygon(part.Points!.Select(p => new Vector2(p.X * part.Width, p.Y * part.Height)), 1e-8), frame.At, ColorExtensions.FromHexRgb(part.Fill));
            else Mesh.Polygon(contour, ColorExtensions.FromHexRgb(part.Fill), null, 0);
            PartPainting.Add(Mesh, part, frame, contour);
            var outline = part.OutlineWidth ?? lineWidth;
            if (outline > 0) Mesh.Polygon(contour, null, ink, outline);
            if (face != "none")
            {
                FaceDrawing.Build(Mesh, facePose ?? FaceExpressions.Get(face),
                p => frame.At(new(p.X * part.Width + part.FaceX, -p.Y * part.Height)) - new Vector3(0, 0, .002f), lineWidth * .6f, ink);
            }
        }
    }
}
