using System.Text.Json;
using System.Text.RegularExpressions;

namespace App2d.Core.Characters;

/// <summary>Portable authored type. Runtime instances own clocks, health and controller state.</summary>
public sealed record EntityTypeDefinition
{
    public const string FormatId = "app2d-entity-type";
    public static class Limits
    {
        public static readonly Limit Health = new Limit(1, 10000).Soft(1, 100), MoveSpeed = new Limit(0, 15).Soft(0, 8), JumpSpeed = new Limit(0, 20).Soft(0, 14),
            PreferredRange = new Limit(.1f, 12).Soft(.1f, 8), Cooldown = new Limit(0, 10).Soft(0, 3), GroundOffset = new Limit(-3, 3).Soft(-2, 2);
    }
    public string Format { get; init; } = FormatId;
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
    public static JsonSerializerOptions JsonOptions => AuthoredJson.Options;
    public EntityTypeDefinition Copy() => JsonSerializer.Deserialize<EntityTypeDefinition>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;
    public static EntityTypeDefinition Load(string path) => JsonSerializer.Deserialize<EntityTypeDefinition>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Empty entity type.");
    public void Save(string path)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions)); File.Move(temporary, path, true);
    }
    public void Validate(PointLibrary library)
    {
        if (Format != FormatId || Version != 1) throw new InvalidDataException($"Unsupported entity type format '{Format}' version {Version}.");
        if (string.IsNullOrWhiteSpace(Id) || !Regex.IsMatch(Id, "^[a-z][a-z0-9._-]{0,63}$")) throw new InvalidDataException($"id '{Id}' must be a stable lowercase identifier (letters, digits, '.', '_' or '-').");
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("name is required.");
        if (Library != library.Id) throw new InvalidDataException($"library '{Library}' does not match the loaded library '{library.Id}'.");
        EntityVocabulary.Require(library.Anatomy, EntityVocabulary.EntityAnatomies, "library anatomy");
        if (Appearance is null || Movement is null || Regions is null || Actions is null) throw new InvalidDataException("Entity definition is incomplete: appearance, movement, regions and actions are required.");
        Appearance.Validate();
        Limits.Health.Check(Health, "health"); Limits.MoveSpeed.Check(MoveSpeed, "moveSpeed"); Limits.JumpSpeed.Check(JumpSpeed, "jumpSpeed");
        Limits.PreferredRange.Check(PreferredRange, "preferredRange"); Limits.Cooldown.Check(Cooldown, "cooldown"); Limits.GroundOffset.Check(GroundOffset, "groundOffset");
        EntityVocabulary.Require(Behavior, EntityVocabulary.Behaviors, "behavior");
        Movement.Validate();
        foreach (var (id, region) in Regions)
        {
            EntityVocabulary.Require(id, EntityVocabulary.Regions, "region");
            if (region is null) throw new InvalidDataException($"regions.{id} is empty.");
            region.Validate("regions." + id);
        }
        foreach (var required in EntityVocabulary.RequiredActions) if (!Actions.ContainsKey(required)) throw new InvalidDataException("Required action missing: " + required);
        foreach (var (id, action) in Actions)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Action IDs cannot be blank.");
            if (action is null) throw new InvalidDataException($"actions.{id} is empty.");
            action.Validate(library, "actions." + id);
        }
    }
}

public sealed record MovementShape
{
    public static class Limits
    {
        public static readonly Limit Width = new Limit(.1f, 8).Soft(.1f, 4), Height = new Limit(.1f, 8).Soft(.1f, 4), OffsetX = new Limit(-4, 4).Soft(-2, 2);
    }
    public float Width { get; set; } = .55f;
    public float Height { get; set; } = 1.9f;
    public float OffsetX { get; set; }
    public void Validate() { Limits.Width.Check(Width, "movement.width"); Limits.Height.Check(Height, "movement.height"); Limits.OffsetX.Check(OffsetX, "movement.offsetX"); }
}

public sealed record RegionSettings
{
    public static class Limits
    {
        public static readonly Limit Padding = new(0, .5f), ScaleX = new(.1f, 3), ScaleY = new(.1f, 3),
            OffsetX = new Limit(-4, 4).Soft(-2, 2), OffsetY = new Limit(-4, 4).Soft(-2, 2), Width = new Limit(.02f, 8).Soft(.05f, 4), Height = new Limit(.02f, 8).Soft(.05f, 4);
    }
    public string Mode { get; set; } = "anatomy";
    public float Padding { get; set; } = .04f;
    public float ScaleX { get; set; } = 1;
    public float ScaleY { get; set; } = 1;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float Width { get; set; } = .6f;
    public float Height { get; set; } = .6f;
    public void Validate(string field = "region")
    {
        EntityVocabulary.Require(Mode, EntityVocabulary.RegionModes, field + ".mode");
        Limits.Padding.Check(Padding, field + ".padding"); Limits.ScaleX.Check(ScaleX, field + ".scaleX"); Limits.ScaleY.Check(ScaleY, field + ".scaleY");
        Limits.OffsetX.Check(OffsetX, field + ".offsetX"); Limits.OffsetY.Check(OffsetY, field + ".offsetY"); Limits.Width.Check(Width, field + ".width"); Limits.Height.Check(Height, field + ".height");
    }
}

public sealed record EntityAction
{
    public static class Limits
    {
        public static readonly Limit Duration = new Limit(.05f, 30).Soft(.05f, 5), ClipPhase = new(0, 1), Contact = new(.01f, .99f), ActiveStart = new(0, .99f), ActiveEnd = new(0, 1),
            Damage = new Limit(0, 1000).Soft(0, 20), HitX = new Limit(-5, 5).Soft(-3, 3), HitY = new Limit(-5, 5).Soft(-3, 3),
            HitWidth = new Limit(.02f, 8).Soft(.05f, 4), HitHeight = new Limit(.02f, 8).Soft(.05f, 4), ProjectileSpeed = new Limit(.1f, 30).Soft(1, 20), CueTime = new(0, 1);
    }
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
    public void Validate(PointLibrary library, string field = "action")
    {
        if (!library.Clips.ContainsKey(Clip)) throw new InvalidDataException($"{field}.clip '{Clip}' is not in library '{library.Id}'.");
        Limits.Duration.Check(Duration, field + ".duration");
        Limits.ClipPhase.Check(ClipStart, field + ".clipStart"); Limits.ClipPhase.Check(ClipEnd, field + ".clipEnd"); Limits.ClipPhase.Check(ClipContact, field + ".clipContact");
        if (ClipEnd < ClipStart || ClipContact < ClipStart || ClipContact > ClipEnd) throw new InvalidDataException($"{field}: clipStart <= clipContact <= clipEnd is required; found {ClipStart}, {ClipContact}, {ClipEnd}.");
        Limits.Contact.Check(Contact, field + ".contact");
        Limits.ActiveStart.Check(ActiveStart, field + ".activeStart"); Limits.ActiveEnd.Check(ActiveEnd, field + ".activeEnd");
        if (ActiveEnd <= ActiveStart) throw new InvalidDataException($"{field}.activeEnd ({ActiveEnd}) must be after activeStart ({ActiveStart}).");
        Limits.Damage.Check(Damage, field + ".damage");
        Limits.HitX.Check(HitX, field + ".hitX"); Limits.HitY.Check(HitY, field + ".hitY"); Limits.HitWidth.Check(HitWidth, field + ".hitWidth"); Limits.HitHeight.Check(HitHeight, field + ".hitHeight");
        Limits.ProjectileSpeed.Check(ProjectileSpeed, field + ".projectileSpeed"); Limits.CueTime.Check(CueTime, field + ".cueTime");
        EntityVocabulary.Require(AttackKind, EntityVocabulary.AttackKinds, field + ".attackKind");
        EntityVocabulary.Require(Attachment, EntityVocabulary.Attachments, field + ".attachment");
        if (AttackKind != "none" && Loop) throw new InvalidDataException($"{field}: attacks must be one-shot actions; repetition is controlled by the type's cooldown.");
        EntityVocabulary.Require(Weapon, EntityVocabulary.ActionWeapons, field + ".weapon");
        EntityVocabulary.Require(Cue, EntityVocabulary.SoundCues, field + ".cue");
        EntityVocabulary.Require(ImpactCue, EntityVocabulary.SoundCues, field + ".impactCue");
    }
}
