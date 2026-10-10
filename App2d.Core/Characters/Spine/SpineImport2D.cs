using App2d.Core.Mathematics;
using App2d.Core.Characters.Authored;
using App2d.Core.IO;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace App2d.Core.Characters.Spine;

public sealed record SpineImportResult2D(CharacterModel Model, IReadOnlyList<MotionClip> Animations, IReadOnlyDictionary<string, byte[]> Images);

/// <summary>Spine 4.2 region skeletons converted to editable native assets. Unsupported behavior is rejected explicitly.</summary>
public static partial class SpineImport2D
{
    private const float Radians = MathF.PI / 180;
    [GeneratedRegex("[^a-z0-9-]+")] private static partial Regex InvalidId();
    internal static string Id(string name, IEnumerable<string> used)
    {
        var id = InvalidId().Replace(name.ToLowerInvariant(), "-").Trim('-');
        if (id.Length == 0) id = "item";
        id = id[..Math.Min(id.Length, 54)];
        return ModelAuthoring.UniqueId(id, used.Concat([CharacterModel.Unit, CharacterModel.Locomotion]));
    }
    internal static float Number(JsonNode? node, string key, float fallback = 0) => node?[key]?.GetValue<float>() ?? fallback;
    internal static string Text(JsonNode? node, string key, string fallback = "") => node?[key]?.GetValue<string>() ?? fallback;
    private static void Unsupported(string path) => throw new InvalidDataException($"Spine 4.2 import does not yet support {path}. Export a region-only rig or bake that feature in Spine first.");

    public static SpineImportResult2D Load(string path, string id, string name, string? imagesDirectory = null, string? atlasPath = null, float unitsPerPixel = .01f)
    {
        var full = Path.GetFullPath(path); var directory = Path.GetDirectoryName(full)!;
        var json = File.ReadAllText(full);
        var root = JsonNode.Parse(json) ?? throw new InvalidDataException("Empty Spine file.");
        var imagePath = Text(root["skeleton"], "images", "images");
        var imageRoot = imagesDirectory ?? Path.GetFullPath(Path.Combine(directory, imagePath));
        atlasPath ??= File.Exists(Path.ChangeExtension(full, ".atlas")) ? Path.ChangeExtension(full, ".atlas") : null;
        var atlas = atlasPath is null ? null : new SpineAtlas2D(atlasPath);
        byte[] Image(string image)
        {
            var local = image.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? image : image + ".png";
            var source = FilePaths.ResolveUnderRoot(imageRoot, local);
            return File.Exists(source) ? File.ReadAllBytes(source) : atlas?.Extract(image) ?? throw new FileNotFoundException($"Missing Spine image '{image}'. Choose its images directory or place the matching .atlas beside the JSON.", source);
        }
        var result = Parse(json, id, name, Image, unitsPerPixel);
        result.Model.Source = new() { Kind = AssetSource.Spine, File = full };
        foreach (var clip in result.Animations) clip.Source = new() { Kind = AssetSource.Spine, File = full, Motion = clip.Name };
        return result;
    }

    public static SpineImportResult2D Parse(string json, string id, string name, Func<string, byte[]> image, float unitsPerPixel = .01f)
    {
        AuthoredAsset.RequireId(id, "model id");
        if (!float.IsFinite(unitsPerPixel) || unitsPerPixel <= 0) throw new ArgumentOutOfRangeException(nameof(unitsPerPixel));
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Empty Spine file.");
        var version = Text(root["skeleton"], "spine");
        if (!version.StartsWith("4.2.", StringComparison.Ordinal)) throw new InvalidDataException($"Expected Spine 4.2 JSON; found '{version}'.");
        foreach (var section in new[] { "ik", "transform", "path", "physics" }) if (root[section] is JsonArray { Count: > 0 }) Unsupported(section + " constraints");
        var model = new CharacterModel { Id = id, Name = name };
        var bones = new Dictionary<string, string>(StringComparer.Ordinal);
        var frames = new Dictionary<string, Matrix3x2>(StringComparer.Ordinal);
        foreach (var entry in root["bones"]?.AsArray() ?? throw new InvalidDataException("Spine file has no bones."))
        {
            var boneName = Text(entry, "name"); var boneId = Id(boneName, bones.Values);
            if (string.IsNullOrWhiteSpace(boneName) || bones.ContainsKey(boneName)) throw new InvalidDataException($"Invalid or duplicate bone '{boneName}'.");
            if (Text(entry, "inherit", "normal") != "normal" || Text(entry, "transform", "normal") != "normal") Unsupported($"bone '{boneName}' transform inheritance");
            if (entry?["skin"]?.GetValue<bool>() == true) Unsupported($"bone '{boneName}' skin activation");
            var parentName = Text(entry, "parent");
            if (parentName.Length > 0 && !bones.ContainsKey(parentName)) throw new InvalidDataException($"Bone '{boneName}': parent '{parentName}' must precede it.");
            var setup = new Affine2D
            {
                X = Number(entry, "x") * unitsPerPixel,
                Y = Number(entry, "y") * unitsPerPixel,
                Rotation = Number(entry, "rotation") * Radians,
                ScaleX = Number(entry, "scaleX", 1),
                ScaleY = Number(entry, "scaleY", 1),
                ShearX = Number(entry, "shearX") * Radians,
                ShearY = Number(entry, "shearY") * Radians
            };
            var frame = setup.Matrix * (parentName.Length == 0 ? Matrix3x2.Identity : frames[parentName]);
            model.Controls.Add(new()
            {
                Id = boneId,
                Name = boneName,
                Parent = parentName.Length == 0 ? null : bones[parentName],
                Transform = setup,
                Rest = new(frame.M31, frame.M32),
                RestAngle = MathF.Atan2(frame.M12, frame.M11),
                Length = Number(entry, "length") * unitsPerPixel
            });
            bones.Add(boneName, boneId); frames.Add(boneName, frame);
        }
        var slots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in root["slots"]?.AsArray() ?? [])
        {
            var slotName = Text(entry, "name"); var slotId = Id(slotName, slots.Values); var bone = Text(entry, "bone");
            if (!bones.TryGetValue(bone, out var parentBone) || string.IsNullOrWhiteSpace(slotName) || !slots.TryAdd(slotName, slotId)) throw new InvalidDataException($"Invalid slot '{slotName}'.");
            if (entry?["dark"] is not null) Unsupported($"slot '{slotName}' two-color tint");
            model.Slots.Add(new() { Id = slotId, Name = slotName, Bone = parentBone, Attachment = entry?["attachment"]?.GetValue<string>(), Color = Text(entry, "color", "ffffffff"), Blend = Text(entry, "blend", "normal") });
        }
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in root["skins"]?.AsArray() ?? [])
        {
            var skinName = Text(entry, "name");
            foreach (var section in new[] { "bones", "ik", "transform", "path", "physics" }) if (entry?[section] is JsonArray { Count: > 0 }) Unsupported($"skin '{skinName}' {section}");
            var skin = new SkeletonSkin2D { Id = skinName == "default" ? "default" : Id(skinName, model.Skins.Select(s => s.Id)), Name = skinName };
            foreach (var slot in entry?["attachments"]?.AsObject() ?? [])
            {
                if (!slots.TryGetValue(slot.Key, out var slotId)) throw new InvalidDataException($"Skin '{skinName}': unknown slot '{slot.Key}'.");
                var attachments = new Dictionary<string, string>(StringComparer.Ordinal); skin.Attachments.Add(slotId, attachments);
                foreach (var attachment in slot.Value?.AsObject() ?? [])
                {
                    var value = attachment.Value; var type = Text(value, "type", "region");
                    if (type != "region") Unsupported($"skin '{skinName}', slot '{slot.Key}', attachment '{attachment.Key}' of type '{type}'");
                    if (value?["sequence"] is not null) Unsupported($"attachment '{attachment.Key}' image sequence");
                    var partId = Id(skin.Id + "-" + slotId + "-" + attachment.Key, model.Parts.Select(p => p.Id));
                    var texture = "images/" + id + "/" + partId + ".png";
                    images.Add(texture, image(Text(value, "path", Text(value, "name", attachment.Key))));
                    var width = Number(value, "width") * unitsPerPixel; var height = Number(value, "height") * unitsPerPixel;
                    var bone = model.Slots.First(s => s.Id == slotId).Bone;
                    model.Parts.Add(new()
                    {
                        Id = partId,
                        A = bone,
                        Frame = bone,
                        Width = width,
                        Height = height,
                        OffsetX = Number(value, "x") * unitsPerPixel,
                        OffsetY = Number(value, "y") * unitsPerPixel,
                        Angle = Number(value, "rotation") * Radians,
                        ScaleX = Number(value, "scaleX", 1),
                        ScaleY = Number(value, "scaleY", 1),
                        Face = "none",
                        Geometry = RectangleShapeDefinition2D.FromSize(new(1, 1)),
                        Material = new() { Texture = texture, Fill = "#ffffff", Tint = Text(value, "color", "ffffffff") }
                    });
                    attachments.Add(attachment.Key, partId);
                }
            }
            model.Skins.Add(skin);
        }
        model.Validate(); var resolved = ResolvedModel.From(model);
        var animations = new List<MotionClip>();
        foreach (var entry in root["animations"]?.AsObject() ?? [])
        {
            var duration = Duration(entry.Value);
            var clip = new MotionClip { Id = Id(id + "-" + entry.Key, animations.Select(c => c.Id)), Name = entry.Key, Model = id, Duration = MathF.Max(.05f, duration) };
            foreach (var section in entry.Value?.AsObject() ?? [])
                if (section.Key is not ("bones" or "slots" or "drawOrder" or "events")) Unsupported($"animation '{entry.Key}' timeline '{section.Key}'");
            foreach (var bone in entry.Value?["bones"]?.AsObject() ?? [])
            {
                if (!bones.TryGetValue(bone.Key, out var boneId)) throw new InvalidDataException($"Unknown animated bone '{bone.Key}'.");
                foreach (var timeline in bone.Value?.AsObject() ?? [])
                {
                    var track = ReadTrack(timeline.Key, timeline.Value!.AsArray(), boneId, unitsPerPixel);
                    if (clip.Tracks.Any(t => t.Kind == track.Kind && t.Target == boneId)) Unsupported($"animation '{entry.Key}', bone '{bone.Key}' separate X/Y timelines. Combine the axes into one timeline");
                    clip.Tracks.Add(track);
                }
            }
            foreach (var slot in entry.Value?["slots"]?.AsObject() ?? [])
            {
                if (!slots.TryGetValue(slot.Key, out var slotId)) throw new InvalidDataException($"Unknown animated slot '{slot.Key}'.");
                foreach (var timeline in slot.Value?.AsObject() ?? [])
                {
                    if (timeline.Key == "attachment")
                        clip.Attachments.Add(new() { Slot = slotId, Keys = [.. timeline.Value!.AsArray().Select(k => new SlotAttachmentKey2D { Time = Number(k, "time"), Attachment = k?["name"]?.GetValue<string>() })] });
                    else if (timeline.Key is SlotColorTrack2D.Rgba or SlotColorTrack2D.Rgb or SlotColorTrack2D.Alpha)
                        clip.Colors.Add(ReadColors(timeline.Key, timeline.Value!.AsArray(), slotId));
                    else Unsupported($"animation '{entry.Key}', slot '{slot.Key}' timeline '{timeline.Key}'");
                }
            }
            foreach (var key in entry.Value?["drawOrder"]?.AsArray() ?? [])
            {
                var order = Enumerable.Repeat<string?>(null, model.Slots.Count).ToArray(); var unchanged = new List<string>(); var source = 0;
                foreach (var offset in key?["offsets"]?.AsArray() ?? [])
                {
                    var original = model.Slots.FindIndex(s => s.Id == slots[Text(offset, "slot")]);
                    if (original < source) throw new InvalidDataException("Invalid Spine draw order offsets.");
                    while (source < original) unchanged.Add(model.Slots[source++].Id);
                    var target = source + (int)Number(offset, "offset");
                    if (target < 0 || target >= order.Length || order[target] is not null) throw new InvalidDataException("Invalid Spine draw order offset.");
                    order[target] = model.Slots[source++].Id;
                }
                while (source < model.Slots.Count) unchanged.Add(model.Slots[source++].Id);
                for (var i = order.Length - 1; i >= 0; i--) if (order[i] is null) { order[i] = unchanged[^1]; unchanged.RemoveAt(unchanged.Count - 1); }
                clip.DrawOrder.Add(new() { Time = Number(key, "time"), Slots = [.. order.Select(s => s!)] });
            }
            foreach (var key in entry.Value?["events"]?.AsArray() ?? [])
            {
                var eventName = Text(key, "name"); var defaults = root["events"]?[eventName];
                if (defaults?["audio"] is not null) Unsupported($"event '{eventName}' audio");
                clip.Events.Add(new() { Name = eventName, Time = Number(key, "time"), Int = (int)Number(key, "int", Number(defaults, "int")), Float = Number(key, "float", Number(defaults, "float")), String = Text(key, "string", Text(defaults, "string")) });
            }
            clip.Validate(resolved); animations.Add(clip);
        }
        return new(model, animations, images);
    }

    private static float Duration(JsonNode? value)
    {
        if (value is JsonObject obj) return MathF.Max(Number(obj, "time"), obj.Select(p => Duration(p.Value)).DefaultIfEmpty().Max());
        if (value is JsonArray array) return array.Select(Duration).DefaultIfEmpty().Max();
        return 0;
    }
    private static ClipTrack ReadTrack(string type, JsonArray keys, string bone, float unit)
    {
        var kind = type.StartsWith("translate", StringComparison.Ordinal) ? MotionClip.TranslateKind : type.StartsWith("scale", StringComparison.Ordinal) ? MotionClip.ScaleKind
            : type.StartsWith("shear", StringComparison.Ordinal) ? MotionClip.ShearKind : type == "rotate" ? MotionClip.RotateKind : "";
        if (kind.Length == 0 || type is not ("translate" or "translatex" or "translatey" or "scale" or "scalex" or "scaley" or "shear" or "shearx" or "sheary" or "rotate")) Unsupported($"bone '{bone}' timeline '{type}'");
        var track = new ClipTrack { Kind = kind, Target = bone, SetupBeforeFirst = true };
        var single = type.EndsWith('x') || type.EndsWith('y') || type == "rotate"; var yOnly = type.EndsWith('y');
        var scale = kind == MotionClip.TranslateKind ? unit : kind is MotionClip.RotateKind or MotionClip.ShearKind ? Radians : 1;
        var baseline = kind == MotionClip.ScaleKind ? 1f : 0;
        for (var i = 0; i < keys.Count; i++)
        {
            var entry = keys[i]; var x = Number(entry, single ? "value" : "x", baseline); var y = Number(entry, "y", baseline);
            var key = new ClipKey { Time = Number(entry, "time") };
            if (kind == MotionClip.RotateKind)
            {
                key.Angle = x * scale;
            }
            else { key.X = yOnly ? 0 : (x - baseline) * scale; key.Y = yOnly ? (x - baseline) * scale : single ? 0 : (y - baseline) * scale; }
            if (entry?["curve"] is JsonValue curve && curve.TryGetValue<string>(out var stepped) && stepped == "stepped")
            {
                key.Ease = ClipEase.Step;
            }
            else if (entry?["curve"] is JsonArray controls && i + 1 < keys.Count)
            {
                var next = keys[i + 1]; var endTime = Number(next, "time");
                if (controls.Count != (single ? 4 : 8)) throw new InvalidDataException("Invalid Spine Bezier component count.");
                var c = ReadCurve(controls, 0, key.Time, endTime, scale, -baseline * scale);
                if (yOnly) key.CurveY = c; else key.Curve = c;
                if (!single) key.CurveY = ReadCurve(controls, 4, key.Time, endTime, scale, -baseline * scale);
            }
            else if (entry?["curve"] is not null && i + 1 < keys.Count)
            {
                throw new InvalidDataException("Invalid Spine timeline curve.");
            }

            track.Keys.Add(key);
        }
        return track;
    }

    private static KeyCurve2D ReadCurve(JsonArray controls, int index, float startTime, float endTime, float scale = 1, float offset = 0)
    {
        if (controls.Count < index + 4 || endTime <= startTime) throw new InvalidDataException("Invalid Spine Bezier controls.");
        var curve = new KeyCurve2D
        {
            X1 = (controls[index]!.GetValue<float>() - startTime) / (endTime - startTime),
            X2 = (controls[index + 2]!.GetValue<float>() - startTime) / (endTime - startTime),
            Y1 = controls[index + 1]!.GetValue<float>() * scale + offset,
            Y2 = controls[index + 3]!.GetValue<float>() * scale + offset,
            Absolute = true
        };
        curve.Validate(); return curve;
    }

    private static SlotColorTrack2D ReadColors(string kind, JsonArray keys, string slot)
    {
        var track = new SlotColorTrack2D { Slot = slot, Kind = kind };
        for (var i = 0; i < keys.Count; i++)
        {
            var entry = keys[i];
            var value = kind == SlotColorTrack2D.Alpha ? new Vector4(1, 1, 1, Number(entry, "value"))
                : SlotColorTrack2D.ParseColor(Text(entry, "color") + (kind == SlotColorTrack2D.Rgb ? "ff" : ""));
            var key = new SlotColorKey2D { Time = Number(entry, "time"), R = value.X, G = value.Y, B = value.Z, A = value.W };
            if (entry?["curve"] is JsonValue step && step.TryGetValue<string>(out var ease) && ease == "stepped")
            {
                key.Ease = ClipEase.Step;
            }
            else if (entry?["curve"] is JsonArray controls && i + 1 < keys.Count)
            {
                var endTime = Number(keys[i + 1], "time");
                var components = kind == SlotColorTrack2D.Alpha ? 1 : kind == SlotColorTrack2D.Rgb ? 3 : 4;
                if (controls.Count != components * 4) throw new InvalidDataException("Invalid Spine color Bezier component count.");
                if (kind == SlotColorTrack2D.Alpha)
                {
                    key.CurveA = ReadCurve(controls, 0, key.Time, endTime);
                }
                else
                {
                    key.CurveR = ReadCurve(controls, 0, key.Time, endTime); key.CurveG = ReadCurve(controls, 4, key.Time, endTime);
                    key.CurveB = ReadCurve(controls, 8, key.Time, endTime);
                    if (kind == SlotColorTrack2D.Rgba) key.CurveA = ReadCurve(controls, 12, key.Time, endTime);
                }
            }
            else if (entry?["curve"] is not null && i + 1 < keys.Count)
            {
                throw new InvalidDataException("Invalid Spine color curve.");
            }

            track.Keys.Add(key);
        }
        return track;
    }
}
