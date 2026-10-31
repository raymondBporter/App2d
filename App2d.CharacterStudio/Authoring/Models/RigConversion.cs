using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.CharacterStudio.Authoring;

/// <summary>
/// Explicit conversions between rig representations (character-system migration, P3b). A conversion is a deliberate
/// authoring act on a model in memory; the caller decides whether to save the result. Each call returns a report of
/// what changed, so a bundle's conversion can be recorded.
/// </summary>
public static class RigConversion
{
    /// <summary>
    /// Converts a parent-to-child run of point controls into canonical bone nodes: the first becomes a bone whose local
    /// setup is its rest offset from its parent, pointing at and as long as the distance to the next; every later control
    /// is bound to its parent's tip with its own relative direction and length; the last keeps its length (usually 0, an
    /// ordinary end control). Every derived rest position is unchanged, and so is every position-only constraint on the
    /// run, because the point solver reads rest distances the links reproduce by construction. The converted frames point
    /// along their bones, which is the one observable change: a socket framed by a converted control, or a clip rotating
    /// one, now measures from the bone's direction (its rest angle) instead of the character axes.
    /// </summary>
    public static IReadOnlyList<string> ToBoneChain(CharacterModel model, IReadOnlyList<string> controls) => ToBoneChain(model, controls, validate: true);

    private static List<string> ToBoneChain(CharacterModel model, IReadOnlyList<string> controls, bool validate)
    {
        if (controls.Count < 2)
        {
            throw new InvalidOperationException("A bone chain needs at least two controls.");
        }

        var byId = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var run = new List<ModelControl>();
        foreach (var id in controls)
        {
            if (!byId.TryGetValue(id, out var control))
            {
                throw new InvalidOperationException($"Model '{model.Id}' has no control '{id}'.");
            }

            if (control.Node != RigNodeKinds.Point)
            {
                throw new InvalidOperationException($"'{id}' is a {control.Node} node; only point controls convert.");
            }

            if (control.RestAngle != 0)
            {
                throw new InvalidOperationException($"'{id}' has a rest angle; a bone's direction is its own, so clear the rest angle first.");
            }

            if (run.Count > 0 && control.Parent != run[^1].Id)
            {
                throw new InvalidOperationException($"'{id}' is not a child of '{run[^1].Id}'; convert a parent-to-child run.");
            }

            run.Add(control);
        }
        var first = run[0];
        var nodes = RigGraph.Compile(model);
        var report = new List<string>();
        for (var i = 0; i < run.Count; i++)
        {
            var control = run[i];
            var node = nodes[control.Id];
            var parent = control.Parent is null ? null : nodes[control.Parent];
            var next = i + 1 < run.Count ? nodes[run[i + 1].Id] : null;
            var toNext = next is null ? Vector2.Zero : new Vector2(next.Rest.X - node.Rest.X, next.Rest.Y - node.Rest.Y);
            var direction = next is null ? node.RestAngle : MathF.Atan2(toNext.Y, toNext.X);
            var parentAngle = parent?.RestAngle ?? 0;
            var parentHand = parent?.Handedness ?? 1;
            var scale = parent is null ? 1 : Vector2.TransformNormal(Vector2.UnitX, parent.RestFrame).Length();
            var setup = new RigSetup
            {
                Rotation = i == 0 ? parentHand * (direction - parentAngle) : parentHand * (direction - nodes[run[i - 1].Id].RestAngle - (nodes[run[i - 1].Id].Handedness * 0)),
                Layer = node.Layer,
            };
            if (i == 0)
            {
                setup.X = node.Setup.X;
                setup.Y = node.Setup.Y;
                control.Link = null;
            }
            else
            {
                // The previous control's frame now points along the run, so this one's direction is relative to that.
                var previousDirection = MathF.Atan2(node.Rest.Y - nodes[run[i - 1].Id].Rest.Y, node.Rest.X - nodes[run[i - 1].Id].Rest.X);
                setup.Rotation = parentHand * (direction - previousDirection);
                control.Link = new RigLink();
            }
            var length = next is null ? control.Length : toNext.Length() / scale;
            control.Node = RigNodeKinds.Bone;
            control.Local = setup;
            control.Length = length;
            control.Rest = default;
            control.RestAngle = 0;
            report.Add(i == 0
                ? $"{control.Id}: bone, local ({setup.X:0.####}, {setup.Y:0.####}) rotation {setup.Rotation:0.####} layer {setup.Layer:0.####}, length {length:0.####}"
                : $"{control.Id}: bone bound to the tip of {control.Parent}, rotation {setup.Rotation:0.####} layer {setup.Layer:0.####}, length {length:0.####}");
            // Later controls in the run measure from this one's new frame.
            nodes = RigGraph.Compile(model);
            if (Vector2.Distance(new(nodes[control.Id].Rest.X, nodes[control.Id].Rest.Y), new(node.Rest.X, node.Rest.Y)) > 1e-4f || MathF.Abs(nodes[control.Id].Rest.Z - node.Rest.Z) > 1e-4f)
            {
                throw new InvalidOperationException($"'{control.Id}' did not reproduce its rest after conversion; the model is left unsaved.");
            }
        }
        // A parameter that scaled a child's offset from its parent now scales the parent's length (P4a).
        for (var i = 1; i < run.Count; i++)
        {
            var child = run[i];
            var parent = run[i - 1];
            var x = Binding(model, child.Id, RigProperties.LocalX);
            var y = Binding(model, child.Id, RigProperties.LocalY);
            if (x is null && y is null)
            {
                continue;
            }

            if ((x is null) != (y is null) && PlainScale((x ?? y)!.Value.Binding))
            {
                // One component of the offset was scaled (P10a bound the quadruped legs' y only); a bone length scales the
                // whole segment and keeps its direction. Declared here, reported, and exact at the default value.
                var (parameter, bound) = (x ?? y)!.Value;
                var missing = x is null ? RigProperties.LocalX : RigProperties.LocalY;
                var added = bound with { Target = PropertyRef.OfControl(child.Id, missing) };
                parameter.Bindings.Insert(parameter.Bindings.IndexOf(bound) + 1, added);
                report.Add($"{child.Id}: parameter '{parameter.Id}' scaled only its {bound.Target.Property}; the whole offset now scales as a length of '{parent.Id}' (direction kept; identical at the default)");
                x = (parameter, x is null ? added : bound);
                y = (parameter, y is null ? added : bound);
            }

            if (x is null || y is null || x.Value.Parameter != y.Value.Parameter || !PlainScale(x.Value.Binding) || !PlainScale(y.Value.Binding))
            {
                throw new InvalidOperationException($"'{child.Id}': its local translation bindings cannot become a length of '{parent.Id}'; bind local.x and local.y to one parameter with a plain scale, or remove them first.");
            }

            x.Value.Binding.Target = PropertyRef.OfControl(parent.Id, RigProperties.Length);
            x.Value.Parameter.Bindings.Remove(y.Value.Binding);
            report.Add($"{parent.Id}: length bound to parameter '{x.Value.Parameter.Id}' (was {child.Id}'s local x and y)");
        }
        if (validate)
        {
            model.Validate();
        }

        return report;
    }

    private static (ModelParameter Parameter, ParameterBinding Binding)? Binding(CharacterModel model, string control, string property)
    {
        foreach (var parameter in model.Parameters)
        {
            foreach (var binding in parameter.Bindings)
            {
                if (binding.Target.Control == control && binding.Target.Property == property)
                {
                    return (parameter, binding);
                }
            }
        }

        return null;
    }

    /// <summary>A multiplier of the authored value about zero: the one binding form a length and a local offset share.</summary>
    private static bool PlainScale(ParameterBinding binding) => binding.Kind == BindingKinds.Scale && binding.Reference is null && binding.About == 0;

    /// <summary>One point-solved constraint converted onto the bone solver: the solved root, the pivot that kept the root's authored frame (null when none was needed) and the report.</summary>
    public sealed record ConstraintConversion(string Chain, string Root, string? Pivot, IReadOnlyList<string> Report);

    /// <summary>
    /// Converts a two-bone constraint authored over point controls into one over bone nodes (plan rule 7; P11a, the semantic
    /// conversion of the retired point adapter; since P11c the authoring step a seed or editor takes to make a point run a
    /// constraint). The run becomes bone nodes (<see cref="ToBoneChain"/>) and the constraint turns their frames. What a
    /// position-only solve never turned keeps its meaning explicitly: the end node binds to
    /// the joint's tip with the <see cref="RigLinkOrientations.Character"/> policy, so its artwork, sockets and children keep
    /// the character axes as authored; and when anything reads the root's own frame beyond the constraint (the constraint's
    /// target frame, a socket framed on it, a part or child hanging from it, or a clip keying its rotation), the root stays
    /// as that authored pivot and the solved upper segment becomes a new bone node at its position, so authored turns,
    /// socket frames and target keys mean exactly what they did. Target keys of a constraint framed on a bone with a rest
    /// rotation are re-expressed in that frame's rest axes. The caller verifies the result against the source and saves it.
    /// </summary>
    public static ConstraintConversion ToBoneConstraint(CharacterModel model, string chainId, IReadOnlyList<MotionClip> clips, string? boneId = null)
    {
        var chain = model.IkChains.FirstOrDefault(c => c.Id == chainId) ?? throw new InvalidOperationException($"Model '{model.Id}' has no constraint '{chainId}'.");
        var byId = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var root = byId[chain.Root];
        var joint = byId[chain.Joint];
        var end = byId[chain.End];
        if (new[] { root, joint, end }.Any(c => c.Node == RigNodeKinds.Bone))
        {
            throw new InvalidOperationException($"Constraint '{chainId}' already runs over bone nodes.");
        }

        var report = new List<string>();

        var readers = new List<string>();
        if (chain.Frame == chain.Root)
        {
            readers.Add("its target frame");
        }

        foreach (var other in model.IkChains.Where(c => c != chain && c.Frame == chain.Root))
        {
            readers.Add($"constraint '{other.Id}' framed on it");
        }

        foreach (var socket in model.Sockets)
        {
            if (socket.Frame == chain.Root)
            {
                readers.Add($"socket '{socket.Id}' framed on it");
            }
            else if (socket.Control == chain.Root && socket.Frame is null && socket.Toward is null)
            {
                readers.Add($"socket '{socket.Id}' on it");
            }
        }

        foreach (var part in model.Parts.Where(p => p.Frame == chain.Root || p.A == chain.Root && p.B is null))
        {
            readers.Add($"part '{part.Id}' on it");
        }

        foreach (var child in model.Controls.Where(c => c.Parent == chain.Root && c.Id != chain.Joint))
        {
            readers.Add($"child '{child.Id}'");
        }

        foreach (var clip in clips)
        {
            foreach (var track in clip.Tracks.Where(t => t.Target == chain.Root && t.Kind is MotionClip.RotateKind or MotionClip.BendKind or MotionClip.ScaleKind))
            {
                readers.Add($"clip '{clip.Id}' keys its {track.Kind}");
            }
        }

        string bone;
        if (readers.Count > 0)
        {
            bone = boneId ?? chain.Root + "-bone";
            if (byId.ContainsKey(bone) || model.Parts.Any(p => p.Id == bone) || model.Attachments.Any(a => a.Id == bone) || model.IkChains.Any(c => c.Id == bone))
            {
                throw new InvalidOperationException($"Constraint '{chainId}': the bone id '{bone}' is already taken.");
            }

            var node = new ModelControl { Id = bone, Parent = root.Id, Rest = root.Rest };
            model.Controls.Insert(model.Controls.IndexOf(root) + 1, node);
            joint.Parent = bone;
            foreach (var group in model.Groups.Where(g => g.Targets.Contains(chain.Root)))
            {
                group.Targets.Insert(group.Targets.IndexOf(chain.Root) + 1, bone);
                report.Add($"group '{group.Id}': '{bone}' joins beside '{chain.Root}'");
            }

            report.Add($"'{chain.Root}' keeps its authored frame as the pivot ({string.Join(", ", readers.Distinct())}); the solved upper segment is the new bone node '{bone}' at its position");
            chain.Root = bone;
        }
        else
        {
            bone = chain.Root;
        }

        report.AddRange(ToBoneChain(model, [bone, joint.Id, end.Id], validate: false));
        // The point solver never turned the end: it keeps the character's axes while its origin rides the joint's tip.
        end.Link!.Orientation = RigLinkOrientations.Character;
        end.Local!.Rotation = 0;
        report.Add($"{end.Id}: keeps the character axes (link orientation '{RigLinkOrientations.Character}'), as a position-only solve left it");
        if (chain.Frame != CharacterModel.Locomotion)
        {
            var restAngle = RigGraph.Compile(model)[chain.Frame].RestAngle;
            if (MathF.Abs(restAngle) > 1e-6f)
            {
                // A point constraint keyed its target in the character axes turned by the frame's rotation since rest; a bone
                // constraint keys it in the frame's own axes, so the keys turn back by the frame's rest rotation.
                foreach (var clip in clips)
                {
                    foreach (var track in clip.Tracks.Where(t => t.Kind == MotionClip.TargetKind && t.Target == chain.Id))
                    {
                        if (track.Keys.Any(k => k.Curve is not null || k.CurveY is not null))
                        {
                            throw new InvalidOperationException($"Clip '{clip.Id}': the target keys of '{chain.Id}' carry easing curves that cannot be turned into the frame's rest axes; re-express them first.");
                        }

                        foreach (var key in track.Keys)
                        {
                            key.Value = Rotation2D.ApplyXY(key.Value, -restAngle);
                        }

                        report.Add($"clip '{clip.Id}': {track.Keys.Count} target key(s) of '{chain.Id}' turned by {-restAngle:0.####} into the rest axes of '{chain.Frame}'");
                    }
                }
            }
        }

        // A model mid-conversion still carries constraints over point runs; it validates when the last of them converts.
        var pending = model.IkChains.Where(c => c != chain && byId[c.Root].Node == RigNodeKinds.Point).Select(c => c.Id).ToList();
        if (pending.Count == 0)
        {
            model.Validate();
        }
        else
        {
            report.Add($"validation deferred: {string.Join(", ", pending.Select(id => $"'{id}'"))} still run{(pending.Count == 1 ? "s" : "")} over point controls");
        }

        report.Add($"constraint '{chain.Id}': bone constraint over {bone} > {joint.Id} > {end.Id}, frame {chain.Frame}");
        return new(chain.Id, bone, readers.Count > 0 ? root.Id : null, report);
    }
}
