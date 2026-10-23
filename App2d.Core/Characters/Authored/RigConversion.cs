using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

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
    public static IReadOnlyList<string> ToBoneChain(CharacterModel model, IReadOnlyList<string> controls)
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
        if (first.Parent is not null && byId[first.Parent].Node == RigNodeKinds.Affine)
        {
            throw new InvalidOperationException($"'{first.Id}' hangs from the affine node '{first.Parent}'; the legacy affine adapter cannot carry a canonical bone.");
        }

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

            if (x is null || y is null || x.Value.Parameter != y.Value.Parameter || !PlainScale(x.Value.Binding) || !PlainScale(y.Value.Binding))
            {
                throw new InvalidOperationException($"'{child.Id}': its local translation bindings cannot become a length of '{parent.Id}'; bind local.x and local.y to one parameter with a plain scale, or remove them first.");
            }

            x.Value.Binding.Target = PropertyRef.OfControl(parent.Id, RigProperties.Length);
            x.Value.Parameter.Bindings.Remove(y.Value.Binding);
            report.Add($"{parent.Id}: length bound to parameter '{x.Value.Parameter.Id}' (was {child.Id}'s local x and y)");
        }
        model.Validate();
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

    /// <summary>
    /// The inverse of <see cref="ToBoneChain"/>: the given bone nodes become point controls whose character-frame rest is
    /// the derived one, with no rest angle, length or link. Rest positions and position-only constraints are unchanged;
    /// the frames return to the character axes. Seed generators use it to rebuild a point-control reference copy.
    /// </summary>
    public static IReadOnlyList<string> ToPointControls(CharacterModel model, IReadOnlyList<string> controls)
    {
        var nodes = RigGraph.Compile(model);
        var report = new List<string>();
        foreach (var id in controls)
        {
            var control = model.Controls.FirstOrDefault(c => c.Id == id) ?? throw new InvalidOperationException($"Model '{model.Id}' has no control '{id}'.");
            if (control.Node != RigNodeKinds.Bone)
            {
                throw new InvalidOperationException($"'{id}' is a {control.Node} node; only bone nodes convert back.");
            }

            if (model.Controls.Any(c => c.Parent == id && c.Node == RigNodeKinds.Bone && c.Link is not null && !controls.Contains(c.Id)))
            {
                throw new InvalidOperationException($"'{id}' still carries a linked child; convert the children with it.");
            }

            // A parameter that scaled this bone's length now scales its tip-bound child's offset (P4a); a link binding has no point form.
            foreach (var property in new[] { RigProperties.LinkT, RigProperties.LinkOffsetX, RigProperties.LinkOffsetY })
            {
                if (Binding(model, id, property) is { } link)
                {
                    throw new InvalidOperationException($"'{id}': parameter '{link.Parameter.Id}' binds its {property}; a point control has no link, so remove the binding first.");
                }
            }
            if (Binding(model, id, RigProperties.Length) is { } length)
            {
                var tip = model.Controls.FirstOrDefault(c => c.Parent == id && c.Node == RigNodeKinds.Bone && c.Link is { IsTip: true } && controls.Contains(c.Id));
                if (tip is null || !PlainScale(length.Binding))
                {
                    throw new InvalidOperationException($"'{id}': parameter '{length.Parameter.Id}' binds its length, which only a plain scale with a converted tip-bound child can carry as a point offset; remove the binding first.");
                }

                var index = length.Parameter.Bindings.IndexOf(length.Binding);
                length.Binding.Target = PropertyRef.OfControl(tip.Id, RigProperties.LocalX);
                length.Parameter.Bindings.Insert(index + 1, length.Binding with { Target = PropertyRef.OfControl(tip.Id, RigProperties.LocalY) });
                report.Add($"{tip.Id}: local x and y bound to parameter '{length.Parameter.Id}' (was {id}'s length)");
            }

            control.Node = RigNodeKinds.Point;
            control.Rest = PuppetPoint.From(nodes[id].Rest);
            control.RestAngle = 0;
            control.Local = null;
            control.Link = null;
            control.Length = 0;
            report.Add($"{id}: point control at rest ({control.Rest.X:0.####}, {control.Rest.Y:0.####}, {control.Rest.Z:0.####})");
        }
        model.Validate();
        return report;
    }
}
