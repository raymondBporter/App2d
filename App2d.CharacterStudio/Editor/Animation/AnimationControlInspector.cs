using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Constraints;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Selected control values, transform channels, outgoing curves, and planted contacts. A nested part control (P7a) is keyed through its namespaced channels.</summary>
internal sealed class AnimationControlInspector(EditorSession session)
{
    public void Draw(AssetDocument<MotionClip> document, Subject primary, string control)
    {
        var clip = document.Asset;
        var model = primary.Model;
        var time = session.Transport.Time;
        // A nested control lives in an animated instance's rig: its node, limits and pose are the part's; its channels are namespaced.
        var nested = ClipAuthoring.Nested(model, control);
        var rig = nested?.Attachment.Rig ?? model;
        var local = nested?.Target ?? control;
        var pose = nested is null ? primary.Pose : primary.Pose.Attachments[nested.Value.Attachment.Id].Pose;
        Ui.Header((nested is null ? "Control " : $"Part {nested.Value.Attachment.Id}, control ") + local);
        var channel = ClipAuthoring.ChannelFor(model, control);
        if (channel is null)
        {
            var chain = rig.Chains.Values.First(c => c.Joint == local);
            Ui.Help($"Solved by IK chain '{chain.Id}'. Pose its end '{chain.End}', or flip the bend on the base model.");
            return;
        }
        if (nested is not null && channel.Value.Kind == MotionClip.TargetKind)
        {
            Ui.Help($"'{local}' is the end of IK chain '{MotionClip.Split(channel.Value.Target).Target}' inside part '{nested.Value.Attachment.Asset.Id}'; a wearer keys a part's controls and pieces, not its chains. Animate it in the part's own clips.");
            return;
        }
        if (channel.Value.Kind == MotionClip.FitKind)
        {
            DrawPiece(document, model, control, local, nested?.Attachment.Asset.Id);
            return;
        }

        var track = ClipAuthoring.Track(clip, channel.Value);
        var semantics = ChannelSemantics.Of(ChannelSemantics.OfTrack(channel.Value.Kind));
        ImGui.TextDisabled($"{channel.Value.Kind} channel on '{channel.Value.Target}' ({semantics.Semantics}, {semantics.Units}){(track is null ? ", not keyed yet" : $", {track.Keys.Count} keys")}");
        if (nested is not null)
        {
            ImGui.TextDisabled($"  keyed in part '{nested.Value.Attachment.Asset.Id}' units, over the part's own clips on this instance.");
        }
        // Measure provenance (P4c): which declared measure scales this channel's units, the value the clip was authored against and this build's.
        var measure = nested is not null ? CharacterModel.Unit : track?.Scale ?? (channel.Value.Kind == MotionClip.TargetKind ? model.Chains[channel.Value.Target].Scale : model.Controls[channel.Value.Target].Scale);
        if (measure != CharacterModel.Unit && clip.Reference.TryGetValue(measure, out var reference))
        {
            var current = model.Measure(measure);
            ImGui.TextDisabled($"  units scale with measure '{measure}': authored against {reference:0.###}, this build {current:0.###} (x{current / reference:0.###}){(reference != current ? "; the same keys plant and reach at this build's scale" : "")}");
        }
        else if (measure == CharacterModel.Unit && nested is null)
        {
            ImGui.TextDisabled("  units are authored lengths (no measure scaling).");
        }
        var (value, _) = ClipAuthoring.Value(clip, channel.Value, time);
        var layered = LayeredPoint2D.From(value);
        if (CharacterUi.LayeredPoint("Delta from rest (reference units)", ref layered, layerIsOffset: true))
        {
            session.Change(document, () => ClipAuthoring.SetKey(document.Asset, channel.Value, time, layered.ToVector3()));
        }

        if (channel.Value.Kind == MotionClip.TranslateKind)
        {
            var boneIk = rig.Chains.Values.Any(c => c.Root == local);
            var limit = rig.Nodes[local].Limit;
            if (boneIk)
            {
                Ui.Help("Rotation is solved by the IK target." + (limit is null ? "" : " The joint limits hold the solve; an out-of-reach target reports its gap below."));
            }
            else
            {
                // One owner per node per clip: the normalized bend (through the declared limits) or the ordinary angle channel.
                var rotate = new Channel(MotionClip.RotateKind, control);
                var bend = new Channel(MotionClip.BendKind, control);
                var bendTrack = limit is null ? null : ClipAuthoring.Track(clip, bend);
                var rotateTrack = ClipAuthoring.Track(clip, rotate);
                if (limit is not null && rotateTrack is null)
                {
                    var t = ClipAuthoring.Value(clip, bend, time).Value.X;
                    if (Ui.Slider($"Normalized bend (0 = {float.RadiansToDegrees(limit.Min):0.#}°, 1 = {float.RadiansToDegrees(limit.Max):0.#}°)", ref t, 0, 1))
                    {
                        session.Change(document, () => ClipAuthoring.SetKey(document.Asset, bend, time, new(Math.Clamp(t, 0, 1), 0, 0)));
                    }

                    ImGui.TextDisabled($"  normalized-parameter channel through the node's joint limits; {(bendTrack is null ? "not keyed yet: the setup rotation holds" : $"{bendTrack.Keys.Count} keys")}. Changing the limits changes this performance.");
                }

                if (bendTrack is null)
                {
                    var degrees = float.RadiansToDegrees(ClipAuthoring.Value(clip, rotate, time).Angle);
                    if (Ui.Drag("Rotation (degrees, turns children)", ref degrees, .5f, -720, 720))
                    {
                        session.Change(document, () => ClipAuthoring.SetKey(document.Asset, rotate, time, default, float.DegreesToRadians(degrees)));
                    }

                    if (limit is not null)
                    {
                        ImGui.TextDisabled($"  angle channel: authored degrees, held between {float.RadiansToDegrees(limit.Min):0.#}° and {float.RadiansToDegrees(limit.Max):0.#}° of parent-local rotation.");
                    }
                }
                else
                {
                    ImGui.TextDisabled("  the angle channel is owned by the bend in this clip; delete the bend keys to key angles.");
                }

                if (pose.Limits.TryGetValue(local, out var state))
                {
                    if (state.State == ConstraintLimitState1D.Inactive)
                    {
                        ImGui.TextDisabled($"  within limits at {float.RadiansToDegrees(state.Coordinate):0.#}° parent-local.");
                    }
                    else
                    {
                        Ui.Problem($"Held at the {state.State.ToString().ToLowerInvariant()} limit, {float.RadiansToDegrees(MathF.Abs(state.Error)):0.#}° short of the requested rotation.");
                    }
                }
            }

            // The uniform pose scale channel: one multiplier on the node's similarity (ChannelSemantics.UniformPoseScale).
            var scaleChannel = new Channel(MotionClip.ScaleKind, control);
            var scale = ClipAuthoring.Value(clip, scaleChannel, time).Value.X + 1;
            if (Ui.Drag("Uniform pose scale multiplier", ref scale, .01f, .01f, 100))
            {
                session.Change(document, () => ClipAuthoring.SetKey(document.Asset, scaleChannel, time, new(scale - 1, scale - 1, 0)));
            }
        }
        var channels = ClipAuthoring.ChannelsForControl(model, control);
        var key = channels.SelectMany(c => ClipAuthoring.Track(clip, c)?.Keys ?? []).FirstOrDefault(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
        if (key is not null)
        {
            if (Ui.Combo("Ease to next key", key.Ease, ClipEase.All) is { } ease)
            {
                session.Edit(document, () => ClipAuthoring.SetEase(document.Asset, time, ease, channels));
            }

            if (ImGui.SmallButton("Delete this key"))
            {
                session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, time, channels));
            }

            AnimationCurveEditor.DrawChannels(session, document, channels, time);
        }
        else
        {
            Ui.Help("No key at this time; dragging or editing adds one.");
        }

        if (channel.Value.Kind != MotionClip.TargetKind)
        {
            return;
        }

        var chainId = channel.Value.Target;
        var spec = model.Chains[chainId];
        Ui.Header("Contact");
        if (ClipAuthoring.ActiveContact(clip, chainId, time) is { } contact)
        {
            var index = clip.Contacts.IndexOf(contact);
            var range = new Vector2(contact.Start, contact.Finish);
            if (Ui.Drag2("Planted from / until (seconds)", ref range, .002f, 0, clip.Duration))
            {
                session.Change(document, () => ClipAuthoring.SetContactRange(document.Asset, index, range));
            }

            if (ImGui.SmallButton("Remove contact"))
            {
                session.Edit(document, () => document.Asset.Contacts.RemoveAt(index));
            }

            if (primary.Pose.Contacts.FirstOrDefault(c => c.Chain == chainId) is { } result)
            {
                if (result.Residual < .005f)
                {
                    ImGui.TextColored(UiTheme.Accent, "Planted and reached.");
                }
                else
                {
                    Ui.Problem($"Cannot reach the planted target by {result.Residual:F3}.");
                }
            }

            Ui.Help("Dragging the planted end moves its world target for the whole interval.");
        }
        else if (spec.Frame == CharacterModel.Locomotion)
        {
            if (ImGui.Button("Plant here"))
            {
                session.Edit(document, () => ClipAuthoring.Plant(model, document.Asset, PoseEvaluator.Sample(model, document.Asset, time), chainId, time));
            }

            Ui.Help("Holds the end where it is now, in the locomotion frame, while travel carries the body on.");
        }
        else
        {
            Ui.Help($"Keyed in '{spec.Frame}'; only locomotion-frame chains plant.");
        }

        if (primary.Pose.Chains.FirstOrDefault(c => c.Chain == chainId) is { Reached: false } reach)
        {
            Ui.Problem($"Out of reach by {reach.Residual:F3}. Limb lengths are kept.");
        }
    }

    /// <summary>A drawing part's channels (P7a): its explicit artwork fit (multipliers of its leaf scale) and its held visibility.</summary>
    private void DrawPiece(AssetDocument<MotionClip> document, ResolvedModel model, string control, string local, string? asset)
    {
        var clip = document.Asset;
        var time = session.Transport.Time;
        var fit = new Channel(MotionClip.FitKind, control);
        var show = new Channel(MotionClip.ShowKind, control);
        var fitTrack = ClipAuthoring.Track(clip, fit);
        var showTrack = ClipAuthoring.Track(clip, show);
        ImGui.TextDisabled($"piece '{local}'{(asset is null ? "" : $" of part '{asset}'")}: artwork-fit channel ({ChannelSemantics.Of(ChannelSemantics.ArtworkFit).Units}){(fitTrack is null ? ", not keyed yet" : $", {fitTrack.Keys.Count} keys")}; enabled-state channel (held){(showTrack is null ? ", not keyed yet" : $", {showTrack.Keys.Count} keys")}");
        var value = ClipAuthoring.Value(clip, fit, time).Value;
        var multipliers = new Vector2(value.X + 1, value.Y + 1);
        if (Ui.Drag2("Fit X / Y multipliers (artwork only)", ref multipliers, .01f, -100, 100))
        {
            session.Change(document, () => ClipAuthoring.SetKey(document.Asset, fit, time, new(multipliers.X - 1, multipliers.Y - 1, 0)));
        }

        var shownNow = showTrack is null || showTrack.Keys.Count == 0 || (showTrack.Keys.LastOrDefault(k => k.Time <= time) ?? showTrack.Keys[0]).Value.X != 0;
        var shown = shownNow;
        if (ImGui.Checkbox("Shown (held from this key)", ref shown))
        {
            session.Edit(document, () => ClipAuthoring.SetKey(document.Asset, show, time, new(shown ? 1 : 0, 0, 0)));
        }

        Ui.Help("A fit squashes or stretches this piece's artwork without touching the rig: a pupil closing, a cheek puffing. Nothing inherits it.");
        var channels = ClipAuthoring.ChannelsForControl(model, control);
        var key = channels.SelectMany(c => ClipAuthoring.Track(clip, c)?.Keys ?? []).FirstOrDefault(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
        if (key is null)
        {
            Ui.Help("No key at this time; editing adds one.");
            return;
        }

        if (Ui.Combo("Ease to next key", key.Ease, ClipEase.All) is { } ease)
        {
            session.Edit(document, () => ClipAuthoring.SetEase(document.Asset, time, ease, channels));
        }

        if (ImGui.SmallButton("Delete this key"))
        {
            session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, time, channels));
        }

        AnimationCurveEditor.DrawChannels(session, document, [fit], time);
    }

    /// <summary>
    /// An animated part instance's own section in the clip (P7a): the expression the clip selects at this time (the asset's
    /// bound words, or none to hide), its layers and clocks, and what the instance shows in the preview.
    /// </summary>
    public void DrawPart(AssetDocument<MotionClip> document, Subject primary, string instance)
    {
        if (primary.Model.Attachment(instance) is not { IsAnimated: true } attachment)
        {
            return;
        }

        var asset = attachment.Asset;
        var clip = document.Asset;
        var time = session.Transport.Time;
        Ui.Header($"Part {instance} ({asset.Name})");
        var track = clip.Plays.FirstOrDefault(p => p.Instance == instance);
        var here = track?.Keys.FirstOrDefault(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
        var selected = clip.PlayAt(instance, time);
        var words = asset.Expressions.Keys.Append(Part.None).ToArray();
        var state = here is not null ? "keyed here" : selected is null ? "instance default" : "held from earlier key";
        if (Ui.Combo($"Expression ({state})", here?.Expression ?? selected?.Expression ?? attachment.DefaultExpression ?? Part.None, words) is { } expression)
        {
            session.Edit(document, () => ClipAuthoring.SetPlay(document.Asset, instance, time, expression));
        }

        if (here is not null && ImGui.SmallButton("Remove expression key"))
        {
            session.Edit(document, () => ClipAuthoring.SetPlay(document.Asset, instance, time, null));
        }

        if (primary.Pose.Attachments.TryGetValue(instance, out var evaluated))
        {
            ImGui.TextDisabled($"  shows '{evaluated.Expression ?? "nothing"}'{(evaluated.Hidden ? ", hidden" : "")}; a gameplay expression (viewport toolbar) wins over this channel.");
        }

        foreach (var layer in asset.Layers)
        {
            ImGui.TextDisabled($"  layer '{layer.Id}': {(layer.Mask is null ? "everything" : "mask '" + layer.Mask + "'")}, clock {layer.Clock}{(layer.Clock == PartClocks.Mapped ? $" (x{layer.Rate:0.##} + {layer.Offset:0.##} s)" : "")}, plays {(layer.Clip ?? "nothing")} by default");
        }

        Ui.Help($"The asset binds {string.Join(", ", asset.Expressions.Keys)}. Exposed targets ({string.Join(", ", asset.Exposed)}) are keyed through the Parts rows of the outline.");
    }
}
