using App2d.Core;

namespace App2d.Gameplay.Combat;

/// <summary>
/// Session-owned lookup from runtime identity to the current gameplay object.
/// Registration is independent of alive/streaming state. Unregister when an
/// entity leaves the session, not when it dies or is temporarily disabled.
/// </summary>
public sealed class CombatantRegistry2D
{
    private readonly Dictionary<EntityId2D, ICombatant2D> _combatants = [];

    public void Register(ICombatant2D combatant)
    {
        ArgGuard.ThrowIfNull(combatant);
        StateGuard.ThrowIf(!combatant.Id.IsValid, "A combatant must have a valid entity ID.");
        StateGuard.ThrowIf(combatant.Body.EntityId != combatant.Id,
            "The body must identify its owning combatant.");
        StateGuard.ThrowIf(!_combatants.TryAdd(combatant.Id, combatant),
            "The combatant ID is already registered.");
    }

    public ICombatant2D? Find(EntityId2D id) => _combatants.GetValueOrDefault(id);

    public bool Unregister(EntityId2D id) => _combatants.Remove(id);
}
