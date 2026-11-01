using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Text.Json.Serialization;

namespace App2d.Core.Physics;

/// <summary>Serializable body settings. The world creates and owns the live body and collider.</summary>
public sealed record PhysicsBodySettings2D
{
    [JsonConverter(typeof(JsonStringEnumConverter<BodyMotionType2D>))]
    public BodyMotionType2D MotionType { get; set; } = BodyMotionType2D.Dynamic;
    public float Mass { get; set; } = 1f;
    public float MomentOfInertia { get; set; } = 1f;
    public bool FreezeRotation { get; set; } = true;
    public float GravityScale { get; set; } = 1f;
    public float Friction { get; set; }
    public float Restitution { get; set; } = .5f;
    public bool TransfersContactMotion { get; set; }

    public void Validate()
    {
        Ensure.ThrowIf(!Enum.IsDefined(MotionType), "Unknown body motion type.", nameof(MotionType));
        Ensure.Positive(Mass, nameof(Mass));
        Ensure.Positive(MomentOfInertia, nameof(MomentOfInertia));
        Ensure.Finite(GravityScale, nameof(GravityScale));
        Ensure.NonNegative(Friction, nameof(Friction));
        Ensure.InRange(Restitution, 0f, 1f, nameof(Restitution));
    }

    /// <summary>Validates before registering anything. Identity and collision layers belong to the spawning world.</summary>
    public RigidBody2D CreateBody(PhysicsWorld2D world, SpatialObject2D worldObject)
    {
        Ensure.ThrowIfNull(world);
        Ensure.ThrowIfNull(worldObject);
        Validate();
        var body = world.AddBody(worldObject, MotionType);
        ApplyTo(body);
        return body;
    }

    /// <summary>Applies settings to an existing body, including a path-driven kinematic body.</summary>
    public void ApplyTo(RigidBody2D body)
    {
        Ensure.ThrowIfNull(body);
        Validate();
        body.MotionType = MotionType;
        body.Mass = Mass;
        body.MomentOfInertia = MomentOfInertia;
        body.FreezeRotation = FreezeRotation;
        body.GravityScale = GravityScale;
        body.Friction = Friction;
        body.Restitution = Restitution;
        body.TransfersContactMotion = TransfersContactMotion;
    }
}
