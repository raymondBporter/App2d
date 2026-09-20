using App2d.Core.Characters;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame
{
    private static readonly string[] Roles = ["Idle", "Walk", "Run", "Jump", "Fall", "Land", "Dash", "WallGrip", "Climb", "Attack", "WallAttack", "DownAttack", "Shot", "WallShot", "Block", "Hit", "Death", "Cry", "Punch", "Kick", "BalanceForward", "BalanceBackward"];
    private static readonly Vector4 Accent = new(.42f, .84f, .75f, 1);
    private void DrawInterface()
    {
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(io.DisplaySize);
        ImGui.Begin("Character Studio", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.HorizontalScrollbar);
        EntityToolbar(); ImGui.TextColored(Accent, _document.Entity?.Name ?? "MOTION STUDIO");
        ImGui.SameLine(Math.Max(400 * Scale, io.DisplaySize.X - 430 * Scale));
        if (ImGui.Button("Open")) OpenLook(); ImGui.SameLine();
        if (ImGui.Button(_document.Dirty ? "Save *" : "Save")) SaveLook(); ImGui.SameLine();
        ImGui.BeginDisabled(!_document.CanUndo); if (ImGui.Button("Undo")) _document.Undo(); ImGui.EndDisabled(); ImGui.SameLine();
        ImGui.BeginDisabled(!_document.CanRedo); if (ImGui.Button("Redo")) _document.Redo(); ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("UI")) ImGui.OpenPopup("UI settings");
        if (ImGui.BeginPopup("UI settings"))
        {
            ImGui.TextUnformatted($"Windows display scaling: {_gui.DpiScale:P0}");
            ImGui.TextUnformatted("Interface size");
            foreach (var factor in new[] { .75f, 1f, 1.25f, 1.5f, 1.75f, 2f })
            {
                if (ImGui.Selectable(factor == 1 ? "100% (Windows default)" : $"{factor:P0}", MathF.Abs(_gui.UserScale - factor) < .001f))
                {
                    _gui.UserScale = factor;
                    Attempt(() =>
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                        File.WriteAllText(_settingsPath, System.Text.Json.JsonSerializer.Serialize(new { uiScale = factor }));
                    });
                }
            }
            ImGui.EndPopup();
        }
        ImGui.Separator();
        if (!io.WantTextInput && io.KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.S)) SaveLook();
            if (ImGui.IsKeyPressed(ImGuiKey.Z)) _document.Undo();
            if (ImGui.IsKeyPressed(ImGuiKey.Y)) _document.Redo();
        }
        if (!io.WantTextInput && !ImGui.IsAnyItemActive() && ImGui.IsKeyPressed(ImGuiKey.Space)) TogglePlay();
        var size = ImGui.GetContentRegionAvail(); var bottom = 27 * Scale;
        var left = 246 * Scale; var right = 292 * Scale;
        var middle = MathF.Max(460 * Scale, size.X - left - right - 16 * Scale);
        ImGui.BeginChild("Library", new Vector2(left, size.Y - bottom), ImGuiChildFlags.Borders);
        LibraryPanel(); ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("Stage", new Vector2(middle, size.Y - bottom), ImGuiChildFlags.Borders);
        StagePanel(); ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("Inspector", new Vector2(right, size.Y - bottom), ImGuiChildFlags.Borders);
        if (_document.Entity is null) AppearancePanel(); else EntityInspector(); ImGui.EndChild();
        if (_error.Length > 0) ImGui.TextColored(new(1, .55f, .40f, 1), _error);
        else if (_status.Length > 0) ImGui.TextDisabled(_status);
        else ImGui.TextDisabled("Scroll to zoom  |  Middle drag to pan  |  Double click to fit  |  Ctrl+S save  |  Ctrl+Z undo");
        ImGui.End();
        DrawHeadEditor();
        DrawArena();
    }
    private static string MotionPack(PointClip clip) => clip.Metadata.TryGetProperty("sourcePack", out var pack) && pack.ValueKind == System.Text.Json.JsonValueKind.String ? pack.GetString()! : "";
    private void LibraryPanel()
    {
        if (_document.Entity is not null) { EntityLibraryPanel(); return; }
        ImGui.TextColored(Accent, "MOTION LIBRARY"); ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##type", _document.Library.Label))
        {
            foreach (var entry in _libraries) if (ImGui.Selectable(entry.Label, entry.Id == _document.Library.Id)) Attempt(() => ChooseLibrary(entry));
            ImGui.EndCombo();
        }
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##search", "Search animations...", ref _search, 128);
        var filters = new List<(string Key, string Label)> { ("All", "All libraries") };
        foreach (var source in _document.Library.Clips.Values.Select(c => c.Source).Where(s => s.Length > 0 && !s.StartsWith("kevin_", StringComparison.Ordinal)).Distinct())
            filters.Add((source, source switch { "universal" => "Main motions", "kaykit" => "KayKit", "platforming" => "Platforming", "wall" => "Wall grip", "tomek_wolf" => "Tomek / Wolf", _ => source }));
        var packs = _document.Library.Clips.Values.Select(MotionPack).Where(p => p.Length > 0).Distinct().Order().ToArray();
        if (packs.Length > 0)
        {
            filters.Add(("kevin", "Kevin Iglesias / All"));
            foreach (var pack in packs) filters.Add(("kevin:" + pack, "Kevin / " + char.ToUpperInvariant(pack[0]) + pack[1..]));
        }
        if (filters.Count > 1)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##source", filters.FirstOrDefault(f => f.Key == _filter).Label ?? "All libraries"))
            { foreach (var filter in filters) if (ImGui.Selectable(filter.Label, _filter == filter.Key)) _filter = filter.Key; ImGui.EndCombo(); }
        }
        ImGui.Checkbox("Switch at clip end", ref _atEnd);
        var clips = _document.Library.Clips.Values.Where(c => (_filter == "All" || c.Source == _filter ||
            (_filter == "kevin" && MotionPack(c).Length > 0) || (_filter.StartsWith("kevin:", StringComparison.Ordinal) && MotionPack(c) == _filter[6..])) &&
            (c.Label.Contains(_search, StringComparison.OrdinalIgnoreCase) || c.Id.Contains(_search, StringComparison.OrdinalIgnoreCase) || c.Category.Contains(_search, StringComparison.OrdinalIgnoreCase))).ToArray();
        ImGui.TextDisabled($"{clips.Length} / {_document.Library.Clips.Count} motions"); ImGui.Separator();
        ImGui.BeginChild("Clip list", Vector2.Zero);
        foreach (var clip in clips)
        {
            ImGui.PushID(clip.Id);
            if (ImGui.Selectable(clip.Label + (clip.Placeholder ? "  *" : ""), _document.Playback.ClipId == clip.Id))
                _document.Playback.Select(clip.Id, _atEnd);
            if (ImGui.IsItemHovered()) { ImGui.BeginTooltip(); ImGui.TextUnformatted(clip.Id); ImGui.TextUnformatted($"{clip.Duration:F2}s  /  {(clip.Loop ? "Loop" : "One-shot")}  /  {clip.SampleCount} samples"); ImGui.EndTooltip(); }
            ImGui.PopID();
        }
        if (clips.Length == 0) ImGui.TextDisabled("No matching motions.");
        ImGui.EndChild();
    }
    private void StagePanel()
    {
        if (_document.Entity is not null) { EntityStagePanel(); return; }
        var playback = _document.Playback; var clip = playback.Clip;
        ImGui.TextUnformatted(clip.Label); ImGui.SameLine(); ImGui.TextDisabled(clip.Loop ? "LOOP" : "ONE-SHOT");
        ImGui.SameLine(Math.Max(180 * Scale, ImGui.GetContentRegionAvail().X - 65 * Scale));
        if (ImGui.SmallButton("Fit motion")) Attempt(FitMotion);
        var available = ImGui.GetContentRegionAvail();
        Preview(new(available.X, MathF.Max(120 * Scale, available.Y - 272 * Scale)));
        if (ImGui.Button(playback.Playing ? "Pause" : playback.Finished ? "Replay" : "Play", new(76 * Scale, 0))) TogglePlay();
        ImGui.SameLine(); if (ImGui.Button("|<")) playback.Seek(0);
        ImGui.SameLine(); if (ImGui.Button("<")) playback.Step(-1);
        ImGui.SameLine(); if (ImGui.Button(">")) playback.Step(1);
        ImGui.SameLine(); ImGui.SetNextItemWidth(105 * Scale); ImGui.SliderFloat("Speed", ref _speed, .1f, 2, "%.2fx");
        var time = (float)playback.Time; ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##time", ref time, 0, (float)clip.Duration, $"%.3f / {clip.Duration:F3} s")) playback.Seek(time);
        var repeat = playback.Repeat; if (ImGui.Checkbox("Repeat", ref repeat)) playback.Repeat = repeat;
        ImGui.SameLine(); ImGui.Checkbox("Points", ref _joints); ImGui.SameLine(); ImGui.Checkbox("Guides", ref _guides);
        if (_document.Library.Anatomy == "person") { ImGui.SameLine(); ImGui.Checkbox("Trails", ref _trails); }
        if (_document.Library.Anatomy is "monster" or "inventory") { ImGui.SameLine(); ImGui.Checkbox("Rest", ref _rest); }
        ImGui.TextDisabled($"{clip.SampleCount:N0} samples   {clip.PackedBytes / 1024f:F1} KiB   {_document.Geometry.Body.TriangleCount:N0} triangles");
        if (playback.Queued is { } queued) ImGui.TextColored(Accent, "Queued: " + _document.Library.Clips[queued].Label);
        else if (playback.SequenceLength > 0) ImGui.TextColored(Accent, $"Sequence {playback.SequenceIndex + 1} / {playback.SequenceLength}");
        else if (playback.Finished) ImGui.TextColored(Accent, "Holding final source pose");
        else ImGui.TextDisabled($"Shared motion storage: {_document.Library.PackedBytes / 1048576f:F2} MiB");
        if (ImGui.CollapsingHeader("Sequence"))
        {
            for (var i = 0; i < 3; i++)
            {
                ImGui.PushID(i); ImGui.SetNextItemWidth(Math.Max(60, (ImGui.GetContentRegionAvail().X - (2 - i) * 8 * Scale) / (3 - i)));
                if (ImGui.BeginCombo("##sequence", _document.Library.Clips[_sequence[i]].Label))
                { foreach (var c in _document.Library.Clips.Values) if (ImGui.Selectable(c.Label, _sequence[i] == c.Id)) _sequence[i] = c.Id; ImGui.EndCombo(); }
                ImGui.PopID(); if (i < 2) ImGui.SameLine();
            }
            if (ImGui.Button("Play sequence")) playback.StartSequence(_sequence, _repeatSequence); ImGui.SameLine(); ImGui.Checkbox("Repeat sequence", ref _repeatSequence);
        }
        if (clip.Placeholder || clip.Warnings.Count > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.98f, .73f, .43f, 1));
            if (clip.Placeholder) ImGui.TextWrapped("Work in progress: review pose, contacts and timing.");
            foreach (var warning in clip.Warnings) ImGui.TextWrapped(warning);
            ImGui.PopStyleColor();
        }
    }
    private void TogglePlay() { if (_arenaOpen) return; if (_document.Entity is not null) { _actionPlaying = !_actionPlaying; return; } if (_document.Playback.Finished) _document.Playback.Select(_document.Playback.ClipId); else _document.Playback.Playing = !_document.Playback.Playing; }
    private static void Slider(string label, float value, float min, float max, Action<float> set)
    { ImGui.TextUnformatted(label); ImGui.SetNextItemWidth(-1); if (ImGui.SliderFloat("##" + label, ref value, min, max, "%.2f", ImGuiSliderFlags.AlwaysClamp)) set(value); }
    private static void Check(string label, bool value, Action<bool> set) { if (ImGui.Checkbox(label, ref value)) set(value); }
    private void AppearancePanel()
    {
        ImGui.TextColored(Accent, "APPEARANCE"); ImGui.TextWrapped("Shared motion, individual character");
        var a = _document.Appearance; var before = a with { }; var anatomy = _document.Library.Anatomy;
        FacePreviewControls();
        if (ImGui.Button("Reset look")) { _document.ResetAppearance(); a = _document.Appearance; before = a with { }; }
        Check("Face left", a.Flip, v => a.Flip = v);
        Slider("Overall size", a.Size, .3f, 2, v => a.Size = v);
        if (anatomy is "person" or "hound")
        {
            if (ImGui.Button("Head editor")) { _headEditorOpen = true; _headFit = true; }
            if (a.CustomHead is not null) { ImGui.SameLine(); if (ImGui.Button("Original head")) a.CustomHead = null; }
        }
        if (anatomy == "person")
        {
            Slider("Body width", a.Width, .4f, 2, v => a.Width = v); Slider("Body height", a.Height, .4f, 2, v => a.Height = v);
            Slider("Head size", a.Head, .4f, 2, v => a.Head = v); Slider("Torso rounding", a.CornerRadius, 0, 1, v => a.CornerRadius = v);
            Slider("Leg length", a.LegLength, .75f, 1.35f, v => a.LegLength = v); Slider("Arm length", a.ArmLength, .75f, 1.35f, v => a.ArmLength = v);
            Slider("Hip width", a.HipWidth, .75f, 1.35f, v => a.HipWidth = v);
            Check("Show weapons", a.Weapons, v => a.Weapons = v);
            ImGui.SetNextItemWidth(-1);
            var weapons = _document.Library.Drawing.GetProperty("weapons");
            if (ImGui.BeginCombo("##Weapon", weapons.GetProperty(a.Weapon).GetProperty("label").GetString()))
            {
                foreach (var weapon in weapons.EnumerateObject()) if (ImGui.Selectable(weapon.Value.GetProperty("label").GetString(), a.Weapon == weapon.Name))
                { a.Weapon = weapon.Name; a.BladeLength = weapon.Value.GetProperty("length").GetSingle(); }
                ImGui.EndCombo();
            }
            var headed = a.Weapon is "mace" or "hammer";
            Slider(headed ? "Handle length" : "Weapon length", a.BladeLength, .1f, 3, v => a.BladeLength = v);
            if (headed) Slider("Weapon head size", a.WeaponHeadSize, .25f, 2, v => a.WeaponHeadSize = v);
            if (a.Weapon != "sword") ImGui.TextWrapped("Uses the animated grips. Sword slash trails are hidden.");
        }
        else
        {
            Slider("View angle", a.Yaw, -90, 90, v => a.Yaw = v);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##Drawing", _mode))
            { foreach (var mode in anatomy == "monster" ? new[] { "filled", "sticks" } : new[] { "filled", "sticks", "skeleton" }) if (ImGui.Selectable(mode, _mode == mode)) _mode = mode; ImGui.EndCombo(); }
            if (anatomy == "hound")
            {
                Slider("Leg length", a.LegLength, .5f, 1, v => a.LegLength = v); Slider("Neck length", a.NeckLength, .25f, 1, v => a.NeckLength = v); Slider("Tail length", a.TailLength, .1f, 1, v => a.TailLength = v);
                Slider("Head size", a.Head, .7f, 1.5f, v => a.Head = v);
                if (a.CustomHead is null) { Slider("Head width", a.HeadWidth, .6f, 1.8f, v => a.HeadWidth = v); Slider("Head height", a.HeadHeight, .6f, 1.8f, v => a.HeadHeight = v); Slider("Head fullness", a.HeadRoundness, 0, 1, v => a.HeadRoundness = v); }
                Slider("Body fullness", a.Body, .18f, .5f, v => a.Body = v);
                Slider("Body depth", a.Thickness, .08f, .45f, v => a.Thickness = v); Slider("Far-leg offset", a.Spread, 0, .25f, v => a.Spread = v);
                Check("Curved neck and tail", a.Curves, v => a.Curves = v); Check("Lighter far legs", a.FarTint, v => a.FarTint = v);
            }
            else if (anatomy == "inventory")
            {
                Slider("Body width", a.Body, .06f, .7f, v => a.Body = v); Slider("Head size", a.Head, .03f, .4f, v => a.Head = v);
                Slider("Neck width", a.Neck, .1f, 1, v => a.Neck = v); Slider("Tail width", a.Tail, .1f, 1, v => a.Tail = v); Slider("Curve softness", a.Softness, 0, 1, v => a.Softness = v);
            }
            else if (_document.Library.Id == "flying") Slider("Wing size", a.WingSize, .1f, 1.4f, v => a.WingSize = v);
        }
        if (anatomy != "inventory" && a.CustomHead is null)
        {
            ImGui.Separator(); ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##Expression", a.Face))
            {
                var faces = anatomy == "person" ? new[] { "none", "cycle" }.Concat(FaceExpressions.Names).Concat(_document.Library.Drawing.GetProperty("faces").GetProperty("expressions").EnumerateArray().Select(f => "source:" + f.GetProperty("id").GetString()!)) : ["none", "happy", "grumpy"];
                foreach (var face in faces) if (ImGui.Selectable(face.StartsWith("source:", StringComparison.Ordinal) ? "Original sample: " + face[7..] : face, a.Face == face)) a.Face = face;
                ImGui.EndCombo();
            }
        }
        void ColorEdit(string label, string hex, Action<string> setter)
        {
            var value = new Vector3(Convert.ToInt32(hex.Substring(1, 2), 16), Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16)) / 255;
            if (ImGui.ColorEdit3(label, ref value, ImGuiColorEditFlags.NoInputs)) setter($"#{(int)(value.X * 255):x2}{(int)(value.Y * 255):x2}{(int)(value.Z * 255):x2}");
        }
        ColorEdit("Ink", a.Ink, v => a.Ink = v); if (anatomy != "monster") ColorEdit("Fill", a.Fill, v => a.Fill = v);
        _document.RecordEdit(before, ImGui.IsAnyItemActive());
        ImGui.Separator();
        if (_document.Entity is null && ImGui.CollapsingHeader("Game animation bindings"))
        {
            ImGui.TextWrapped("Save semantic roles in the look file. Gameplay timing stays in the game.");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##Role", _bindingRole)) { foreach (var role in Roles) if (ImGui.Selectable(role, _bindingRole == role)) _bindingRole = role; ImGui.EndCombo(); }
            ImGui.TextWrapped(_document.Bindings.TryGetValue(_bindingRole, out var bound) ? "Bound: " + bound : "Unassigned");
            if (ImGui.Button("Use current motion")) _document.Bind(_bindingRole, _document.Playback.ClipId);
            foreach (var binding in _document.Bindings) ImGui.TextWrapped(binding.Key + " -> " + binding.Value);
        }
    }
    private void SaveLook() => Attempt(() =>
    {
        if (_document.Entity is not null && _entityDocuments.Values.Any(d => d != _document && d.Entity!.Id == _document.Entity.Id)) throw new InvalidDataException("Another open entity already uses this ID.");
        if (_document.Entity is not null && _document.FilePath is { } path) { _document.Save(path); _status = "Saved " + path; return; }
        using var dialog = new SaveFileDialog { Filter = "Entity type or character look (*.json)|*.json", FileName = _document.FilePath ?? (_document.Entity?.Id ?? _document.Library.Id + "-look") + ".json", AddExtension = true, DefaultExt = "json" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        _document.Save(dialog.FileName); _status = "Saved " + dialog.FileName;
    });
    private void OpenLook() => Attempt(() =>
    {
        using var dialog = new OpenFileDialog { Filter = "Entity type or character look (*.json)|*.json" }; if (dialog.ShowDialog() != DialogResult.OK) return;
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dialog.FileName));
        if (json.RootElement.TryGetProperty("format", out var format) && format.GetString() == "app2d-entity-type")
        {
            var type = EntityTypeDefinition.Load(dialog.FileName); var existing = _entityDocuments.Values.FirstOrDefault(d => d.Entity!.Id == type.Id);
            if (existing is not null && existing.Dirty && MessageBox.Show("Replace unsaved edits to " + existing.Entity!.Name + "?", "Open entity", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            var doc = new StudioDocument(SharedLibrary(type.Library)); doc.SetEntity(type, dialog.FileName); _entityDocuments[type.Id] = doc; ActivateEntity(doc); return;
        }
        var preset = StudioDocument.ReadPreset(dialog.FileName);
        var entry = _libraries.FirstOrDefault(l => l.Id == preset.Library) ?? throw new InvalidDataException("Preset's motion library is not installed.");
        var document = new StudioDocument(Path.Combine(_assetRoot, entry.Path)); document.Apply(preset, dialog.FileName);
        if (_documents.TryGetValue(entry.Id, out var current) && current.Dirty && MessageBox.Show("Replace unsaved changes for " + entry.Label + "?", "Open look", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
        _documents[entry.Id] = document; ChooseLibrary(entry); _status = "Opened " + dialog.FileName;
    });
}
