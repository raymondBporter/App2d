using App2d.Core.Animation;
using App2d.Core.Rendering;
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

public sealed record SlotAttachmentKey2D : IEditableTimedKey
{
    public float Time { get; set; }
    public string? Attachment { get; set; }
}

public sealed record SlotAttachmentTrack2D
{
    public string Slot { get; set; } = "";
    public List<SlotAttachmentKey2D> Keys { get; set; } = [];
}

public sealed record DrawOrderKey2D : IEditableTimedKey
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
