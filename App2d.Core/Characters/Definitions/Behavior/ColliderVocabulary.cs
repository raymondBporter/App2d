using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The gameplay use of an attached collider: the category the combat queries filter on. Every use but the movement body is a
/// sensor; what a hit, a block or a trigger contact means belongs to gameplay (P9), not to the geometry.
/// </summary>
public static class ColliderUses
{
    public const string Hurt = "hurt";
    public const string Attack = "attack";
    public const string Guard = "guard";
    public const string Trigger = "trigger";
    /// <summary>The stable terrain movement body, registered by the actor's owner (a rigid body in the game, a plain collider in the arena).</summary>
    public const string Movement = "movement";
    /// <summary>The uses an entity may declare.</summary>
    public static readonly IReadOnlyList<string> All = [Hurt, Attack, Guard, Trigger];
}

/// <summary>How an attached collider is placed: the binding kinds, derived from which fields a definition sets.</summary>
public static class ColliderBindings
{
    /// <summary>Fixed to the actor: its offset from the feet, +X toward facing.</summary>
    public const string Actor = "actor";
    /// <summary>On a model socket's 3D frame (the clip's tilt/twist/turn applied), projected to XY like a prop.</summary>
    public const string Socket = "socket";
    /// <summary>On an equipped prop's named point, along the prop's projected axis.</summary>
    public const string Prop = "prop";
    /// <summary>On a control's planar placement, with the socket rules for frame, toward, offset and angle.</summary>
    public const string Control = "control";
}
