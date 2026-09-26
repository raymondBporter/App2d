using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Text.RegularExpressions;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// Models, variants, animations, entities and props in one searchable list. Unfiltered, each base model heads its family: its
/// variants, the animations authored for it and the entities standing on it. Creating and duplicating assets goes through one
/// modal so IDs and references stay explicit. Sources lists the imported libraries, read-only; converting a clip or a
/// <c>.puppet.json</c> is an explicit import that creates new drafts and leaves the source untouched.
/// </summary>
internal sealed partial class AssetBrowser(EditorSession session)
{
    private enum Create { None, EmptyModel, PersonModel, Variant, DuplicateVariant, Independent, Animation, DuplicateAnimation, Entity, DuplicateEntity, ImportPuppet, ImportClip }
    private static readonly string[] Filters = ["All", "Models", "Variants", "Animations", "Entities", "Props", "Sources"];
    private string _search = "", _filter = "All";
    private Create _create;
    private string _name = "", _id = "", _source = "", _preset = "", _template = EntityAuthoring.Guard;
    private bool _idEdited, _open, _loop = true;
    private float _duration = 1;
    private int _problemsFor = -1;
    private readonly Dictionary<string, IReadOnlyList<string>> _problems = [];
    // Import dialogs: the library clip, the model it goes onto, its reference clip and the control -> source points mapping.
    private string _library = "", _clip = "", _target = "", _rest = "";
    private Dictionary<string, List<string>> _mapping = [];
    private PuppetDefinition? _puppet;

    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NotId();

    public void Draw()
    {
        if (ImGui.Button("New")) ImGui.OpenPopup("new-asset");
        if (ImGui.BeginPopup("new-asset"))
        {
            if (ImGui.MenuItem("Model: empty")) Start(Create.EmptyModel, "", "New model");
            if (ImGui.MenuItem("Model: Person template")) Start(Create.PersonModel, "", "New person");
            var basis = session.Assets.BaseOf(session.SubjectId ?? "");
            if (ImGui.MenuItem("Variant...", "", false, session.Assets.Models.Any())) Start(Create.Variant, basis ?? session.Assets.Models.First().Id, "New variant");
            if (ImGui.MenuItem("Animation for " + (session.SubjectId ?? "subject") + "...", "", false, session.SubjectId is not null)) Start(Create.Animation, session.SubjectId!, "New animation");
            if (ImGui.MenuItem("Entity from " + (session.SubjectId ?? "a model") + "...", "", false, session.Assets.Models.Any())) Start(Create.Entity, session.SubjectId ?? session.Assets.Models.First().Id, "New entity");
            ImGui.Separator();
            if (ImGui.MenuItem("Import .puppet.json...")) PickPuppet();
            if (ImGui.MenuItem("Convert imported motion...", "", false, session.Sources.Entries.Count > 0)) _filter = "Sources";
            ImGui.EndPopup();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##search", "Search names and ids", ref _search, 64);
        for (var i = 0; i < Filters.Length; i++) { if (ImGui.RadioButton(Filters[i], _filter == Filters[i])) _filter = Filters[i]; if (i % 2 == 0) ImGui.SameLine(); }
        RefreshProblems();
        ImGui.BeginChild("assets");
        if (_filter == "Sources") { Sources(); ImGui.EndChild(); return; }
        if (_filter == "All" && _search.Length == 0)
            foreach (var model in session.Assets.Models.OrderBy(m => m.Name))
            {
                Row(model, 0);
                foreach (var variant in session.Assets.Variants.Where(v => v.Asset.Base == model.Id).OrderBy(v => v.Name)) Row(variant, 1);
                foreach (var clip in session.Assets.Clips.Where(c => c.Asset.Model == model.Id).OrderBy(c => c.Name)) Row(clip, 1);
                foreach (var entity in session.Assets.EntitiesOn(model.Id).OrderBy(e => e.Name)) Row(entity, 1);
            }
        else
            foreach (var document in session.Assets.Documents.Where(Matches).OrderBy(d => d.Kind).ThenBy(d => d.Name)) Row(document, 0);
        // Assets whose base is missing still belong somewhere visible, so they can be repaired.
        var orphans = session.Assets.Documents.Where(d => d.Kind is AssetKind.Variant or AssetKind.Animation && session.Assets.Model(session.Assets.BaseOf(d.Id)) is null
            || d is AssetDocument<EntityAsset> e && session.Assets.Model(session.Assets.BaseOf(e.Asset.Model)) is null).ToArray();
        if (orphans.Length > 0 && _filter == "All" && _search.Length == 0) { Ui.Header("Missing base"); foreach (var orphan in orphans) Row(orphan, 0); }
        if (_filter == "All" && _search.Length == 0 && session.Assets.Props.Any()) { Ui.Header("Props"); foreach (var prop in session.Assets.Props.OrderBy(p => p.Name)) Row(prop, 0); }
        if (session.Assets.LoadErrors.Count > 0) { Ui.Header("Unreadable files"); foreach (var error in session.Assets.LoadErrors) Ui.Problem(error); }
        ImGui.EndChild();
    }

    private void Sources()
    {
        if (session.Sources.Entries.Count == 0) { Ui.Help("No imported libraries: the characters catalog lists none."); return; }
        Ui.Help("Imported motion, read-only. Choose a clip to convert it onto a model through an explicit mapping.");
        foreach (var entry in session.Sources.Entries)
        {
            if (!ImGui.TreeNode($"{entry.Label} ({entry.ClipCount})##{entry.Id}")) continue;
            PointLibrary? library = null;
            try { session.Sources.TryGet(entry.Id, out library); }
            catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex) || ex is IOException) { Ui.Problem(ex.Message); }
            foreach (var clip in library?.Clips.Values.Where(c => _search.Length == 0 || c.Label.Contains(_search, StringComparison.OrdinalIgnoreCase) || c.Id.Contains(_search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Category, StringComparer.Ordinal).ThenBy(c => c.Label, StringComparer.Ordinal) ?? Enumerable.Empty<PointClip>())
            {
                if (ImGui.Selectable($"{clip.Label}##{clip.Id}")) StartImport(library!, clip.Id);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{entry.Id}/{clip.Id}\n{clip.Category}, {clip.Duration:F2} s{(clip.Loop ? ", loops" : "")}{(clip.Placeholder ? ", placeholder" : "")}" + (clip.Warnings.Count > 0 ? "\n\n" + string.Join("\n", clip.Warnings) : ""));
            }
            ImGui.TreePop();
        }
    }

    private void StartImport(PointLibrary library, string clip)
    {
        var models = session.Assets.Models.Select(m => m.Id).Order(StringComparer.Ordinal).ToArray();
        if (models.Length == 0) { session.Report("Create or import a base model first; imported motion converts onto a model.", true); return; }
        var target = new[] { library.Anatomy, session.Assets.BaseOf(session.SubjectId ?? "") }.FirstOrDefault(id => id is not null && models.Contains(id)) ?? models[0];
        Start(Create.ImportClip, library.Id + "/" + clip, library.Clips[clip].Label);
        _library = library.Id; _clip = clip; _rest = LibraryImport.DefaultRest(library, clip);
        Target(target);
    }

    private void Target(string model)
    {
        _target = model;
        _mapping = session.Assets.Model(model) is { } document && session.Sources.TryGet(_library, out var library) ? LibraryImport.DefaultMapping(document.Asset, library) : [];
    }

    private void PickPuppet()
    {
        using var dialog = new OpenFileDialog { Filter = "Puppet character (*.puppet.json)|*.puppet.json|JSON (*.json)|*.json", Title = "Import a puppet character" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try { _puppet = PuppetDefinition.FromJson(File.ReadAllText(dialog.FileName)); }
        catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex) || ex is IOException or UnauthorizedAccessException) { session.Report($"{Path.GetFileName(dialog.FileName)}: {ex.Message}", true); return; }
        Start(Create.ImportPuppet, dialog.FileName, _puppet.Name);
    }

    private bool Matches(AssetDocument document) =>
        (_filter == "All" || _filter == document.Kind switch { AssetKind.Model => "Models", AssetKind.Variant => "Variants", AssetKind.Animation => "Animations", AssetKind.Entity => "Entities", _ => "Props" }) &&
        (document.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) || document.Id.Contains(_search, StringComparison.OrdinalIgnoreCase));

    private void RefreshProblems()
    {
        var revision = session.Assets.Revision;
        if (revision == _problemsFor) return;
        _problems.Clear(); _problemsFor = revision;
        foreach (var document in session.Assets.Documents) _problems[document.Id] = session.Assets.Problems(document);
    }

    private void Row(AssetDocument document, int indent)
    {
        ImGui.PushID(document.Id);
        var problems = _problems.GetValueOrDefault(document.Id) ?? [];
        var tag = document.Kind switch { AssetKind.Model => "[M]", AssetKind.Variant => "[V]", AssetKind.Animation => "[A]", AssetKind.Entity => "[E]", _ => "[P]" };
        var label = $"{new string(' ', indent * 3)}{tag} {document.Name}{(document.Dirty || document.IsNew ? " *" : "")}";
        var selected = document.Id == session.SubjectId && session.Mode == Workspace.Model || document.Id == session.ClipId && session.Mode == Workspace.Animate
            || document.Id == session.EntityId && session.Mode == Workspace.Entity;
        if (problems.Count > 0) ImGui.PushStyleColor(ImGuiCol.Text, Ui.Warning);
        if (ImGui.Selectable(label, selected)) session.Open(document.Id);
        if (problems.Count > 0) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{document.Id}\n{document.Path ?? "not saved yet"}" + (SourceOf(document) is { } source ? $"\nconverted from {source.File}{(source.Motion is null ? "" : ": " + source.Motion)}" : "")
                + (problems.Count > 0 ? "\n\n" + string.Join("\n", problems) : ""));
        if (ImGui.BeginPopupContextItem("row"))
        {
            switch (document)
            {
                case AssetDocument<CharacterModel> model:
                    if (ImGui.MenuItem("New variant of this")) Start(Create.Variant, model.Id, model.Name + " variant");
                    if (ImGui.MenuItem("New animation for this")) { session.Open(model.Id); Start(Create.Animation, model.Id, "New animation"); }
                    if (ImGui.MenuItem("Create entity from this model")) Start(Create.Entity, model.Id, model.Name + " entity");
                    break;
                case AssetDocument<ModelVariant> variant:
                    if (ImGui.MenuItem("Open base", "", false, session.Assets.Model(variant.Asset.Base) is not null)) session.Open(variant.Asset.Base);
                    if (ImGui.MenuItem("Duplicate variant")) Start(Create.DuplicateVariant, variant.Id, variant.Name + " copy");
                    if (ImGui.MenuItem("Make independent model")) Start(Create.Independent, variant.Id, variant.Name + " model");
                    if (ImGui.MenuItem("Create entity from this variant")) Start(Create.Entity, variant.Id, variant.Name + " entity");
                    break;
                case AssetDocument<MotionClip> clip:
                    if (ImGui.MenuItem("Duplicate animation")) Start(Create.DuplicateAnimation, clip.Id, clip.Name + " copy");
                    break;
                case AssetDocument<EntityAsset> entity:
                    if (ImGui.MenuItem("Open model", "", false, session.Assets.Find(entity.Asset.Model) is not null)) session.Open(entity.Asset.Model);
                    if (ImGui.MenuItem("Duplicate entity")) Start(Create.DuplicateEntity, entity.Id, entity.Name + " copy");
                    break;
            }
            if (document.Kind is AssetKind.Model or AssetKind.Variant && ImGui.MenuItem("Pin to compare", "", false, document.Id != session.SubjectId)) session.Pin(document.Id);
            if (document.IsNew && ImGui.MenuItem("Discard (never saved)")) session.Discard(document.Id);
            ImGui.EndPopup();
        }
        ImGui.PopID();
    }

    private static AssetSource? SourceOf(AssetDocument document) => document switch
    {
        AssetDocument<MotionClip> clip => clip.Asset.Source, AssetDocument<CharacterModel> model => model.Asset.Source, _ => null,
    };

    private void Start(Create kind, string source, string name)
    {
        _create = kind; _source = source; _name = name; _idEdited = false; _id = session.Assets.SuggestId(Slug(name)); _open = true;
        _preset = ""; _duration = 1; _loop = true;
    }

    private static string Slug(string name) { var slug = NotId().Replace(name.ToLowerInvariant(), "-").Trim('-'); return slug.Length == 0 ? "asset" : slug[..Math.Min(slug.Length, 60)]; }

    /// <summary>Drawn at the end of the frame so the modal sits above every panel.</summary>
    public void DrawDialogs()
    {
        if (_create == Create.None) return;
        const string title = "New asset";
        if (_open) { ImGui.OpenPopup(title); _open = false; }
        if (!ImGui.BeginPopupModal(title, ImGuiWindowFlags.AlwaysAutoResize)) { _create = Create.None; return; }
        ImGui.TextColored(Ui.Accent, _create switch
        {
            Create.EmptyModel => "New model from Empty", Create.PersonModel => "New model from the Person template", Create.Variant => "New variant",
            Create.DuplicateVariant => "Duplicate variant " + _source, Create.Independent => "Independent model from " + _source,
            Create.Animation => "New animation", Create.DuplicateAnimation => "Duplicate animation " + _source,
            Create.Entity => "New entity", Create.ImportPuppet => "Import " + Path.GetFileName(_source),
            Create.ImportClip => "Convert imported motion " + _source, _ => "Duplicate entity " + _source,
        });
        ImGui.SetNextItemWidth(320 * Ui.Scale);
        if (Ui.Text("Name", ref _name, 100) && !_idEdited) _id = session.Assets.SuggestId(Slug(_name));
        ImGui.SetNextItemWidth(320 * Ui.Scale);
        if (Ui.Text("ID (file name and stable reference)", ref _id, 64)) _idEdited = true;
        switch (_create)
        {
            case Create.Variant:
                if (Ui.Combo("Base model", _source, session.Assets.Models.Select(m => m.Id)) is { } basis) { _source = basis; _preset = ""; }
                if (session.Assets.Model(_source) is { } model && BuildRules.For(model.Asset) is { } rule)
                {
                    var presets = rule.Presets.Select(p => p.Id).Prepend("").ToArray();
                    if (Ui.Combo("Starting build", _preset, presets, id => id.Length == 0 ? "Base proportions" : rule.Presets.First(p => p.Id == id).Name) is { } preset) _preset = preset;
                }
                break;
            case Create.Animation:
                Ui.Help($"Authored against {_source}'s proportions for preview; plays on every build of {session.Assets.BaseOf(_source)}.");
                Ui.Drag("Duration (seconds)", ref _duration, .01f, .05f, 60); ImGui.Checkbox("Loop", ref _loop);
                break;
            case Create.Independent:
                Ui.Help("Flattens the resolved variant into a new base with structure revision 1. Animations are not carried over: copy or convert them explicitly.");
                break;
            case Create.Entity:
                var subjects = session.Assets.Models.Select(m => m.Id).Concat(session.Assets.Variants.Select(v => v.Id)).Order(StringComparer.Ordinal);
                if (Ui.Combo("Model or variant", _source, subjects, id => session.Assets.Find(id)?.Name is { } n ? $"{n} ({id})" : id) is { } subject) _source = subject;
                if (Ui.Combo("Gameplay template", _template, EntityAuthoring.Templates.Select(t => t.Id), id => EntityAuthoring.Templates.First(t => t.Id == id).Name) is { } template) _template = template;
                Ui.Help(EntityAuthoring.Templates.First(t => t.Id == _template).Description + " The template copies defaults; the entity keeps no link to it.");
                break;
            case Create.DuplicateEntity:
                Ui.Help("A sibling with the same model, motion set, clips and props. Entities do not inherit from each other.");
                break;
            case Create.ImportPuppet when _puppet is not null:
                Ui.Help($"Creates a base model with {_puppet.Controls.Count} controls and {_puppet.Chains.Count} IK chain(s), and one animation per motion: "
                    + string.Join(", ", _puppet.Motions.Select(m => m.Name)) + ". Chains with contacts are keyed from the ground; the others from their root. The file is only read.");
                break;
            case Create.ImportClip:
                ImportFields();
                break;
        }
        if (session.Assets.Exists(_id)) Ui.Problem($"The id '{_id}' is already used.");
        if (ImGui.Button("Create") && Commit()) { _create = Create.None; ImGui.CloseCurrentPopup(); }
        ImGui.SameLine(); if (ImGui.Button("Cancel")) { _create = Create.None; ImGui.CloseCurrentPopup(); }
        if (session.MessageIsError) Ui.Problem(session.Message);
        ImGui.EndPopup();
    }

    private bool Commit() => _create switch
    {
        Create.EmptyModel => session.NewModel(_id, _name, "empty"),
        Create.PersonModel => session.NewModel(_id, _name, "person"),
        Create.Variant => session.NewVariant(_id, _name, _source, _preset.Length == 0 ? null : _preset),
        Create.DuplicateVariant => session.DuplicateVariant(_source, _id, _name),
        Create.Independent => session.MakeIndependent(_source, _id, _name),
        Create.Animation => session.NewClip(_id, _name, _duration, _loop),
        Create.DuplicateAnimation => session.DuplicateClip(_source, _id, _name),
        Create.Entity => session.NewEntity(_id, _name, _source, _template),
        Create.DuplicateEntity => session.DuplicateEntity(_source, _id, _name),
        Create.ImportPuppet => session.ImportPuppet(_source, _id, _name),
        Create.ImportClip => session.ImportLibraryClip(_library, _clip, _target, _mapping, _id, _name, _rest),
        _ => false,
    };

    private void ImportFields()
    {
        Ui.Help("Motion is copied as offsets from the reference frame, scaled by each measure, so the model keeps its own proportions. The library is not changed; the mapping is saved with the clip.");
        if (Ui.Combo("Onto base model", _target, session.Assets.Models.Select(m => m.Id).Order(StringComparer.Ordinal), id => session.Assets.Find(id)?.Name is { } n ? $"{n} ({id})" : id) is { } target) Target(target);
        if (!session.Sources.TryGet(_library, out var library) || session.Assets.Model(_target) is not { } model) return;
        if (Ui.Combo("Reference clip (its first frame is the model's rest)", _rest, library.Clips.Keys.Order(StringComparer.Ordinal)) is { } rest) _rest = rest;
        var controls = model.Asset.Controls;
        ImGui.TextDisabled($"{controls.Count(c => _mapping.ContainsKey(c.Id))} of {controls.Count} controls follow source points; the others keep their rest offset. IK joints only measure.");
        ImGui.BeginChild("mapping", new(460 * Ui.Scale, 230 * Ui.Scale), ImGuiChildFlags.Borders);
        foreach (var control in controls)
        {
            ImGui.PushID(control.Id);
            ImGui.TextUnformatted(control.Id); ImGui.SameLine(150 * Ui.Scale); ImGui.SetNextItemWidth(-1);
            var current = _mapping.TryGetValue(control.Id, out var points) ? string.Join(" + ", points) : "(rest)";
            if (ImGui.BeginCombo("##map", current))
            {
                if (ImGui.Selectable("(rest)", points is null)) _mapping.Remove(control.Id);
                foreach (var name in library.PointNames) if (ImGui.Selectable(name, points is [var only] && only == name)) _mapping[control.Id] = [name];
                ImGui.EndCombo();
            }
            ImGui.PopID();
        }
        ImGui.EndChild();
    }
}
