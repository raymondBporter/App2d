using App2d.Collision;
using App2d.Core;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.World;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// The one construction recipe for a side-scroller session and everything it owns. The
/// local host, a future server, and a predicting client all build through here, so they
/// cannot drift apart in wiring, layers, physics settings, or identity allocation.
/// </summary>
public sealed class SideScrollerSimulation2D : IDisposable
{
    private bool _disposed;

    private SideScrollerSimulation2D(SideScrollerSessionDefinition2D definition, EntityIdAllocator2D ids,
        CollisionSystem2D collision, PhysicsWorld2D physics, CombatantRegistry2D combatants, CombatSystem2D combat,
        SideScrollerLevel2D level, int[] groundHeights, Person2D player, PersonArsenal2D arsenal,
        SideScrollerSession2D session)
    {
        Definition = definition; Ids = ids; Collision = collision; Physics = physics; Combatants = combatants;
        Combat = combat; Level = level; GroundHeights = groundHeights; Player = player; Arsenal = arsenal; Session = session;
    }

    public SideScrollerSessionDefinition2D Definition { get; }
    public EntityIdAllocator2D Ids { get; }
    public CollisionSystem2D Collision { get; }
    public PhysicsWorld2D Physics { get; }
    public CombatantRegistry2D Combatants { get; }
    public CombatSystem2D Combat { get; }
    public SideScrollerLevel2D Level { get; }
    /// <summary>Ground tile row per column, derived from the definition's map at construction.</summary>
    public int[] GroundHeights { get; }
    public Person2D Player { get; }
    public PersonArsenal2D Arsenal { get; }
    public SideScrollerSession2D Session { get; }

    public static SideScrollerSimulation2D Create(SideScrollerSessionDefinition2D definition)
    {
        ArgGuard.ThrowIfNull(definition);
        definition.Validate();
        var traversal = definition.Traversal;
        traversal.ValidateScaleContract();

        var ids = new EntityIdAllocator2D();
        var collision = new CollisionSystem2D();
        var combatants = new CombatantRegistry2D();
        var physics = new PhysicsWorld2D(collision)
        {
            Gravity = new Vector2(0f, -traversal.Gravity),
            MaxSubstepSeconds = SideScrollerSession2D.FixedDeltaSeconds,
            PositionIterations = definition.PositionIterations,
            VelocityIterations = definition.VelocityIterations,
        };

        var groundHeights = TileGroundHeights2D.Derive(definition.TileMap);
        var level = new SideScrollerLevel2D(traversal, definition.TileMap,
            x => groundHeights[Math.Clamp(x, 0, groundHeights.Length - 1)],
            definition.MovingPlatforms, definition.WorldThings);
        var respawn = ResolveRespawn(level, definition);
        level.CreateSimulation(collision, physics, ids,
            SideScrollerLayers2D.World, SideScrollerLayers2D.Player, SideScrollerLayers2D.Enemy);
        level.UpdateStreaming(respawn.Position);

        var player = new Person2D(ids.Allocate(), collision, physics, traversal, respawn.Position,
            SideScrollerLayers2D.Player, SideScrollerLayers2D.World, CombatFaction2D.Player,
            maximumHealth: definition.PlayerMaximumHealth, tileMap: definition.TileMap);
        combatants.Register(player);
        var contactDamage = new ContactDamageSystem2D(collision, SideScrollerLayers2D.Enemy, combatants);
        var combat = new CombatSystem2D(collision, combatants);
        level.CreateAuthoredWorldThings(combat);
        var arsenal = new PersonArsenal2D(ids, player.Body, traversal.GunMuzzleOffset, collision,
            SideScrollerLayers2D.World, SideScrollerLayers2D.Enemy, CombatFaction2D.Player, combat,
            overlapsSpikes: bounds => level.TryGetSpikeSource(bounds, out _));
        player.AttachActions(arsenal);

        var session = new SideScrollerSession2D(physics, player, arsenal,
            new SideScrollerSessionWorld2D(level, contactDamage), respawn, combat);
        return new SideScrollerSimulation2D(definition, ids, collision, physics, combatants, combat,
            level, groundHeights, player, arsenal, session);
    }

    /// <summary>Saved progress resumes at its checkpoint when the checkpoint still exists and health is valid.</summary>
    private static RespawnState2D ResolveRespawn(SideScrollerLevel2D level, SideScrollerSessionDefinition2D definition)
    {
        if (definition.SavedProgress is { } saved && saved.HitPoints > 0 &&
            saved.HitPoints <= definition.PlayerMaximumHealth &&
            level.FindSavePoint(saved.SavePointId) is { } savePoint)
        {
            return new RespawnState2D(savePoint.Position, saved.HitPoints, savePoint.ThingId);
        }
        return new RespawnState2D(level.SpawnPoint, definition.PlayerMaximumHealth);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Session.Dispose();
        Level.Dispose();
    }
}
