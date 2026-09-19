using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed partial class Projectile2D(SpatialObject2D worldObject)
{
    public SpatialObject2D WorldObject { get; } = worldObject;
    public EntityId2D Id { get; private set; }
    public Vector2 Origin { get; private set; }
    public Vector2 Velocity { get; private set; }
    public float RemainingLifetime { get; private set; }
    public bool IsActive => RemainingLifetime > 0f;

    public void Launch(Vector2 position, Vector2 velocity, float lifetime, Vector2 origin, EntityId2D id)
    {
        ArgGuard.ThrowIfNotPositive(lifetime);
        if (!id.IsValid)
            throw new ArgumentException("A projectile requires a valid entity ID.", nameof(id));

        Id = id;
        Origin = origin;
        WorldObject.Transform.Position = position;
        Velocity = velocity;
        RemainingLifetime = lifetime;
    }

    public void Update(float deltaSeconds)
    {
        if (!IsActive)
            return;

        WorldObject.Transform.Position += Velocity * deltaSeconds;
        RemainingLifetime = Math.Max(0f, RemainingLifetime - deltaSeconds);
    }

    public void Deactivate()
    {
        RemainingLifetime = 0f;
        Velocity = Vector2.Zero;
    }
}
