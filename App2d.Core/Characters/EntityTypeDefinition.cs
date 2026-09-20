using System.Text.Json;
using System.Text.RegularExpressions;

namespace App2d.Core.Characters;

/// <summary>Portable authored type. Runtime instances own clocks, health and controller state.</summary>
public sealed record EntityTypeDefinition
{
    public string Format { get; init; } = "app2d-entity-type";
    public int Version { get; init; } = 1;
    public string Id { get; set; } = "new-entity";
    public string Name { get; set; } = "New entity";
    public string Library { get; set; } = "person";
    public CharacterAppearance Appearance { get; set; } = new();
    public int Health { get; set; } = 10;
    public float MoveSpeed { get; set; } = 2.5f;
    public float JumpSpeed { get; set; } = 7;
    public string Behavior { get; set; } = "melee";
    public float PreferredRange { get; set; } = 1.1f;
    public float Cooldown { get; set; } = .6f;
    public float GroundOffset { get; set; }
    public MovementShape Movement { get; set; } = new();
    public Dictionary<string, RegionSettings> Regions { get; set; } = new() { ["body"] = new(), ["head"] = new(), ["legs"] = new(), ["arms"] = new() { Mode = "disabled" } };
    public Dictionary<string, EntityAction> Actions { get; set; } = [];
    public string Notes { get; set; } = "";
    public static JsonSerializerOptions JsonOptions { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
    public EntityTypeDefinition Copy() => JsonSerializer.Deserialize<EntityTypeDefinition>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;
    public static EntityTypeDefinition Load(string path) => JsonSerializer.Deserialize<EntityTypeDefinition>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Empty entity type.");
    public void Save(string path)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions)); File.Move(temporary, path, true);
    }
    public void Validate(PointLibrary library)
    {
        if (Format != "app2d-entity-type" || Version != 1) throw new InvalidDataException("Unsupported entity type format.");
        if (string.IsNullOrWhiteSpace(Id) || !Regex.IsMatch(Id, "^[a-z][a-z0-9._-]{0,63}$") || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Use a name and a stable lowercase entity ID.");
        if (Library != library.Id || library.Anatomy is not ("person" or "hound")) throw new InvalidDataException("Entity types currently support Person and Quadruped.");
        if (Appearance is null || Movement is null || Regions is null || Actions is null) throw new InvalidDataException("Entity definition is incomplete.");
        Appearance.Validate();
        Range(Health, 1, 10000); Range(MoveSpeed, 0, 15); Range(JumpSpeed, 0, 20); Range(PreferredRange, .1f, 12); Range(Cooldown, 0, 10); Range(GroundOffset, -3, 3);
        if (Behavior is not ("player" or "melee" or "ranged" or "passive")) throw new InvalidDataException("Unknown controller behavior.");
        Movement.Validate();
        foreach (var (id, region) in Regions)
        {
            if (id is not ("body" or "head" or "legs" or "arms") || region is null) throw new InvalidDataException("Unknown collision region.");
            region.Validate();
        }
        foreach (var required in new[] { "idle", "walk", "attack", "hit", "death" }) if (!Actions.ContainsKey(required)) throw new InvalidDataException("Required action missing: " + required);
        foreach (var (id, action) in Actions)
        {
            if (string.IsNullOrWhiteSpace(id) || action is null) throw new InvalidDataException("Invalid action ID.");
            action.Validate(library);
        }
    }
    internal static void Range(float value, float min, float max)
    { if (!float.IsFinite(value) || value < min || value > max) throw new InvalidDataException($"Entity value must be between {min} and {max}."); }
}

public sealed record MovementShape
{
    public float Width { get; set; } = .55f;
    public float Height { get; set; } = 1.9f;
    public float OffsetX { get; set; }
    public void Validate() { EntityTypeDefinition.Range(Width, .1f, 8); EntityTypeDefinition.Range(Height, .1f, 8); EntityTypeDefinition.Range(OffsetX, -4, 4); }
}

public sealed record RegionSettings
{
    public string Mode { get; set; } = "anatomy";
    public float Padding { get; set; } = .04f;
    public float ScaleX { get; set; } = 1;
    public float ScaleY { get; set; } = 1;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float Width { get; set; } = .6f;
    public float Height { get; set; } = .6f;
    public void Validate()
    {
        if (Mode is not ("anatomy" or "custom" or "disabled")) throw new InvalidDataException("Unknown collision region mode.");
        EntityTypeDefinition.Range(Padding, 0, .5f); EntityTypeDefinition.Range(ScaleX, .1f, 3); EntityTypeDefinition.Range(ScaleY, .1f, 3);
        EntityTypeDefinition.Range(OffsetX, -4, 4); EntityTypeDefinition.Range(OffsetY, -4, 4); EntityTypeDefinition.Range(Width, .02f, 8); EntityTypeDefinition.Range(Height, .02f, 8);
    }
}

public sealed record EntityAction
{
    public string Clip { get; set; } = "idle";
    public float Duration { get; set; } = 1;
    public bool Loop { get; set; }
    public float ClipStart { get; set; }
    public float ClipEnd { get; set; } = 1;
    public float Contact { get; set; } = .5f;
    public float ClipContact { get; set; } = .5f;
    public bool RemoveTravel { get; set; } = true;
    public float ActiveStart { get; set; } = .4f;
    public float ActiveEnd { get; set; } = .65f;
    public int Damage { get; set; }
    public string AttackKind { get; set; } = "none";
    public string Attachment { get; set; } = "root";
    public float HitX { get; set; } = .65f;
    public float HitY { get; set; } = 1;
    public float HitWidth { get; set; } = 1;
    public float HitHeight { get; set; } = .7f;
    public float ProjectileSpeed { get; set; } = 8;
    public string Weapon { get; set; } = "inherit";
    public string Cue { get; set; } = "none";
    public float CueTime { get; set; } = .35f;
    public string ImpactCue { get; set; } = "hit";
    public bool Placeholder { get; set; }
    public string Notes { get; set; } = "";
    public float Phase(double seconds) => Loop ? (float)(seconds / Duration % 1) : Math.Clamp((float)(seconds / Duration), 0, 1);
    public double ClipTime(PointClip clip, double seconds)
    {
        var phase = Phase(seconds);
        var normalized = phase <= Contact ? ClipStart + (ClipContact - ClipStart) * phase / Contact : ClipContact + (ClipEnd - ClipContact) * (phase - Contact) / (1 - Contact);
        return normalized * clip.Duration;
    }
    public bool Active(double seconds) { var phase = Phase(seconds); return AttackKind != "none" && phase >= ActiveStart && phase < ActiveEnd && (Loop || seconds < Duration); }
    public void Validate(PointLibrary library)
    {
        if (!library.Clips.ContainsKey(Clip)) throw new InvalidDataException("Action clip is missing: " + Clip);
        EntityTypeDefinition.Range(Duration, .05f, 30); EntityTypeDefinition.Range(ClipStart, 0, 1); EntityTypeDefinition.Range(ClipEnd, ClipStart, 1);
        EntityTypeDefinition.Range(Contact, .01f, .99f); EntityTypeDefinition.Range(ClipContact, ClipStart, ClipEnd);
        EntityTypeDefinition.Range(ActiveStart, 0, .99f); EntityTypeDefinition.Range(ActiveEnd, ActiveStart + .001f, 1); EntityTypeDefinition.Range(Damage, 0, 1000);
        EntityTypeDefinition.Range(HitX, -5, 5); EntityTypeDefinition.Range(HitY, -5, 5); EntityTypeDefinition.Range(HitWidth, .02f, 8); EntityTypeDefinition.Range(HitHeight, .02f, 8); EntityTypeDefinition.Range(ProjectileSpeed, .1f, 30); EntityTypeDefinition.Range(CueTime, 0, 1);
        if (AttackKind is not ("none" or "melee" or "projectile") || Attachment is not ("root" or "head" or "hand" or "muzzle")) throw new InvalidDataException("Unknown action geometry binding.");
        if (AttackKind != "none" && Loop) throw new InvalidDataException("Attacks must be one-shot actions; repetition is controlled by the action cooldown.");
        if (Weapon is not ("inherit" or "none" or "sword" or "rapier" or "mace" or "hammer" or "pistol")) throw new InvalidDataException("Unknown action weapon.");
        foreach (var cue in new[] { Cue, ImpactCue }) if (cue is not ("none" or "swing" or "shot" or "hit" or "heavy" or "bite")) throw new InvalidDataException("Unknown sound cue.");
    }
}
