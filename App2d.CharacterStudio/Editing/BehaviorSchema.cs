using App2d.Core.Characters.Authored;

namespace App2d.CharacterStudio.Editing;

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
        new("damage", nameof(ReceiveDamageDef.KnockbackResistance), Factor, .1f, 100, "Divides knockback: a heavy entity barely moves when hit."),
        new("damage", nameof(ReceiveDamageDef.HurtSeconds), Seconds, 0, 10, "How long the hurt expression and reaction last after a hit."),
        new("damage", nameof(ReceiveDamageDef.StaggerSeconds), Seconds, 0, 10, "The stagger after a hit without a hit clip."),
    ];

    public static readonly IReadOnlyList<BehaviorField> Guard =
    [
        new("guard", nameof(GuardBehaviorDef.OpenEvent), EventId, 0, 0, "The action event after which the guard is down."),
        new("guard", nameof(GuardBehaviorDef.BlockSound), Sound, 0, 0, "Played where a strike is blocked; empty plays nothing."),
    ];
}
