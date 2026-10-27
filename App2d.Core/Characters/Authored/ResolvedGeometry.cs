using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A geometry attachment resolved against a rig and its parts. Gameplay policy is not read here.</summary>
public class ResolvedGeometry
{
    private readonly ResolvedModel _model;

    public ResolvedGeometry(PartGeometry definition, ResolvedModel model, IReadOnlyDictionary<string, ModelSocket> sockets,
        IReadOnlyList<ResolvedEquipment> equipment, string field)
    {
        Definition = definition;
        _model = model;
        switch (definition.Binding)
        {
            case ColliderBindings.Socket:
                Socket = sockets.TryGetValue(definition.Socket!, out var socket) ? socket
                    : throw new InvalidDataException($"{field}: '{model.Base.Id}' has no socket '{definition.Socket}'.");
                break;
            case ColliderBindings.Prop:
                Equipment = equipment.FirstOrDefault(e => e.Prop.Id == definition.Prop)
                    ?? throw new InvalidDataException($"{field}: prop '{definition.Prop}' is not equipped.");
                PropPoint = Equipment.Prop.Point(definition.Point)
                    ?? throw new InvalidDataException($"{field}: prop '{definition.Prop}' has no '{definition.Point}' point.");
                break;
            case ColliderBindings.Control:
                foreach (var (name, control) in new[] { ("control", definition.A), ("frame", definition.Frame), ("toward", definition.B) })
                {
                    if (control is not null && control is not (CharacterModel.Locomotion or PartLocation.Origin) && !model.Controls.ContainsKey(control))
                        throw new InvalidDataException($"{field} {name}: '{model.Base.Id}' has no control '{control}'.");
                }
                if (definition.B == CharacterModel.Locomotion || definition.A == CharacterModel.Locomotion)
                    throw new InvalidDataException($"{field}: the locomotion frame orients a binding; it cannot be its control or toward target.");
                Socket = new ModelSocket
                {
                    Id = definition.Id,
                    Control = definition.A,
                    Frame = definition.Frame,
                    Toward = definition.B,
                    OffsetX = definition.OffsetX,
                    OffsetY = definition.OffsetY,
                    Angle = definition.Angle
                };
                if (definition.B is { } toward)
                    RestSpan = Vector2.Distance(model.Rest[definition.A].XY, toward == PartLocation.Origin ? Vector2.Zero : model.Rest[toward].XY);
                break;
        }
    }

    public PartGeometry Definition { get; }
    public string Id => Definition.Id;
    public string Binding => Definition.Binding;
    public ModelSocket? Socket { get; }
    public ResolvedEquipment? Equipment { get; }
    public PuppetPoint PropPoint { get; }
    public float RestSpan { get; }

    /// <summary>The composed frame supplies drawing and collision. Depth only affects drawing order.</summary>
    public LayeredFrame2D Frame(ActorPose pose)
    {
        if (Binding is ColliderBindings.Actor or ColliderBindings.Control)
        {
            Vector3 World(string id) => id == PartLocation.Origin ? Vector3.Zero : pose.Local.World(id);
            LayeredFrame2D Bone(string id) => id is PartLocation.Origin or CharacterModel.Locomotion ? LayeredFrame2D.Identity : pose.Local.Placement(id);
            Vector3 Rest(string id) => id == PartLocation.Origin ? Vector3.Zero : _model.Rest[id];
            return PartAttachment2D.FrameOf(Definition, World, Bone, Rest).Then(pose.Placement);
        }
        var anchor = Anchor(pose);
        var axis = PartPlacement3D.ProjectAxis(anchor, pose.Facing);
        var mirror = new Vector2d(axis).Cross(new(anchor.YAxis.XY)) < 0d;
        var projected = Similarity2D.FromAxis(anchor.TransformPoint(new(Definition.Along, 0, 0)).XY, axis, mirror);
        var carrier = new LayeredFrame2D(new Frame2D(projected.Translation, projected.XAxis, projected.YAxis), 0);
        return PartAttachment2D.Attachment(Definition).Then(carrier);
    }

    public Similarity2D Place(ActorPose pose, float scale = 1)
    {
        var frame = Frame(pose);
        var mirror = new Vector2d(frame.XAxis).Cross(new(frame.YAxis)) < 0d;
        return Similarity2D.FromAxis(frame.Origin.XY * scale, frame.XAxis, mirror, frame.XAxis.Length() * scale);
    }

    /// <summary>The spatial anchor for weapon points and sockets, before this geometry's local placement.</summary>
    public Frame3D Anchor(ActorPose pose)
    {
        if (Binding == ColliderBindings.Socket) return pose.Socket(Socket!);
        if (Binding == ColliderBindings.Actor) return pose.Placement3D.WithOrigin(pose.Place(new(Definition.OffsetX, Definition.OffsetY, 0)));
        if (Equipment is not { } equipment)
            throw new InvalidOperationException($"Geometry '{Id}' is bound to a control; it has no 3D anchor.");
        var frame = pose.Socket(equipment.Socket);
        return frame.WithOrigin(PartPlacement3D.Point(frame, equipment.Prop, PropPoint));
    }
}
