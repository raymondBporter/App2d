using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// How a character moves. Only clips for the subject's base are offered. Dragging a handle captures the channel it drives
/// (keyed at once with autokey, held until Key pose without); a planted end moves its contact instead. The timeline shows the
/// pose keys, the selected control's channels, contacts, markers and face keys.
/// </summary>
internal sealed class AnimateView(EditorSession session, Viewport viewport) : IWorkspaceView
{
    private const float RowHeight = 20, LabelWidth = 118;
    private string? _dragging;
    private Vector3 _grab;
    private bool _grabbed;
    private bool _onion = true, _showSource = true;
    // The converted clip's source, sampled for the comparison overlay: rebuilt when the clip, its source or the preview build changes.
    private (MotionClip Clip, AssetSource Source, ResolvedModel Model, LibraryImport.Sampler Sampler, float Ratio, Vector3 Offset)? _source;
    private string _marker = "strike";
    // Timeline rows are rebuilt each frame, so key drags and selection name their row by label.
    private (string Row, float From, float To)? _keyDrag;
    private (string Row, IReadOnlyCollection<Channel>? Channels)? _keyRow;
    private (IReadOnlyCollection<Channel>? Channels, float Time)? _keyMenu;

    public Workspace Mode => Workspace.Animate;
    public float TimelineHeight => 230;

    private AssetDocument<MotionClip>? Clip => session.ClipDocument;
    private Subject? Primary => session.Scene().FirstOrDefault();

    // ---- Outline -------------------------------------------------------------------------------------------------

    public void Outline()
    {
        if (Clip is not { } clip) { Ui.Help("Choose an animation in the browser, or create one with New."); return; }
        var basis = clip.Asset.Model;
        var subjects = session.Assets.Models.Where(m => m.Id == basis).Select(m => m.Id).Concat(session.Assets.Variants.Where(v => v.Asset.Base == basis).Select(v => v.Id)).ToArray();
        if (Ui.Combo("Preview on", session.SubjectId ?? basis, subjects, id => session.Assets.Find(id)?.Name ?? id) is { } subject) session.SetSubject(subject);
        Ui.Help("Keys are stored in the base's reference units, so they play on every build.");
        if (Primary is not { } primary) return;
        Ui.Header("Channels");
        foreach (var control in primary.Model.Order)
        {
            var channel = ClipAuthoring.ChannelFor(primary.Model, control.Id);
            if (channel is null) continue;
            var keyed = ClipAuthoring.Track(clip.Asset, channel.Value) is not null || ClipAuthoring.Track(clip.Asset, new(MotionClip.RotateKind, control.Id)) is not null;
            if (!keyed) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            if (ImGui.Selectable($"{control.Id}{(channel.Value.Kind == MotionClip.TargetKind ? "  (IK " + channel.Value.Target + ")" : "")}", session.Selection.Control == control.Id))
            { session.Selection.Clear(); session.Selection.Control = control.Id; }
            if (!keyed) ImGui.PopStyleColor();
        }
    }

    // ---- Inspector -----------------------------------------------------------------------------------------------

    public void Inspector()
    {
        if (Clip is not { } document) { Ui.Help("No animation open."); return; }
        var clip = document.Asset;
        Ui.Header("Animation");
        var name = clip.Name; if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0) session.Change(document, () => document.Asset.Name = name);
        var builds = 1 + session.Assets.Variants.Count(v => v.Asset.Base == clip.Model);
        var entities = session.Assets.EntitiesOn(clip.Model).Count(e => session.Assets.CompileEntity(e.Id) is { } compiled && (compiled.Roles.Values.Any(r => r.Clip == clip) || compiled.Actions.Values.Any(a => a.Clip == clip)));
        ImGui.TextDisabled($"Shared: plays on {builds} build(s) of '{clip.Model}' and in {entities} entit{(entities == 1 ? "y" : "ies")}. Edits reach all of them.");
        if (ImGui.SmallButton("Duplicate to specialize"))
        {
            var id = session.Assets.SuggestId(clip.Id + "-copy"); session.DuplicateClip(clip.Id, id, clip.Name + " copy");
        }
        var duration = clip.Duration;
        if (Ui.Drag("Duration (seconds, retimes keys)", ref duration, .005f, .05f, 60)) session.Change(document, () => ClipAuthoring.Retime(document.Asset, Math.Clamp(duration, .05f, 60)));
        var loop = clip.Loop; if (ImGui.Checkbox("Loop", ref loop)) session.Edit(document, () => document.Asset.Loop = loop);
        TravelFields(document);
        SourceFields(clip);

        Ui.Header("Keying");
        var autokey = session.AutoKey; if (ImGui.Checkbox("Autokey", ref autokey)) session.AutoKey = autokey;
        ImGui.SameLine(); if (ImGui.Button("Key pose")) session.KeyPose();
        if (session.HasPendingPose) { Ui.Problem("Unkeyed pose: Key pose keeps it; changing time discards it."); if (ImGui.SmallButton("Discard")) session.DiscardPendingPose(); }
        ImGui.Checkbox("Onion skin", ref _onion);

        if (Primary is { } primary && session.Selection.Control is { } control && primary.Model.Controls.ContainsKey(control)) ControlFields(document, primary, control);
        Faces(document, Primary);
        Markers(document);
        foreach (var problem in session.Assets.Problems(document)) Ui.Problem(problem);
        References.Draw(session, document.Id);
    }

    private void SourceFields(MotionClip clip)
    {
        if (clip.Source is not { } source) return;
        Ui.Header("Source");
        Ui.Help(source.Kind == AssetSource.Library
            ? $"Converted from imported library '{source.File}', clip '{source.Motion}', with '{source.Rest}' as the rest reference. The library is unchanged."
            : $"Converted from {source.File}, motion '{source.Motion}'. The file is unchanged.");
        if (source.Kind == AssetSource.Library)
        {
            ImGui.Checkbox("Show source points", ref _showSource);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Orange: the mapped source points at the same time, scaled to this build. Compare them with the converted pose.");
        }
    }

    /// <summary>
    /// Mapped source points in the subject's model space: scaled by the overall measure ratio and moved so the source's root in
    /// its reference frame sits on the model's rest root. Empty when the source is unavailable.
    /// </summary>
    private IEnumerable<(string Control, Vector3 Point)> SourcePoints(Subject subject, MotionClip clip)
    {
        if (clip.Source is not { Kind: AssetSource.Library, Points: { } points } source) return [];
        if (_source is not { } cached || cached.Clip != clip || cached.Source != source || cached.Model != subject.Model)
        {
            try
            {
                var library = session.Sources.Get(source.File);
                var reference = new LibraryImport.Sampler(library, source.Rest ?? LibraryImport.DefaultRest(library, source.Motion!), points).At(0);
                var ratio = LibraryImport.Ratios(subject.Model, reference).Overall;
                var root = subject.Model.Order.FirstOrDefault(c => reference.ContainsKey(c.Id))?.Id;
                var offset = root is null ? Vector3.Zero : subject.Model.Rest[root] - reference[root] * ratio;
                _source = cached = (clip, source, subject.Model, new(library, source.Motion!, points), ratio, offset);
            }
            catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex) || ex is IOException) { session.Report("Source unavailable: " + ex.Message, true); _showSource = false; return []; }
        }
        var time = session.Transport.Time;
        return cached.Sampler.At(time).Select(p => (p.Key, p.Value * cached.Ratio + cached.Offset + subject.Pose.Locomotion));
    }

    private void TravelFields(AssetDocument<MotionClip> document)
    {
        var travel = document.Asset.Travel;
        // The common case is steady travel: one distance per cycle. Anything else is shown, not flattened.
        var steady = travel.Keys.Count == 0 || travel.Keys.Count == 2 && travel.Keys[0] is { Time: 0, X: 0, Y: 0 } && MathF.Abs(travel.Keys[1].Time - document.Asset.Duration) < ClipAuthoring.SameTime && travel.Keys[1].Y == 0;
        if (steady)
        {
            var distance = travel.Keys.Count == 2 ? travel.Keys[1].X : 0;
            if (Ui.Drag($"Travel per cycle ({travel.Scale} units)", ref distance, .005f, -20, 20))
                session.Change(document, () => document.Asset.Travel.Keys = distance == 0 ? [] : [new() { Time = 0 }, new() { Time = document.Asset.Duration, X = distance }]);
        }
        else ImGui.TextDisabled($"Travel: {travel.Keys.Count} keys, scaled by '{travel.Scale}'.");
    }

    private void ControlFields(AssetDocument<MotionClip> document, Subject primary, string control)
    {
        var clip = document.Asset; var model = primary.Model; var time = session.Transport.Time;
        Ui.Header("Control " + control);
        var channel = ClipAuthoring.ChannelFor(model, control);
        if (channel is null)
        {
            var chain = model.Chains.Values.First(c => c.Joint == control);
            Ui.Help($"Solved by IK chain '{chain.Id}'. Pose its end '{chain.End}', or flip the bend on the base model.");
            return;
        }
        var track = ClipAuthoring.Track(clip, channel.Value);
        ImGui.TextDisabled($"{channel.Value.Kind} channel on '{channel.Value.Target}'{(track is null ? ", not keyed yet" : $", {track.Keys.Count} keys")}");
        var (value, _) = ClipAuthoring.Value(clip, channel.Value, time);
        if (Ui.Drag3("Delta from rest (reference units)", ref value)) session.Change(document, () => ClipAuthoring.SetKey(document.Asset, channel.Value, time, value));
        if (channel.Value.Kind == MotionClip.TranslateKind)
        {
            var rotate = new Channel(MotionClip.RotateKind, control);
            var degrees = ClipAuthoring.Value(clip, rotate, time).Angle * 180 / MathF.PI;
            if (Ui.Drag("Rotation (degrees, turns children)", ref degrees, .5f, -720, 720)) session.Change(document, () => ClipAuthoring.SetKey(document.Asset, rotate, time, default, degrees * MathF.PI / 180));
        }
        var channels = RowChannels(model, control);
        var key = channels.SelectMany(c => ClipAuthoring.Track(clip, c)?.Keys ?? []).FirstOrDefault(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
        if (key is not null)
        {
            if (Ui.Combo("Ease to next key", key.Ease, ClipEase.All) is { } ease) session.Edit(document, () => ClipAuthoring.SetEase(document.Asset, time, ease, channels));
            if (ImGui.SmallButton("Delete this key")) session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, time, channels));
        }
        else Ui.Help("No key at this time; dragging or editing adds one.");

        if (channel.Value.Kind != MotionClip.TargetKind) return;
        var chainId = channel.Value.Target; var spec = model.Chains[chainId];
        Ui.Header("Contact");
        if (ClipAuthoring.ActiveContact(clip, chainId, time) is { } contact)
        {
            var index = clip.Contacts.IndexOf(contact);
            var range = new Vector2(contact.Start, contact.Finish);
            if (Ui.Drag2("Planted from / until (seconds)", ref range, .002f, 0, clip.Duration))
                session.Change(document, () => { var c = document.Asset.Contacts[index]; c.Start = Math.Min(range.X, range.Y - .01f); c.Finish = range.Y; });
            if (ImGui.SmallButton("Remove contact")) session.Edit(document, () => document.Asset.Contacts.RemoveAt(index));
            if (primary.Pose.Contacts.FirstOrDefault(c => c.Chain == chainId) is { } result)
                if (result.Residual < .005f) ImGui.TextColored(Ui.Accent, "Planted and reached."); else Ui.Problem($"Cannot reach the planted target by {result.Residual:F3}.");
            Ui.Help("Dragging the planted end moves its world target for the whole interval.");
        }
        else if (spec.Frame == CharacterModel.Locomotion)
        {
            if (ImGui.Button("Plant here")) session.Edit(document, () => ClipAuthoring.Plant(model, document.Asset, PoseEvaluator.Sample(model, document.Asset, time), chainId, time));
            Ui.Help("Holds the end where it is now, in the locomotion frame, while travel carries the body on.");
        }
        else Ui.Help($"Keyed in '{spec.Frame}'; only locomotion-frame chains plant.");
        if (primary.Pose.Chains.FirstOrDefault(c => c.Chain == chainId) is { Reached: false } reach) Ui.Problem($"Out of reach by {reach.Residual:F3}. Limb lengths are kept.");
    }

    private void Faces(AssetDocument<MotionClip> document, Subject? primary)
    {
        if (primary is null) return;
        var parts = primary.Model.Parts.Where(p => p.Kind != "stroke" && p.Face != "none").ToArray();
        if (parts.Length == 0) return;
        Ui.Header("Face");
        var time = session.Transport.Time;
        foreach (var part in parts)
        {
            var track = document.Asset.Faces.FirstOrDefault(f => f.Part == part.Id);
            var here = track?.Keys.FirstOrDefault(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
            var shown = PoseEvaluator.FaceAt(document.Asset, part.Id, time);
            ImGui.PushID(part.Id);
            if (Ui.Combo($"{part.Id}: {(here is not null ? "keyed here" : shown is null ? "model default" : "held from earlier key")}", here?.Expression ?? shown ?? part.Face,
                FaceExpressions.Names) is { } expression) session.Edit(document, () => ClipAuthoring.SetFace(document.Asset, part.Id, time, expression));
            if (here is not null && ImGui.SmallButton("Remove face key")) session.Edit(document, () => ClipAuthoring.SetFace(document.Asset, part.Id, time, null));
            ImGui.PopID();
        }
        Ui.Help("A gameplay expression (viewport toolbar) overrides these; limb keys never carry face data.");
    }

    private void Markers(AssetDocument<MotionClip> document)
    {
        Ui.Header("Markers");
        foreach (var marker in document.Asset.Markers.ToArray())
        {
            ImGui.PushID(marker.Id);
            var at = marker.Time; ImGui.SetNextItemWidth(120 * Ui.Scale);
            if (ImGui.DragFloat(marker.Id, ref at, .002f, 0, document.Asset.Duration, "%.3f s")) session.Change(document, () => ClipAuthoring.SetMarker(document.Asset, marker.Id, at));
            ImGui.SameLine(); if (ImGui.SmallButton("Go")) session.Seek(marker.Time);
            ImGui.SameLine(); if (ImGui.SmallButton("Remove")) session.Edit(document, () => document.Asset.Markers.RemoveAll(m => m.Id == marker.Id));
            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(130 * Ui.Scale); ImGui.InputText("##marker", ref _marker, 64); ImGui.SameLine();
        if (ImGui.SmallButton("Add at playhead")) session.Edit(document, () => ClipAuthoring.SetMarker(document.Asset, _marker, session.Transport.Time));
        Ui.Help("Markers name visual moments such as footsteps or a strike. Gameplay binds events to them later.");
    }

    // ---- Viewport ------------------------------------------------------------------------------------------------

    public void Overlay(ViewportFrame frame)
    {
        if (frame.Primary is not { } view || Clip is not { } document) return;
        var subject = view.Subject; var model = subject.Model; var pose = subject.Pose; var draw = frame.Draw;
        if (_onion && !session.Transport.Playing && subject.Clip is { } shown)
        {
            var times = ClipAuthoring.KeyTimes(shown); var now = session.Transport.Time;
            foreach (var time in new[] { times.LastOrDefault(t => t < now - .001f, -1), times.FirstOrDefault(t => t > now + .001f, -1) }.Where(t => t >= 0))
            {
                var ghost = PoseEvaluator.Sample(model, shown, time);
                foreach (var control in model.Order.Where(c => c.Parent is not null))
                    draw.AddLine(frame.Screen(ghost.World(control.Parent!)), frame.Screen(ghost.World(control.Id)), Ui.Color(135, 128, 83, 110), 2);
            }
        }
        foreach (var control in model.Order.Where(c => c.Parent is not null)) draw.AddLine(frame.Screen(pose.World(control.Parent!)), frame.Screen(pose.World(control.Id)), Ui.Color(104, 112, 122, 130), 1);
        if (_showSource)
        {
            var points = SourcePoints(subject, document.Asset).ToDictionary(p => p.Control, p => frame.Screen(p.Point), StringComparer.Ordinal);
            foreach (var control in model.Order.Where(c => c.Parent is not null && points.ContainsKey(c.Id) && points.ContainsKey(c.Parent)))
                draw.AddLine(points[control.Parent!], points[control.Id], Ui.Color(240, 106, 50, 150), 1.5f);
            foreach (var p in points.Values) draw.AddCircleFilled(p, 3 * Ui.Scale, Ui.Color(240, 106, 50, 200));
        }
        foreach (var control in model.Order)
        {
            var channel = ClipAuthoring.ChannelFor(model, control.Id); var p = frame.Screen(pose.World(control.Id));
            var selected = session.Selection.Control == control.Id;
            if (channel is null) { draw.AddCircle(p, 3 * Ui.Scale, Ui.Color(104, 112, 122), 12, 1); continue; }
            var planted = channel.Value.Kind == MotionClip.TargetKind && ClipAuthoring.ActiveContact(document.Asset, channel.Value.Target, session.Transport.Time) is not null;
            var color = selected ? Ui.Color(232, 169, 55) : planted ? Ui.Color(36, 140, 104) : channel.Value.Kind == MotionClip.TargetKind ? Ui.Color(50, 145, 160) : Ui.Color(230, 233, 229);
            var radius = (selected ? 6 : 4.5f) * Ui.Scale;
            if (planted) draw.AddRectFilled(p - new Vector2(radius), p + new Vector2(radius), color); else draw.AddCircleFilled(p, radius, color);
            draw.AddCircle(p, radius, Ui.Color(64, 78, 89), 16, 1);
            if (selected) draw.AddText(p + new Vector2(9, -18) * Ui.Scale, Ui.Color(44, 53, 68), control.Id);
        }
        draw.AddText(frame.Origin + new Vector2(10, 8) * Ui.Scale, session.AutoKey ? Ui.Color(200, 60, 50) : Ui.Color(90, 100, 96), session.AutoKey ? "AUTOKEY" : "autokey off");

        if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragging = model.Order.Select(c => c.Id).Where(id => ClipAuthoring.ChannelFor(model, id) is not null)
                .OrderBy(id => Vector2.Distance(frame.Screen(pose.World(id)), frame.Mouse)).FirstOrDefault(id => Vector2.Distance(frame.Screen(pose.World(id)), frame.Mouse) < 12 * Ui.Scale);
            if (_dragging is not null) { session.Selection.Clear(); session.Selection.Control = _dragging; session.BeginDrag(); _grabbed = false; }
        }
        if (frame.Hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && _dragging is null) viewport.Fit(subject);
        if (_dragging is null) return;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { _dragging = null; session.EndDrag(); return; }
        // The first frame after the click has paused playback; measure the grab offset in that paused pose.
        if (!_grabbed) { _grab = pose.World(_dragging) - frame.World(frame.Mouse); _grabbed = true; return; }
        if (ImGui.GetIO().MouseDelta == Vector2.Zero) return;
        var target = frame.World(frame.Mouse) + _grab;
        session.DragControl(_dragging, target with { Z = pose.World(_dragging).Z });
    }

    // ---- Timeline ------------------------------------------------------------------------------------------------

    private static IReadOnlyCollection<Channel> RowChannels(ResolvedModel model, string control) =>
        ClipAuthoring.ChannelFor(model, control) is { } channel
            ? channel.Kind == MotionClip.TranslateKind ? [channel, new(MotionClip.RotateKind, control)] : [channel]
            : [];

    public void Timeline()
    {
        TransportBar.Draw(session, preview: false);
        if (Clip is not { } document || Primary is not { } primary) return;
        var clip = document.Asset; var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X; var scale = Ui.Scale;
        var left = origin.X + LabelWidth * scale; var right = origin.X + width - 8 * scale;
        float X(float time) => left + (right - left) * time / clip.Duration;
        float Time(float x) => Math.Clamp((x - left) / (right - left) * clip.Duration, 0, clip.Duration);

        var rows = new List<(string Label, IReadOnlyCollection<Channel>? Channels)> { ("Pose (all)", null) };
        if (session.Selection.Control is { } control && primary.Model.Controls.ContainsKey(control) && RowChannels(primary.Model, control) is { Count: > 0 } selected)
            rows.Add((control, selected));
        var y = origin.Y;
        // Ruler: click or drag to scrub.
        var ruler = RowHeight * scale;
        draw.AddRectFilled(new(left, y), new(right, y + ruler), Ui.Color(34, 42, 52));
        for (var i = 0; i <= 10; i++) { var x = X(clip.Duration * i / 10); draw.AddLine(new(x, y + ruler * .55f), new(x, y + ruler), Ui.Color(120, 130, 140)); draw.AddText(new(x + 2, y), Ui.Color(120, 130, 140), $"{clip.Duration * i / 10:0.##}"); }
        ImGui.SetCursorScreenPos(new(left, y)); ImGui.InvisibleButton("ruler", new(Math.Max(1, right - left), ruler));
        if (ImGui.IsItemActive()) { session.Transport.Pause(); session.Seek(Time(ImGui.GetMousePos().X)); }
        y += ruler + 2 * scale;

        foreach (var (label, channels) in rows)
        {
            KeyRow(draw, document, label, channels, origin.X, y, X, Time);
            y += RowHeight * scale;
        }
        ContactRows(draw, document, primary.Model, origin.X, ref y, X);
        PointRow(draw, "Markers", clip.Markers.Select(m => (m.Time, m.Id)), origin.X, y, X, Ui.Color(210, 140, 220)); y += RowHeight * scale;
        foreach (var face in clip.Faces) { PointRow(draw, "Face " + face.Part, face.Keys.Select(k => (k.Time, k.Expression)), origin.X, y, X, Ui.Color(240, 200, 90)); y += RowHeight * scale; }

        var head = X(session.Transport.Time);
        draw.AddLine(new(head, origin.Y), new(head, y), Ui.Color(232, 169, 55), 2);
        KeyMenu(document);
        if (!ImGui.GetIO().WantTextInput && session.Selection.Key is { } selectedKey && ImGui.IsKeyPressed(ImGuiKey.Delete))
            session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, selectedKey, _keyRow?.Channels));
        ImGui.SetCursorScreenPos(new(origin.X, y + 4 * scale));
        ImGui.TextDisabled("Click a key to jump  |  drag to move, Ctrl+drag to copy  |  right-click for easing  |  Delete removes the selected key");
    }

    private void KeyRow(ImDrawListPtr draw, AssetDocument<MotionClip> document, string label, IReadOnlyCollection<Channel>? channels, float x0, float y, Func<float, float> X, Func<float, float> Time)
    {
        var scale = Ui.Scale; var height = RowHeight * scale; var center = y + height / 2; var clip = document.Asset;
        draw.AddText(new(x0 + 4, y + 2), Ui.Color(200, 205, 210), label);
        draw.AddLine(new(X(0), center), new(X(clip.Duration), center), Ui.Color(60, 70, 80));
        var times = ClipAuthoring.KeyTimes(clip, channels);
        var row = (label, channels);
        foreach (var time in times)
        {
            var shown = _keyDrag is { } d && d.Row == label && MathF.Abs(d.From - time) < ClipAuthoring.SameTime ? d.To : time;
            var x = X(shown); var r = 5 * scale;
            var selected = session.Selection.Key is { } k && MathF.Abs(k - time) < ClipAuthoring.SameTime && _keyRow?.Row == label;
            draw.AddQuadFilled(new(x, center - r), new(x + r, center), new(x, center + r), new(x - r, center), selected ? Ui.Color(232, 169, 55) : Ui.Color(150, 200, 210));
            ImGui.SetCursorScreenPos(new(x - r, center - r));
            ImGui.InvisibleButton($"key-{label}-{time:R}", new(2 * r, 2 * r));
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) { session.Transport.Pause(); session.Seek(time); session.Selection.Key = time; _keyRow = row; _keyDrag = (label, time, time); }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) { _keyMenu = (channels, time); session.Selection.Key = time; _keyRow = row; ImGui.OpenPopup("key-menu"); }
        }
        if (_keyDrag is { } active && active.Row == label)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left)) _keyDrag = active with { To = Time(ImGui.GetMousePos().X) };
            else
            {
                _keyDrag = null;
                if (MathF.Abs(active.To - active.From) > ClipAuthoring.SameTime)
                {
                    var copy = ImGui.GetIO().KeyCtrl;
                    session.Edit(document, () => ClipAuthoring.MoveKeys(document.Asset, active.From, active.To, channels, copy));
                    session.Selection.Key = active.To; session.Seek(active.To);
                }
            }
        }
    }

    private void KeyMenu(AssetDocument<MotionClip> document)
    {
        if (!ImGui.BeginPopup("key-menu")) return;
        if (_keyMenu is { } menu)
        {
            ImGui.TextDisabled($"Key at {menu.Time:F3}s");
            foreach (var ease in ClipEase.All) if (ImGui.MenuItem("Ease: " + ease)) session.Edit(document, () => ClipAuthoring.SetEase(document.Asset, menu.Time, ease, menu.Channels));
            if (ImGui.MenuItem("Copy to playhead")) session.Edit(document, () => ClipAuthoring.MoveKeys(document.Asset, menu.Time, session.Transport.Time, menu.Channels, copy: true));
            if (ImGui.MenuItem("Delete")) session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, menu.Time, menu.Channels));
        }
        ImGui.EndPopup();
    }

    private void ContactRows(ImDrawListPtr draw, AssetDocument<MotionClip> document, ResolvedModel model, float x0, ref float y, Func<float, float> X)
    {
        var scale = Ui.Scale; var height = RowHeight * scale;
        foreach (var chain in model.Chains.Values.Where(c => c.Frame == CharacterModel.Locomotion))
        {
            draw.AddText(new(x0 + 4, y + 2), Ui.Color(160, 200, 170), "Plant " + chain.Id);
            draw.AddLine(new(X(0), y + height / 2), new(X(document.Asset.Duration), y + height / 2), Ui.Color(50, 60, 60));
            foreach (var contact in document.Asset.Contacts.Where(c => c.Chain == chain.Id))
            {
                var min = new Vector2(X(contact.Start), y + 4 * scale); var max = new Vector2(X(contact.Finish), y + height - 4 * scale);
                draw.AddRectFilled(min, max, Ui.Color(36, 140, 104, 200), 3);
                ImGui.SetCursorScreenPos(min); ImGui.InvisibleButton($"contact-{chain.Id}-{contact.Start:R}", Vector2.Max(max - min, Vector2.One));
                if (ImGui.IsItemClicked()) { session.Transport.Pause(); session.Seek(contact.Start); session.Selection.Clear(); session.Selection.Control = chain.End; }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{chain.Id} planted {contact.Start:F3}s to {contact.Finish:F3}s");
            }
            y += height;
        }
    }

    private static void PointRow(ImDrawListPtr draw, string label, IEnumerable<(float Time, string Name)> points, float x0, float y, Func<float, float> X, uint color)
    {
        var scale = Ui.Scale; var height = RowHeight * scale;
        draw.AddText(new(x0 + 4, y + 2), Ui.Color(200, 205, 210), label);
        foreach (var (time, name) in points)
        {
            var x = X(time); draw.AddTriangleFilled(new(x - 5 * scale, y + 3 * scale), new(x + 5 * scale, y + 3 * scale), new(x, y + height - 4 * scale), color);
            draw.AddText(new(x + 6 * scale, y + 2), color, name);
        }
    }
}
