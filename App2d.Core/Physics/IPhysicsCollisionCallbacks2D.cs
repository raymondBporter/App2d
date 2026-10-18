namespace App2d.Core.Physics;

/// <summary>Optional application policy around contact response; the world owns detection and solving.</summary>
public interface IPhysicsCollisionCallbacks2D
{
    /// <summary>
    /// After geometry detection, before recording or resolving this contact. False rejects it, including sensor reporting.
    /// May run more than once per pair as position iterations refresh geometry. Do not add/remove bodies or step here.
    /// </summary>
    bool OnBeforeCollision(PhysicsContact2D contact) => true;

    /// <summary>
    /// After the complete public step, once per accepted pair, including sensors. Bodies have their final step velocities.
    /// Removing bodies here is supported; recursively stepping the same world is not.
    /// </summary>
    void OnAfterCollision(PhysicsContact2D contact) { }
}
