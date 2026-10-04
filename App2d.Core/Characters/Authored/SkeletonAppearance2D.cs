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

/// <summary>Normalized cubic timing curve. Y control values may overshoot the keyed values.</summary>
public sealed record KeyCurve2D
{
    public float X1 { get; set; }
    public float Y1 { get; set; }
    public float X2 { get; set; } = 1;
    public float Y2 { get; set; } = 1;

    public void Validate()
    {
        if (!float.IsFinite(X1) || !float.IsFinite(X2) || X1 < 0 || X1 > 1 || X2 < 0 || X2 > 1 ||
            !float.IsFinite(Y1) || !float.IsFinite(Y2)) throw new InvalidDataException("Invalid keyframe Bezier curve.");
    }
    public float Apply(float time)
    {
        static float Cubic(float t, float a, float b) => 3 * (1 - t) * (1 - t) * t * a + 3 * (1 - t) * t * t * b + t * t * t;
        var low = 0f; var high = 1f;
        for (var i = 0; i < 24; i++) { var mid = (low + high) / 2; if (Cubic(mid, X1, X2) < time) low = mid; else high = mid; }
        return Cubic((low + high) / 2, Y1, Y2);
    }
}

public sealed record EvaluatedSlot2D(SkeletonSlot2D Slot, PuppetPart? Part);

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
                    if (string.IsNullOrWhiteSpace(name) || !model.Parts.Any(p => p.Id == part && p.A == model.Slots.First(s => s.Id == slot).Bone))
                        throw new InvalidDataException($"Skin '{skin.Id}': invalid attachment '{name}'.");
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
        var order = clip.DrawOrder.LastOrDefault(k => k.Time <= time)?.Slots ?? model.Base.Slots.Select(s => s.Id).ToList();
        foreach (var id in order)
        {
            var slot = model.Base.Slots.First(s => s.Id == id);
            var track = clip.Attachments.FirstOrDefault(t => t.Slot == id);
            var key = track?.Keys.LastOrDefault(k => k.Time <= time);
            var name = key is null ? slot.Attachment : key.Attachment;
            string? part = null;
            if (name is not null)
                part = skin?.Attachments.GetValueOrDefault(id)?.GetValueOrDefault(name) ?? fallback?.Attachments.GetValueOrDefault(id)?.GetValueOrDefault(name);
            pose.Slots.Add(new(slot, part is null ? null : model.Parts.First(p => p.Id == part)));
        }
    }
}
