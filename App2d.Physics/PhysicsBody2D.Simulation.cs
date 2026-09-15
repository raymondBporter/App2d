using App2d.Core;
using App2d.Core.Mathematics;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Physics;

public sealed partial class PhysicsBody2D
{
    internal sealed record SimulationState(int ColliderId,
        TransformState2D Pose,
        BodyMotionType2D MotionType,
        Vector2 LinearVelocity,
        float AngularVelocity,
        bool FreezeRotation,
        Vector2 AccumulatedForce,
        float AccumulatedTorque,
        Vector2 PreviousPosition,
        float PreviousRotation,
        float GravityScale,
        float Restitution,
        float Friction,
        bool IsCollider,
        bool IsSensor,
        bool IsOneWayPlatform,
        bool IsWallGrippable,
        uint CollisionLayer,
        uint CollisionMask,
        EntityId2D EntityId,
        float OneWaySlop,
        float Mass,
        float MomentOfInertia,
        ImmutableArray<int> IgnoredPlatforms);

    internal SimulationState CaptureSimulation() => new(Collider.Id,
        TransformState2D.Capture(WorldObject.Transform), MotionType, LinearVelocity, AngularVelocity, FreezeRotation, AccumulatedForce, AccumulatedTorque, PreviousPosition, PreviousRotation, GravityScale, Restitution, Friction, IsCollider, IsSensor, IsOneWayPlatform, IsWallGrippable, CollisionLayer, CollisionMask, EntityId, OneWaySlop, Mass, MomentOfInertia,
        _ignoredOneWayPlatforms?.Select(b => b.Collider.Id).Order().ToImmutableArray() ?? []);

    internal void RestoreSimulation(SimulationState state, IReadOnlyDictionary<int, PhysicsBody2D> bodies)
    {
        state.Pose.Apply(WorldObject.Transform);
        MotionType = state.MotionType;
        LinearVelocity = state.LinearVelocity;
        AngularVelocity = state.AngularVelocity;
        FreezeRotation = state.FreezeRotation;
        AccumulatedForce = state.AccumulatedForce;
        AccumulatedTorque = state.AccumulatedTorque;
        PreviousPosition = state.PreviousPosition;
        PreviousRotation = state.PreviousRotation;
        GravityScale = state.GravityScale;
        Restitution = state.Restitution;
        Friction = state.Friction;
        IsCollider = state.IsCollider;
        IsSensor = state.IsSensor;
        IsOneWayPlatform = state.IsOneWayPlatform;
        IsWallGrippable = state.IsWallGrippable;
        CollisionLayer = state.CollisionLayer;
        CollisionMask = state.CollisionMask;
        EntityId = state.EntityId;
        OneWaySlop = state.OneWaySlop;
        Mass = state.Mass;
        MomentOfInertia = state.MomentOfInertia;
        ClearIgnoredOneWayPlatforms();
        foreach (var id in state.IgnoredPlatforms) IgnoreOneWayPlatform(bodies[id]);
    }
}
