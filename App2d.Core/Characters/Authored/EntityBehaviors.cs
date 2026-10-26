using App2d.Core.Validation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>The impact kinds a strike may name; the game maps them to its presentation and audio.</summary>
public static class ImpactKinds
{
    public const string Generic = "generic";
    public const string Sword = "sword";
    public static readonly IReadOnlyList<string> All = [Generic, Sword];
}

/// <summary>
/// Damage on contact (P9a): what an accepted contact of one hit group does. Keyed in <see cref="EntityBehaviors.Strikes"/>
/// by the hit group: a hit window's id, or an action's id for the projectile it fires. Damage is an integer, the sound an
/// asset reference; neither lives on the window or the projectile geometry any more.
/// </summary>
public sealed record StrikeDef
{
    public int Damage { get; set; } = 1;
    /// <summary>Played where the contact is accepted; null plays the game's default impact.</summary>
    public string? Sound { get; set; }
    public string Impact { get; set; } = ImpactKinds.Generic;

    public void Validate(string field)
    {
        new Limit(0, 10000).Check(Damage, field + " damage");
        if (Sound is not null)
        {
            AuthoredAsset.RequireId(Sound, field + " sound");
        }

        EntityVocabulary.Require(Impact, ImpactKinds.All, field + " impact");
    }
}

/// <summary>Receiving damage (P9a): the health pool, the mass dividing knockback, and how long a hit shows and staggers.</summary>
public sealed record ReceiveDamageDef
{
    public int Health { get; set; } = 3;
    /// <summary>Divides knockback: a heavy entity barely moves when hit.</summary>
    public float Mass { get; set; } = 1;
    /// <summary>How long the hurt expression and reaction state last after an accepted contact.</summary>
    public float HurtSeconds { get; set; } = .45f;
    /// <summary>The stagger after a hit when the entity has no hit clip (the clip's length otherwise).</summary>
    public float StaggerSeconds { get; set; } = EntityReaction.DefaultStaggerSeconds;

    public void Validate(string field)
    {
        new Limit(1, 10000).Check(Health, field + " health");
        new Limit(.1f, 100).Check(Mass, field + " mass");
        new Limit(0, 10).Check(HurtSeconds, field + " hurtSeconds");
        new Limit(0, 10).Check(StaggerSeconds, field + " staggerSeconds");
    }
}

/// <summary>Guarding (P9a): the event of an action after which the guard is down, and the sound an accepted block plays.</summary>
public sealed record GuardBehaviorDef
{
    public string OpenEvent { get; set; } = "guard-open";
    public string? BlockSound { get; set; } = "shield-block";

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(OpenEvent, field + " openEvent");
        if (BlockSound is not null)
        {
            AuthoredAsset.RequireId(BlockSound, field + " blockSound");
        }
    }
}

/// <summary>
/// The typed gameplay behavior configuration of an entity (entity version 4). Known behaviors are typed, defaulted and
/// validated here; <see cref="Properties"/> is the extensible store for custom authored values, which gameplay reads by
/// name and never parses during combat. Geometry (colliders) and timing (windows) carry none of these.
/// </summary>
public sealed record EntityBehaviors
{
    /// <summary>Damage on contact per hit group: a window id, or an action id for its projectile.</summary>
    public Dictionary<string, StrikeDef> Strikes { get; set; } = [];
    public ReceiveDamageDef Damage { get; set; } = new();
    /// <summary>Present when the entity guards; null otherwise.</summary>
    public GuardBehaviorDef? Guard { get; set; }
    /// <summary>Custom authored properties by name; values are text, read explicitly by the behavior that wants them.</summary>
    public Dictionary<string, string> Properties { get; set; } = [];

    public void Validate(string owner)
    {
        if (Strikes is null || Damage is null || Properties is null)
        {
            throw new InvalidDataException($"{owner} behaviors: collections cannot be null.");
        }

        foreach (var (group, strike) in Strikes)
        {
            AuthoredAsset.RequireId(group, $"{owner} behaviors.strikes key");
            if (strike is null)
            {
                throw new InvalidDataException($"{owner} behaviors.strikes.{group}: null strike.");
            }

            strike.Validate($"{owner} behaviors.strikes.{group}");
        }
        Damage.Validate($"{owner} behaviors.damage");
        Guard?.Validate($"{owner} behaviors.guard");
        foreach (var (key, value) in Properties)
        {
            AuthoredAsset.RequireId(key, $"{owner} behaviors.properties key");
            if (value is null)
            {
                throw new InvalidDataException($"{owner} behaviors.properties.{key}: null value.");
            }
        }
    }
}

/// <summary>Editor metadata for one typed behavior field: where it lives, its kind and range, and what it means.</summary>
public sealed record BehaviorField(string Owner, string Name, string Kind, float Min, float Max, string Help)
{
    /// <summary>The field's words for an inspector: "HurtSeconds" reads "Hurt seconds".</summary>
    public string Label => string.Concat(Name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
}

/// <summary>The typed behavior fields Studio offers, one entry per known property (no reflection, no string parsing).</summary>
public static class BehaviorSchema
{
    public const string Integer = "integer", Seconds = "seconds", Factor = "factor", Sound = "sound", Choice = "choice", EventId = "event";

    public static readonly IReadOnlyList<BehaviorField> Strike =
    [
        new("strike", nameof(StrikeDef.Damage), Integer, 0, 10000, "Health taken by one accepted contact of this hit group."),
        new("strike", nameof(StrikeDef.Sound), Sound, 0, 0, "Played where the contact is accepted; empty plays the default impact."),
        new("strike", nameof(StrikeDef.Impact), Choice, 0, 0, "The impact kind the presentation reacts to."),
    ];

    public static readonly IReadOnlyList<BehaviorField> Damage =
    [
        new("damage", nameof(ReceiveDamageDef.Health), Integer, 1, 10000, "The health pool."),
        new("damage", nameof(ReceiveDamageDef.Mass), Factor, .1f, 100, "Divides knockback: a heavy entity barely moves when hit."),
        new("damage", nameof(ReceiveDamageDef.HurtSeconds), Seconds, 0, 10, "How long the hurt expression and reaction last after a hit."),
        new("damage", nameof(ReceiveDamageDef.StaggerSeconds), Seconds, 0, 10, "The stagger after a hit without a hit clip."),
    ];

    public static readonly IReadOnlyList<BehaviorField> Guard =
    [
        new("guard", nameof(GuardBehaviorDef.OpenEvent), EventId, 0, 0, "The action event after which the guard is down."),
        new("guard", nameof(GuardBehaviorDef.BlockSound), Sound, 0, 0, "Played where a strike is blocked; empty plays nothing."),
    ];
}
