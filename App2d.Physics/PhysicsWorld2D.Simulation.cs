using App2d.Collision;
using App2d.Collision.Contacts;
using App2d.Core;
using App2d.Physics.Filtering;
using App2d.Physics.Integration;
using App2d.Physics.Solvers;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Physics;

/// <summary>Owned values for the built-in unconstrained solver at a tick boundary.</summary>
public sealed class PhysicsWorldState2D
{
    internal PhysicsWorldState2D(Guid owner, ImmutableArray<PhysicsBody2D.SimulationState> bodies,
        ImmutableArray<ContactState> contacts, CollisionOrderState2D order,
        Vector2 gravity, int positionIterations, int velocityIterations, float maxSubstepSeconds)
    {
        Owner = owner; Bodies = bodies; Contacts = contacts; Order = order;
        Gravity = gravity; PositionIterations = positionIterations;
        VelocityIterations = velocityIterations; MaxSubstepSeconds = maxSubstepSeconds;
    }
    internal Guid Owner { get; }
    internal ImmutableArray<PhysicsBody2D.SimulationState> Bodies { get; }
    internal ImmutableArray<ContactState> Contacts { get; }
    internal CollisionOrderState2D Order { get; }
    internal Vector2 Gravity { get; }
    internal int PositionIterations { get; }
    internal int VelocityIterations { get; }
    internal float MaxSubstepSeconds { get; }
    internal readonly record struct ContactState(int First, int Second, CollisionContact2D Geometry);
}

public sealed partial class PhysicsWorld2D
{
    private readonly Guid _checkpointOwner = Guid.NewGuid();

    public PhysicsWorldState2D CaptureSimulation()
    {
        RequireRollbackSupport();
        var ids = _bodies.Select(b => b.Collider.Id).ToHashSet();
        var bodies = _bodies.Select(b => b.CaptureSimulation()).ToImmutableArray();
        StateGuard.ThrowIf(bodies.Any(b => b.IgnoredPlatforms.Any(id => !ids.Contains(id))) ||
            _lastContacts.Any(c => !ids.Contains(c.First.Collider.Id) || !ids.Contains(c.Second.Collider.Id)),
            "Capture requires a completed tick with no stale body references.");
        return new(_checkpointOwner, bodies, _lastContacts.Select(c => new PhysicsWorldState2D.ContactState(
            c.First.Collider.Id, c.Second.Collider.Id, c.Geometry)).ToImmutableArray(),
            CollisionSystem.CaptureOrder(), Gravity, PositionIterations, VelocityIterations, MaxSubstepSeconds);
    }

    // Terrain may be reconstructed between validation and restore. All other bodies must still exist.
    public void ValidateSimulation(PhysicsWorldState2D state, IEnumerable<int>? currentTerrain = null,
        IEnumerable<int>? restoredTerrain = null)
    {
        ArgGuard.ThrowIfNull(state);
        RequireRollbackSupport();
        StateGuard.ThrowIf(state.Owner != _checkpointOwner, "The physics checkpoint belongs to another world.");
        var current = _bodies.Select(b => b.Collider.Id).Except(currentTerrain ?? []).Order();
        var expected = state.Bodies.Select(b => b.ColliderId).Except(restoredTerrain ?? []).Order();
        StateGuard.ThrowIf(!current.SequenceEqual(expected), "Physics body membership changed since capture.");
    }

    public void RestoreSimulation(PhysicsWorldState2D state)
    {
        ValidateSimulation(state);
        var byId = _bodies.ToDictionary(b => b.Collider.Id);
        _bodies.Clear();
        foreach (var body in state.Bodies)
        {
            var target = byId[body.ColliderId];
            target.RestoreSimulation(body, byId);
            _bodies.Add(target);
        }
        Gravity = state.Gravity;
        PositionIterations = state.PositionIterations;
        VelocityIterations = state.VelocityIterations;
        MaxSubstepSeconds = state.MaxSubstepSeconds;
        _lastContacts.Clear();
        foreach (var c in state.Contacts) _lastContacts.Add(new(byId[c.First], byId[c.Second], c.Geometry));
        _frameContacts.Clear();
        _substepContacts.Clear();
        _collisionContacts.Clear();
        CollisionSystem.RestoreOrder(state.Order);
    }

    private void RequireRollbackSupport()
    {
        StateGuard.ThrowIf(_constraints.Count != 0 || Integrator is not SemiImplicitEulerIntegrator2D ||
            PairFilter is not DefaultPhysicsPairFilter2D || PositionSolver is not MassWeightedPositionSolver2D ||
            VelocitySolver is not ImpulseVelocitySolver2D || CollisionSystem.PairFilter is not DefaultColliderPairFilter2D ||
            CollisionSystem.ContactProvider is not ShapeCollisionContactProvider2D ||
            CollisionSystem.Colliders.Count != _bodies.Count,
            "Rollback requires the built-in physics pipeline with no constraints or standalone colliders.");
    }
}
