using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using System.Collections.Immutable;

namespace App2d.Game.Presentation.World.Presentation;

/// <summary>Code-only tuning for confirmed sword contact. All durations are seconds; simulation always stays live.</summary>
public static class CombatHitstop2D
{
    public const float HoldSeconds = .070f;
    public const float ReleaseSeconds = .025f;
    public const float RecoveryPeak = 2.5f;
    public static readonly ReactionTimeCurve2D Curve = new(0, ReleaseSeconds, RecoveryPeak, HoldSeconds);
}

/// <summary>Delays only the attack pose; new actions and reactions can interrupt it immediately.</summary>
public sealed class PlayerContactHold2D(ReactionTimeCurve2D curve)
{
    private double _contactTime = double.NegativeInfinity;
    private string? _swing;

    public void Present(CombatDamage2D damage, EntityId2D playerId, double time)
    {
        if (damage.Contact is not { Kind: CombatImpactKind2D.Sword } contact || contact.AttackerId != playerId) return;
        _contactTime = time; _swing = null;
    }

    public void Reset() { _contactTime = double.NegativeInfinity; _swing = null; }

    public PersonFrame Sample(PersonFrameHistory2D history, double time, PersonFrame live)
    {
        var elapsed = time - _contactTime;
        if (elapsed < 0 || elapsed >= curve.Duration) return live;
        var locomotion = live.Key is PersonMoves.Walk or PersonMoves.Run or PersonMoves.Jump or PersonMoves.Fall;
        var swing = live.PropClip.Markers.Any(m => m.Id == PersonLoadout.DrawMarker);
        var recovery = live.PropClip.Markers.Any(m => m.Id == PersonLoadout.SheatheMarker);
        if (live.Gear != PersonGear.Sword || (!locomotion && !swing && !recovery) || (swing && _swing is not null && _swing != live.PropClip.Id))
        { Reset(); return live; }
        _swing ??= live.PropClip.Id;
        var delayed = history.Sample(_contactTime + curve.Sample(elapsed));
        return locomotion
            ? live with { Overlay = new(delayed.PropClip, delayed.PropSeconds, PersonLoadout.SwordUpperBody) }
            : delayed;
    }
}

/// <summary>Samples the contacted enemy's reaction at a delayed time, retaining its live root and facing.</summary>
public sealed class EnemyContactHold2D(ReactionTimeCurve2D curve)
{
    private readonly Dictionary<EntityId2D, Reaction> _reactions = [];

    public void Present(CombatDamage2D damage)
    {
        if (damage.Contact is not { Kind: CombatImpactKind2D.Sword } contact) return;
        _reactions[damage.TargetId] = new(damage.WasKilled ? EntityControllers.Death : EntityControllers.Hit, contact.Direction.X);
    }

    public void Reset() => _reactions.Clear();

    public ImmutableArray<EnemyState2D> Sample(ImmutableArray<EnemyState2D> states)
    {
        if (_reactions.Count == 0) return states;
        var present = states.Select(s => s.Id).ToHashSet();
        foreach (var id in _reactions.Keys.Where(id => !present.Contains(id)).ToArray()) _reactions.Remove(id);
        return [.. states.Select(state =>
        {
            if (!_reactions.TryGetValue(state.Id, out var reaction)) return state;
            if (state.ActionId != reaction.Role || !state.IsEnabled || state.ActionSeconds >= curve.Duration ||
                state.AuthoredEntity is not { } entity || state.AuthoredPose is not { } live || entity.Clip(reaction.Role) is not { } clip)
            { _reactions.Remove(state.Id); return state; }
            reaction.Reverse ??= reaction.Direction * live.Facing > 0;
            var delayed = reaction.Hold.Evaluate(entity.Model, clip, curve.Sample(state.ActionSeconds), false,
                live.Position, live.Facing, new(reaction.Role == EntityControllers.Death ? "knocked-out" : "hurt")
                { ReverseHorizontalMotion = reaction.Reverse.Value });
            return state with { AuthoredPose = delayed };
        })];
    }

    private sealed class Reaction(string role, float direction)
    {
        public string Role { get; } = role;
        public float Direction { get; } = direction;
        public bool? Reverse { get; set; }
        public ContactHold Hold { get; } = new();
    }
}
