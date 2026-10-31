using App2d.Core.Characters.Geometry;
using App2d.Core.Constraints;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>Compiles authored controls into canonical nodes and their rest frame graph.</summary>
internal static class RigGraph
{
    /// <summary>Controls ordered so every parent precedes its children, keeping document order otherwise.</summary>
    public static List<ModelControl> Order(CharacterModel model)
    {
        var controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var order = new List<ModelControl>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        void Place(ModelControl control)
        {
            if (!placed.Add(control.Id))
            {
                return;
            }

            if (control.Parent is not null)
            {
                Place(controls[control.Parent]);
            }

            order.Add(control);
        }
        foreach (var control in model.Controls)
        {
            Place(control);
        }

        return order;
    }

    /// <summary>Compiles from the authored controls alone: no variant, no parameter values, no overrides.</summary>
    public static Dictionary<string, RigNode> Compile(CharacterModel model) => Compile(Order(model), null);

    /// <summary>
    /// Compiles every control in <paramref name="order"/> from its final setup, length and link: the <paramref name="layout"/>
    /// after parameter bindings and explicit overrides wrote it, or the authored values when it is null. A point control nobody
    /// moved keeps its authored rest bit for bit; one whose setup changed, or whose ancestor's did, derives its rest from the
    /// setup like a bone node. There is no absolute-position pin: a variant moves a node through its setup overrides (P6a).
    /// </summary>
    public static Dictionary<string, RigNode> Compile(IReadOnlyList<ModelControl> order, RigLayout? layout)
    {
        var nodes = new Dictionary<string, RigNode>(StringComparer.Ordinal);
        var derived = new HashSet<string>(StringComparer.Ordinal);
        foreach (var control in order)
        {
            var parent = control.Parent is null ? null : nodes[control.Parent];
            var parentFrame = parent?.RestFrame ?? Matrix3x2.Identity;
            var parentAngle = parent?.RestAngle ?? 0;
            var parentLayer = parent?.RestLayer ?? 0;
            var parentHand = parent?.Handedness ?? 1;
            var moved = layout?.Moved.Contains(control.Id) == true || parent is not null && derived.Contains(parent.Id);
            if (moved)
            {
                derived.Add(control.Id);
            }

            RigNode node;
            switch (control.Node)
            {
                case RigNodeKinds.Bone:
                    {
                        var setup = layout?.Setups[control.Id] ?? control.Local ?? new RigSetup();
                        var link = layout is null ? control.Link : layout.Links[control.Id];
                        var length = layout?.Lengths[control.Id] ?? control.Length;
                        var limit = layout is null ? control.Limit : layout.Limits[control.Id];
                        if (limit?.Contains(setup.Rotation) == false)
                        {
                            throw new InvalidDataException($"Control '{control.Id}': its rest rotation {setup.Rotation:0.###} lies outside its joint limits [{limit.Min:0.###}, {limit.Max:0.###}]; widen the limits or turn the rest.");
                        }

                        var origin = link is null ? setup.Translation : link.Origin(parent?.Length ?? 0);
                        Matrix3x2 frame;
                        float angle;
                        int hand;
                        if (link?.Orientation == RigLinkOrientations.Character)
                        {
                            var at = Vector2.Transform(origin, parentFrame);
                            frame = LayeredFrame2D.Local(setup.ScaleVector, setup.Rotation, at, 0).Frame.Matrix;
                            angle = setup.Rotation;
                            hand = setup.Reflect ? -1 : 1;
                        }
                        else
                        {
                            frame = LayeredFrame2D.Local(setup.ScaleVector, setup.Rotation, origin, setup.Layer).Frame.Matrix * parentFrame;
                            angle = parentAngle + parentHand * setup.Rotation;
                            hand = parentHand * (setup.Reflect ? -1 : 1);
                        }
                        node = new(control.Id, control.Parent, control.Node, setup, length, link, frame, angle, parentLayer + setup.Layer, hand, limit);
                        break;
                    }
                default:
                    {
                        // A point control's character-frame rest is authoritative unless a binding or override moved it or an
                        // ancestor; the local similarity is what reproduces it on the parent.
                        if (!moved || layout is null)
                        {
                            var r = control.Rest.XYZ;
                            var frame = Matrix3x2.CreateRotation(control.RestAngle) * Matrix3x2.CreateTranslation(r.X, r.Y);
                            Vector2 translation;
                            if (parent is null)
                            {
                                translation = new(r.X, r.Y);
                            }
                            else if (!new Frame2D(parentFrame).TryToLocal(new(r.X, r.Y), out translation))
                            {
                                throw new InvalidDataException($"Cannot place '{control.Id}' beneath a collapsed parent.");
                            }
                            var setup = new RigSetup { X = translation.X, Y = translation.Y, Rotation = parentHand * (control.RestAngle - parentAngle), Layer = r.Z - parentLayer };
                            node = new(control.Id, control.Parent, RigNodeKinds.Point, setup, control.Length, null, frame, control.RestAngle, r.Z, parentHand);
                        }
                        else
                        {
                            var setup = layout.Setups[control.Id];
                            var position = parent is null ? setup.Translation : Vector2.Transform(setup.Translation, parentFrame);
                            var angle = parentAngle + parentHand * setup.Rotation;
                            var frame = Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(position.X, position.Y);
                            node = new(control.Id, control.Parent, RigNodeKinds.Point, setup, control.Length, null, frame, angle, parentLayer + setup.Layer, parentHand);
                        }
                        break;
                    }
            }
            nodes.Add(control.Id, node);
        }

        return nodes;
    }
}
