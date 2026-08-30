using App2d.Core;
using App2d.Rendering;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed class Projectile2D(WorldObject2D worldObject)
{
    public WorldObject2D WorldObject { get; } = worldObject;
    public Vector2 Velocity { get; private set; }
    public float RemainingLifetime { get; private set; }
    public bool IsActive => RemainingLifetime > 0f;

    public void Launch(Vector2 position, Vector2 velocity, float lifetime)
    {
        ArgGuard.ThrowIfNotPositive(lifetime);

        WorldObject.Transform.Position = position;
        WorldObject.IsVisible = true;
        Velocity = velocity;
        RemainingLifetime = lifetime;
    }

    public void Update(float deltaSeconds)
    {
        if (!IsActive)
            return;

        WorldObject.Transform.Position += Velocity * deltaSeconds;
        RemainingLifetime = Math.Max(0f, RemainingLifetime - deltaSeconds);
        if (!IsActive)
            WorldObject.IsVisible = false;
    }

    public void Deactivate()
    {
        RemainingLifetime = 0f;
        Velocity = Vector2.Zero;
        WorldObject.IsVisible = false;
    }
}
