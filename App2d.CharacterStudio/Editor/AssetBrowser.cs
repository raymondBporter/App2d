using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Text.RegularExpressions;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// Models, variants and animations in one searchable list. Unfiltered, each base model heads its family: its variants, then
/// the animations authored for it. Creating and duplicating assets goes through one modal so IDs and references stay explicit.
/// </summary>
internal sealed partial class AssetBrowser(EditorSession session)
{
    private enum Create { None, EmptyModel, PersonModel, Variant, DuplicateVariant, Independent, Animation, DuplicateAnimation }
    private static readonly string[] Filters = ["All", "Models", "Variants", "Animations"];
    private string _search = "", _filter = "All";
    private Create _create;
    private string _name = "", _id = "", _source = "", _preset = "";
    private bool _idEdited, _open, _loop = true;
    private float _duration = 1;
    private int _problemsFor = -1;
    private readonly Dictionary<string, IReadOnlyList<string>> _problems = [];

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
            ImGui.EndPopup();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##search", "Search names and ids", ref _search, 64);
        foreach (var filter in Filters) { if (ImGui.RadioButton(filter, _filter == filter)) _filter = filter; if (filter != Filters[^1]) ImGui.SameLine(); }
        RefreshProblems();
        ImGui.BeginChild("assets");
        if (_filter == "All" && _search.Length == 0)
            foreach (var model in session.Assets.Models.OrderBy(m => m.Name))
            {
                Row(model, 0);
                foreach (var variant in session.Assets.Variants.Where(v => v.Asset.Base == model.Id).OrderBy(v => v.Name)) Row(variant, 1);
                foreach (var clip in session.Assets.Clips.Where(c => c.Asset.Model == model.Id).OrderBy(c => c.Name)) Row(clip, 1);
            }
        else
            foreach (var document in session.Assets.Documents.Where(Matches).OrderBy(d => d.Kind).ThenBy(d => d.Name)) Row(document, 0);
        // Assets whose base is missing still belong somewhere visible, so they can be repaired.
        var orphans = session.Assets.Documents.Where(d => d.Kind != AssetKind.Model && session.Assets.Model(session.Assets.BaseOf(d.Id)) is null).ToArray();
        if (orphans.Length > 0 && _filter == "All" && _search.Length == 0) { Ui.Header("Missing base"); foreach (var orphan in orphans) Row(orphan, 0); }
        if (session.Assets.LoadErrors.Count > 0) { Ui.Header("Unreadable files"); foreach (var error in session.Assets.LoadErrors) Ui.Problem(error); }
        ImGui.EndChild();
    }

    private bool Matches(AssetDocument document) =>
        (_filter == "All" || _filter == document.Kind switch { AssetKind.Model => "Models", AssetKind.Variant => "Variants", _ => "Animations" }) &&
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
        var tag = document.Kind switch { AssetKind.Model => "[M]", AssetKind.Variant => "[V]", _ => "[A]" };
        var label = $"{new string(' ', indent * 3)}{tag} {document.Name}{(document.Dirty || document.IsNew ? " *" : "")}";
        var selected = document.Id == session.SubjectId && session.Mode == Workspace.Model || document.Id == session.ClipId && session.Mode == Workspace.Animate;
        if (problems.Count > 0) ImGui.PushStyleColor(ImGuiCol.Text, Ui.Warning);
        if (ImGui.Selectable(label, selected)) session.Open(document.Id);
        if (problems.Count > 0) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{document.Id}\n{document.Path ?? "not saved yet"}" + (problems.Count > 0 ? "\n\n" + string.Join("\n", problems) : ""));
        if (ImGui.BeginPopupContextItem("row"))
        {
            switch (document)
            {
                case AssetDocument<CharacterModel> model:
                    if (ImGui.MenuItem("New variant of this")) Start(Create.Variant, model.Id, model.Name + " variant");
                    if (ImGui.MenuItem("New animation for this")) { session.Open(model.Id); Start(Create.Animation, model.Id, "New animation"); }
                    break;
                case AssetDocument<ModelVariant> variant:
                    if (ImGui.MenuItem("Open base", "", false, session.Assets.Model(variant.Asset.Base) is not null)) session.Open(variant.Asset.Base);
                    if (ImGui.MenuItem("Duplicate variant")) Start(Create.DuplicateVariant, variant.Id, variant.Name + " copy");
                    if (ImGui.MenuItem("Make independent model")) Start(Create.Independent, variant.Id, variant.Name + " model");
                    break;
                case AssetDocument<MotionClip> clip:
                    if (ImGui.MenuItem("Duplicate animation")) Start(Create.DuplicateAnimation, clip.Id, clip.Name + " copy");
                    break;
            }
            if (document.Kind != AssetKind.Animation && ImGui.MenuItem("Pin to compare", "", false, document.Id != session.SubjectId)) session.Pin(document.Id);
            ImGui.EndPopup();
        }
        ImGui.PopID();
    }

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
            Create.Animation => "New animation", _ => "Duplicate animation " + _source,
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
        _ => false,
    };
}
