using App2d.Core.Mathematics.Progress;
using App2d.Core.Animation;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>One animated value: a control's translate or rotate channel, or a chain's end-target channel.</summary>
public readonly record struct Channel(string Kind, string Target)
{
    public override string ToString() => $"{Target} {Kind}";
}

/// <summary>
/// Graphics-free clip edits. Pose edits are the inverse of <see cref="PoseEvaluator"/>: a desired world position becomes a
/// delta in the channel's frame and reference units, so the same key reads correctly on every build.
/// </summary>
public static partial class ClipAuthoring
{
    /// <summary>Keys closer than this are the same key.</summary>
    public const float SameTime = KeyframeEditing.TimeTolerance;

    /// <summary>
    /// An empty clip authored against <paramref name="preview"/>'s measures. Travel scales like the model's planted limbs, when
    /// its locomotion-frame chains agree on a scale, so contacts can be added without changing it.
    /// </summary>
    public static MotionClip New(ResolvedModel preview, string id, string name, float duration = 1, bool loop = true)
    {
        var scales = preview.Chains.Values.Where(c => c.Frame == CharacterModel.Locomotion).Select(c => c.Scale).Distinct().ToArray();
        return new()
        {
            Id = id,
            Name = name,
            Model = preview.Base.Id,
            StructureRevision = preview.Base.StructureRevision,
            Duration = duration,
            Loop = loop,
            Reference = preview.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            Travel = new()
            {
                Scale = scales.Length == 1 ? scales[0] : CharacterModel.Unit
            },
        };
    }

    public static MotionClip Duplicate(MotionClip clip, string id, string name)
    {
        var copy = MotionClip.FromJson(clip.ToJson());
        copy.Id = id;
        copy.Name = name;
        return copy;
    }

    /// <summary>
    /// A namespaced control (<c>instance/control</c>, P7a) resolved to the animated instance it lives in and the control's
    /// name inside that asset's rig; null for the wearer's own control.
    /// </summary>
    public static (ResolvedAttachment Attachment, string Target)? Nested(ResolvedModel model, string control)
    {
        if (!control.Contains(Part.Namespace))
        {
            return null;
        }

        var (instance, target) = MotionClip.Split(control);
        var attachment = model.Attachment(instance) ?? throw new InvalidOperationException($"'{model.Id}' has no attachment '{instance}'.");
        if (attachment.Rig is null)
        {
            throw new InvalidOperationException($"Attachment '{instance}' wears the static part '{attachment.Asset.Id}'; it has no animated targets.");
        }

        return (attachment, target);
    }

    /// <summary>The channel a control is posed through: its chain's target for an IK end, none for an IK joint, else its translation. A nested control's channel is namespaced.</summary>
    public static Channel? ChannelFor(ResolvedModel model, string control)
    {
        if (Nested(model, control) is { } nested)
        {
            return ChannelFor(nested.Attachment.Rig!, nested.Target) is { } inner ? inner with { Target = nested.Attachment.Id + Part.Namespace + inner.Target } : null;
        }

        foreach (var chain in model.Chains.Values)
        {
            if (chain.End == control)
            {
                return new(MotionClip.TargetKind, chain.Id);
            }

            if (chain.Joint == control)
            {
                return null;
            }
        }
        // A drawing part is animated through its fit (P7a); its visibility rides along in ChannelsForControl.
        if (!model.Controls.ContainsKey(control) && model.Parts.Any(p => p.Id == control))
        {
            return new(MotionClip.FitKind, control);
        }

        return new(MotionClip.TranslateKind, control);
    }

    public static ClipTrack? Track(MotionClip clip, Channel channel) => clip.Tracks.FirstOrDefault(t => t.Kind == channel.Kind && t.Target == channel.Target);

    public static (Vector3 Value, float Angle) Value(MotionClip clip, Channel channel, float time) =>
        Track(clip, channel) is { } track ? PoseEvaluator.Interpolate(track, time) : (MotionClip.DefaultValue(channel.Kind), 0);

    /// <summary>Writes a key at <paramref name="time"/>, replacing a key already there and keeping its easing.</summary>
    public static void SetKey(MotionClip clip, Channel channel, float time, Vector3 value, float angle = 0)
    {
        var track = Track(clip, channel);
        if (track is null)
        {
            clip.Tracks.Add(track = new()
            {
                Kind = channel.Kind,
                Target = channel.Target
            });
        }

        var rotate = channel.Kind == MotionClip.RotateKind;
        var key = new ClipKey { Time = time, Value = rotate ? Vector3.Zero : value, Angle = rotate ? angle : 0 };
        if (track.Keys.AtTime(time) is { } existing)
        {
            key.Ease = existing.Ease;
            key.Curve = existing.Curve;
            key.CurveY = existing.CurveY;
            key.CurveZ = existing.CurveZ;
            key.Bend = existing.Bend;
        }
        track.Keys.Put(key);
    }

    /// <summary>
    /// Poses <paramref name="control"/> at a world position at <paramref name="time"/>. <paramref name="pose"/> is the clip
    /// sampled at that time without repetition. An active contact moves its planted target instead of a key.
    /// Returns the channel keyed, or null when a contact target moved.
    /// </summary>
    public static Channel? Pose(ResolvedModel model, MotionClip clip, EvaluatedPose pose, float time, string control, Vector3 world)
    {
        if (Nested(model, control) is { } nested)
        {
            // A nested control (P7a): the desired position goes into the instance's space (its placement and layout fit
            // inverted), the inverse runs on the part's rig and nested pose, and the key lands on the namespaced channel.
            if (!pose.Attachments.TryGetValue(nested.Attachment.Id, out var evaluated))
            {
                throw new InvalidOperationException($"Attachment '{nested.Attachment.Id}' was not evaluated in this pose.");
            }

            if (!evaluated.Placement.TryToLocal(world, out var local))
            {
                throw new InvalidOperationException("Cannot pose a nested control through a collapsed instance placement.");
            }

            var fit = evaluated.LayoutFit;
            var inner = Delta(nested.Attachment.Rig!, clip, evaluated.Pose, nested.Target, new(local.X / fit.X, local.Y / fit.Y, local.Z), nested: true);
            var channel = inner.Channel with { Target = nested.Attachment.Id + Part.Namespace + inner.Channel.Target };
            SetKey(clip, channel, time, inner.Value);
            return channel;
        }

        var own = ChannelFor(model, control) ?? throw new InvalidOperationException(
            $"'{control}' is solved by IK; drag the chain's end or flip its bend.");
        if (own.Kind == MotionClip.TargetKind)
        {
            var chain = model.Chains[own.Target];
            if (ActiveContact(clip, chain.Id, time) is { } contact)
            {
                var origin = TravelAt(model, clip, 0);
                var ratio = Ratio(model, clip, chain.Scale);
                var local = world - origin - model.Rest[chain.End];
                contact.Target = new(local.X / ratio, local.Y / ratio, local.Z);
                return null;
            }
        }

        var delta = Delta(model, clip, pose, control, world, nested: false);
        SetKey(clip, delta.Channel, time, delta.Value);
        return delta.Channel;
    }

    /// <summary>
    /// The key that puts a control at a world position in this pose: the inverse of the evaluator. A chain's end becomes a
    /// target delta in the chain's frame; any other control becomes the desired origin in the carrier's local axes, less the
    /// node's setup origin, in reference units, its layer offset less the node's. Uniform scale and reflection of the carrier
    /// divide out. A nested rig (P7a) is keyed in its own units.
    /// </summary>
    private static (Channel Channel, Vector3 Value) Delta(ResolvedModel model, MotionClip clip, EvaluatedPose pose, string control, Vector3 world, bool nested)
    {
        var channel = ChannelFor(model, control) ?? throw new InvalidOperationException(
            $"'{control}' is solved by IK; drag the chain's end or flip its bend.");
        if (channel.Kind == MotionClip.TargetKind)
        {
            var chain = model.Chains[channel.Target];
            var offset = RigConstraintEvaluator.TargetDelta(model, pose, chain, world);
            var scale = nested ? 1 : Ratio(model, clip, Track(clip, channel)?.Scale ?? chain.Scale);
            return (channel, new(offset.X / scale, offset.Y / scale, offset.Z));
        }

        var spec = model.Controls[control];
        var node = model.Nodes[control];
        if (!Carrier(model, pose, node).TryToLocal(world, out var inCarrier))
        {
            throw new InvalidOperationException("Cannot pose a control through a collapsed parent frame.");
        }

        var parentLength = node.Parent is null ? 0 : model.Nodes[node.Parent].Length;
        var setupOrigin = node.Origin(parentLength);
        var units = nested ? 1 : Ratio(model, clip, Track(clip, channel)?.Scale ?? spec.Scale);
        return (channel, new((inCarrier.X - setupOrigin.X) / units, (inCarrier.Y - setupOrigin.Y) / units, inCarrier.Z - node.Layer));
    }

    /// <summary>The placement a node's local setup composes onto in this pose: the evaluator's carrier rule.</summary>
    private static Geometry.LayeredFrame2D Carrier(ResolvedModel model, EvaluatedPose pose, RigNode node)
    {
        if (node.Parent is null)
        {
            return Geometry.LayeredFrame2D.FromMatrix(Matrix3x2.CreateTranslation(pose.Locomotion.X, pose.Locomotion.Y));
        }

        return pose.Placement(node.Parent);
    }

    /// <summary>Adds <paramref name="radians"/> to a control's rotation at a time.</summary>
    public static void Rotate(MotionClip clip, string control, float time, float radians)
    {
        var channel = new Channel(MotionClip.RotateKind, control);
        SetKey(clip, channel, time, default, Value(clip, channel, time).Angle + radians);
    }

    /// <summary>The contact holding a chain at a time under the runtime's half-open rule.</summary>
    public static ClipContact? ActiveContact(MotionClip clip, string chain, float time) =>
        clip.Contacts.FirstOrDefault(c => c.Chain == chain && time >= c.Start && (time < c.Finish || time == clip.Duration && c.Finish == clip.Duration));

    /// <summary>Plants a chain's end where it is now, from <paramref name="time"/> for up to <paramref name="length"/> seconds, stopping short of the next contact.</summary>
    public static ClipContact Plant(ResolvedModel model, MotionClip clip, EvaluatedPose pose, string chain, float time, float length = .3f)
    {
        var spec = model.Chains[chain];
        if (ActiveContact(clip, chain, time) is not null)
        {
            throw new InvalidOperationException($"'{chain}' is already planted at {time:F3}s.");
        }

        if (spec.Frame != CharacterModel.Locomotion)
        {
            throw new InvalidOperationException($"'{chain}' is keyed in '{spec.Frame}'; only chains in the locomotion frame can plant.");
        }

        if (spec.Scale != clip.Travel.Scale)
        {
            throw new InvalidOperationException($"'{chain}' scales with '{spec.Scale}' but travel scales with '{clip.Travel.Scale}'; contacts need them to match.");
        }

        var next = clip.Contacts.Where(c => c.Chain == chain && c.Start > time).Select(c => c.Start).DefaultIfEmpty(clip.Duration).Min();
        var finish = MathF.Min(next, MathF.Min(clip.Duration, time + length));
        if (finish - time < SameTime)
        {
            throw new InvalidOperationException($"No room to plant '{chain}' at {time:F3}s.");
        }

        var ratio = Ratio(model, clip, spec.Scale);
        var local = pose.World(spec.End) - TravelAt(model, clip, 0) - model.Rest[spec.End];
        var contact = new ClipContact { Chain = chain, Start = time, Finish = finish, Target = new(local.X / ratio, local.Y / ratio, local.Z) };
        clip.Contacts.Add(contact);
        clip.Contacts.Sort((a, b) => (a.Chain, a.Start).CompareTo((b.Chain, b.Start)));
        return contact;
    }

    /// <summary>Every time at which a selected channel has a key; null selects motion, travel, and appearance.</summary>
    public static IReadOnlyList<float> KeyTimes(MotionClip clip, IReadOnlyCollection<Channel>? channels = null)
    {
        var distinct = new List<float>();
        foreach (var time in Keys(clip, channels).Select(k => k.Time).Order())
        {
            if (distinct.Count == 0 || !KeyframeEditing.SameTime(time, distinct[^1]))
            {
                distinct.Add(time);
            }
        }

        return distinct;
    }

    /// <summary>Pins the current pose at <paramref name="time"/>: every existing channel, and travel, gets a key holding its present value.</summary>
    public static void KeyPose(MotionClip clip, float time)
    {
        foreach (var track in clip.Tracks)
        {
            var (value, angle) = PoseEvaluator.Interpolate(track, time);
            SetKey(clip, new(track.Kind, track.Target), time, value, angle);
        }
        if (clip.Travel.Keys.Count > 0)
        {
            var (value, _) = PoseEvaluator.Interpolate(clip.Travel.Keys, time);
            clip.Travel.Keys.Put(new ClipKey { Time = time, Value = value with { Z = 0 } });
        }
    }

    /// <summary>Moves (or copies) keys at one time to another. Null channels selects motion, travel, and appearance.</summary>
    public static void MoveKeys(MotionClip clip, float from, float to, IReadOnlyCollection<Channel>? channels = null, bool copy = false)
    {
        to = Math.Clamp(to, 0, clip.Duration);
        if (KeyframeEditing.SameTime(from, to))
        {
            return;
        }

        foreach (var track in Tracks(clip, channels))
        {
            track.Keys.Move(from, to, copy, static (key, at) => key with { Time = at });
        }

        if (channels is null)
        {
            clip.Travel.Keys.Move(from, to, copy, static (key, at) => key with { Time = at });
            foreach (var track in clip.Plays)
            {
                track.Keys.Move(from, to, copy, static (key, at) => key with { Time = at });
            }
            foreach (var track in clip.Attachments)
            {
                track.Keys.Move(from, to, copy, static (key, at) => key with { Time = at });
            }

            foreach (var track in clip.Colors)
            {
                track.Keys.Move(from, to, copy, static (key, at) => key with { Time = at });
            }

            clip.DrawOrder.Move(from, to, copy, static (key, at) => key with { Time = at, Slots = [.. key.Slots] });
        }
    }

    public static void DeleteKeys(MotionClip clip, float time, IReadOnlyCollection<Channel>? channels = null)
    {
        foreach (var track in Tracks(clip, channels))
        {
            track.Keys.DeleteAtTime(time);
        }

        if (channels is null)
        {
            clip.Travel.Keys.DeleteAtTime(time);
            foreach (var track in clip.Plays)
            {
                track.Keys.DeleteAtTime(time);
            }
            foreach (var track in clip.Attachments)
            {
                track.Keys.DeleteAtTime(time);
            }

            foreach (var track in clip.Colors)
            {
                track.Keys.DeleteAtTime(time);
            }

            clip.DrawOrder.DeleteAtTime(time);
            clip.Plays.RemoveAll(t => t.Keys.Count == 0);
            clip.Attachments.RemoveAll(t => t.Keys.Count == 0);
            clip.Colors.RemoveAll(t => t.Keys.Count == 0);
        }
        clip.Tracks.RemoveAll(t => t.Keys.Count == 0);
    }

    public static void SetEase(MotionClip clip, float time, ProgressMapKind ease, IReadOnlyCollection<Channel>? channels = null)
    {
        MotionClip.ValidateEase(ease, "ease");
        foreach (var key in Tracks(clip, channels).SelectMany(t => t.Keys).Concat(channels is null ? clip.Travel.Keys : []).Where(k => MathF.Abs(k.Time - time) < SameTime))
        {
            key.Ease = ease;
            key.Curve = key.CurveY = key.CurveZ = null;
        }
        if (channels is null)
        {
            foreach (var key in clip.Colors.SelectMany(t => t.Keys).Where(k => MathF.Abs(k.Time - time) < SameTime))
            {
                key.Ease = ease;
                key.CurveR = key.CurveG = key.CurveB = key.CurveA = null;
            }
        }
    }

    public static void SetCurve(MotionClip clip, Channel channel, float time, int component, KeyCurve2D? curve)
    {
        ArgGuard.InRange(component, 0, 2);

        curve?.Validate();
        var key = Track(clip, channel)?.Keys.AtTime(time)
            ?? throw new InvalidOperationException("Add a key at the playhead before editing its curve.");
        var copy = curve is null ? null : curve with
        {
        };
        if (copy is not null && key.Ease == ProgressMapKind.Step)
        {
            key.Ease = ProgressMapKind.Linear;
        }

        if (component == 0)
        {
            key.Curve = copy;
        }
        else if (component == 1)
        {
            key.CurveY = copy;
        }
        else
        {
            key.CurveZ = copy;
        }
    }

    /// <summary>Changes the duration, scaling every key, contact, marker and selection time with it.</summary>
    public static void Retime(MotionClip clip, float duration)
    {
        new Limit(.05f, 60).Check(duration, "duration");
        var ratio = duration / clip.Duration;
        float Scale(float time) => MathF.Min(duration, time * ratio);
        foreach (var key in Keys(clip, null))
        {
            key.Time = Scale(key.Time);
        }

        foreach (var contact in clip.Contacts)
        {
            contact.Start = Scale(contact.Start);
            contact.Finish = Scale(contact.Finish);
        }
        foreach (var marker in clip.Markers)
        {
            marker.Time = Scale(marker.Time);
        }

        foreach (var key in clip.Events)
        {
            key.Time = Scale(key.Time);
        }

        clip.Duration = duration;
    }

    /// <summary>
    /// A new clip holding [<paramref name="from"/>, <paramref name="to"/>] of <paramref name="clip"/>, shifted to start at
    /// zero: keys inside, a key on each end holding the value there (an existing key keeps its easing and bend; a new one
    /// takes the easing of the segment it cuts and the bend in force), contacts clipped to the range, markers inside and the
    /// expression showing at the start. Cut on key times to keep the motion exactly as authored.
    /// </summary>
    public static MotionClip Excerpt(MotionClip clip, float from, float to, string id, string name, bool loop = false)
    {
        if (from < -SameTime || to > clip.Duration + SameTime || to - from < .05f)
        {
            ArgGuard.ThrowOutOfRange(to, $"'{clip.Id}': excerpt {from:F3}..{to:F3} is outside 0..{clip.Duration:F3} or too short.");
        }

        var copy = Duplicate(clip, id, name);
        copy.Loop = loop;
        copy.Duration = to - from;
        KeyCurve2D? Segment(KeyCurve2D? curve, ProgressMapKind ease, float startTime, float endTime, float a, float b, float start, float end)
        {
            if (ease == ProgressMapKind.Step)
            {
                return null;
            }

            if (startTime == a && endTime == b)
            {
                return curve;
            }

            if (curve is null && ease is not (ProgressMapKind.Linear or ProgressMapKind.Smooth))
                throw new InvalidOperationException($"Cannot cut through a segment using {ease}. Cut at its key times or replace its easing with an editable curve first.");
            curve ??= ease == ProgressMapKind.Smooth ? KeyCurve2D.FromEase(ease) : null;
            return curve?.Slice(Math.Clamp((a - startTime) / (endTime - startTime), 0, 1), Math.Clamp((b - startTime) / (endTime - startTime), 0, 1), start, end);
        }
        // Both numeric timelines use the same boundary selection, source segments, and time shift.
        // Callers supply their sampled values and component-curve mappings.
        List<T> CutKeys<T>(List<T> keys, float startTime, Func<T?, float, T> boundary, Action<T, T?, T?, float> curves)
            where T : class, IEditableTimedKey
        {
            if (keys.Count == 0 || startTime > to) return [];
            var cut = new List<T> { boundary(keys.FirstOrDefault(k => k.Time == startTime), startTime) };
            cut.AddRange(keys.Where(k => k.Time > startTime && k.Time < to)
                .Select(k => boundary(k, k.Time)));
            if (to > startTime) cut.Add(boundary(keys.FirstOrDefault(k => k.Time == to), to));
            for (var i = 0; i + 1 < cut.Count; i++)
            {
                var key = cut[i];
                var index = keys.FindLastIndex(k => k.Time <= key.Time);
                curves(key, index < 0 ? null : keys[index], index + 1 >= keys.Count ? null : keys[index + 1], cut[i + 1].Time);
            }
            foreach (var key in cut) key.Time = Math.Clamp(key.Time - from, 0, copy.Duration);
            return cut;
        }
        List<ClipKey> CutMotion(List<ClipKey> keys, ClipTrack? track = null)
        {
            var rotate = track?.Kind == MotionClip.RotateKind;

            ClipKey End(ClipKey? existing, float time)
            {
                if (existing is not null) return existing with { Time = time };
                var (value, angle) = track is null ? PoseEvaluator.Interpolate(keys, time) : PoseEvaluator.Interpolate(track, time);
                var before = keys.LastOrDefault(k => k.Time < time);
                return new()
                {
                    Time = time,
                    Value = value,
                    Angle = angle,
                    Ease = before?.Ease ?? ProgressMapKind.Linear
                };
            }
            void Curves(ClipKey key, ClipKey? start, ClipKey? end, float until)
            {
                if (start is null || end is null || track?.Kind == MotionClip.ShowKind)
                {
                    key.Ease = ProgressMapKind.Step;
                    key.Curve = key.CurveY = key.CurveZ = null;
                    return;
                }
                key.Curve = Segment(start.Curve, start.Ease, start.Time, end.Time, key.Time, until, rotate ? start.Angle : start.Value.X, rotate ? end.Angle : end.Value.X);
                key.CurveY = rotate ? null : Segment(start.CurveY ?? (start.Curve?.Absolute == true ? null : start.Curve), start.Ease, start.Time, end.Time, key.Time, until, start.Value.Y, end.Value.Y);
                key.CurveZ = rotate ? null : Segment(start.CurveZ ?? (start.Curve?.Absolute == true ? null : start.Curve), start.Ease, start.Time, end.Time, key.Time, until, start.Value.Z, end.Value.Z);
            }
            var cut = CutKeys(keys, from, End, Curves);
            if (cut.Count > 0) cut[0].Bend ??= keys.LastOrDefault(k => k.Time <= from && k.Bend is not null)?.Bend;
            return cut;
        }
        foreach (var track in copy.Tracks)
        {
            track.Keys = CutMotion(track.Keys, track);
        }

        copy.Travel.Keys = CutMotion(copy.Travel.Keys);
        copy.Contacts = [.. copy.Contacts.Where(c => c.Finish > from + SameTime && c.Start < to - SameTime)
            .Select(c => new ClipContact { Chain = c.Chain, Start = MathF.Max(0, c.Start - from), Finish = MathF.Min(copy.Duration, c.Finish - from), Target = c.Target })];
        copy.Markers = [.. copy.Markers.Where(m => m.Time >= from - SameTime && m.Time <= to + SameTime).Select(m => new ClipMarker { Id = m.Id, Time = Math.Clamp(m.Time - from, 0, copy.Duration) })];
        foreach (var play in copy.Plays)
        {
            var showing = play.Keys.LastOrDefault(k => k.Time <= from + SameTime);
            play.Keys = [.. (showing is null ? [] : new[] { new ClipPlayKey { Time = 0, Expression = showing.Expression } })
                .Concat(play.Keys.Where(k => k.Time > from + SameTime && k.Time < to - SameTime).Select(k => new ClipPlayKey { Time = k.Time - from, Expression = k.Expression }))];
        }
        copy.Plays.RemoveAll(p => p.Keys.Count == 0);
        foreach (var track in copy.Attachments)
        {
            var showing = track.Keys.LastOrDefault(k => k.Time <= from);
            track.Keys = [.. (showing is null ? [] : new[] { showing with { Time = 0 } })
                .Concat(track.Keys.Where(k => k.Time > from && k.Time <= to).Select(k => k with { Time = k.Time - from }))];
        }
        copy.Attachments.RemoveAll(t => t.Keys.Count == 0);
        var order = copy.DrawOrder.LastOrDefault(k => k.Time <= from);
        copy.DrawOrder = [.. (order is null ? [] : new[] { order with { Time = 0, Slots = [.. order.Slots] } })
            .Concat(copy.DrawOrder.Where(k => k.Time > from && k.Time <= to).Select(k => k with { Time = k.Time - from, Slots = [.. k.Slots] }))];
        copy.Events = [.. copy.Events.Where(k => k.Time >= from && k.Time <= to).Select(k => k with { Time = k.Time - from })];
        foreach (var track in copy.Colors)
        {
            var keys = track.Keys;
            var startTime = track.SetupBeforeFirst && keys.Count > 0 ? MathF.Max(from, keys[0].Time) : from;
            SlotColorKey2D End(SlotColorKey2D? existing, float at)
            {
                if (existing is not null) return existing with { Time = at };
                var value = Vector4.Clamp(SlotColorTrack2D.Sample(keys, at), Vector4.Zero, Vector4.One);
                return new()
                {
                    Time = at,
                    Value = value,
                    Ease = keys.LastOrDefault(k => k.Time < at)?.Ease ?? ProgressMapKind.Linear
                };
            }
            void Curves(SlotColorKey2D key, SlotColorKey2D? start, SlotColorKey2D? end, float until)
            {
                if (start is null || end is null)
                {
                    key.Ease = ProgressMapKind.Step;
                    key.CurveR = key.CurveG = key.CurveB = key.CurveA = null;
                    return;
                }
                key.CurveR = Segment(start.CurveR, start.Ease, start.Time, end.Time, key.Time, until, start.Value.X, end.Value.X);
                key.CurveG = Segment(start.CurveG, start.Ease, start.Time, end.Time, key.Time, until, start.Value.Y, end.Value.Y);
                key.CurveB = Segment(start.CurveB, start.Ease, start.Time, end.Time, key.Time, until, start.Value.Z, end.Value.Z);
                key.CurveA = Segment(start.CurveA, start.Ease, start.Time, end.Time, key.Time, until, start.Value.W, end.Value.W);
            }
            track.Keys = CutKeys(keys, startTime, End, Curves);
        }
        copy.Colors.RemoveAll(t => t.Keys.Count == 0);
        return copy;
    }

    public static void SetMarker(MotionClip clip, string id, float time)
    {
        AuthoredAsset.RequireId(id, "marker id");
        var marker = clip.Markers.FirstOrDefault(m => m.Id == id);
        if (marker is null)
        {
            clip.Markers.Add(marker = new()
            {
                Id = id
            });
        }

        marker.Time = Math.Clamp(time, 0, clip.Duration);
        clip.Markers.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>Holds <paramref name="expression"/> on an animated part instance from <paramref name="time"/> (P7a); null removes the key there. The asset's bindings are checked when the clip validates against the model.</summary>
    public static void SetPlay(MotionClip clip, string instance, float time, string? expression)
    {
        var track = clip.Plays.FirstOrDefault(p => p.Instance == instance);
        if (expression is null)
        {
            track?.Keys.DeleteAtTime(time);
            clip.Plays.RemoveAll(p => p.Keys.Count == 0);
            return;
        }

        AuthoredAsset.RequireId(expression, "expression");
        if (track is null)
        {
            clip.Plays.Add(track = new()
            {
                Instance = instance
            });
        }

        track.Keys.Put(new ClipPlayKey { Time = time, Expression = expression });
    }

    private static IEnumerable<ClipTrack> Tracks(MotionClip clip, IReadOnlyCollection<Channel>? channels) =>
        channels is null ? clip.Tracks : clip.Tracks.Where(t => channels.Contains(new(t.Kind, t.Target)));

    private static IEnumerable<IEditableTimedKey> Keys(MotionClip clip, IReadOnlyCollection<Channel>? channels)
    {
        IEnumerable<IEditableTimedKey> keys = Tracks(clip, channels).SelectMany(t => t.Keys);
        return channels is not null ? keys : keys.Concat(clip.Travel.Keys)
            .Concat(clip.Plays.SelectMany(t => t.Keys)).Concat(clip.Attachments.SelectMany(t => t.Keys))
            .Concat(clip.Colors.SelectMany(t => t.Keys)).Concat(clip.DrawOrder);
    }

    /// <summary>The locomotion frame's origin at a clip time, as the evaluator places it for the first cycle.</summary>
    private static Vector3 TravelAt(ResolvedModel model, MotionClip clip, float time)
    {
        var value = PoseEvaluator.Interpolate(clip.Travel.Keys, time).Value * Ratio(model, clip, clip.Travel.Scale);
        return new(value.X, value.Y, 0);
    }

    /// <summary>Variant measure over reference measure. A measure the clip has no reference for is recorded now, against this preview.</summary>
    private static float Ratio(ResolvedModel model, MotionClip clip, string scale)
    {
        if (scale == CharacterModel.Unit)
        {
            return 1;
        }

        if (!clip.Reference.TryGetValue(scale, out var reference))
        {
            clip.Reference[scale] = reference = model.Measure(scale);
        }

        return model.Measure(scale) / reference;
    }
}
