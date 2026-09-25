using App2d.Core.Characters;
using App2d.Rendering.Characters;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame
{
    private bool _workshopActive, _workshopRest = true, _workshopPlaying, _workshopGhosts = true, _workshopHandles = true;
    private readonly bool _workshopSmoke;
    private PuppetDocument? _puppet;
    private readonly PuppetDrawing _puppetDrawing = new();
    private readonly CharacterMesh _puppetGround = new(4096);
    private RenderTarget2D? _puppetTarget;
    private nint _puppetTexture;
    private int _puppetMotion;
    private float _puppetTime, _puppetZoom = 1;
    private int _puppetCycles;
    private bool _puppetFollow = true;
    private float _puppetSpeed = 1;
    private Vector2 _puppetPan;
    private string _puppetControl = "hips", _puppetPart = "", _newControl = "point", _chainRoot = "", _chainJoint = "", _chainEnd = "";
    private string? _puppetDrag;
    private string _puppetMessage = "";
    private PuppetDefinition Puppet => _puppet!.Definition;
    private PuppetMotion PuppetMotion => Puppet.Motions[Math.Clamp(_puppetMotion, 0, Puppet.Motions.Count - 1)];

    private void EnterWorkshop()
    {
        _puppet ??= new(PuppetTemplates.StickFigure());
        _workshopActive = true; _arenaOpen = false; _headEditorOpen = false;
    }
    private void ResetWorkshop(PuppetDefinition definition, string? path = null)
    {
        _puppet = new(definition, path); _puppetMotion = 0; _puppetTime = 0; _puppetCycles = 0; _workshopPlaying = false;
        _workshopRest = true; _puppetControl = definition.Controls.FirstOrDefault()?.Id ?? ""; _puppetPart = "";
        _puppetZoom = 1; _puppetPan = default; _puppetMessage = "";
    }
    private bool SavePuppet(bool saveAs = false)
    {
        try
        {
            var path = saveAs ? null : _puppet!.FilePath;
            if (path is null)
            {
                using var dialog = new SaveFileDialog { Filter = "Puppet character (*.puppet.json)|*.puppet.json|JSON (*.json)|*.json", FileName = "character.puppet.json", AddExtension = true, DefaultExt = "json" };
                if (dialog.ShowDialog() != DialogResult.OK) return false;
                path = dialog.FileName;
            }
            _puppet!.Save(path); _puppetMessage = "Saved " + path; return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { _puppetMessage = ex.Message; return false; }
    }
    private bool ConfirmPuppetReplacement()
    {
        if (_puppet is null || !_puppet.Dirty) return true;
        var answer = MessageBox.Show("Save changes to " + Puppet.Name + "?", "Character workshop", MessageBoxButtons.YesNoCancel);
        return answer == DialogResult.No || answer == DialogResult.Yes && SavePuppet();
    }
    private void PuppetAttempt(Action action)
    {
        var before = Puppet.ToJson();
        try { action(); Puppet.Validate(); _puppetMessage = ""; }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            // Keep the current document and its history when an invalid structural edit is rejected.
            RestorePuppetStructure(before); _puppetMessage = ex.Message;
        }
    }
    private void RestorePuppetStructure(string json)
    {
        var restored = PuppetDefinition.FromJson(json);
        Puppet.Controls = restored.Controls; Puppet.Bones = restored.Bones; Puppet.Chains = restored.Chains;
        Puppet.Parts = restored.Parts; Puppet.Motions = restored.Motions;
    }
    private PuppetPose CurrentPuppetPose() => _workshopRest ? PuppetPose.Rest(Puppet) : PuppetPose.Sample(Puppet, PuppetMotion, _puppetTime);
    private void AdvanceWorkshop(float seconds)
    {
        var next = _puppetTime + seconds * _puppetSpeed;
        if (PuppetMotion.Loop)
        {
            _puppetCycles += (int)(next / PuppetMotion.Duration);
            _puppetTime = next % PuppetMotion.Duration;
        }
        else
        {
            _puppetTime = Math.Min(next, PuppetMotion.Duration);
            if (_puppetTime >= PuppetMotion.Duration) _workshopPlaying = false;
        }
    }
    private void StorePuppetPose(PuppetPose pose)
    {
        if (_workshopRest)
        {
            foreach (var control in Puppet.Controls) control.Rest = PuppetPoint.From(pose.Points[control.Id]);
            return;
        }
        var key = pose.Key(_puppetTime);
        var index = PuppetMotion.Keys.FindIndex(k => MathF.Abs(k.Time - _puppetTime) < .0001f);
        if (index >= 0) PuppetMotion.Keys[index] = key; else PuppetMotion.Keys.Add(key);
        PuppetMotion.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
    }
    private void DrawWorkshop()
    {
        _puppet ??= new(PuppetTemplates.StickFigure());
        _puppetMotion = Math.Clamp(_puppetMotion, 0, Puppet.Motions.Count - 1);
        _puppetTime = Math.Clamp(_puppetTime, 0, PuppetMotion.Duration);
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(io.DisplaySize);
        ImGui.Begin("Character workshop", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        if (ImGui.Button("New")) ImGui.OpenPopup("New puppet");
        if (ImGui.BeginPopup("New puppet"))
        {
            foreach (var name in new[] { "Empty character", "Stick figure", "Walk loop study", "Run loop study" })
                if (ImGui.MenuItem(name) && ConfirmPuppetReplacement())
                {
                    ResetWorkshop(name switch
                    {
                        "Empty character" => new(), "Stick figure" => PuppetTemplates.StickFigure(),
                        "Walk loop study" => PuppetTemplates.StepStudy(), _ => PuppetTemplates.RunStudy()
                    });
                    if (name.EndsWith("loop study")) _workshopRest = false;
                }
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Open"))
        {
            try
            {
                using var dialog = new OpenFileDialog { Filter = "Puppet character (*.json)|*.json" };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var definition = PuppetDefinition.FromJson(File.ReadAllText(dialog.FileName));
                    if (ConfirmPuppetReplacement()) ResetWorkshop(definition, dialog.FileName);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
            { _puppetMessage = ex.Message; }
        }
        ImGui.SameLine(); if (ImGui.Button(_puppet.Dirty ? "Save *" : "Save")) SavePuppet();
        ImGui.SameLine(); if (ImGui.Button("Save as")) SavePuppet(true);
        ImGui.SameLine(); ImGui.BeginDisabled(!_puppet.CanUndo); if (ImGui.Button("Undo")) { _puppet.Undo(); _workshopPlaying = false; } ImGui.EndDisabled();
        ImGui.SameLine(); ImGui.BeginDisabled(!_puppet.CanRedo); if (ImGui.Button("Redo")) { _puppet.Redo(); _workshopPlaying = false; } ImGui.EndDisabled();
        if (_libraries.Length > 0)
        {
            ImGui.SameLine(); if (ImGui.Button("Imported characters")) Attempt(() => { LoadImportedStudio(); _workshopActive = false; });
        }
        ImGui.SameLine(); ImGui.TextColored(Accent, "CHARACTER WORKSHOP");
        if (!io.WantTextInput && io.KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.S)) SavePuppet();
            if (ImGui.IsKeyPressed(ImGuiKey.Z)) { _puppet.Undo(); _workshopPlaying = false; }
            if (ImGui.IsKeyPressed(ImGuiKey.Y)) { _puppet.Redo(); _workshopPlaying = false; }
        }
        if (!io.WantTextInput && !ImGui.IsAnyItemActive() && ImGui.IsKeyPressed(ImGuiKey.Space) && !_workshopRest) _workshopPlaying = !_workshopPlaying;
        ImGui.Separator();
        var size = ImGui.GetContentRegionAvail(); var left = 225 * Scale; var right = 300 * Scale;
        ImGui.BeginChild("Puppet parts", new(left, size.Y - 34 * Scale), ImGuiChildFlags.Borders); PuppetStructurePanel(); ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("Puppet stage", new(Math.Max(160, size.X - left - right - 18 * Scale), size.Y - 34 * Scale), ImGuiChildFlags.Borders);
        if (ImGui.RadioButton("Build", _workshopRest)) { _workshopRest = true; _workshopPlaying = false; }
        ImGui.SameLine(); if (ImGui.RadioButton("Animate", !_workshopRest)) _workshopRest = false;
        ImGui.SameLine(); if (ImGui.SmallButton("Reset view")) { _puppetZoom = 1; _puppetPan = default; }
        ImGui.SameLine(); ImGui.Checkbox("Handles", ref _workshopHandles);
        if (!_workshopRest) { ImGui.SameLine(); ImGui.Checkbox("Follow", ref _puppetFollow); }
        ImGui.TextDisabled(_workshopRest ? "Rest shape / controls" : "Drag a handle to create or update a key pose");
        var stageSize = ImGui.GetContentRegionAvail();
        PuppetCanvas(new(stageSize.X, Math.Max(100 * Scale, stageSize.Y - (_workshopRest ? 65 : 212) * Scale)));
        if (!_workshopRest) PuppetTimeline();
        else ImGui.TextWrapped("Select a control, then add a connected control or attach a shape. Bones and drawing parts are independent.");
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("Puppet inspector", new(right, size.Y - 34 * Scale), ImGuiChildFlags.Borders); PuppetInspector(); ImGui.EndChild();
        _puppet.Record(ImGui.IsAnyItemActive());
        ImGui.TextWrapped(_puppetMessage.Length > 0 ? _puppetMessage : "Scroll: zoom  |  Middle drag: pan  |  Ctrl+S: save  |  Ctrl+Z: undo");
        ImGui.End();
    }

    private void PuppetStructurePanel()
    {
        var name = Puppet.Name; ImGui.SetNextItemWidth(-1); if (ImGui.InputText("##Character name", ref name, 100) && !string.IsNullOrWhiteSpace(name)) Puppet.Name = name;
        ImGui.TextColored(Accent, "CONTROLS");
        ImGui.BeginChild("Control list", new(0, 180 * Scale), ImGuiChildFlags.Borders);
        foreach (var control in Puppet.Controls)
            if (ImGui.Selectable(control.Id, _puppetControl == control.Id && _puppetPart.Length == 0)) { _puppetControl = control.Id; _puppetPart = ""; }
        ImGui.EndChild();
        ImGui.BeginDisabled(!_workshopRest);
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##New control", "Control name", ref _newControl, 80);
        if (ImGui.Button("Add control")) PuppetAttempt(() => AddPuppetControl(false));
        ImGui.SameLine(); if (ImGui.Button("Connect")) PuppetAttempt(() => AddPuppetControl(true));
        ImGui.TextWrapped("Connect extends the selected control.");
        ImGui.EndDisabled();
        ImGui.Separator(); ImGui.TextColored(Accent, "DRAWING PARTS");
        ImGui.BeginChild("Part list", new(0, 150 * Scale), ImGuiChildFlags.Borders);
        foreach (var part in Puppet.Parts) if (ImGui.Selectable(part.Id, _puppetPart == part.Id)) _puppetPart = part.Id;
        ImGui.EndChild();
        ImGui.BeginDisabled(!_workshopRest || !Puppet.Controls.Any(c => c.Id == _puppetControl));
        if (ImGui.Button("Ellipse")) AddPuppetPart("ellipse"); ImGui.SameLine(); if (ImGui.Button("Box")) AddPuppetPart("box");
        ImGui.SameLine(); ImGui.BeginDisabled(Puppet.Controls.Count < 2); if (ImGui.Button("Stroke")) AddPuppetPart("stroke"); ImGui.EndDisabled();
        ImGui.EndDisabled();
        if (ImGui.CollapsingHeader("Two-bone IK"))
        {
            ImGui.TextWrapped("Connect two bones, then choose their root, bend and tip. The tip becomes a drag handle.");
            PuppetControlChoice("Root", ref _chainRoot); PuppetControlChoice("Bend", ref _chainJoint); PuppetControlChoice("Tip", ref _chainEnd);
            ImGui.BeginDisabled(!_workshopRest);
            if (ImGui.Button("Add IK chain")) PuppetAttempt(() => Puppet.Chains.Add(new() { Root = _chainRoot, Joint = _chainJoint, End = _chainEnd }));
            ImGui.EndDisabled();
            foreach (var chain in Puppet.Chains.ToArray())
            {
                ImGui.PushID(chain.End); ImGui.TextUnformatted(chain.End);
                if (ImGui.SmallButton("Reverse bend")) chain.Bend *= -1;
                ImGui.SameLine(); if (ImGui.SmallButton("Remove"))
                { Puppet.Chains.Remove(chain); foreach (var motion in Puppet.Motions) motion.Contacts.RemoveAll(c => c.End == chain.End); }
                ImGui.PopID();
            }
        }
    }
    private void PuppetControlChoice(string label, ref string value, bool optional = false)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo("##" + label, value.Length == 0 ? "(none)" : value)) return;
        if (optional && ImGui.Selectable("(none)", value.Length == 0)) value = "";
        foreach (var control in Puppet.Controls) if (ImGui.Selectable(control.Id, value == control.Id)) value = control.Id;
        ImGui.EndCombo();
    }
    private void AddPuppetControl(bool connected)
    {
        if (string.IsNullOrWhiteSpace(_newControl) || Puppet.Controls.Any(c => c.Id == _newControl)) throw new InvalidDataException("Choose an unused control name.");
        var parent = Puppet.Controls.FirstOrDefault(c => c.Id == _puppetControl);
        if (connected && parent is null) throw new InvalidDataException("Select a parent control first.");
        var point = connected ? PuppetPoint.From(parent!.Rest.XYZ + new Vector3(.35f, 0, 0)) : new PuppetPoint(0, 1);
        Puppet.Controls.Add(new() { Id = _newControl, Rest = point });
        if (connected)
        {
            Puppet.Bones.Add(new() { From = parent!.Id, To = _newControl });
            Puppet.Parts.Add(new() { Id = UniquePartId(parent.Id + "-" + _newControl), Kind = "stroke", A = parent.Id, B = _newControl, Width = Puppet.LineWidth });
        }
        _puppetControl = _newControl; _puppetPart = ""; _newControl = "point-" + (Puppet.Controls.Count + 1);
    }
    private string UniquePartId(string basis)
    { var id = basis; var index = 2; while (Puppet.Parts.Any(p => p.Id == id)) id = basis + "-" + index++; return id; }
    private void AddPuppetPart(string kind)
    {
        var part = new PuppetPart { Id = UniquePartId(kind), Kind = kind, A = _puppetControl };
        if (kind == "stroke") { part.B = Puppet.Controls.First(c => c.Id != _puppetControl).Id; part.Width = Puppet.LineWidth; }
        Puppet.Parts.Add(part); _puppetPart = part.Id;
    }

    private void PuppetInspector()
    {
        var part = Puppet.Parts.FirstOrDefault(p => p.Id == _puppetPart);
        if (part is not null)
        {
            ImGui.TextColored(Accent, part.Id + " / " + part.Kind);
            var a = part.A; var b = part.B ?? "";
            PuppetControlChoice("Attach to", ref a); PuppetControlChoice(part.Kind == "stroke" ? "End" : "Point toward", ref b, part.Kind != "stroke");
            if (a != part.A || b != (part.B ?? "")) PuppetAttempt(() => { part.A = a; part.B = b.Length == 0 ? null : b; });
            Slider(part.Kind == "stroke" ? "Thickness" : "Width", part.Width, .01f, 2, v => part.Width = v);
            if (part.Kind != "stroke")
            {
                Slider("Height", part.Height, .01f, 2, v => part.Height = v);
                Slider("Local X", part.OffsetX, -2, 2, v => part.OffsetX = v); Slider("Local Y", part.OffsetY, -2, 2, v => part.OffsetY = v);
                if (part.Kind == "box") Slider("Roundness", part.Roundness, 0, 1, v => part.Roundness = v);
                var fill = new Vector3(Convert.ToInt32(part.Fill.Substring(1, 2), 16), Convert.ToInt32(part.Fill.Substring(3, 2), 16), Convert.ToInt32(part.Fill.Substring(5, 2), 16)) / 255;
                if (ImGui.ColorEdit3("Fill", ref fill, ImGuiColorEditFlags.NoInputs)) part.Fill = $"#{(int)(fill.X * 255):x2}{(int)(fill.Y * 255):x2}{(int)(fill.Z * 255):x2}";
                ImGui.TextUnformatted("Expression"); ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##Expression", part.Face)) { foreach (var face in new[] { "none" }.Concat(FaceExpressions.Names)) if (ImGui.Selectable(face, face == part.Face)) part.Face = face; ImGui.EndCombo(); }
                if (part.Face != "none") Slider("Face horizontal offset", part.FaceX, -.3f, .3f, v => part.FaceX = v);
            }
            Slider("Depth (positive is behind)", part.Depth, -2, 2, v => part.Depth = v);
            if (ImGui.Button("Delete drawing part")) { Puppet.Parts.Remove(part); _puppetPart = ""; }
            ImGui.TextWrapped("Deleting a drawing part keeps its controls and animation.");
            return;
        }
        var control = Puppet.Controls.FirstOrDefault(c => c.Id == _puppetControl);
        if (control is not null)
        {
            ImGui.TextColored(Accent, control.Id);
            var pose = CurrentPuppetPose(); var point = pose.Points[control.Id];
            var xy = new Vector2(point.X, point.Y); ImGui.TextUnformatted("Local X / Y"); ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat2("##XY", ref xy, .01f, -20, 20)) MovePuppetControl(pose, control.Id, new(xy, point.Z));
            var depth = point.Z; ImGui.TextUnformatted("Depth"); ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##Depth", ref depth, -2, 2)) MovePuppetControl(pose, control.Id, new(point.X, point.Y, depth));
            if (_workshopRest && ImGui.Button("Delete control and attachments")) { Puppet.RemoveControl(control.Id); _puppetControl = ""; }
            if (!_workshopRest)
            {
                var pinned = PuppetMotion.Contacts.Any(c => c.End == control.Id && _puppetTime >= c.Start && _puppetTime <= c.Finish);
                ImGui.TextWrapped(pinned ? "This tip is planted. Move the contact target below to change its world position." : "Pose edits create a key at the current time. IK tips preserve the two bone lengths.");
                ImGui.BeginDisabled(!Puppet.Chains.Any(c => c.End == control.Id) || pinned || _puppetTime >= PuppetMotion.Duration);
                if (ImGui.Button("Plant here")) PuppetAttempt(() => PuppetMotion.Contacts.Add(new()
                {
                    End = control.Id, Start = _puppetTime, Finish = Math.Min(PuppetMotion.Duration, _puppetTime + .3f), Target = PuppetPoint.From(pose.World(control.Id))
                }));
                ImGui.EndDisabled();
            }
        }
        if (!_workshopRest)
        {
            ImGui.Separator(); ImGui.TextColored(Accent, "CHARACTER TRAVEL");
            var pose = CurrentPuppetPose(); var position = new Vector2(pose.Position.X, pose.Position.Y); ImGui.TextUnformatted("World X / Y"); ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat2("##Position", ref position, .01f, -100, 100)) { _workshopPlaying = false; pose.Position = new(position, pose.Position.Z); StorePuppetPose(pose); }
            ImGui.TextWrapped("Travel moves the whole character. Planted tips remain fixed when reachable.");
            ImGui.Separator(); ImGui.TextColored(Accent, "CONTACT INTERVALS");
            foreach (var contact in PuppetMotion.Contacts.ToArray())
            {
                ImGui.PushID(PuppetMotion.Contacts.IndexOf(contact)); ImGui.TextUnformatted(contact.End);
                var range = new Vector2(contact.Start, contact.Finish); ImGui.TextUnformatted("Start / end (seconds)"); ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat2("##Start / end", ref range, .01f, 0, PuppetMotion.Duration)) PuppetAttempt(() => { contact.Start = range.X; contact.Finish = range.Y; });
                var target = contact.Target.XY; ImGui.TextUnformatted("World target X / Y"); ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat2("##World target", ref target, .01f, -100, 100)) contact.Target = new(target.X, target.Y, contact.Target.Z);
                if (ImGui.SmallButton("Remove contact")) PuppetMotion.Contacts.Remove(contact);
                ImGui.PopID();
            }
            foreach (var result in CurrentPuppetPose().Contacts)
                ImGui.TextColored(result.Error < .005f ? Accent : new(1, .5f, .3f, 1), $"{result.End}: {(result.Error < .005f ? "planted" : $"unreachable ({result.Error:F3} units)")}");
        }
        else
        {
            ImGui.Separator(); ImGui.TextWrapped("Build edits change the rest shape and IK lengths. Existing key poses keep their authored positions.");
            Slider("Outline width", Puppet.LineWidth, .005f, .12f, v => Puppet.LineWidth = v);
        }
    }

    private void MovePuppetControl(PuppetPose pose, string id, Vector3 target)
    {
        _workshopPlaying = false;
        if (_workshopRest) pose.Points[id] = target;
        else
        {
            if (PuppetMotion.Contacts.Any(c => c.End == id && _puppetTime >= c.Start && _puppetTime <= c.Finish))
            { _puppetMessage = "This control is planted. Edit its world target or remove the contact first."; return; }
            if (Puppet.Chains.Any(c => c.Joint == id))
            { _puppetMessage = "This bend is solved by IK. Move the tip, or change the chain's bend direction."; return; }
            pose.Move(Puppet, id, target);
        }
        PuppetAttempt(() => StorePuppetPose(pose));
    }
    private void PuppetTimeline()
    {
        ImGui.SetNextItemWidth(180 * Scale);
        if (ImGui.BeginCombo("Motion", PuppetMotion.Name))
        {
            for (var i = 0; i < Puppet.Motions.Count; i++) if (ImGui.Selectable(Puppet.Motions[i].Name, i == _puppetMotion)) { _puppetMotion = i; _puppetTime = 0; _puppetCycles = 0; _workshopPlaying = false; }
            ImGui.EndCombo();
        }
        ImGui.SameLine(); if (ImGui.SmallButton("New motion")) { Puppet.Motions.Add(new() { Name = "Motion " + (Puppet.Motions.Count + 1) }); _puppetMotion = Puppet.Motions.Count - 1; _puppetTime = 0; _puppetCycles = 0; _workshopPlaying = false; }
        var name = PuppetMotion.Name; ImGui.SetNextItemWidth(180 * Scale); if (ImGui.InputText("Name", ref name, 100)) PuppetMotion.Name = name;
        var duration = PuppetMotion.Duration; ImGui.SameLine(); ImGui.SetNextItemWidth(90 * Scale);
        if (ImGui.DragFloat("Seconds", ref duration, .01f, .05f, 60)) PuppetAttempt(() =>
        {
            var ratio = duration / PuppetMotion.Duration;
            foreach (var key in PuppetMotion.Keys) key.Time = Math.Min(duration, key.Time * ratio);
            foreach (var contact in PuppetMotion.Contacts) { contact.Start = Math.Min(duration, contact.Start * ratio); contact.Finish = Math.Min(duration, contact.Finish * ratio); }
            _puppetTime *= ratio; PuppetMotion.Duration = duration; _workshopPlaying = false;
        });
        if (ImGui.Button(_workshopPlaying ? "Pause" : "Play")) { if (_puppetTime >= PuppetMotion.Duration) _puppetTime = 0; _workshopPlaying = !_workshopPlaying; }
        ImGui.SameLine(); if (ImGui.SmallButton("Restart")) { _puppetTime = 0; _puppetCycles = 0; _workshopPlaying = false; }
        ImGui.SameLine(); if (ImGui.Button("Key pose")) { _workshopPlaying = false; StorePuppetPose(CurrentPuppetPose()); }
        ImGui.SameLine(); ImGui.Checkbox("Ghost keys", ref _workshopGhosts);
        ImGui.SameLine(); var loop = PuppetMotion.Loop; if (ImGui.Checkbox("Loop", ref loop)) { PuppetMotion.Loop = loop; _puppetCycles = 0; }
        ImGui.SameLine(); ImGui.SetNextItemWidth(70 * Scale); ImGui.SliderFloat("Speed", ref _puppetSpeed, .1f, 2, "%.1fx");
        ImGui.SetNextItemWidth(-1); if (ImGui.SliderFloat("##Puppet time", ref _puppetTime, 0, PuppetMotion.Duration, "%.3f s")) { _workshopPlaying = false; _puppetCycles = 0; }
        ImGui.BeginChild("Pose keys", new(0, 28 * Scale), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
        foreach (var key in PuppetMotion.Keys.ToArray())
        {
            ImGui.PushID(key.Time.ToString("R"));
            if (ImGui.SmallButton($"{key.Time:F2}s")) { _puppetTime = key.Time; _puppetCycles = 0; _workshopPlaying = false; }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) PuppetMotion.Keys.Remove(key);
            ImGui.SameLine(); ImGui.PopID();
        }
        ImGui.EndChild();
        ImGui.TextDisabled("Click key: seek  |  Right-click key: remove  |  Playback shows authored travel");
    }

    private void PuppetCanvas(Vector2 size)
    {
        var width = Math.Max(1, (int)size.X); var height = Math.Max(1, (int)size.Y);
        if (_puppetTarget is null || _puppetTarget.Width != width || _puppetTarget.Height != height)
        {
            if (_puppetTarget is not null) { _gui.Unregister(_puppetTexture); _puppetTarget.Dispose(); }
            _puppetTarget = new(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
            _puppetTexture = _gui.Register(_puppetTarget);
        }
        var ppu = Math.Min(height / 3.1f, width / 4.2f) * _puppetZoom;
        var baseAnchor = new Vector2(width * .42f, height * .87f) + _puppetPan;
        var pose = CurrentPuppetPose();
        var cycleTravel = !_workshopRest && PuppetMotion.Keys.Count > 1
            ? (PuppetMotion.Keys[^1].Position.XYZ - PuppetMotion.Keys[0].Position.XYZ) * _puppetCycles : Vector3.Zero;
        var cameraX = !_workshopRest && _puppetFollow ? pose.Position.X + cycleTravel.X : 0;
        var anchor = baseAnchor + new Vector2(cycleTravel.X - cameraX, -cycleTravel.Y) * ppu;
        _puppetDrawing.Build(Puppet, pose);
        GraphicsDevice.SetRenderTarget(_puppetTarget); GraphicsDevice.Clear(new Color(237, 238, 226));
        var projection = PointCharacterRenderer.Projection(width, height, anchor, ppu);
        _puppetGround.Clear(); var groundInk = new Color(154, 169, 158);
        var firstTick = (int)MathF.Floor(cameraX * 4);
        Vector3 Ground(float x, float y = 0) => new(x - cycleTravel.X, y - cycleTravel.Y, 7);
        _puppetGround.Line(Ground(cameraX - 100), Ground(cameraX + 100), 1 / ppu, groundInk);
        for (var x = firstTick - 30; x <= firstTick + 30; x++)
            _puppetGround.Line(Ground(x * .25f), Ground(x * .25f, -(x % 4 == 0 ? 9 : 4) / ppu), 1 / ppu, groundInk);
        _renderer.Draw(_puppetGround, projection, Matrix.Identity, writeDepth: false);
        _renderer.Draw(_puppetDrawing.Mesh, projection, Matrix.Identity);
        GraphicsDevice.SetRenderTarget(null);
        var start = ImGui.GetCursorScreenPos(); ImGui.InvisibleButton("Puppet canvas", new(width, height));
        var hovered = ImGui.IsItemHovered(); var active = ImGui.IsItemActive();
        var draw = ImGui.GetWindowDrawList(); draw.AddImage(_puppetTexture, start, start + new Vector2(width, height));
        draw.PushClipRect(start, start + new Vector2(width, height), true);
        Vector2 Screen(Vector3 p) => start + anchor + new Vector2(p.X, -p.Y) * ppu;
        if (!_workshopRest && _workshopGhosts)
        {
            foreach (var key in new[] { PuppetMotion.Keys.LastOrDefault(k => k.Time < _puppetTime - .001f), PuppetMotion.Keys.FirstOrDefault(k => k.Time > _puppetTime + .001f) }.OfType<PuppetKey>())
            {
                var ghost = PuppetPose.Sample(Puppet, PuppetMotion, key.Time);
                foreach (var bone in Puppet.Bones) draw.AddLine(Screen(ghost.World(bone.From)), Screen(ghost.World(bone.To)), 0x55878053, 2);
            }
        }
        if (_workshopHandles)
        {
            foreach (var bone in Puppet.Bones) draw.AddLine(Screen(pose.World(bone.From)), Screen(pose.World(bone.To)), 0x557a7068, 1);
            foreach (var control in Puppet.Controls)
            {
                var selected = control.Id == _puppetControl && _puppetPart.Length == 0;
                var position = Screen(pose.World(control.Id));
                var ik = Puppet.Chains.Any(c => c.End == control.Id);
                draw.AddCircleFilled(position, (selected ? 6 : 4) * Scale, selected ? 0xff37a9e8 : ik ? 0xffa09132 : 0xffe5e9e6);
                draw.AddCircle(position, (selected ? 6 : 4) * Scale, 0xff594e40, 16, 1);
                if (selected) draw.AddText(position + new Vector2(9, -18) * Scale, 0xff44352c, control.Id);
            }
        }
        foreach (var contact in pose.Contacts)
        {
            var p = Screen(contact.Target); var color = contact.Error < .005f ? 0xff688c24u : 0xff326af0u;
            draw.AddLine(p - new Vector2(8, 0), p + new Vector2(8, 0), color, 2); draw.AddLine(p - new Vector2(0, 8), p + new Vector2(0, 8), color, 2);
            if (contact.Error >= .005f) draw.AddLine(p, Screen(pose.World(contact.End)), color, 2);
        }
        if (Puppet.Controls.Count == 0) draw.AddText(start + new Vector2(25, 30) * Scale, 0xff66594e, "Add your first control to begin.");
        draw.PopClipRect();
        if (hovered)
        {
            _puppetZoom = Math.Clamp(_puppetZoom * MathF.Pow(1.1f, ImGui.GetIO().MouseWheel), .15f, 5);
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle)) _puppetPan += ImGui.GetIO().MouseDelta;
            if (_workshopHandles && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                var mouse = ImGui.GetMousePos();
                _puppetDrag = Puppet.Controls.OrderBy(c => Vector2.DistanceSquared(Screen(pose.World(c.Id)), mouse)).FirstOrDefault(c => Vector2.Distance(Screen(pose.World(c.Id)), mouse) < 12 * Scale)?.Id;
                if (_puppetDrag is not null) { _puppetControl = _puppetDrag; _puppetPart = ""; _workshopPlaying = false; }
            }
        }
        if (active && _puppetDrag is not null && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var delta = ImGui.GetIO().MouseDelta / ppu;
            MovePuppetControl(pose, _puppetDrag, pose.Points[_puppetDrag] + new Vector3(delta.X, -delta.Y, 0));
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _puppetDrag = null;
    }
}
