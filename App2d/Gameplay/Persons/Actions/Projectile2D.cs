using App2d.Core;
using App2d.Core.Spatial;
using App2d.Core.Validation;

namespace App2d.Gameplay.Persons.Actions;

public sealed class Projectile2D(SpatialObject2D worldObject)
{
    public SpatialObject2D WorldObject { get; } = worldObject;
    public EntityId2D Id { get; private set; }
    public Vector2f Origin { get; private set; }
    public Vector2f Velocity { get; private set; }
    public float RemainingLifetime { get; private set; }
    public bool IsActive => RemainingLifetime > 0f;

    public void Launch(Vector2f position, Vector2f velocity, float lifetime, Vector2f origin, EntityId2D id)
    {
        Ensure.Positive(lifetime);
        Ensure.ThrowIf(!id.IsValid, "A projectile requires a valid entity ID.", nameof(id));

        Id = id;
        Origin = origin;
        WorldObject.Position = position;
        Velocity = velocity;
        RemainingLifetime = lifetime;
    }

    public void Update(float deltaSeconds)
    {
        if (!IsActive)
        {
            return;
        }

        WorldObject.Position += Velocity * deltaSeconds;
        RemainingLifetime = Math.Max(0f, RemainingLifetime - deltaSeconds);
    }

    public void Deactivate()
    {
        RemainingLifetime = 0f;
        Velocity = Vector2f.Zero;
    }
}
