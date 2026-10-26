namespace App2d.Gameplay.Combat;

/// <summary>
/// A combatant whose hurt geometry is registered attached colliders in the collision system: the ordinary broad phase finds
/// it, so an attack that reaches only its movement body (or its guard) has not reached it. Every authored entity actor is one.
/// </summary>
public interface IRegisteredHurt2D;
