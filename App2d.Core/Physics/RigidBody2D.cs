using App2d.Core.Collision;
using App2d.Core.Geometry.Operations.MassProperties;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics;

public sealed class RigidBody2D
{
    private float _mass = 1f;
    private float _momentOfInertia = 1f;
    private float _friction;

    private BodyMotionType2D _motionType;

    internal RigidBody2D(SpatialObject2D worldObject, BodyMotionType2D motionType, Collider2D collider)
    {
        WorldObject = ArgGuard.RequireNotNull(worldObject);
        Collider = ArgGuard.RequireNotNull(collider);
        _motionType = motionType;
        Collider.Mobility = ToColliderMobility(motionType);
        PreviousPosition = worldObject.Transform.Position;
        PreviousRotation = worldObject.Transform.Rotation;
    }

    public SpatialObject2D WorldObject { get; }
    public Collider2D Collider { get; }
    public BodyMotionType2D MotionType
    {
        get => _motionType;
        set
        {
            _motionType = value;
            Collider.Mobility = ToColliderMobility(value);
        }
    }
    /// <summary>Velocity of the center of mass in world units per second.</summary>
    public Vector2 LinearVelocity { get; set; }
    public float AngularVelocity { get; set; }

    /// <summary>Defaults to the transform origin; shape-derived mass methods set the actual local centroid.</summary>
    public Vector2 LocalCenterOfMass { get; private set; }
    public Vector2 WorldCenterOfMass => LocalCenterOfMass == Vector2.Zero
        ? WorldObject.Transform.Position
        : Vector2.Transform(LocalCenterOfMass, WorldObject.Transform.Matrix);

    /// <summary>
    /// When true (the default) contacts and constraints never impart spin; the body only
    /// rotates when gameplay sets AngularVelocity or applies torque directly.
    /// </summary>
    public bool FreezeRotation { get; set; } = true;
    public Vector2 AccumulatedForce { get; private set; }
    public float AccumulatedTorque { get; private set; }
    public Vector2 PreviousPosition { get; internal set; }
    public float PreviousRotation { get; internal set; }
    internal Vector2 LastStepLinearVelocity { get; set; }
    public float GravityScale { get; set; } = 1f;
    public float Restitution { get; set; } = 0.5f;

    /// <summary>Coulomb friction coefficient; 0 (the default) disables friction.</summary>
    public float Friction
    {
        get => _friction;
        set
        {
            ArgGuard.NonNegative(value, nameof(Friction));
            _friction = value;
        }
    }
    public bool IsCollider
    {
        get => Collider.IsEnabled;
        set => Collider.IsEnabled = value;
    }
    public bool IsSensor
    {
        get => Collider.IsSensor;
        set => Collider.IsSensor = value;
    }
    /// <summary>
    /// When kinematic, transfer this body's tangential step displacement and
    /// supporting normal velocity to dynamic bodies from the previous contact step.
    /// </summary>
    public bool TransfersContactMotion { get; set; }

    public uint CollisionLayer
    {
        get => Collider.CollisionLayer;
        set => Collider.CollisionLayer = value;
    }

    public uint CollisionMask
    {
        get => Collider.CollisionMask;
        set => Collider.CollisionMask = value;
    }

    /// <summary>Owning gameplay entity; collision queries expose the same ID.</summary>
    public EntityId2D EntityId
    {
        get => Collider.EntityId;
        set => Collider.EntityId = value;
    }

    public float Mass
    {
        get => _mass;
        set
        {
            ArgGuard.Positive(value, nameof(Mass));
            _mass = value;
        }
    }

    /// <summary>Inertia about the center of mass, perpendicular to the plane, in mass * world units squared.</summary>
    public float MomentOfInertia
    {
        get => _momentOfInertia;
        set
        {
            ArgGuard.Positive(value, nameof(MomentOfInertia));
            _momentOfInertia = value;
        }
    }

    public float InverseMass => MotionType == BodyMotionType2D.Dynamic ? 1f / Mass : 0f;
    public float InverseInertia => MotionType == BodyMotionType2D.Dynamic ? 1f / MomentOfInertia : 0f;
    public float EffectiveInverseInertia => FreezeRotation ? 0f : InverseInertia;

    /// <summary>
    /// Sets uniform-solid mass, centroid and inertia from density per square world unit.
    /// Includes the current uniform scale. Recalculate after changing scale; explicit legacy values remain supported.
    /// </summary>
    public void SetMassFromDensity(float density = 1f)
    {
        ArgGuard.Positive(density);
        var properties = ShapeMassProperties2D.Calculate(WorldObject.Shape, density);
        var scale = (double)WorldObject.CollisionPose.Scale;
        ApplyMassProperties(properties, properties.Mass * scale * scale,
            properties.MomentOfInertia * scale * scale * scale * scale);
    }

    /// <summary>Sets a chosen total mass and derives the uniform-solid centroid and inertia at the current uniform scale.</summary>
    public void SetMassFromShape(float mass)
    {
        ArgGuard.Positive(mass);
        var properties = ShapeMassProperties2D.Calculate(WorldObject.Shape).WithMass(mass);
        var scale = (double)WorldObject.CollisionPose.Scale;
        ApplyMassProperties(properties, mass, properties.MomentOfInertia * scale * scale);
    }

    private void ApplyMassProperties(MassProperties2D properties, double mass, double inertia)
    {
        // Validate every conversion before changing the body, so unsupported or oversized shapes leave it intact.
        var nextMass = ArgGuard.Positive((float)mass);
        var nextInertia = ArgGuard.Positive((float)inertia);
        var nextCenter = properties.CenterOfMass.ToVector2();
        ArgGuard.Finite(nextCenter);
        var nextWorldCenter = Vector2.Transform(nextCenter, WorldObject.Transform.Matrix);
        ArgGuard.Finite(nextWorldCenter);
        // Preserve the velocity field when the reference point changes on an already spinning body.
        var offset = nextWorldCenter - WorldCenterOfMass;
        var nextVelocity = LinearVelocity + AngularVelocity * new Vector2(-offset.Y, offset.X);
        ArgGuard.Finite(nextVelocity);
        Mass = nextMass;
        MomentOfInertia = nextInertia;
        LocalCenterOfMass = nextCenter;
        LinearVelocity = nextVelocity;
    }

    public void AddForce(Vector2 force) => AccumulatedForce += force;
    public void AddTorque(float torque) => AccumulatedTorque += torque;

    public void AddImpulse(Vector2 impulse)
    {
        if (MotionType == BodyMotionType2D.Dynamic)
        {
            LinearVelocity += impulse * InverseMass;
        }
    }

    public void AddAngularImpulse(float impulse)
    {
        if (MotionType == BodyMotionType2D.Dynamic)
        {
            AngularVelocity += impulse * InverseInertia;
        }
    }

    public void ClearAccumulators()
    {
        AccumulatedForce = Vector2.Zero;
        AccumulatedTorque = 0f;
    }

    /// <summary>Enabled/layer-mask compatibility only. Use PhysicsWorld2D.CanCollide to include world pair policies.</summary>
    public bool CanCollideWith(RigidBody2D other) => Collider.CanCollideWith(other.Collider);

    private static ColliderMobility2D ToColliderMobility(BodyMotionType2D motionType) =>
        motionType == BodyMotionType2D.Static
            ? ColliderMobility2D.Static
            : ColliderMobility2D.Dynamic;
}
