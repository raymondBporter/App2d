using App2d.Contracts.Combat;
using App2d.Core.Characters;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry.Transforms;
using App2d.Core.Kinematics;
using App2d.Gameplay.Characters;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>
/// The player's action timing and geometry, read from its <see cref="Character"/>. The sword swings are the "attack" action
/// and the actions its <see cref="CharacterAction.Next"/> chain reaches: each takes its length and its hit's times from its
/// clip, and hits with a box fixed to the player. A press chains to the next swing while one runs or its recovery clip still
/// holds the sword out. The gun's muzzle comes from the same pose the player is drawn in. Sizes are scaled by the collider's
/// height over the character's drawn height; positions are offsets from the collider's centre, Y up.
/// </summary>
public sealed class Hero2D
{
    private const string Attack = "attack", Shoot = "shoot", WallShot = "wall-shot", Fire = "fire";
    private readonly float _halfHeight;
    private readonly Dictionary<string, IShape2D> _shapes = new(StringComparer.Ordinal);

    public Hero2D(Combatant character, Vector2f colliderSize)
    {
        Character = character;
        WorldUnitsPerModelUnit = colliderSize.Y / character.Height;
        _halfHeight = colliderSize.Y / 2;
        for (var swing = Swing(null); !_shapes.ContainsKey(swing.Name); swing = swing.Next is { } next ? Swing(next) : swing)
        {
            if (swing.Hits is not [var hit] || character.Colliders[hit.Collider] is not { Joint: Skeleton2D.NoParent, Mount: Skeleton2D.NoParent } collider)
            {
                throw new InvalidDataException($"'{character.Name}' action '{swing.Name}' needs exactly one hit whose collider is fixed to the character: the player's sword hit is a box on the player.");
            }
            _shapes[swing.Name] = ShapeTransforms2D.Scaled(collider.Shape, WorldUnitsPerModelUnit);
        }
    }

    public Combatant Character { get; }
    public float WorldUnitsPerModelUnit { get; }

    /// <summary>The swing with a name, or the first swing of the chain.</summary>
    public CharacterAction Swing(string? name) => Character.Action(name ?? Attack) ?? throw new InvalidDataException($"'{Character.Name}' has no '{name ?? Attack}' action.");

    public IShape2D Shape(string? swing = null) => _shapes[Swing(swing).Name];
    public int Damage(string? swing = null) => Swing(swing).Hits[0].Strike.Damage;
    /// <summary>The group a swing's contacts are counted once in.</summary>
    public string HitGroup(string? swing = null) => Swing(swing).Hits[0].Group;
    public CombatImpactKind2D Impact(string? swing = null) => Swing(swing).Hits[0].Strike.Sword ? CombatImpactKind2D.Sword : CombatImpactKind2D.Generic;

    /// <summary>A swing's timing and its box, centred where the character puts it. A press at any time during the swing is kept for its end, so mashing never drops one.</summary>
    internal MeleeAttackProfile2D Profile(string? name)
    {
        var swing = Swing(name);
        var hit = swing.Hits[0];
        var centre = ToWorld(Character.Colliders[hit.Collider].Offset);
        return new(swing.Clip.Duration, hit.Start, hit.Finish, swing.Clip.Duration, centre.X, centre.Y)
        {
            Shape = _shapes[swing.Name]
        };
    }

    /// <summary>
    /// The swing a press plays <paramref name="sinceEnd"/> seconds after <paramref name="last"/> ended (zero while it runs):
    /// its next while its recovery still holds the sword out, otherwise the first swing again.
    /// </summary>
    public string Chain(string? last, float sinceEnd)
    {
        if (last is null || Swing(last).Next is not { } next) return Attack;
        var recovery = Swing(last).Recovery;
        var window = recovery is null ? 0 : recovery.Marker(ClipMarkers.Sheathe) ?? recovery.Duration;
        return sinceEnd <= window ? next : Attack;
    }

    /// <summary>The player's bolt, in world units.</summary>
    internal GunPersonWeapon2D.Shot? Shot => Character.Action(Shoot)?.Projectile is { } shot
        ? new(shot.Size * WorldUnitsPerModelUnit, shot.Speed * WorldUnitsPerModelUnit, shot.Lifetime, shot.Strike.Damage) : null;

    /// <summary>Where a shot leaves the pistol: the muzzle at the moment the shot fires, standing or gripping a wall.</summary>
    public Vector2f Muzzle(float facing, bool wallGrip)
    {
        var action = (wallGrip ? Character.Action(WallShot) : null) ?? Character.Action(Shoot) ?? throw new InvalidOperationException($"'{Character.Name}' has no shoot action.");
        var pose = CharacterPose.Sample(Character.Skeleton, action.Clip, action.Cue(Fire)!.Seconds).At(Vector2f.Zero, facing < 0 ? -1 : 1);
        return ToWorld(Character.Muzzle(pose).Point);
    }

    private Vector2f ToWorld(Vector2f units) => units * WorldUnitsPerModelUnit - new Vector2f(0, _halfHeight);
}
