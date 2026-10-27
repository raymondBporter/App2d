using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Characters.Geometry;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Physics.Motion;
using App2d.Core.Rendering.Characters;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>The world owns a placed Part and its optional body. Passive parts have no combat or AI registration.</summary>
public sealed class AuthoredObject2D : IDisposable
{
    private readonly PhysicsWorld2D _physics;
    private readonly CurveMotion2D? _motion;
    private Vector2 _pausedVelocity;
    private float _pausedSpin;
    private double _time;
    private bool _disposed;

    public AuthoredObject2D(EntityId2D id, ObjectPlacement2D placement, PhysicsWorld2D physics, uint layer, uint mask)
    {
        ArgGuard.ThrowIf(!id.IsValid, "An object needs a valid entity id.", nameof(id));
        ArgGuard.ThrowIfNull(placement);
        placement.Validate();
        _physics = ArgGuard.RequireNotNull(physics);
        Id = id;
        Placement = placement;
        var asset = placement.Asset;
        var shape = asset.Physics is not null ? PartShapes2D.Collision(asset) : new PartShader2D(asset).BoundsShape();
        _motion = asset.Motion?.CreateMotion(placement.Position);
        WorldObject = new(shape);
        WorldObject.Transform.Position = _motion?.Position(0) ?? placement.Position;
        Body = asset.Physics?.CreateBody(physics, WorldObject);
        if (Body is not null)
        {
            Body.EntityId = id;
            Body.CollisionLayer = layer;
            Body.CollisionMask = mask;
            if (asset.Surface is { } surface)
            {
                var settings = PlatformCollisionRules2D.Attach(physics).Surface(Body);
                settings.OneWaySurfaceNormal = surface.OneWayNormal;
                settings.OneWaySlop = surface.OneWaySlop;
            }
        }
    }

    public EntityId2D Id { get; }
    public ObjectPlacement2D Placement { get; }
    public SpatialObject2D WorldObject { get; }
    public RigidBody2D? Body { get; }
    public bool IsEnabled { get; private set; } = true;

    public void Update(float dt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.NonNegative(dt);
        if (!IsEnabled) return;
        _time += dt;
        if (_motion is null) return;
        var target = _motion.Position(_time);
        if (Body is not null) Body.LinearVelocity = dt == 0 ? Vector2.Zero : (target - WorldObject.Transform.Position) / dt;
        else WorldObject.Transform.Position = target;
    }

    public void SetSimulationEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsEnabled == enabled) return;
        if (Body is not null)
        {
            if (!enabled) { _pausedVelocity = Body.LinearVelocity; _pausedSpin = Body.AngularVelocity; }
            Body.IsCollider = enabled;
            Body.MotionType = enabled ? Placement.Asset.Physics!.MotionType : BodyMotionType2D.Static;
            Body.LinearVelocity = enabled ? _pausedVelocity : Vector2.Zero;
            Body.AngularVelocity = enabled ? _pausedSpin : 0;
        }
        IsEnabled = enabled;
    }

    public ObjectDefinition2D CaptureDefinition() => new(Id, Placement.Id, Placement.Asset);
    public ObjectState2D CaptureState() => new(Id, WorldObject.Transform.Position, WorldObject.Transform.Rotation)
    {
        LinearVelocity = IsEnabled ? Body?.LinearVelocity ?? Vector2.Zero : _pausedVelocity,
        AngularVelocity = IsEnabled ? Body?.AngularVelocity ?? 0 : _pausedSpin,
        Time = _time, IsEnabled = IsEnabled
    };

    public void RestoreState(ObjectState2D state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIf(state.Id != Id, "State belongs to another object.", nameof(state));
        ArgGuard.Finite(state.Position); ArgGuard.Finite(state.Rotation);
        ArgGuard.Finite(state.LinearVelocity); ArgGuard.Finite(state.AngularVelocity); ArgGuard.NonNegative(state.Time);
        SetSimulationEnabled(true);
        WorldObject.Transform.Position = state.Position;
        WorldObject.Transform.Rotation = state.Rotation;
        _time = state.Time;
        if (Body is not null) { Body.LinearVelocity = state.LinearVelocity; Body.AngularVelocity = state.AngularVelocity; }
        SetSimulationEnabled(state.IsEnabled);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Body is not null) _physics.RemoveBody(Body);
    }
}
