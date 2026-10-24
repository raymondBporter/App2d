using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Constraints;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Selected control values, transform channels, outgoing curves, and planted contacts.</summary>
internal sealed class AnimationControlInspector(EditorSession session)
{
    public void Draw(AssetDocument<MotionClip> document, Subject primary, string control)
    {
        var clip = document.Asset;
        var model = primary.Model;
        var time = session.Transport.Time;
        Ui.Header("Control " + control);
        var channel = ClipAuthoring.ChannelFor(model, control);
        if (channel is null)
        {
            var chain = model.Chains.Values.First(c => c.Joint == control);
            Ui.Help($"Solved by IK chain '{chain.Id}'. Pose its end '{chain.End}', or flip the bend on the base model.");
            return;
        }
        var track = ClipAuthoring.Track(clip, channel.Value);
        var semantics = ChannelSemantics.Of(ChannelSemantics.OfTrack(channel.Value.Kind, model.Controls.GetValueOrDefault(channel.Value.Target)?.Node));
        ImGui.TextDisabled($"{channel.Value.Kind} channel on '{channel.Value.Target}' ({semantics.Semantics}, {semantics.Units}){(track is null ? ", not keyed yet" : $", {track.Keys.Count} keys")}");
        var (value, _) = ClipAuthoring.Value(clip, channel.Value, time);
        var layered = LayeredPoint2D.From(value);
        if (CharacterUi.LayeredPoint("Delta from rest (reference units)", ref layered, layerIsOffset: true))
        {
            session.Change(document, () => ClipAuthoring.SetKey(document.Asset, channel.Value, time, layered.ToVector3()));
        }

        if (channel.Value.Kind == MotionClip.TranslateKind)
        {
            var boneIk = model.Chains.Values.Any(c => c.Solver == ModelChain.BoneSolver && c.Root == control);
            var limit = model.Nodes[control].Limit;
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

                if (primary.Pose.Limits.TryGetValue(control, out var state))
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

            var affine = model.Controls[control].Node == RigNodeKinds.Affine;
            if (!affine || boneIk)
            {
                // The uniform pose scale channel: one multiplier on the node's similarity (ChannelSemantics.UniformPoseScale).
                var scaleChannel = new Channel(MotionClip.ScaleKind, control);
                var scale = ClipAuthoring.Value(clip, scaleChannel, time).Value.X + 1;
                if (Ui.Drag("Uniform pose scale multiplier", ref scale, .01f, .01f, 100))
                {
                    session.Change(document, () => ClipAuthoring.SetKey(document.Asset, scaleChannel, time, new(scale - 1, scale - 1, 0)));
                }
            }
            else if (affine)
            {
                void Axis(string label, string kind, int axis)
                {
                    var channel = new Channel(kind, control);
                    var delta = ClipAuthoring.Value(clip, channel, time).Value;
                    var value = (axis == 0 ? delta.X : delta.Y) + (kind == MotionClip.ScaleKind ? 1 : 0);
                    if (kind == MotionClip.ShearKind)
                    {
                        value = float.RadiansToDegrees(value);
                    }

                    if (Ui.Drag(label, ref value, .01f, -100, 100))
                    {
                        var keyed = kind == MotionClip.ScaleKind ? value - 1 : float.DegreesToRadians(value);
                        session.Change(document, () => ClipAuthoring.SetComponentKey(document.Asset, channel, time, axis, keyed));
                    }
                }
                Axis("Scale X multiplier", MotionClip.ScaleKind, 0);
                Axis("Scale Y multiplier", MotionClip.ScaleKind, 1);
                Axis("Shear X offset (degrees)", MotionClip.ShearKind, 0);
                Axis("Shear Y offset (degrees)", MotionClip.ShearKind, 1);
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
}
