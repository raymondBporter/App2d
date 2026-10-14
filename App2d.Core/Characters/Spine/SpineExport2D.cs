using App2d.Core.Mathematics;
using App2d.Core.Characters.Authored;
using App2d.Core.IO;
using App2d.Core.Rendering.Characters;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Spine;

public sealed record SpineExportPackage2D(string Json, string Atlas, string NativeBackup, IReadOnlyDictionary<string, byte[]> Images, IReadOnlyList<string> Notes)
{
    /// <summary>Writes a portable JSON, loose editor images, atlas, native backup and conversion notes.</summary>
    public void Save(string jsonPath)
    {
        var path = Path.GetFullPath(jsonPath); var root = Path.GetDirectoryName(path)!;
        foreach (var (image, bytes) in Images)
        {
            var target = FilePaths.ResolveUnderRoot(root, image); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, bytes);
        }
        Directory.CreateDirectory(root);
        AtomicFile.WriteAllText(path, Json);
        AtomicFile.WriteAllText(Path.ChangeExtension(path, ".atlas"), Atlas);
        AtomicFile.WriteAllText(Path.ChangeExtension(path, ".app2d.json"), NativeBackup);
        AtomicFile.WriteAllText(Path.ChangeExtension(path, ".conversion.txt"), string.Join(Environment.NewLine, Notes));
    }
}

/// <summary>Editable Spine 4.2 export. Procedural artwork becomes region images; native-only data stays in a companion backup.</summary>
public static class SpineExport2D
{
    private const float Degrees = 180 / MathF.PI;
    private sealed record PartBone(PuppetPart Part, string Name, Matrix3x2 Rest, float StrokeLength);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SpineExportPackage2D Build(ResolvedModel model, IEnumerable<MotionClip> animations, float pixelsPerUnit = 100, int samplesPerSecond = 60)
    {
        if (!float.IsFinite(pixelsPerUnit) || pixelsPerUnit <= 0 || pixelsPerUnit > 2048 || samplesPerSecond is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(pixelsPerUnit));
        var clips = animations.ToList(); foreach (var clip in clips) clip.Validate(model);
        var animationNames = new Dictionary<MotionClip, string>();
        foreach (var clip in clips)
            animationNames.Add(clip, Unique(clips.Count(c => c.Name == clip.Name) > 1 ? clip.Name + " (" + clip.Id + ")" : clip.Name, animationNames.Values));
        var names = model.Controls.ToDictionary(c => c.Key, c => c.Value.Name ?? c.Key, StringComparer.Ordinal);
        if (names.Values.Distinct(StringComparer.Ordinal).Count() != names.Count) throw new InvalidDataException("Spine bone names must be unique.");
        var rootName = model.Order.All(c => c.Transform is not null) && model.Order.Count(c => c.Parent is null) == 1 && clips.All(c => c.Travel.Keys.Count == 0)
            ? null : Unique("app2d-motion", names.Values);
        var used = names.Values.ToHashSet(StringComparer.Ordinal);
        var bones = new JsonArray();
        if (rootName is not null) { used.Add(rootName); bones.Add(new JsonObject { ["name"] = rootName }); }
        var setup = new Dictionary<string, Affine2D>();
        Matrix3x2 Relative(Matrix3x2 frame, Matrix3x2 parent)
        {
            if (!Matrix3x2.Invert(parent, out var inverse)) throw new InvalidDataException("Cannot bake an attachment or point rig through a singular parent transform. Use nonzero parent scale when exporting this rig.");
            return frame * inverse;
        }
        foreach (var control in model.Order)
        {
            var local = model.SetupTransforms.TryGetValue(control.Id, out var resolvedSetup) ? resolvedSetup
                : Affine2D.FromMatrix(Relative(model.RestTransforms[control.Id], control.Parent is null ? Matrix3x2.Identity : model.RestTransforms[control.Parent]));
            setup[control.Id] = local;
            bones.Add(Bone(names[control.Id], control.Parent is null ? rootName : names[control.Parent], local, control.Length, pixelsPerUnit));
        }
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal); var atlas = new StringBuilder();
        void AddImage(string path, byte[] bytes)
        {
            images.Add("images/" + path + ".png", bytes);
            using var stream = new MemoryStream(bytes); using var bitmap = new Bitmap(stream);
            atlas.AppendLine("images/" + path + ".png").AppendLine($"size: {bitmap.Width}, {bitmap.Height}").AppendLine("filter: Linear, Linear").AppendLine("pma: false")
                .AppendLine(path).AppendLine($"bounds: 0, 0, {bitmap.Width}, {bitmap.Height}").AppendLine();
        }
        var restPose = PoseEvaluator.Rest(model);
        var slots = new JsonArray(); var skins = new JsonArray(); var parts = new List<PartBone>(); var slotNames = new Dictionary<string, string>();
        var nativeImages = model.Base.Slots.Count > 0;
        if (nativeImages)
        {
            var managed = model.Base.Skins.SelectMany(s => s.Attachments.Values).SelectMany(a => a.Values).ToHashSet(StringComparer.Ordinal);
            if (model.Parts.Any(p => !p.Hidden && !managed.Contains(p.Id))) throw new InvalidDataException("Spine export of a slot rig currently requires every visible part to belong to a skin. Assign the additional artwork to a slot first.");
            foreach (var slot in model.Base.Slots)
            {
                var slotName = slot.Name ?? slot.Id;
                if (slotNames.Values.Contains(slotName, StringComparer.Ordinal)) throw new InvalidDataException("Spine slot names must be unique.");
                slotNames.Add(slot.Id, slotName);
                var value = new JsonObject { ["name"] = slotName, ["bone"] = names[slot.Bone], ["color"] = slot.Color, ["blend"] = slot.Blend };
                if (slot.Attachment is not null) value["attachment"] = slot.Attachment;
                slots.Add(value);
            }
            foreach (var skin in model.Base.Skins)
            {
                var attachments = new JsonObject();
                foreach (var (slot, entries) in skin.Attachments)
                {
                    var values = new JsonObject(); attachments[slotNames[slot]] = values;
                    foreach (var (name, partId) in entries)
                    {
                        var part = model.Parts.First(p => p.Id == partId);
                        if (part.Material?.Texture is not { } texture || part.Frame != part.A) throw new InvalidDataException($"Slot '{slot}': export currently requires a bone-attached image region.");
                        var imageName = Unique(model.Id + "-" + part.Id, images.Keys.Select(p => Path.GetFileNameWithoutExtension(p))); AddImage(imageName, File.ReadAllBytes(FilePaths.ResolveUnderRoot(model.TextureRoot, texture)));
                        values[name] = new JsonObject
                        {
                            ["path"] = imageName,
                            ["x"] = part.OffsetX * pixelsPerUnit,
                            ["y"] = part.OffsetY * pixelsPerUnit,
                            ["rotation"] = part.Angle * Degrees,
                            ["scaleX"] = part.ScaleX,
                            ["scaleY"] = part.ScaleY,
                            ["width"] = part.Width * pixelsPerUnit,
                            ["height"] = part.Height * pixelsPerUnit,
                            ["color"] = RegionColor(part)
                        };
                    }
                }
                skins.Add(new JsonObject { ["name"] = skin.Name ?? skin.Id, ["attachments"] = attachments });
            }
        }
        else
        {
            var attachments = new JsonObject();
            foreach (var part in model.Parts.Where(p => !p.Hidden).OrderByDescending(p => Depth(p, restPose)))
            {
                if (part.Material?.Texture is not null) throw new InvalidDataException($"Image part '{part.Id}' needs a slot and skin before Spine export.");
                var helper = Unique("app2d-part-" + part.Id, used); used.Add(helper);
                var length = part.B is { } end ? Vector2.Distance(restPose.World(part.A).XY(), restPose.World(end).XY()) : 1;
                var frame = PartFrame(part, restPose, MathF.Max(length, .0001f));
                var local = Affine2D.FromMatrix(Relative(frame, restPose.Bones[part.A]));
                bones.Add(Bone(helper, names[part.A], local, 0, pixelsPerUnit));
                parts.Add(new(part, helper, frame, MathF.Max(length, .0001f))); setup[helper] = local;
                slots.Add(new JsonObject { ["name"] = part.Id, ["bone"] = helper, ["attachment"] = part.Face });
                slotNames[part.Id] = part.Id;
                var choices = clips.SelectMany(c => c.Faces.Where(t => t.Part == part.Id).SelectMany(t => t.Keys.Select(k => k.Expression))).Append(part.Face).Distinct(StringComparer.Ordinal);
                var regions = new JsonObject(); attachments[part.Id] = regions;
                foreach (var face in choices)
                {
                    var baked = Rasterize(model, restPose, part with { Face = face }, frame, pixelsPerUnit);
                    var imageName = model.Id + "-" + part.Id + "-" + face; AddImage(imageName, baked.Image);
                    regions[face] = new JsonObject { ["path"] = imageName, ["x"] = baked.Center.X * pixelsPerUnit, ["y"] = baked.Center.Y * pixelsPerUnit, ["width"] = baked.Size.X * pixelsPerUnit, ["height"] = baked.Size.Y * pixelsPerUnit };
                }
            }
            skins.Add(new JsonObject { ["name"] = "default", ["attachments"] = attachments });
        }
        var outputAnimations = new JsonObject();
        var exact = model.Order.All(c => c.Transform is not null) && model.Chains.Count == 0 && clips.All(c => c.Contacts.Count == 0);
        foreach (var clip in clips)
        {
            var animation = new JsonObject(); var timelines = new JsonObject(); animation["bones"] = timelines;
            var motion = new JsonObject { ["translate"] = new JsonArray() };
            if (rootName is not null) timelines[rootName] = motion;
            if (exact)
            {
                foreach (var track in clip.Tracks.Where(t => t.Kind is MotionClip.TranslateKind or MotionClip.RotateKind or MotionClip.ScaleKind or MotionClip.ShearKind))
                {
                    if ((track.Scale ?? model.Controls[track.Target].Scale) != CharacterModel.Unit) throw new InvalidDataException("Affine Spine tracks must use unscaled model units.");
                    if (timelines[names[track.Target]] is not JsonObject channels) timelines[names[track.Target]] = channels = [];
                    var keys = track.Keys.ToList();
                    if (!track.SetupBeforeFirst && keys.Count > 0 && keys[0].Time > 0) keys.Insert(0, keys[0] with { Time = 0, Ease = ClipEase.Step, Curve = null, CurveY = null });
                    channels[track.Kind] = Track(keys, track.Kind, pixelsPerUnit);
                }
                var travelRatio = clip.Travel.Scale == CharacterModel.Unit ? 1 : model.Measure(clip.Travel.Scale) / clip.Reference[clip.Travel.Scale];
                KeyCurve2D? Retarget(KeyCurve2D? curve) => curve?.Absolute == true ? curve with { Y1 = curve.Y1 * travelRatio, Y2 = curve.Y2 * travelRatio } : curve;
                motion["translate"] = Track(clip.Travel.Keys.Count == 0 ? [new() { Time = clip.Duration }] : [.. clip.Travel.Keys.Select(k => k with
                { X = k.X * travelRatio, Y = k.Y * travelRatio, Curve = Retarget(k.Curve), CurveY = Retarget(k.CurveY) })], MotionClip.TranslateKind, pixelsPerUnit);
            }
            else
            {
                var times = SampleTimes(clip, samplesPerSecond); var previous = new Dictionary<string, float>();
                foreach (var time in times)
                {
                    var pose = PoseEvaluator.Sample(model, clip, time);
                    ((JsonArray)motion["translate"]!).Add(new JsonObject { ["time"] = time, ["x"] = pose.Locomotion.X * pixelsPerUnit, ["y"] = pose.Locomotion.Y * pixelsPerUnit });
                    var motionFrame = Matrix3x2.CreateTranslation(pose.Locomotion.X, pose.Locomotion.Y);
                    foreach (var control in model.Order)
                    {
                        var frame = Relative(pose.Bones[control.Id], control.Parent is null ? motionFrame : pose.Bones[control.Parent]);
                        Append(timelines, names[control.Id], control.Id, Affine2D.FromMatrix(frame), setup[control.Id], time, previous, pixelsPerUnit);
                    }
                    foreach (var part in parts)
                    {
                        var frame = Relative(PartFrame(part.Part, pose, part.StrokeLength), pose.Bones[part.Part.A]);
                        Append(timelines, part.Name, part.Name, Affine2D.FromMatrix(frame), setup[part.Name], time, previous, pixelsPerUnit);
                    }
                }
            }
            // Affine rigs with procedural parts still require the helper-bone channels.
            if (exact && parts.Count > 0)
            {
                var previous = new Dictionary<string, float>();
                foreach (var time in SampleTimes(clip, samplesPerSecond))
                {
                    var pose = PoseEvaluator.Sample(model, clip, time);
                    foreach (var part in parts)
                    {
                        Append(timelines, part.Name, part.Name,
                        Affine2D.FromMatrix(Relative(PartFrame(part.Part, pose, part.StrokeLength), pose.Bones[part.Part.A])), setup[part.Name], time, previous, pixelsPerUnit);
                    }
                }
            }
            var slotTimelines = new JsonObject();
            foreach (var track in clip.Attachments) slotTimelines[slotNames[track.Slot]] = new JsonObject { ["attachment"] = new JsonArray([.. track.Keys.Select(k => (JsonNode)new JsonObject { ["time"] = k.Time, ["name"] = k.Attachment })]) };
            foreach (var track in clip.Colors)
            {
                if (slotTimelines[slotNames[track.Slot]] is not JsonObject channels) slotTimelines[slotNames[track.Slot]] = channels = [];
                channels[track.Kind] = Colors(track);
            }
            if (!nativeImages)
            {
                foreach (var track in clip.Faces.Where(t => slotNames.ContainsKey(t.Part)))
                {
                    var keys = track.Keys.ToList();
                    if (keys.Count > 0 && keys[0].Time > 0) keys.Insert(0, keys[0] with { Time = 0 });
                    slotTimelines[track.Part] = new JsonObject { ["attachment"] = new JsonArray([.. keys.Select(k => (JsonNode)new JsonObject { ["time"] = k.Time, ["name"] = k.Expression })]) };
                }
            }

            if (slotTimelines.Count > 0) animation["slots"] = slotTimelines;
            var drawOrder = new JsonArray(); var initial = slots.Select(s => s!["name"]!.GetValue<string>()).ToArray();
            if (nativeImages)
            {
                foreach (var key in clip.DrawOrder) drawOrder.Add(Order(key.Time, initial, [.. key.Slots.Select(s => slotNames[s])]));
            }
            else
            {
                var last = initial;
                foreach (var time in SampleTimes(clip, samplesPerSecond))
                {
                    var pose = PoseEvaluator.Sample(model, clip, time); var order = parts.OrderByDescending(p => Depth(p.Part, pose)).Select(p => p.Part.Id).ToArray();
                    if (!order.SequenceEqual(last)) { drawOrder.Add(Order(time, initial, order)); last = order; }
                }
            }
            if (drawOrder.Count > 0) animation["drawOrder"] = drawOrder;
            var events = clip.Events.ConvertAll(e => new JsonObject { ["time"] = e.Time, ["name"] = e.Name, ["int"] = e.Int, ["float"] = e.Float, ["string"] = e.String });
            events.AddRange(clip.Markers.Select(m => new JsonObject { ["time"] = m.Time, ["name"] = m.Id }));
            if (events.Count > 0) animation["events"] = new JsonArray([.. events.OrderBy(e => e["time"]!.GetValue<float>()).Cast<JsonNode>()]);
            // JSON has no duration field: retain the native end time even if every channel finishes earlier.
            var timelineEnd = timelines.SelectMany(b => b.Value!.AsObject().SelectMany(c => c.Value!.AsArray())).Select(k => SpineImport2D.Number(k, "time")).DefaultIfEmpty().Max();
            if (timelineEnd < clip.Duration)
            {
                if (timelines.Count == 0)
                {
                    timelines[names[model.Order[0].Id]] = new JsonObject { ["translate"] = new JsonArray(new JsonObject { ["time"] = clip.Duration }) };
                }
                else
                {
                    var channel = timelines.First().Value!.AsObject().First().Value!.AsArray();
                    var end = channel.LastOrDefault()?.DeepClone()?.AsObject() ?? []; end["time"] = clip.Duration; channel.Add(end);
                }
            }
            outputAnimations[animationNames[clip]] = animation;
        }
        var eventDefinitions = new JsonObject(); foreach (var name in clips.SelectMany(c => c.Events.Select(e => e.Name).Concat(c.Markers.Select(m => m.Id))).Distinct()) eventDefinitions[name] = new JsonObject();
        var root = new JsonObject { ["skeleton"] = new JsonObject { ["spine"] = "4.2.22", ["images"] = "./images/", ["fps"] = samplesPerSecond }, ["bones"] = bones, ["slots"] = slots, ["skins"] = skins, ["events"] = eventDefinitions, ["animations"] = outputAnimations };
        var backup = new JsonObject { ["format"] = "app2d-spine-backup", ["version"] = 1, ["model"] = JsonNode.Parse(model.Base.ToJson()), ["variant"] = model.Variant is null ? null : JsonNode.Parse(model.Variant.ToJson()), ["animations"] = new JsonArray([.. clips.Select(c => JsonNode.Parse(c.ToJson()))]) };
        var notes = new List<string> { "Spine 4.2 JSON. Import Data into a NEW Spine project; images are in the adjacent images folder.",
            "The .app2d.json companion is a native backup, not a merge format. Spine does not preserve its contents. Keep it for procedural geometry, contacts, sockets, model metadata and 3D orientation. Entity equipment and game bindings remain in the native asset library.",
            "Export back from Spine with Nonessential data checked, then import the JSON into Character Studio as a new model." };
        if (!nativeImages) notes.Add($"Procedural artwork is rasterized at {pixelsPerUnit} pixels per model unit. Two-point artwork is carried by editable helper bones sampled at {samplesPerSecond} Hz; edit those helper bones in Spine. Z becomes per-attachment slot order, so intersecting surfaces may differ.");
        if (!exact) notes.Add($"Point IK, contacts and proportion retargeting are baked to bone timelines at {samplesPerSecond} Hz, including key and contact boundaries. They remain editable animation, but their original solver relationships are in the native backup.");
        if (clips.Any(c => c.Tracks.Any(t => t.Kind == MotionClip.OrientKind))) notes.Add("3D socket orientation has no Spine equivalent and is retained only in the native backup.");
        if (animationNames.Any(p => p.Key.Name != p.Value)) notes.Add("Duplicate animation display names are disambiguated with their native clip IDs in the Spine JSON.");
        return new(root.ToJsonString(JsonOptions), atlas.ToString(), backup.ToJsonString(JsonOptions), images, notes);
    }

    private static string Unique(string basis, IEnumerable<string> used) => ModelAuthoring.UniqueId(basis, used);
    private static string RegionColor(PuppetPart part)
    {
        var color = Convert.ToUInt32(part.RenderMaterial.Tint ?? "ffffffff", 16);
        var fill = part.RenderMaterial.Fill is { } value ? App2d.Core.Rendering.ColorExtensions.FromHexRgb(value) : Microsoft.Xna.Framework.Color.White;
        return $"{(byte)((color >> 24) * fill.R / 255):x2}{(byte)(((color >> 16) & 255) * fill.G / 255):x2}{(byte)(((color >> 8) & 255) * fill.B / 255):x2}{(part.Hidden ? 0 : (byte)color):x2}";
    }
    private static JsonObject Bone(string name, string? parent, Affine2D t, float length, float ppu)
    {
        var result = new JsonObject
        {
            ["name"] = name,
            ["x"] = t.X * ppu,
            ["y"] = t.Y * ppu,
            ["rotation"] = t.Rotation * Degrees,
            ["scaleX"] = t.ScaleX,
            ["scaleY"] = t.ScaleY,
            ["shearX"] = t.ShearX * Degrees,
            ["shearY"] = t.ShearY * Degrees,
            ["length"] = length * ppu
        };
        if (parent is not null) result["parent"] = parent;
        return result;
    }
    private static Matrix3x2 PartFrame(PuppetPart part, EvaluatedPose pose, float strokeLength)
    {
        if (part.Geometry is App2d.Core.Curves.CurveDefinition2D)
        {
            var a = pose.World(part.A); var b = pose.World(part.B!); var along = new Vector2(b.X - a.X, b.Y - a.Y);
            var right = along.LengthSquared() > 1e-12f ? new Vector2(along.Y, -along.X) / along.Length() : Vector2.UnitX;
            return new(right.X, right.Y, along.X / strokeLength, along.Y / strokeLength, a.X, a.Y);
        }
        var frame = PartGeometry.FrameOf(part, pose.World, id => pose.Angles[id], id => pose.Bones[id]);
        return new(frame.Right.X, frame.Right.Y, frame.Up.X, frame.Up.Y, frame.Origin.X, frame.Origin.Y);
    }
    private static float Depth(PuppetPart part, EvaluatedPose pose) => part.Depth + (pose.World(part.A).Z + (part.B is null ? pose.World(part.A).Z : pose.World(part.B).Z)) / 2;
    private static float[] SampleTimes(MotionClip clip, int fps) => [.. Enumerable.Range(0, (int)MathF.Ceiling(clip.Duration * fps) + 1).Select(i => MathF.Min(clip.Duration, (float)i / fps))
        .Concat(clip.Tracks.SelectMany(t => t.Keys).Select(k => k.Time)).Concat(clip.Travel.Keys.Select(k => k.Time)).Concat(clip.Contacts.SelectMany(c => new[] { c.Start, c.Finish })).Append(clip.Duration).Distinct().Order()];
    private static void Append(JsonObject timelines, string name, string id, Affine2D value, Affine2D setup, float time, Dictionary<string, float> previous, float ppu)
    {
        if (timelines[name] is not JsonObject channels)
            timelines[name] = channels = new() { ["translate"] = new JsonArray(), ["rotate"] = new JsonArray(), ["scale"] = new JsonArray(), ["shear"] = new JsonArray() };
        var angle = value.Rotation - setup.Rotation;
        if (previous.TryGetValue(id, out var prior)) { while (angle - prior > MathF.PI) angle -= MathF.Tau; while (angle - prior < -MathF.PI) angle += MathF.Tau; }
        previous[id] = angle;
        ((JsonArray)channels["translate"]!).Add(new JsonObject { ["time"] = time, ["x"] = (value.X - setup.X) * ppu, ["y"] = (value.Y - setup.Y) * ppu });
        ((JsonArray)channels["rotate"]!).Add(new JsonObject { ["time"] = time, ["value"] = angle * Degrees });
        ((JsonArray)channels["scale"]!).Add(new JsonObject { ["time"] = time, ["x"] = setup.ScaleX == 0 ? 1 : value.ScaleX / setup.ScaleX, ["y"] = setup.ScaleY == 0 ? 1 : value.ScaleY / setup.ScaleY });
        ((JsonArray)channels["shear"]!).Add(new JsonObject { ["time"] = time, ["x"] = (value.ShearX - setup.ShearX) * Degrees, ["y"] = (value.ShearY - setup.ShearY) * Degrees });
    }
    private static JsonArray Track(List<ClipKey> keys, string kind, float ppu)
    {
        var result = new JsonArray(); var rotate = kind == MotionClip.RotateKind; var scale = kind == MotionClip.ScaleKind;
        var multiplier = kind == MotionClip.TranslateKind ? ppu : kind is MotionClip.RotateKind or MotionClip.ShearKind ? Degrees : 1;
        float X(ClipKey k) => (rotate ? k.Angle : k.X) * multiplier + (scale ? 1 : 0);
        float Y(ClipKey k) => k.Y * multiplier + (scale ? 1 : 0);
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i]; var node = new JsonObject { ["time"] = key.Time };
            if (rotate) { node["value"] = X(key); } else { node["x"] = X(key); node["y"] = Y(key); }
            if (key.Ease == ClipEase.Step)
            {
                node["curve"] = "stepped";
            }
            else if (i + 1 < keys.Count && (key.Curve is not null || key.CurveY is not null || key.Ease == ClipEase.Smooth))
            {
                var next = keys[i + 1]; var controls = new JsonArray();
                var curve = key.Curve ?? (key.Ease == ClipEase.Smooth ? new KeyCurve2D { X1 = 1f / 3, X2 = 2f / 3, Y1 = 0, Y2 = 1 } : new KeyCurve2D { X1 = 1f / 3, Y1 = 1f / 3, X2 = 2f / 3, Y2 = 2f / 3 });
                void Add(KeyCurve2D c, float a, float b)
                {
                    var dt = next.Time - key.Time; var (first, second) = c.Absolute
                        ? (c.Y1 * multiplier + (scale ? 1 : 0), c.Y2 * multiplier + (scale ? 1 : 0)) : c.Controls(a, b);
                    controls.Add(key.Time + c.X1 * dt); controls.Add(first); controls.Add(key.Time + c.X2 * dt); controls.Add(second);
                }
                Add(curve, X(key), X(next));
                if (!rotate) Add(key.CurveY ?? (curve.Absolute ? DefaultCurve(key.Ease) : curve), Y(key), Y(next)); node["curve"] = controls;
            }
            result.Add(node);
        }
        return result;
    }
    private static KeyCurve2D DefaultCurve(string ease) => ease == ClipEase.Smooth
        ? new() { X1 = 1f / 3, X2 = 2f / 3, Y1 = 0, Y2 = 1 }
        : new() { X1 = 1f / 3, Y1 = 1f / 3, X2 = 2f / 3, Y2 = 2f / 3 };

    private static JsonArray Colors(SlotColorTrack2D track)
    {
        var keys = track.Keys.ToList();
        if (!track.SetupBeforeFirst && keys.Count > 0 && keys[0].Time > 0)
            keys.Insert(0, keys[0] with { Time = 0, Ease = ClipEase.Step, CurveR = null, CurveG = null, CurveB = null, CurveA = null });
        var result = new JsonArray();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i]; var node = new JsonObject { ["time"] = key.Time };
            if (track.Kind == SlotColorTrack2D.Alpha) node["value"] = key.A;
            else node["color"] = SlotColorTrack2D.FormatColor(key.Value)[..(track.Kind == SlotColorTrack2D.Rgb ? 6 : 8)];
            if (key.Ease == ClipEase.Step)
            {
                node["curve"] = "stepped";
            }
            else if (i + 1 < keys.Count && (key.Ease == ClipEase.Smooth || key.CurveR is not null || key.CurveG is not null || key.CurveB is not null || key.CurveA is not null))
            {
                var next = keys[i + 1]; var controls = new JsonArray();
                void Add(KeyCurve2D? curve, float start, float end)
                {
                    var c = curve ?? DefaultCurve(key.Ease); var (first, second) = c.Controls(start, end); var dt = next.Time - key.Time;
                    controls.Add(key.Time + c.X1 * dt); controls.Add(first); controls.Add(key.Time + c.X2 * dt); controls.Add(second);
                }
                if (track.Kind != SlotColorTrack2D.Alpha)
                { Add(key.CurveR, key.R, next.R); Add(key.CurveG, key.G, next.G); Add(key.CurveB, key.B, next.B); }
                if (track.Kind != SlotColorTrack2D.Rgb) Add(key.CurveA, key.A, next.A);
                node["curve"] = controls;
            }
            result.Add(node);
        }
        return result;
    }
    private static JsonObject Order(float time, string[] setup, string[] order)
    {
        var offsets = new JsonArray(); for (var i = 0; i < setup.Length; i++)
        { var delta = Array.IndexOf(order, setup[i]) - i; if (delta != 0) offsets.Add(new JsonObject { ["slot"] = setup[i], ["offset"] = delta }); }
        return new() { ["time"] = time, ["offsets"] = offsets };
    }
    private sealed record Raster(byte[] Image, Vector2 Center, Vector2 Size);
    private static Raster Rasterize(ResolvedModel model, EvaluatedPose pose, PuppetPart part, Matrix3x2 frame, float ppu)
    {
        if (!Matrix3x2.Invert(frame, out var inverse)) throw new InvalidDataException($"Part '{part.Id}' has a collapsed setup frame.");
        var drawing = new PuppetDrawing(); drawing.Build(model.Base.Ink, model.Base.LineWidth, [part], pose.World, angle: id => pose.Angles[id], transform: id => pose.Bones[id]);
        var vertices = drawing.Mesh.Vertices.ToArray();
        if (vertices.Length == 0) throw new InvalidDataException($"Part '{part.Id}' has no visible geometry to export.");
        var points = vertices.Select(v => Vector2.Transform(new(v.Position.X, v.Position.Y), inverse)).ToArray();
        var min = new Vector2(points.Min(p => p.X), points.Min(p => p.Y)) - new Vector2(2 / ppu);
        var max = new Vector2(points.Max(p => p.X), points.Max(p => p.Y)) + new Vector2(2 / ppu);
        var width = (int)MathF.Ceiling((max.X - min.X) * ppu); var height = (int)MathF.Ceiling((max.Y - min.Y) * ppu);
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096) throw new InvalidDataException($"Part '{part.Id}' export image exceeds 4096 pixels; lower export resolution.");
        max = min + new Vector2(width / ppu, height / ppu);
        using var large = new Bitmap(width * 2, height * 2, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(large))
        {
            graphics.SmoothingMode = SmoothingMode.None;
            for (var i = 0; i < vertices.Length; i += 3)
            {
                var c = vertices[i].Color; using var brush = new SolidBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
                var triangle = Enumerable.Range(i, 3).Select(n => new PointF((points[n].X - min.X) * ppu * 2, (max.Y - points[n].Y) * ppu * 2)).ToArray();
                graphics.FillPolygon(brush, triangle);
            }
        }
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) { graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.DrawImage(large, new Rectangle(0, 0, width, height)); }
        using var stream = new MemoryStream(); bitmap.Save(stream, ImageFormat.Png);
        return new(stream.ToArray(), (min + max) / 2, max - min);
    }
    private static Vector2 XY(this Vector3 value) => new(value.X, value.Y);
}
