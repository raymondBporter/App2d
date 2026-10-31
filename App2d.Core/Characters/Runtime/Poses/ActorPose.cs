using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The final pose placed in the world: the evaluated pose in actor-local units (facing +X, feet origin) plus the actor's
/// position and facing. Rendering draws <see cref="Local"/> through a mirrored matrix; collision and props use
/// <see cref="Place"/>, so both read one pose.
/// </summary>
public sealed record ActorPose(EvaluatedPose Local, Vector2 Position, int Facing)
{
    /// <summary>
    /// The entity-to-world placement: actor-local XY mirrored to the facing at the actor's position, with no layer
    /// offset, so a character's draw order is the same whichever way it faces.
    /// </summary>
    public LayeredFrame2D Placement => new(new Frame2D(Position, new(Facing, 0), Vector2.UnitY), 0);

    /// <summary>The same placement for real 3D prop geometry: the planar frame extended with an unchanged Z axis.</summary>
    public Frame3D Placement3D => new(new(Position, 0), new(Facing, 0, 0), Vector3.UnitY, Vector3.UnitZ);

    public Vector3 Place(Vector3 local) => Placement.At(local);
    public Vector3 ToLocal(Vector3 world) => Placement.ToLocal(world);
    public Vector3 World(string control) => Place(Local.Points[control]);

    /// <summary>
    /// The carrier a socket attaches to, in actor-local units: its control's point turned by the frame control's angle
    /// (screen axes for the locomotion frame), or by the direction toward another control when one is named.
    /// </summary>
    private LayeredFrame2D SocketCarrier(ModelSocket socket)
    {
        if (ResolvedModel.NestedSocket(socket) is { } nested)
        {
            // A socket on a module instance's control (P10c): the instance's evaluated frames, in the wearer's frame.
            if (!Local.Attachments.TryGetValue(nested.Instance, out var instance))
            {
                throw new InvalidOperationException($"Socket '{socket.Id}' sits on '{socket.Control}', but attachment '{nested.Instance}' was not evaluated in this pose.");
            }

            Vector3 World(string control) => instance.World(control);
            float AngleOf(string control) { var frame = instance.Frame(control); return MathF.Atan2(frame.XAxis.Y, frame.XAxis.X); }
            var nestedAngle = socket.Frame == CharacterModel.Locomotion ? 0 : AngleOf(socket.Frame is { } f ? MotionClip.Split(f).Target : nested.Control);
            if (socket.Toward is { } nestedToward)
            {
                var direction = World(MotionClip.Split(nestedToward).Target) - World(nested.Control);
                if (direction.X * direction.X + direction.Y * direction.Y > 1e-10f)
                {
                    nestedAngle = MathF.Atan2(-direction.X, direction.Y);
                }
            }
            return LayeredFrame2D.FromRotation(nestedAngle, World(nested.Control));
        }

        var angle = socket.Frame == CharacterModel.Locomotion ? 0 : Local.Angles[socket.Frame ?? socket.Control];
        if (socket.Toward is { } toward)
        {
            var direction = Local.Points[toward] - Local.Points[socket.Control];
            if (direction.X * direction.X + direction.Y * direction.Y > 1e-10f)
            {
                angle = MathF.Atan2(-direction.X, direction.Y);
            }
        }
        return LayeredFrame2D.FromRotation(angle, Local.Points[socket.Control]);
    }

    /// <summary>
    /// An ordinary socket's planar placement in the world: its authored offset, angle and layer offset on its carrier,
    /// carried by the actor's placement. Clothing, picking and editor handles read this; props lift it to 3D.
    /// </summary>
    public LayeredFrame2D SocketPlacement(ModelSocket socket) =>
        socket.LocalPlacement.Then(SocketCarrier(socket)).Then(Placement);

    /// <summary>The socket's screen-plane turn with its animated orientation applied, shared with editor handles.</summary>
    public float SocketTurn(ModelSocket socket)
    {
        var planar = socket.LocalPlacement.Then(SocketCarrier(socket));
        return planar.XAxis.AngleRadians + Local.SocketAngles.GetValueOrDefault(socket.Id).Z;
    }

    /// <summary>The socket's 3D frame for props: its planar placement lifted by the clip's tilt/twist/turn orientation.</summary>
    public Frame3D Socket(ModelSocket socket) =>
        PartPlacement3D.Lift(SocketPlacement(socket), Local.SocketAngles.GetValueOrDefault(socket.Id));
}
