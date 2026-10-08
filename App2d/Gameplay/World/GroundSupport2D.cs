using App2d.Core.Physics;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>What a body stands on, read from the last physics step's contacts.</summary>
public static class GroundSupport2D
{
    /// <summary>How upward a contact normal must point to count as ground rather than a wall.</summary>
    public const float MinimumNormalY = 0.55f;

    /// <summary>
    /// The velocity of the fastest-rising support touching <paramref name="body"/> from below (a moving platform's; terrain
    /// is still), or null when nothing supports it.
    /// </summary>
    public static Vector2? Velocity(IReadOnlyList<PhysicsContact2D> contacts, PhysicsBody2D body, Func<PhysicsBody2D, bool> canSupport)
    {
        Vector2? support = null;
        foreach (var contact in contacts)
        {
            var other = contact.First == body && contact.Geometry.Normal.Y >= MinimumNormalY ? contact.Second
                : contact.Second == body && -contact.Geometry.Normal.Y >= MinimumNormalY ? contact.First
                : null;
            if (other is not null && canSupport(other) && (support is not { } best || other.LinearVelocity.Y > best.Y))
                support = other.LinearVelocity;
        }
        return support;
    }
}
