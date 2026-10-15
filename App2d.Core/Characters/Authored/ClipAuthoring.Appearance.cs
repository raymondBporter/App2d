using System.Numerics;

namespace App2d.Core.Characters.Authored;

public static partial class ClipAuthoring
{
    /// <summary>Keys a held attachment choice, including null to hide the slot.</summary>
    public static void SetAttachmentKey(MotionClip clip, string slot, float time, string? attachment)
    {
        var track = clip.Attachments.FirstOrDefault(t => t.Slot == slot);
        if (track is null) clip.Attachments.Add(track = new() { Slot = slot });
        Put(track.Keys, new() { Time = time, Attachment = attachment }, k => k.Time);
    }

    public static void DeleteAttachmentKey(MotionClip clip, string slot, float time)
    {
        clip.Attachments.FirstOrDefault(t => t.Slot == slot)?.Keys.RemoveAll(k => MathF.Abs(k.Time - time) < SameTime);
        clip.Attachments.RemoveAll(t => t.Keys.Count == 0);
    }

    /// <summary>Moves a slot through the held order, starting from setup order before the first key.</summary>
    public static bool MoveSlot(MotionClip clip, IReadOnlyList<SkeletonSlot2D> slots, string slot, float time, int delta)
    {
        var order = (clip.DrawOrder.LastOrDefault(k => k.Time <= time)?.Slots ?? [.. slots.Select(s => s.Id)]).ToList();
        var index = order.IndexOf(slot);
        var next = (long)index + delta;
        if (index < 0 || next < 0 || next >= order.Count) return false;
        (order[index], order[(int)next]) = (order[(int)next], order[index]);
        Put(clip.DrawOrder, new() { Time = time, Slots = order }, k => k.Time);
        return true;
    }

    public static void DeleteDrawOrderKey(MotionClip clip, float time) =>
        clip.DrawOrder.RemoveAll(k => MathF.Abs(k.Time - time) < SameTime);

    public static void SetColorKey(MotionClip clip, string slot, string kind, float time, Vector4 value)
    {
        var track = clip.Colors.FirstOrDefault(t => t.Slot == slot && t.Kind == kind);
        if (track is null) clip.Colors.Add(track = new() { Slot = slot, Kind = kind });
        var key = new SlotColorKey2D { Time = time, R = value.X, G = value.Y, B = value.Z, A = value.W };
        if (track.Keys.FirstOrDefault(k => MathF.Abs(k.Time - time) < SameTime) is { } previous)
        { key.Ease = previous.Ease; key.CurveR = previous.CurveR; key.CurveG = previous.CurveG; key.CurveB = previous.CurveB; key.CurveA = previous.CurveA; }
        Put(track.Keys, key, k => k.Time);
    }

    public static void DeleteColorKey(MotionClip clip, string slot, string kind, float time)
    {
        clip.Colors.FirstOrDefault(t => t.Slot == slot && t.Kind == kind)?.Keys.RemoveAll(k => MathF.Abs(k.Time - time) < SameTime);
        clip.Colors.RemoveAll(t => t.Keys.Count == 0);
    }

    /// <summary>Changes easing and clears the per-component curves that it replaces.</summary>
    public static void SetColorEase(MotionClip clip, string slot, string kind, float time, string ease)
    {
        EntityVocabulary.Require(ease, ClipEase.All, "ease");
        var key = ColorKey(clip, slot, kind, time);
        key.Ease = ease;
        key.CurveR = key.CurveG = key.CurveB = key.CurveA = null;
    }

    /// <summary>Sets one outgoing tint curve without changing the other components.</summary>
    public static void SetColorCurve(MotionClip clip, string slot, string kind, float time, int component, KeyCurve2D? curve)
    {
        if (component is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(component));
        curve?.Validate();
        var key = ColorKey(clip, slot, kind, time);
        var copy = curve is null ? null : curve with { };
        if (copy is not null && key.Ease == ClipEase.Step) key.Ease = ClipEase.Linear;
        if (component == 0) key.CurveR = copy;
        else if (component == 1) key.CurveG = copy;
        else if (component == 2) key.CurveB = copy;
        else key.CurveA = copy;
    }

    private static SlotColorKey2D ColorKey(MotionClip clip, string slot, string kind, float time) =>
        clip.Colors.FirstOrDefault(t => t.Slot == slot && t.Kind == kind)?.Keys.FirstOrDefault(k => MathF.Abs(k.Time - time) < SameTime)
        ?? throw new InvalidOperationException("Add a tint key at the playhead before editing its curve.");
}
