namespace App2d.Core.Characters.Authored;

/// <summary>Game-specific opt-ins. A plain Piece has no gameplay requirements.</summary>
public sealed record PieceGameSettings
{
    public EquipmentSettings? Equipment { get; set; }
}

public static class PersonEquipmentSlots
{
    public const string Shoes = "shoes", Gloves = "gloves", Shins = "shins";
    public static readonly string[] All = [Shoes, Gloves, Shins];
}

/// <summary>One owned item occupies one standard Person slot and supplies artwork for both sides.</summary>
public sealed record EquipmentSettings
{
    public string Slot { get; set; } = "";
    public bool Replace { get; set; }
    public bool MirrorRight { get; set; } = true;

    public void Validate()
    {
        if (!PersonEquipmentSlots.All.Contains(Slot, StringComparer.Ordinal))
            throw new InvalidDataException($"Unknown Person equipment slot '{Slot}'; choose {string.Join(", ", PersonEquipmentSlots.All)}.");
    }
}
