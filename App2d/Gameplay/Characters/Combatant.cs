using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using System.Collections.Immutable;

namespace App2d.Gameplay.Characters;

/// <summary>What a hit that lands does: its damage, the sound it makes and whether it cuts like a sword.</summary>
public sealed record Strike(int Damage = 1, string? Sound = null, bool Sword = false);

/// <summary>What a collider on a character is for.</summary>
public enum ColliderUse
{
    /// <summary>Where the character can be hit.</summary>
    Hurt,
    /// <summary>What it hits with, while one of its action's hits is open.</summary>
    Attack,
    /// <summary>What stops a hit from the front, such as a shield.</summary>
    Guard,
    /// <summary>A region that only reports what is inside it.</summary>
    Trigger,
}

/// <summary>
/// A convex shape that follows a character. It sits in one of three places: fixed to the character (no joint, no mount),
/// upright on a joint, or on a mount, turning with it. <see cref="Offset"/> is in the axes of whichever it is on. A mount
/// turned out of the plane carries its collider flattened into the plane: play stays 2D.
/// </summary>
public sealed record CharacterCollider(string Name, ColliderUse Use, IConvexShape2D Shape, int Joint = Skeleton2D.NoParent, int Mount = Skeleton2D.NoParent, Vector2f Offset = default)
{
    /// <summary>Fixed to the character: feet at the origin, +X the way it faces.</summary>
    public static CharacterCollider OnBody(string name, ColliderUse use, IConvexShape2D shape, Vector2f offset = default) => new(name, use, shape, Offset: offset);
    /// <summary>Follows a joint without turning.</summary>
    public static CharacterCollider OnJoint(string name, ColliderUse use, IConvexShape2D shape, int joint, Vector2f offset = default) => new(name, use, shape, Joint: joint, Offset: offset);
    /// <summary>Follows a mount and turns with it; <paramref name="point"/> is a point on what the mount holds, such as a club's head.</summary>
    public static CharacterCollider OnMount(string name, ColliderUse use, IConvexShape2D shape, int mount, Vector2f point = default) => new(name, use, shape, Mount: mount, Offset: point);

    /// <summary>Where the shape is in the world for a pose, with every length multiplied by <paramref name="scale"/>.</summary>
    public Similarity2D Place(CharacterPose pose, float scale = 1)
    {
        if (Mount == Skeleton2D.NoParent)
        {
            var at = Joint == Skeleton2D.NoParent ? Offset : pose.World[Joint].Translation + Offset;
            return Similarity2D.FromAxis(pose.Place(at) * scale, new(pose.Facing, 0), mirror: pose.Facing < 0, scale);
        }
        // The mount's X axis as seen in the plane; pointing straight at the viewer it has no direction there, and faces forward.
        var frame = pose.MountFrame(Mount);
        var flat = frame.XAxis.XY;
        var axis = flat.LengthSquared() > 1e-8f ? Vector2f.Normalize(flat) : Vector2f.UnitX;
        var up = frame.YAxis.XY;
        var flipped = axis.X * up.Y < axis.Y * up.X;
        var origin = frame.TransformPoint(new(Offset, 0)).XY;
        return Similarity2D.FromAxis(pose.Place(origin) * scale, new(axis.X * pose.Facing, axis.Y), mirror: flipped != pose.Facing < 0, scale);
    }
}

/// <summary>
/// A stretch of an action during which one attack collider can hit, from <see cref="Start"/> up to but not including
/// <see cref="Finish"/>, in seconds. One play of the action hits each target once per <see cref="Group"/>, however many of
/// the target's colliders it crosses.
/// </summary>
public sealed record HitWindow(string Group, float Start, float Finish, int Collider, Strike Strike)
{
    /// <summary>Whether the window is open at any moment of a step, so a window shorter than the step is never missed.</summary>
    public bool Active(double previous, double now) => previous < Finish && now >= Start;
}

/// <summary>Something that happens at a moment of an action: a sound, a shot leaving the barrel, a guard opening.</summary>
public sealed record ActionCue(string Name, float Seconds, string? Sound = null);

/// <summary>What an action shoots or throws. With a flight time it is lobbed to land on its target after that long.</summary>
public sealed record Projectile(Vector2f Size, Strike Strike, float Speed = 8, float Lifetime = 3, float Gravity = 0, float FlightSeconds = 0);

/// <summary>
/// Something a character does: a clip, the hits it opens and the things that happen along the way. With a
/// <see cref="Mask"/> it plays over whatever the rest of the body is doing and fades in and out.
/// </summary>
public sealed record CharacterAction(string Name, SkeletonClip2D Clip)
{
    public ImmutableArray<HitWindow> Hits { get; init; } = [];
    public ImmutableArray<ActionCue> Cues { get; init; } = [];
    public Projectile? Projectile { get; init; }
    public SkeletonMask2D? Mask { get; init; }
    public float BlendIn { get; init; }
    public float BlendOut { get; init; }
    /// <summary>The action another press goes on to while this one plays or its recovery still runs.</summary>
    public string? Next { get; init; }
    /// <summary>The clip played once the action ends, when nothing else takes over.</summary>
    public SkeletonClip2D? Recovery { get; init; }

    public ActionCue? Cue(string name) => Cues.FirstOrDefault(c => c.Name == name);

    /// <summary>How strongly a masked action shows at a time: fading in at its start and out at its end. One without a mask.</summary>
    public float Weight(double seconds)
    {
        if (Mask is null) return 1;
        var weight = 1f;
        if (BlendIn > 0) weight = MathF.Min(weight, (float)seconds / BlendIn);
        if (BlendOut > 0) weight = MathF.Min(weight, (Clip.Duration - (float)seconds) / BlendOut);
        return Realf.Clamp01(weight);
    }
}

/// <summary>
/// A character that takes part in play: the actions it can take, the shape it moves through the world as and the shapes that
/// follow it. One is made per kind and shared by every one of that kind in the world.
/// </summary>
public sealed class Combatant : Character
{
    public ImmutableArray<CharacterAction> Actions { get; init; } = [];
    /// <summary>The shape it moves through the world as: feet at the origin.</summary>
    public required IConvexShape2D Movement { get; init; }
    public ImmutableArray<CharacterCollider> Colliders { get; init; } = [];

    public CharacterAction? Action(string name) => Actions.FirstOrDefault(a => a.Name == name);

    /// <summary>The first thing held that shoots.</summary>
    public Worn? Gun => Worn.FirstOrDefault(w => w.Muzzle is not null);

    /// <summary>Where a shot leaves the gun for a pose, in the world, and the way it flies. Play stays in the plane: a barrel turned toward the viewer shoots forward.</summary>
    public (Vector2f Point, Vector2f Axis) Muzzle(CharacterPose pose)
    {
        var gun = Gun ?? throw new InvalidOperationException($"'{Name}' holds nothing that shoots.");
        var flat = pose.MountFrame(gun.Mount).XAxis.XY;
        var axis = flat.LengthSquared() > 1e-8f ? Vector2f.Normalize(flat) : Vector2f.UnitX;
        return (pose.Place(pose.MountPoint(gun.Mount, gun.Muzzle!.Value)), new(axis.X * pose.Facing, axis.Y));
    }
}
