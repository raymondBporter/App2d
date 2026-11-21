using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Progress;
using System.Text;

namespace App2d.Tests.Characters;

/// <summary>
/// Writes an authored animated face part out as a <see cref="Face"/>: for every expression, where the current rig draws
/// each piece, read back from its own contours in the axes of the mount the face is on. Nothing of the face's rig, clips,
/// layers or clocks is carried over. Dies with the authored parts.
/// </summary>
internal static class LegacyFaceEmitter
{
    private static string Number(float value) => LegacyClipEmitter.Number(value);
    private static string Point(Vector2f point) => LegacyClipEmitter.Vector(point);

    public static string Emit(string className, string summary, string regenerateWith, ResolvedModel model, Skeleton2D skeleton, string instance, string blinkLayer)
    {
        var attachment = model.Attachments.Single(a => a.Id == instance);
        var asset = attachment.Asset;
        var mount = skeleton.Mounts.Single(m => m.Name == attachment.Socket.Id);
        var toMount = mount.World(skeleton.RestWorld.AsSpan()).Inverse();
        var blinkClip = asset.Clips.Single(c => c.Id == asset.Layers.Single(l => l.Id == blinkLayer).Clip);
        Assert.True(attachment.Instance is { Y: 0, Angle: 0, Scale: 1, Mirror: false }, "Only a face offset along X is written.");

        var pieces = new StringBuilder();
        var blinking = new Dictionary<string, Vector2f>(StringComparer.Ordinal);
        foreach (var part in attachment.Parts)
        {
            var shape = part.Geometry switch { Ellipse2D => "Ellipse", SimplePolygon2D => "Polygon", LineSegmentCurve2D => "Line", QuadraticBezier2D => "Curve", _ => throw new InvalidDataException(part.Id) };
            var material = part.RenderMaterial;
            var paint = "new() { Fill = \"" + material.Fill + "\"" + (material.StrokeWidth is { } stroke ? $", StrokeWidth = {Number(stroke)}" : "")
                + (material.Outline is { Color: { } edge, Width: > 0 and var width } ? $", Outline = new() {{ Color = \"{edge}\", Width = {Number(width)} }}" : "") + " }";
            var blink = "";
            if (blinkClip.Tracks.SingleOrDefault(t => t.Kind == MotionClip.FitKind && t.Target == part.Id) is { } track)
            {
                // A fit key is a change of scale; the blink is written as the scale itself.
                Assert.True(part.Geometry is Ellipse2D { Center: var centre } && centre == Vector2f.Zero && track.Keys.All(k => k.Ease == EasingCurve.Linear && k.Interpolator is null));
                blinking[part.Id] = Vector2f.One + track.Keys[0].Value.XY;
                blink = ", Blink: Track.Of<Vector2f>(" + string.Join(", ", track.Keys.Select(k => $"new({LegacyClipEmitter.Time(k.Time)}, {Point(Vector2f.One + k.Value.XY)})")) + ")";
            }
            pieces.Append($"            new(\"{part.Id}\", FaceShape.{shape}, {paint}{(part.Layer == 0 && blink.Length == 0 ? "" : ", " + Number(part.Layer))}{blink}),\n");
        }

        var expressions = new StringBuilder();
        foreach (var (name, binding) in asset.Expressions)
        {
            var blinks = !binding.Mutes.Contains(blinkLayer);
            // At clock zero the blink sits on its first key, whose scale is divided back out of a blinking piece.
            var face = PoseEvaluator.Rest(model, new PoseInput(name) { Clock = 0 }).Attachments[instance];
            expressions.Append($"            new(\"{name}\", {(blinks ? "true" : "false")},\n            [\n");
            foreach (var part in attachment.Parts)
            {
                if (!face.Pose.Visible(part))
                {
                    expressions.Append("                [],\n");
                    continue;
                }
                var contour = PartContours2D.Contour(face.Pose.Drawn(part), face.World, face.Frame, face.Rest, face.RestFrame);
                Vector2f[] points = [.. contour.Select(p => toMount.TransformPoint(p.XY))];
                Vector2f[] written = part.Geometry switch
                {
                    Ellipse2D => Ellipse(points, blinks && blinking.TryGetValue(part.Id, out var scale) ? scale : Vector2f.One),
                    QuadraticBezier2D => [points[0], points[points.Length / 2] * 2 - (points[0] + points[^1]) / 2, points[^1]],
                    _ => points,
                };
                expressions.Append(LegacyClipEmitter.Wrap("", "[", [.. written.Select(Point)], 16, ",", "]")).Append('\n');
            }
            expressions.Append("            ]),\n");
        }

        var source = new StringBuilder();
        source.Append("// Generated from the authored JSON face part by LegacyFaceEmitter in App2d.Tests. While that part exists, change it and\n");
        source.Append($"// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named {regenerateWith}) instead of editing this file.\n");
        source.Append("using App2d.Core.Animation;\n\nnamespace App2d.Core.Characters;\n\n");
        source.Append($"/// <summary>{summary}</summary>\npublic static class {className}\n{{\n");
        source.Append($"    public static Face Face {{ get; }} = new(new({Number(attachment.Instance.X)}, 0f), {LegacyClipEmitter.Time(blinkClip.Duration)},\n        [\n");
        source.Append(pieces).Append("        ],\n        [\n").Append(expressions).Append("        ]);\n\n");
        source.Append($"    /// <summary>How far the face sits in front of the head it is drawn on.</summary>\n    public const float Layer = {Number(attachment.Instance.Layer)};\n");
        source.Append($"    /// <summary>The expression a person wears when nothing asks for another.</summary>\n    public const string Default = \"{attachment.Instance.Expression}\";\n}}\n");
        return source.ToString();
    }

    /// <summary>An ellipse's centre and radii from its outline, which lies square to the mount.</summary>
    private static Vector2f[] Ellipse(Vector2f[] outline, Vector2f blink)
    {
        Vector2f low = outline[0], high = outline[0];
        foreach (var point in outline)
        {
            low = Vector2f.Min(low, point);
            high = Vector2f.Max(high, point);
        }
        return [(low + high) / 2, (high - low) / 2 / blink];
    }
}
