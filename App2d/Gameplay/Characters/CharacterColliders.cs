using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Collision;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Spatial;
using App2d.Core.Validation;

namespace App2d.Gameplay.Characters;

/// <summary>One character's own copy of one of its colliders, registered in the collision system. It is made once and then only moved.</summary>
public sealed class PlacedCollider
{
    internal PlacedCollider(CharacterCollider definition, int index, Collider2D collider)
    {
        Definition = definition;
        Index = index;
        Collider = collider;
    }

    public CharacterCollider Definition { get; }
    /// <summary>Its place in the character's list of colliders; -1 for the movement shape.</summary>
    public int Index { get; }
    public Collider2D Collider { get; }
    public SpatialObject2D Object => Collider.WorldObject;
    public string Name => Definition.Name;
    public ColliderUse Use => Definition.Use;
    public bool Enabled => Collider.IsEnabled;
    public Rect2f Bounds => Object.WorldBounds;

    internal void Place(Similarity2D pose)
    {
        var transform = Object.Transform;
        var mirrored = pose.IsMirrored;
        var rotation = (mirrored ? -pose.XAxis : pose.XAxis).AngleRadians;
        var scale = new Vector2f(mirrored ? -pose.Scale : pose.Scale, pose.Scale);
        if (transform.Position != pose.Translation) transform.Position = pose.Translation;
        if (transform.Rotation != rotation) transform.Rotation = rotation;
        if (transform.Scale != scale) transform.Scale = scale;
    }

    internal void SetEnabled(bool enabled) => Collider.IsEnabled = enabled;
}

/// <summary>
/// The colliders of one character in the world. They are registered once when the character appears, and
/// <see cref="Sync"/> moves and enables them from its pose every step. Each carries the owner's id and layer, so the ordinary
/// broad phase finds a hurt region that reaches past the body.
/// </summary>
public sealed class CharacterColliders
{
    private readonly List<PlacedCollider> _all = [];
    private bool _active = true;

    /// <param name="scale">Turns the skeleton's units into the collision system's.</param>
    /// <param name="movement">Also registers the movement shape, for a character with no rigid body of its own.</param>
    public CharacterColliders(Combatant character, CollisionSystem2D system, EntityId2D owner, uint layer, float scale = 1, bool movement = false)
    {
        Ensure.Positive(scale);
        Character = character;
        Owner = owner;
        Scale = scale;
        if (movement) Movement = Register(new("movement", ColliderUse.Trigger, character.Movement), -1, system, layer, sensor: false);
        for (var i = 0; i < character.Colliders.Length; i++) Register(character.Colliders[i], i, system, layer, sensor: true);
    }

    public Combatant Character { get; }
    public EntityId2D Owner { get; }
    public float Scale { get; }
    public PlacedCollider? Movement { get; }

    public PlacedCollider? Find(string name) => _all.FirstOrDefault(c => c.Name == name);
    /// <summary>The placed copy of the character's collider at an index.</summary>
    public PlacedCollider At(int index) => _all.First(c => c.Index == index);

    private PlacedCollider Register(CharacterCollider definition, int index, CollisionSystem2D system, uint layer, bool sensor)
    {
        var collider = system.AddCollider(new SpatialObject2D(definition.Shape), ColliderMobility2D.Dynamic);
        collider.IsSensor = sensor;
        collider.CollisionLayer = layer;
        collider.CollisionMask = uint.MaxValue;
        collider.EntityId = Owner;
        collider.IsEnabled = false;
        var placed = new PlacedCollider(definition, index, collider);
        collider.UserData = placed;
        _all.Add(placed);
        return placed;
    }

    /// <summary>
    /// Places every collider from the pose and sets which are on for this step: attack colliders by the hits open during the
    /// step, the guard by <paramref name="guardUp"/>, the rest by the character being alive.
    /// </summary>
    public void Sync(CharacterPose pose, IEnumerable<HitWindow>? activeHits, bool alive, bool guardUp = true)
    {
        foreach (var placed in _all)
        {
            if (ReferenceEquals(placed, Movement))
            {
                placed.Place(Similarity2D.FromAxis(pose.Position * Scale, new(pose.Facing, 0), mirror: pose.Facing < 0, Scale));
                placed.SetEnabled(_active);
                continue;
            }
            placed.Place(placed.Definition.Place(pose, Scale));
            placed.SetEnabled(_active && placed.Use switch
            {
                ColliderUse.Attack => activeHits?.Any(hit => hit.Collider == placed.Index) == true,
                ColliderUse.Guard => alive && guardUp,
                _ => alive,
            });
        }
    }

    /// <summary>Off, the colliders stay registered but disabled until the next <see cref="Sync"/>.</summary>
    public void SetActive(bool active)
    {
        _active = active;
        if (!active)
        {
            foreach (var placed in _all) placed.SetEnabled(false);
        }
    }
}

/// <summary>
/// One hit landing on one target: which hit, which play of the action, whom it reached and where, and whether the target's
/// guard took it instead of its body.
/// </summary>
public readonly record struct AcceptedContact(HitWindow Hit, int Sequence, EntityId2D Target, Vector2f Position, string? TargetCollider, bool Blocked)
{
    public Strike Strike => Hit.Strike;
}

/// <summary>Remembers whom one attacker has hit, so one play of an action reaches each target once per hit group, however long the overlap lasts.</summary>
public sealed class HitLedger
{
    private readonly HashSet<(int Sequence, string Group, EntityId2D Target)> _hits = [];

    public bool TryHit(int sequence, string group, EntityId2D target)
    {
        _hits.RemoveWhere(h => h.Sequence != sequence);
        return _hits.Add((sequence, group, target));
    }
}

/// <summary>
/// Finds whom an attacker's open hits reach, through the ordinary collision queries. A target's guard, where it overlaps,
/// takes the hit as a block. What to do about a contact (damage, sound, reaction) is up to the caller.
/// </summary>
public static class CombatContacts
{
    /// <param name="targetOf">Says which overlapped collider belongs to a live target, and whether it is that target's guard.</param>
    public static int Resolve(CharacterColliders attacker, int sequence, IEnumerable<HitWindow> active, uint targetMask, HitLedger ledger,
        Func<PlacedCollider, Collider2D, (EntityId2D Target, bool Guard)?> targetOf, List<AcceptedContact> results, List<CollisionOverlap2D> scratch)
    {
        results.Clear();
        var seen = new List<(EntityId2D Target, Vector2f Position, string? Collider, bool Blocked)>();
        foreach (var hit in active)
        {
            var attack = attacker.At(hit.Collider);
            if (!attack.Enabled) continue;
            seen.Clear();
            var bounds = attack.Bounds;
            attack.Collider.System.Overlap(attack.Object, scratch, targetMask, includeSensors: true, attack.Collider);
            foreach (var overlap in scratch)
            {
                if (overlap.Collider.EntityId == attacker.Owner || targetOf(attack, overlap.Collider) is not { } found) continue;
                var other = overlap.Collider.WorldObject.WorldBounds;
                var position = (Vector2f.Max(bounds.Min, other.Min) + Vector2f.Min(bounds.Max, other.Max)) / 2;
                var name = (overlap.Collider.UserData as PlacedCollider)?.Name;
                var index = seen.FindIndex(s => s.Target == found.Target);
                if (index < 0) seen.Add((found.Target, position, name, found.Guard));
                // The guard wins over the body it covers: a blocked contact, where the guard is.
                else if (found.Guard && !seen[index].Blocked) seen[index] = (found.Target, position, name, true);
            }
            foreach (var (target, position, collider, blocked) in seen)
            {
                if (ledger.TryHit(sequence, hit.Group, target)) results.Add(new(hit, sequence, target, position, collider, blocked));
            }
        }
        return results.Count;
    }
}
