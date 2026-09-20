using App2d.Core.Characters;
using App2d.Rendering.Characters;
using ImGuiNET;
using System.Numerics;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame
{
    private readonly Dictionary<string, StudioDocument> _entityDocuments = [];
    private readonly Dictionary<string, PointLibrary> _sharedLibraries = [];
    private string _actionId = "idle", _newActionId = "special", _regionId = "body";
    private double _actionTime;
    private bool _actionPlaying = true, _showCollision = true, _previewCues;
    private string? _selectEntityTab;
    private string _activeEntityTab = "Type";
    private bool _geometryDrag;
    private readonly CharacterMesh _collisionOverlay = new(4096);
    private EntityAction CurrentAction => _document.Entity!.Actions.GetValueOrDefault(_actionId) ?? _document.Entity.Actions.Values.First();
    private PointLibrary SharedLibrary(string id)
    {
        if (!_sharedLibraries.TryGetValue(id, out var library))
        {
            var entry = _libraries.First(l => l.Id == id);
            _sharedLibraries[id] = library = PointLibrary.Load(Path.Combine(_assetRoot, entry.Path));
        }
        return library;
    }
    private void LoadEntityTypes()
    {
        var source = Path.Combine(Environment.CurrentDirectory, "Assets", "Characters", "entities");
        var folder = Directory.Exists(source) ? source : Path.Combine(_assetRoot, "entities");
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            var entity = EntityTypeDefinition.Load(path); var document = new StudioDocument(SharedLibrary(entity.Library)); document.SetEntity(entity, path);
            _entityDocuments.Add(entity.Id, document);
        }
    }
    private void ActivateEntity(StudioDocument document)
    {
        _document = document; _actionId = "idle"; _actionTime = 0; _actionPlaying = true; _headEditorOpen = false; _rest = false; _mode = "filled";
        _error = ""; FitMotion();
    }
    private void EntityToolbar()
    {
        if (ImGui.Button("Entities")) ImGui.OpenPopup("Entity types");
        if (ImGui.BeginPopup("Entity types"))
        {
            foreach (var doc in _entityDocuments.Values) if (ImGui.Selectable(doc.Entity!.Name + (doc.Dirty ? " *" : ""), ReferenceEquals(doc, _document))) ActivateEntity(doc);
            ImGui.Separator();
            ImGui.BeginDisabled(_document.Library.Anatomy is not ("person" or "hound"));
            if (ImGui.MenuItem("New from current look")) Attempt(() =>
            {
                var seed = _entityDocuments.Values.First(d => d.Library.Id == _document.Library.Id).Entity!.Copy();
                seed.Id = "new-entity-" + (_entityDocuments.Count + 1); seed.Name = "New entity"; seed.Appearance = _document.Appearance with { };
                var doc = new StudioDocument(SharedLibrary(seed.Library)); doc.SetEntity(seed); _entityDocuments.Add(seed.Id, doc); ActivateEntity(doc);
            });
            ImGui.EndDisabled();
            if (ImGui.MenuItem("Browse source motions")) ChooseLibrary(_libraries.First(l => l.Id == _document.Library.Id));
            ImGui.EndPopup();
        }
        ImGui.SameLine(); if (ImGui.Button("Playtest")) Attempt(StartArena);
        ImGui.SameLine();
    }
    private void EntityLibraryPanel()
    {
        ImGui.TextColored(Accent, _document.Entity!.Name); ImGui.TextDisabled(_document.Library.Label);
        ImGui.Separator(); ImGui.TextUnformatted("Actions");
        foreach (var (id, action) in _document.Entity.Actions)
        {
            if (ImGui.Selectable(id + (action.Placeholder ? "  *" : ""), id == _actionId)) { _actionId = id; _actionTime = 0; _actionPlaying = true; FitMotion(); }
        }
        ImGui.Separator(); ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##New action", "new_action", ref _newActionId, 48);
        if (ImGui.Button("Duplicate action")) Attempt(() =>
        {
            if (string.IsNullOrWhiteSpace(_newActionId) || _document.Entity.Actions.ContainsKey(_newActionId)) throw new InvalidDataException("Choose a unique action name.");
            _document.Entity.Actions.Add(_newActionId, CurrentAction with { }); _actionId = _newActionId; _actionTime = 0;
            _document.RecordEdit(_document.Appearance, false);
        });
        ImGui.TextWrapped("* Placeholder art or motion. Select an action, then edit its timing and geometry on the right.");
    }
    private void EntityStagePanel()
    {
        var action = CurrentAction;
        ImGui.TextUnformatted(_actionId); ImGui.SameLine(); if (ImGui.SmallButton("Fit motion")) Attempt(FitMotion);
        var available = ImGui.GetContentRegionAvail(); Preview(new(available.X, MathF.Max(120 * Scale, available.Y - 210 * Scale)));
        if (ImGui.Button(_actionPlaying ? "Pause" : "Play")) { if (_actionTime >= action.Duration) _actionTime = 0; _actionPlaying = !_actionPlaying; }
        ImGui.SameLine(); if (ImGui.Button("Restart")) { _actionTime = 0; _actionPlaying = true; }
        ImGui.SameLine(); ImGui.Checkbox("Collision", ref _showCollision); ImGui.SameLine(); ImGui.Checkbox("Sound", ref _previewCues);
        var time = (float)_actionTime; ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##Action time", ref time, 0, action.Duration, "%.3f s")) { _actionTime = time; _actionPlaying = false; }
        var pos = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X; var draw = ImGui.GetWindowDrawList(); var height = 16 * Scale;
        draw.AddRectFilled(pos, pos + new Vector2(width, height), 0xff534533);
        if (action.AttackKind != "none") draw.AddRectFilled(pos + new Vector2(action.ActiveStart * width, 0), pos + new Vector2(action.ActiveEnd * width, height), 0xff527adb);
        draw.AddLine(pos + new Vector2(action.Contact * width, 0), pos + new Vector2(action.Contact * width, height), 0xffefeeee, 2 * Scale);
        draw.AddLine(pos + new Vector2(action.Phase(_actionTime) * width, 0), pos + new Vector2(action.Phase(_actionTime) * width, height), 0xffbae456, 3 * Scale);
        ImGui.Dummy(new(width, height + 4 * Scale));
        ImGui.TextWrapped("Cyan: movement body  /  Green: hurt regions  /  Orange: active attack  /  White marker: contact");
        ImGui.TextDisabled($"{action.Duration:F2}s action → {_document.Library.Clips[action.Clip].Label}");
        if (action.Placeholder) ImGui.TextColored(new(1, .72f, .4f, 1), "Placeholder");
        if (!string.IsNullOrEmpty(action.Notes)) ImGui.TextWrapped(action.Notes);
    }
    private static void Choice(string label, string current, string[] values, Action<string> set)
    {
        ImGui.TextUnformatted(label); ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo("##" + label, current)) return;
        foreach (var value in values) if (ImGui.Selectable(value, value == current)) set(value);
        ImGui.EndCombo();
    }
    private void EntityInspector()
    {
        if (!ImGui.BeginTabBar("Entity inspector")) return;
        unsafe bool Tab(string name)
        {
            var flags = _selectEntityTab == name ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            if (_selectEntityTab == name) _selectEntityTab = null;
            var label = System.Text.Encoding.UTF8.GetBytes(name + "\0");
            bool active; fixed (byte* text = label) active = ImGuiNative.igBeginTabItem(text, null, flags) != 0;
            if (active) _activeEntityTab = name;
            return active;
        }
        if (Tab("Type"))
        {
            var type = _document.Entity!;
            var name = type.Name; ImGui.TextUnformatted("Name"); ImGui.SetNextItemWidth(-1); if (ImGui.InputText("##Entity name", ref name, 80)) type.Name = name;
            var id = type.Id; ImGui.TextUnformatted("Stable ID"); ImGui.SetNextItemWidth(-1); if (ImGui.InputText("##Entity ID", ref id, 64)) type.Id = id;
            Slider("Health", type.Health, 1, 100, v => type.Health = (int)v); Slider("Move speed", type.MoveSpeed, 0, 8, v => type.MoveSpeed = v); Slider("Jump speed", type.JumpSpeed, 0, 14, v => type.JumpSpeed = v);
            Choice("Default controller", type.Behavior, ["player", "melee", "ranged", "passive"], v => type.Behavior = v);
            Slider("Preferred distance", type.PreferredRange, .1f, 8, v => type.PreferredRange = v); Slider("Attack cooldown", type.Cooldown, 0, 3, v => type.Cooldown = v);
            Slider("Ground alignment", type.GroundOffset, -2, 2, v => type.GroundOffset = v);
            var notes = type.Notes; ImGui.TextUnformatted("Notes"); if (ImGui.InputTextMultiline("##Type notes", ref notes, 1024, new(-1, 90 * Scale))) type.Notes = notes;
            if (ImGui.Button("Save as new type")) SaveEntityAs();
            ImGui.TextWrapped("Types share motions. Save as creates an independent named type you can refine.");
            ImGui.EndTabItem();
        }
        if (Tab("Look")) { AppearancePanel(); ImGui.EndTabItem(); }
        if (Tab("Action")) { ActionInspector(); ImGui.EndTabItem(); }
        if (Tab("Collision")) { CollisionInspector(); ImGui.EndTabItem(); }
        ImGui.EndTabBar(); _document.RecordEdit(_document.Appearance, ImGui.IsAnyItemActive());
    }
    private void ActionInspector()
    {
        var a = CurrentAction; ImGui.TextColored(Accent, _actionId);
        ImGui.TextUnformatted("Source motion"); ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##Action clip", _document.Library.Clips[a.Clip].Label))
        {
            ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##Clip search", "Search...", ref _search, 80);
            foreach (var clip in _document.Library.Clips.Values.Where(c => c.Label.Contains(_search, StringComparison.OrdinalIgnoreCase) || c.Id.Contains(_search, StringComparison.OrdinalIgnoreCase)))
                if (ImGui.Selectable(clip.Label + "##" + clip.Id, a.Clip == clip.Id)) { a.Clip = clip.Id; _actionTime = 0; }
            ImGui.EndCombo();
        }
        Slider("Duration (seconds)", a.Duration, .05f, 5, v => a.Duration = v);
        ImGui.BeginDisabled(a.AttackKind != "none"); Check("Loop", a.Loop, v => a.Loop = v); ImGui.EndDisabled(); Check("Remove horizontal travel", a.RemoveTravel, v => a.RemoveTravel = v);
        Slider("Clip start", a.ClipStart, 0, 1, v => { a.ClipStart = v; a.ClipEnd = Math.Max(v, a.ClipEnd); a.ClipContact = Math.Clamp(a.ClipContact, a.ClipStart, a.ClipEnd); });
        Slider("Clip end", a.ClipEnd, a.ClipStart, 1, v => { a.ClipEnd = v; a.ClipContact = Math.Clamp(a.ClipContact, a.ClipStart, a.ClipEnd); });
        Slider("Contact in action", a.Contact, .01f, .99f, v => a.Contact = v); Slider("Contact in clip", a.ClipContact, a.ClipStart, a.ClipEnd, v => a.ClipContact = v);
        Choice("Weapon", a.Weapon, ["inherit", "none", "sword", "rapier", "mace", "hammer", "pistol"], v => a.Weapon = v);
        Choice("Attack", a.AttackKind, ["none", "melee", "projectile"], v => { a.AttackKind = v; if (v != "none") a.Loop = false; });
        if (a.AttackKind != "none")
        {
            Slider("Damage", a.Damage, 0, 20, v => a.Damage = (int)v);
            Slider("Active start", a.ActiveStart, 0, .98f, v => { a.ActiveStart = v; a.ActiveEnd = Math.Max(a.ActiveEnd, v + .01f); }); Slider("Active end", a.ActiveEnd, a.ActiveStart + .001f, 1, v => a.ActiveEnd = v);
            Choice("Attach attack to", a.Attachment, ["root", "head", "hand", "muzzle"], v => a.Attachment = v);
            Slider("Attack X", a.HitX, -3, 3, v => a.HitX = v); Slider("Attack Y", a.HitY, -3, 3, v => a.HitY = v);
            Slider("Attack width", a.HitWidth, .05f, 4, v => a.HitWidth = v); Slider("Attack height", a.HitHeight, .05f, 4, v => a.HitHeight = v);
            if (a.AttackKind == "projectile") Slider("Projectile speed", a.ProjectileSpeed, 1, 20, v => a.ProjectileSpeed = v);
        }
        Choice("Timeline sound", a.Cue, ["none", "swing", "shot", "hit", "heavy", "bite"], v => a.Cue = v); Slider("Sound at phase", a.CueTime, 0, 1, v => a.CueTime = v);
        if (ImGui.Button("Audition sound")) PlayCue(a.Cue);
        Choice("On impact sound", a.ImpactCue, ["none", "swing", "shot", "hit", "heavy", "bite"], v => a.ImpactCue = v);
        Check("Placeholder", a.Placeholder, v => a.Placeholder = v);
        var notes = a.Notes; if (ImGui.InputTextMultiline("##Action notes", ref notes, 1024, new(-1, 70 * Scale))) a.Notes = notes;
        if (_actionId is not ("idle" or "walk" or "attack" or "hit" or "death") && ImGui.Button("Delete action")) { _document.Entity!.Actions.Remove(_actionId); _actionId = "idle"; _actionTime = 0; }
    }
    private void CollisionInspector()
    {
        var type = _document.Entity!; ImGui.Checkbox("Show collision", ref _showCollision);
        ImGui.TextColored(Accent, "Movement body");
        Slider("Body width", type.Movement.Width, .1f, 4, v => type.Movement.Width = v); Slider("Body height", type.Movement.Height, .1f, 4, v => type.Movement.Height = v); Slider("Body offset X", type.Movement.OffsetX, -2, 2, v => type.Movement.OffsetX = v);
        if (ImGui.Button("Fit standing body"))
        {
            var pose = _document.EntityPose!; pose.Evaluate(type, type.Actions["idle"], 0, false);
            var points = pose.Hurt.SelectMany(r => r.Points).ToArray();
            if (points.Length > 0) { var min = points.Aggregate(Vector2.Min); var max = points.Aggregate(Vector2.Max); type.Movement.Width = Math.Clamp(max.X - min.X, .1f, 8); type.Movement.Height = Math.Clamp(max.Y, .1f, 8); type.Movement.OffsetX = (max.X + min.X) / 2; }
        }
        ImGui.TextWrapped("Stable during animation. Floor and wall collision use this body.");
        ImGui.Separator(); Choice("Hurt region", _regionId, ["body", "head", "legs", "arms"], v => _regionId = v);
        if (!type.Regions.TryGetValue(_regionId, out var region)) type.Regions.Add(_regionId, region = new());
        Choice("Geometry", region.Mode, ["anatomy", "custom", "disabled"], v => region.Mode = v);
        if (region.Mode == "anatomy") { Slider("Padding", region.Padding, 0, .5f, v => region.Padding = v); Slider("Horizontal scale", region.ScaleX, .1f, 3, v => region.ScaleX = v); Slider("Vertical scale", region.ScaleY, .1f, 3, v => region.ScaleY = v); }
        if (region.Mode == "custom") { Slider("Region width", region.Width, .05f, 4, v => region.Width = v); Slider("Region height", region.Height, .05f, 4, v => region.Height = v); }
        if (region.Mode != "disabled") { Slider("Region offset X", region.OffsetX, -2, 2, v => region.OffsetX = v); Slider("Region offset Y", region.OffsetY, -2, 2, v => region.OffsetY = v); }
        ImGui.TextWrapped("Drag this region in the preview to offset it; Shift-drag resizes it. Anatomy regions follow the pose. Legs use a filled envelope; arms start disabled.");
    }
    private void SaveEntityAs() => Attempt(() =>
    {
        using var dialog = new SaveFileDialog { Filter = "Entity type (*.json)|*.json", FileName = _document.Entity!.Id + "-variant.json", DefaultExt = "json", AddExtension = true };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        var entity = _document.Entity.Copy(); entity.Id = Path.GetFileNameWithoutExtension(dialog.FileName).ToLowerInvariant(); entity.Name += " variant";
        if (_entityDocuments.Values.Any(d => d.Entity!.Id == entity.Id)) throw new InvalidDataException("Choose a new filename/ID for this type.");
        var doc = new StudioDocument(_document.Library); doc.SetEntity(entity); doc.Save(dialog.FileName); _entityDocuments.Add(entity.Id, doc); ActivateEntity(doc);
    });
    private void FitEntityMotion()
    {
        var type = _document.Entity!; var action = CurrentAction; var min = new Vector2(float.PositiveInfinity); var max = new Vector2(float.NegativeInfinity);
        for (var i = 0; i <= 30; i++)
        {
            _document.EntityPose!.Evaluate(type, action, action.Duration * i / 30d, type.Appearance.Flip);
            var pose = _document.EntityPose;
            _document.Geometry.Build(_document.Library.Clips[action.Clip], pose.ClipTime, pose.Look, new(false, false, false), true);
            var a = _document.Geometry.Body.Min; var b = _document.Geometry.Body.Max;
            min = Vector2.Min(min, new Vector2(a.X, a.Y) + pose.Offset); max = Vector2.Max(max, new Vector2(b.X, b.Y) + pose.Offset);
            foreach (var p in pose.Movement.Points.Concat(action.AttackKind == "none" ? [] : pose.Hit.Points)) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        }
        _center = (min + max) / 2; _fitSpan = Math.Max(1.5f, Math.Max(max.Y - min.Y + .5f, (max.X - min.X + .5f) / 1.2f)); _pan = Vector2.Zero; _zoom = 1;
    }
    private void BuildCollisionOverlay(EntityPose pose, bool active, EntityAction? action = null)
    {
        _collisionOverlay.Clear();
        void Outline(EntityRegion region, Color color)
        { for (var i = 0; i < region.Points.Count; i++) _collisionOverlay.Line(new(region.Points[i], -10), new(region.Points[(i + 1) % region.Points.Count], -10), .018f, color); }
        Outline(pose.Movement, new Color(40, 170, 215));
        foreach (var region in pose.Hurt) Outline(region, new Color(70, 175, 80));
        if (action?.AttackKind != "none") Outline(pose.Hit, active ? new Color(240, 95, 25) : new Color(170, 140, 110, 100));
        if (action?.AttackKind == "projectile")
        {
            var center = pose.Hit.Points.Aggregate(Vector2.Zero, (sum, p) => sum + p) / pose.Hit.Points.Count;
            _collisionOverlay.Line(new(center, -10), new(center + pose.Aim * .75f, -10), .015f, new Color(240, 130, 40));
        }
    }
    private void EditPreviewGeometry(float ppu, Vector2 anchor, Vector2 screenStart)
    {
        if (_document.Entity is null || !_showCollision || _activeEntityTab is not ("Action" or "Collision")) return;
        var pose = _document.EntityPose!; var type = _document.Entity; var action = CurrentAction;
        var region = _activeEntityTab == "Action" ? (action.AttackKind == "none" ? null : pose.Hit) : pose.Hurt.FirstOrDefault(r => r.Id == _regionId);
        if (region is null) return;
        var mouse = ImGui.GetIO().MousePos - screenStart - anchor; mouse = new(mouse.X / ppu, -mouse.Y / ppu);
        var min = region.Points.Aggregate(Vector2.Min); var max = region.Points.Aggregate(Vector2.Max);
        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _geometryDrag = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _geometryDrag = false;
        if (!_geometryDrag || !ImGui.IsItemActive() || !ImGui.IsMouseDragging(ImGuiMouseButton.Left)) return;
        var delta = ImGui.GetIO().MouseDelta / ppu; delta.Y = -delta.Y;
        var flip = type.Appearance.Flip ? -1 : 1;
        if (_activeEntityTab == "Action")
        {
            if (ImGui.GetIO().KeyShift) { action.HitWidth = Math.Clamp(action.HitWidth + delta.X * 2, .02f, 8); action.HitHeight = Math.Clamp(action.HitHeight + delta.Y * 2, .02f, 8); }
            else { action.HitX = Math.Clamp(action.HitX + delta.X * flip, -5, 5); action.HitY = Math.Clamp(action.HitY + delta.Y, -5, 5); }
        }
        else
        {
            var settings = type.Regions[_regionId];
            if (ImGui.GetIO().KeyShift)
            {
                if (settings.Mode == "custom") { settings.Width = Math.Clamp(settings.Width + delta.X * 2, .02f, 8); settings.Height = Math.Clamp(settings.Height + delta.Y * 2, .02f, 8); }
                else { settings.ScaleX = Math.Clamp(settings.ScaleX + delta.X * 2 / Math.Max(.1f, max.X - min.X), .1f, 3); settings.ScaleY = Math.Clamp(settings.ScaleY + delta.Y * 2 / Math.Max(.1f, max.Y - min.Y), .1f, 3); }
            }
            else { settings.OffsetX = Math.Clamp(settings.OffsetX + delta.X * flip, -4, 4); settings.OffsetY = Math.Clamp(settings.OffsetY + delta.Y, -4, 4); }
        }
        _document.RecordEdit(_document.Appearance, true);
    }
    private void ConfirmClose(object? sender, FormClosingEventArgs args)
    {
        foreach (var doc in _entityDocuments.Values.Concat(_documents.Values).Append(_document).Distinct().Where(d => d.Dirty))
        {
            var result = MessageBox.Show("Save changes to " + (doc.Entity?.Name ?? doc.Library.Label) + "?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel) { args.Cancel = true; return; }
            if (result != DialogResult.Yes) continue;
            var path = doc.FilePath;
            if (path is null)
            {
                using var dialog = new SaveFileDialog { Filter = "Entity type or look (*.json)|*.json", FileName = (doc.Entity?.Id ?? doc.Library.Id + "-look") + ".json", AddExtension = true, DefaultExt = "json" };
                if (dialog.ShowDialog() != DialogResult.OK) { args.Cancel = true; return; } path = dialog.FileName;
            }
            try { doc.Save(path); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { MessageBox.Show(ex.Message, "Could not save"); args.Cancel = true; return; }
        }
    }
}
