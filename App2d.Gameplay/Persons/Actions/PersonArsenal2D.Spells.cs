using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Player;
using App2d.Gameplay.Combat;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed partial class PersonArsenal2D
{
    private readonly Health2D? _spellHealth;
    private readonly SpellTuning2D _spellTuning;
    private readonly Func<Vector2> _spellPosition;
    private int _energy;
    private double _energyRecharge;
    private float _healElapsed;
    private bool _healing, _healNeedsRelease;
    private bool SpellBusy => _healing || _gun.IsCharging || _gun.CaptureActionState().IsActive;

    public SpellState2D CaptureSpellState() => new(_energy, _spellTuning.MaximumEnergy,
        _spellTuning.ShotCost, _spellTuning.HealCost, _healing,
        _healElapsed / _spellTuning.HealSeconds, _gun.ChargeProgress, _spellTuning.HealSeconds, _spellTuning.ShotChargeSeconds);

    public void SetSpellInput(bool castHeld, bool castPressed, bool healHeld, bool canHeal, bool canCast, float facing)
    {
        var tuning = _spellTuning;
        if (!healHeld) _healNeedsRelease = false;
        if (!healHeld || !canHeal) CancelHealing();
        _gun.SetInput(castHeld, canCast && !_healing && !IsMeleeAttackActive && _energy >= tuning.ShotCost);
        if (castPressed) _gun.Use(facing);
        if (healHeld && canHeal && !_healNeedsRelease && !SpellBusy && !IsMeleeAttackActive &&
            _energy >= tuning.HealCost && _spellHealth is { IsAlive: true } health && health.Current < health.Maximum)
        {
            _healing = true;
            _healElapsed = 0;
            Publish(new HealStarted2D(_spellPosition()));
        }
    }

    public void CancelHealing()
    {
        if (_healing) Publish(new HealCancelled2D(_spellPosition()));
        _healing = false;
        _healElapsed = 0;
    }

    private bool SpendEnergy(int amount)
    {
        if (_energy < amount) return false;
        _energy -= amount;
        return true;
    }

    private void RechargeEnergy(float deltaSeconds)
    {
        // Retain fractional points across fixed ticks, but never bank recharge at full capacity.
        _energyRecharge = Math.Min(_spellTuning.MaximumEnergy - _energy,
            _energyRecharge + (double)deltaSeconds * _spellTuning.EnergyPerSecond);
        var restored = (int)_energyRecharge;
        _energy += restored;
        _energyRecharge -= restored;
    }

    private void UpdateHealing(float deltaSeconds)
    {
        if (!_healing || _spellHealth is not { IsAlive: true } health) return;
        var t = _spellTuning;
        _healElapsed += deltaSeconds;
        if (_healElapsed + .000001f < t.HealSeconds) return;
        if (health.Current >= health.Maximum || !SpendEnergy(t.HealCost)) { CancelHealing(); return; }
        var amount = health.Heal(Math.Max(1, (int)MathF.Round(health.Maximum * t.HealFraction)));
        Publish(new HealCompleted2D(_spellPosition(), amount));
        _healElapsed = 0;
        _healing = false; // A held input starts the next cycle on the next simulation tick.
    }
}
