using System.Text.Json;
using App2d.Core.Characters;
using App2d.Rendering.Characters;

namespace App2d.CharacterStudio;

internal sealed record LibraryEntry(string Id, string Label, string Anatomy, string Path, int ClipCount, int Bytes);
internal sealed record StudioPreset(int Version, string Library, string Clip, CharacterAppearance Appearance, Dictionary<string, string> Bindings);

/// <summary>One editable character: a shared motion library plus the appearance, bindings or entity type being authored. Undo is snapshot based, so edits mutate the records directly and are grouped by widget activity.</summary>
internal sealed class StudioDocument
{
    private readonly Stack<string> _undo = [], _redo = [];
    private string? _editStart;
    private string _observed = "";
    private sealed record Snapshot(CharacterAppearance Appearance, EntityTypeDefinition? Entity, Dictionary<string, string> Bindings);
    public PointLibrary Library { get; }
    public PointPlayback Playback { get; }
    public CharacterGeometry Geometry { get; }
    public CharacterAppearance Appearance { get; private set; }
    public EntityTypeDefinition? Entity { get; private set; }
    public EntityPose? EntityPose { get; private set; }
    public Dictionary<string, string> Bindings { get; private set; } = [];
    public bool Dirty { get; private set; }
    public string? FilePath { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public StudioDocument(PointLibrary library)
    {
        Library = library; Geometry = new(Library); Appearance = Defaults(Library);
        Playback = new(Library, Library.Clips.Values.FirstOrDefault(c => c.Label is "Walk" or "Flying Idle")?.Id ?? Library.Clips.Keys.First());
        _observed = Capture();
    }
    public static CharacterAppearance Defaults(PointLibrary library)
    {
        var look = new CharacterAppearance();
        switch (library.Anatomy)
        {
            case "person": look.BladeLength = library.Drawing.GetProperty("weaponLength").GetSingle(); look.Face = "relaxed"; break;
            case "hound": look.Yaw = 15; look.Head = 1.1f; look.HeadRoundness = .35f; look.Body = .32f; look.Ink = "#1c262b"; look.Fill = "#fcfaed"; look.Face = "happy"; break;
            case "monster": look.Ink = "#1f262b"; look.Face = "happy"; break;
            case "inventory":
                var d = library.Drawing.GetProperty("defaults"); look.Head = d.GetProperty("head").GetSingle(); look.Body = d.GetProperty("body").GetSingle();
                look.Neck = d.GetProperty("neck").GetSingle(); look.Tail = d.GetProperty("tail").GetSingle(); look.Softness = d.GetProperty("softness").GetSingle();
                look.Yaw = library.Drawing.GetProperty("defaultYaw").GetSingle(); look.Ink = "#1a2126";
                look.Fill = library.Drawing.GetProperty("pack").GetString() == "dinosaurs" ? "#adc99c" : "#d1b5e0"; break;
        }
        return look;
    }
    /// <summary>Call once per frame after widgets ran. While a widget is active the change accumulates; when it releases, the whole drag becomes one undo step.</summary>
    public void RecordEdit(bool active)
    {
        var current = Capture();
        if (current != _observed) { _editStart ??= _observed; Dirty = true; _observed = current; }
        if (!active && _editStart is { } start) { _undo.Push(start); _redo.Clear(); _editStart = null; }
    }
    private string Capture() => JsonSerializer.Serialize(new Snapshot(Appearance, Entity, Bindings), AuthoredJson.Options);
    private void Restore(string state)
    {
        var snapshot = JsonSerializer.Deserialize<Snapshot>(state, AuthoredJson.Options)!;
        Entity = snapshot.Entity; Appearance = Entity?.Appearance ?? snapshot.Appearance; Bindings = snapshot.Bindings;
        _observed = Capture(); _editStart = null; Dirty = true;
    }
    public void ResetAppearance() { Appearance = Defaults(Library); if (Entity is not null) Entity.Appearance = Appearance; RecordEdit(false); }
    public void Undo() { RecordEdit(false); if (_undo.TryPop(out var previous)) { _redo.Push(Capture()); Restore(previous); } }
    public void Redo() { if (_redo.TryPop(out var next)) { _undo.Push(Capture()); Restore(next); } }
    public void Bind(string role, string clip) { if (!Library.Clips.ContainsKey(clip)) throw new InvalidDataException("Unknown binding clip."); Bindings[role] = clip; RecordEdit(false); }
    public void SetEntity(EntityTypeDefinition entity, string? path = null)
    {
        entity.Validate(Library); Entity = entity; Appearance = entity.Appearance; EntityPose = new(Library);
        Bindings = []; Playback.Select(entity.Actions["idle"].Clip); FilePath = path; Dirty = path is null;
        _undo.Clear(); _redo.Clear(); _editStart = null; _observed = Capture();
    }
    public void Save(string path)
    {
        RecordEdit(false);
        if (Entity is not null) { Entity.Validate(Library); Entity.Save(path); FilePath = path; Dirty = false; return; }
        Appearance.Validate();
        var content = JsonSerializer.Serialize(new StudioPreset(1, Library.Id, Playback.ClipId, Appearance, Bindings), AuthoredJson.Options);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content); File.Move(temporary, path, true); FilePath = path; Dirty = false;
    }
    public static StudioPreset ReadPreset(string path)
    {
        var preset = JsonSerializer.Deserialize<StudioPreset>(File.ReadAllText(path), AuthoredJson.Options) ?? throw new InvalidDataException("Empty preset.");
        if (preset.Version != 1 || preset.Appearance is null || preset.Bindings is null || string.IsNullOrWhiteSpace(preset.Library)) throw new InvalidDataException("Unsupported studio preset.");
        preset.Appearance.Validate(); return preset;
    }
    public void Apply(StudioPreset preset, string path)
    {
        if (preset.Library != Library.Id || !Library.Clips.ContainsKey(preset.Clip) || preset.Bindings.Any(b => !Library.Clips.ContainsKey(b.Value))) throw new InvalidDataException("Preset references a missing library or clip.");
        preset.Appearance.Validate();
        if (preset.Appearance.CustomHead is not null && !EntityVocabulary.EntityAnatomies.Contains(Library.Anatomy)) throw new InvalidDataException("Custom heads currently require a person or quadruped library.");
        Geometry.Build(Library.Clips[preset.Clip], 0, preset.Appearance, new(false, false, false), true);
        Appearance = preset.Appearance; Bindings = preset.Bindings; Playback.Select(preset.Clip); Playback.Seek(0);
        _undo.Clear(); _redo.Clear(); _editStart = null; FilePath = path; Dirty = false;
        _observed = Capture();
    }
}
