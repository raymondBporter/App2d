using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>An ordered attachment location on a bone. A socket is a placement frame; a slot also owns visible artwork.</summary>
public sealed record SkeletonSlot2D
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string Bone { get; set; } = "";
    public string? Attachment { get; set; }
    public string Color { get; set; } = "ffffffff";
    public string Blend { get; set; } = "normal";
}

/// <summary>Slot → attachment name → drawing part. Missing entries fall back to the default skin.</summary>
public sealed record SkeletonSkin2D
{
    public string Id { get; set; } = "default";
    public string? Name { get; set; }
    public Dictionary<string, Dictionary<string, string>> Attachments { get; set; } = [];
}

public sealed record SlotAttachmentKey2D
{
    public float Time { get; set; }
    public string? Attachment { get; set; }
}
public sealed record SlotAttachmentTrack2D
{
    public string Slot { get; set; } = "";
    public List<SlotAttachmentKey2D> Keys { get; set; } = [];
}
public sealed record DrawOrderKey2D
{
    public float Time { get; set; }
    public List<string> Slots { get; set; } = [];
}
public sealed record AnimationEvent2D
{
    public string Name { get; set; } = "";
    public float Time { get; set; }
    public int Int { get; set; }
    public float Float { get; set; }
    public string? String { get; set; }
}

/// <summary>Cubic curve with normalized time controls. Value controls may overshoot, including between equal endpoints.</summary>
public sealed record KeyCurve2D
{
    public float X1 { get; set; }
    public float Y1 { get; set; }
    public float X2 { get; set; } = 1;
    public float Y2 { get; set; } = 1;
    /// <summary>When true, Y1/Y2 are channel values; otherwise they are fractions of the endpoint difference.</summary>
    public bool Absolute { get; set; }

    public void Validate()
    {
        if (!float.IsFinite(X1) || !float.IsFinite(X2) || X1 < 0 || X1 > 1 || X2 < 0 || X2 > 1 ||
            !float.IsFinite(Y1) || !float.IsFinite(Y2))
        {
            throw new InvalidDataException("Invalid keyframe Bezier curve.");
        }
    }
    public float Apply(float time)
    {
        var t = Parameter(time);
        return Cubic(t, 0, Y1, Y2, 1);
    }
    public float Evaluate(float time, float start, float end)
    {
        if (time <= 0) return start;
        if (time >= 1) return end;
        var (a, b) = Controls(start, end);
        return Cubic(Parameter(time), start, a, b, end);
    }
    public (float First, float Second) Controls(float start, float end) => Absolute ? (Y1, Y2)
        : (start + Y1 * (end - start), start + Y2 * (end - start));

    /// <summary>The same curve restricted to a time interval, with time controls normalized to the new segment.</summary>
    public KeyCurve2D Slice(float from, float to, float start, float end)
    {
        if (!(from >= 0 && to <= 1 && to > from)) throw new ArgumentOutOfRangeException(nameof(to));
        var (first, second) = Controls(start, end);
        var points = new[] { new Vector2(0, start), new Vector2(X1, first), new Vector2(X2, second), new Vector2(1, end) };
        static (Vector2[] Left, Vector2[] Right) Split(Vector2[] p, float t)
        {
            var a = Vector2.Lerp(p[0], p[1], t); var b = Vector2.Lerp(p[1], p[2], t); var c = Vector2.Lerp(p[2], p[3], t);
            var d = Vector2.Lerp(a, b, t); var e = Vector2.Lerp(b, c, t); var f = Vector2.Lerp(d, e, t);
            return ([p[0], a, d, f], [f, e, c, p[3]]);
        }
        var t0 = Parameter(from); var t1 = Parameter(to);
        var left = Split(points, t1).Left;
        var segment = from == 0 ? left : Split(left, t0 / t1).Right;
        return new()
        {
            X1 = Math.Clamp((segment[1].X - from) / (to - from), 0, 1),
            Y1 = segment[1].Y,
            X2 = Math.Clamp((segment[2].X - from) / (to - from), 0, 1),
            Y2 = segment[2].Y,
            Absolute = true
        };
    }

    private float Parameter(float time)
    {
        if (time <= 0) return 0;
        if (time >= 1) return 1;
        var low = 0f; var high = 1f;
        for (var i = 0; i < 24; i++) { var mid = (low + high) / 2; if (Cubic(mid, 0, X1, X2, 1) < time) low = mid; else high = mid; }
        return (low + high) / 2;
    }
    private static float Cubic(float t, float start, float a, float b, float end) =>
        (1 - t) * (1 - t) * (1 - t) * start + 3 * (1 - t) * (1 - t) * t * a + 3 * (1 - t) * t * t * b + t * t * t * end;
}

public sealed record EvaluatedSlot2D(SkeletonSlot2D Slot, PuppetPart? Part, Vector4 Color);

internal static class SkeletonAppearance2D
{
    public static void Validate(CharacterModel model)
    {
        if (model.Slots is null || model.Skins is null) throw new InvalidDataException("Slots and skins cannot be null.");
        var slots = new HashSet<string>();
        foreach (var slot in model.Slots)
        {
            if (slot is null) throw new InvalidDataException("Null slot.");
            AuthoredAsset.RequireId(slot.Id, "slot id");
            if (!slots.Add(slot.Id) || !model.Controls.Any(c => c.Id == slot.Bone)) throw new InvalidDataException($"Invalid or duplicate slot '{slot.Id}'.");
            Color(slot.Color);
            if (slot.Blend is not ("normal" or "additive")) throw new InvalidDataException($"Slot '{slot.Id}': unsupported blend '{slot.Blend}'.");
        }
        var skins = new HashSet<string>();
        foreach (var skin in model.Skins)
        {
            if (skin is null || skin.Attachments is null) throw new InvalidDataException("Null skin or attachments.");
            AuthoredAsset.RequireId(skin.Id, "skin id");
            if (!skins.Add(skin.Id)) throw new InvalidDataException($"Duplicate skin '{skin.Id}'.");
            foreach (var (slot, attachments) in skin.Attachments)
            {
                if (!slots.Contains(slot) || attachments is null) throw new InvalidDataException($"Skin '{skin.Id}': unknown slot '{slot}'.");
                foreach (var (name, part) in attachments)
                {
                    if (string.IsNullOrWhiteSpace(name) || !model.Parts.Any(p => p.Id == part && p.A == model.Slots.First(s => s.Id == slot).Bone))
                        throw new InvalidDataException($"Skin '{skin.Id}': invalid attachment '{name}'.");
                }
            }
        }
    }
    public static void Color(string color)
    {
        if (color is null || color.Length != 8 || !uint.TryParse(color, System.Globalization.NumberStyles.HexNumber, null, out _))
            throw new InvalidDataException("Slot colors must be eight RGBA hexadecimal digits.");
    }
    public static void Evaluate(ResolvedModel model, MotionClip clip, float time, string? skinId, EvaluatedPose pose)
    {
        var skin = model.Base.Skins.FirstOrDefault(s => s.Id == (skinId ?? "default"));
        var fallback = model.Base.Skins.FirstOrDefault(s => s.Id == "default");
        if (skinId is not null && skin is null) throw new InvalidDataException($"Unknown skin '{skinId}'.");
        var order = clip.DrawOrder.LastOrDefault(k => k.Time <= time)?.Slots ?? [.. model.Base.Slots.Select(s => s.Id)];
        foreach (var id in order)
        {
            var slot = model.Base.Slots.First(s => s.Id == id);
            var track = clip.Attachments.FirstOrDefault(t => t.Slot == id);
            var key = track?.Keys.LastOrDefault(k => k.Time <= time);
            var name = key is null ? slot.Attachment : key.Attachment;
            string? part = null;
            if (name is not null)
                part = skin?.Attachments.GetValueOrDefault(id)?.GetValueOrDefault(name) ?? fallback?.Attachments.GetValueOrDefault(id)?.GetValueOrDefault(name);
            pose.Slots.Add(new(slot, part is null ? null : model.Parts.First(p => p.Id == part), SlotColorTrack2D.Evaluate(clip, slot, time)));
        }
    }
}
