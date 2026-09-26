using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// What a character does in the game: its model, motion set and role overrides, controller, movement and hurt geometry,
/// equipment and actions. Selecting an action previews it and brings its timing and hit windows into focus; the viewport
/// shows the collision the game would build from the same final pose. Every change is an entity edit: models, clips and
/// sets are opened, never edited from here.
/// </summary>
internal sealed class EntityView(EditorSession session) : IWorkspaceView
{
    private string _newProp = "", _newSocket = "", _newEvent = "hit";

    public Workspace Mode => Workspace.Entity;
    public float TimelineHeight => 150;

    private AssetDocument<EntityAsset>? Document => session.EntityDocument;
    private string? BaseId => Document is { } document ? session.Assets.BaseOf(document.Asset.Model) : null;
    private CharacterModel? Base => session.Assets.Model(BaseId)?.Asset;

    // ---- Outline -------------------------------------------------------------------------------------------------

    public void Outline()
    {
        if (Document is not { } document) { Ui.Help("Open an entity from the browser, or create one from a model with New."); return; }
        var entity = session.Entity;
        Ui.Header("Preview");
        var roles = entity?.Roles.Keys.Order(StringComparer.Ordinal).ToArray() ?? [];
        foreach (var role in roles)
            if (ImGui.Selectable($"{role}  ({entity!.Roles[role].Clip.Name})", session.PreviewAction is null && session.PreviewRole == role)) { session.PreviewActionOf(null); session.PreviewRoleOf(role); }
        if (roles.Length == 0) Ui.Help("Nothing to preview until the entity compiles.");

        Ui.Header("Actions");
        var spec = TryController(document.Asset.Controller.Kind);
        foreach (var action in spec?.Actions ?? [])
        {
            ImGui.PushID(action);
            var enabled = document.Asset.Actions.Any(a => a.Id == action);
            if (ImGui.Checkbox("##enabled", ref enabled))
                session.Edit(document, () =>
                {
                    if (!enabled) { document.Asset.Actions.RemoveAll(a => a.Id == action); return; }
                    var def = new EntityActionDef { Id = action, Role = action };
                    if (action == EntityControllers.Jump) def.Events.Add(new() { Id = EntityControllers.Launch, At = new() { At = .25f } });
                    document.Asset.Actions.Add(def);
                }, enabled ? $"Enabled '{action}'." : $"Disabled '{action}'.");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Only listed actions are enabled; a shared library containing a clip never enables it.");
            ImGui.SameLine();
            ImGui.BeginDisabled(!enabled);
            if (ImGui.Selectable(action + (document.Asset.Actions.FirstOrDefault(a => a.Id == action)?.Mask is { } mask ? $"  (over locomotion, {mask})" : ""), session.Selection.Action == action))
                session.PreviewActionOf(action);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        if (spec is null) Ui.Problem($"Unknown controller '{document.Asset.Controller.Kind}'.");
        Ui.Help($"The '{document.Asset.Controller.Kind}' controller supports these. Hit and death reactions play their roles when assigned.");
    }

    private static ControllerSpec? TryController(string kind) => EntityControllers.All.FirstOrDefault(s => s.Id == kind);

    // ---- Inspector -----------------------------------------------------------------------------------------------

    public void Inspector()
    {
        if (Document is not { } document) { Ui.Help("No entity open."); return; }
        var asset = document.Asset;
        foreach (var problem in session.Assets.Problems(document)) Ui.Problem(problem);
        if (session.Selection.Action is { } action && asset.Actions.Any(a => a.Id == action)) { ActionInspector(document, action); ImGui.Separator(); }

        Ui.Header("Entity");
        var name = asset.Name; if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0) session.Change(document, () => document.Asset.Name = name);
        var subjects = session.Assets.Models.Select(m => m.Id).Concat(session.Assets.Variants.Select(v => v.Id)).Order(StringComparer.Ordinal);
        if (Ui.Combo("Model or variant", asset.Model, subjects, id => session.Assets.Find(id)?.Name is { } n ? $"{n} ({id})" : id + " (missing)") is { } model)
            session.Edit(document, () => document.Asset.Model = model, $"'{asset.Id}' now uses '{model}'. Motion sets, sockets and hurt layouts come from its base.");
        if (Ui.Button("Open model", session.Assets.Find(asset.Model) is not null)) session.Open(asset.Model);
        if (Ui.Combo("Controller", asset.Controller.Kind, EntityControllers.All.Select(s => s.Id)) is { } kind) session.Edit(document, () => document.Asset.Controller.Kind = kind);
        var health = asset.Health; ImGui.TextUnformatted("Health"); ImGui.SetNextItemWidth(-1);
        if (ImGui.DragInt("##health", ref health, .2f, 1, 10000)) session.Change(document, () => document.Asset.Health = Math.Clamp(health, 1, 10000));

        Motion(document);
        Controller(document);
        Movement(document);
        Hurt(document);
        Equipment(document);
        References.Draw(session, asset.Id);
    }

    private void Motion(AssetDocument<EntityAsset> document)
    {
        var asset = document.Asset; var basis = Base;
        Ui.Header("Motion");
        if (basis is null) { Ui.Problem("The model does not resolve to a base; choose a model first."); return; }
        if (Ui.Combo("Motion set", asset.MotionSet, basis.MotionSets.Select(s => s.Id), id => basis.MotionSets.FirstOrDefault(s => s.Id == id)?.Name ?? id + " (missing)") is { } set)
            session.Edit(document, () => document.Asset.MotionSet = set);
        if (ImGui.SmallButton("Edit sets on " + basis.Name)) session.Open(basis.Id);
        var selected = basis.MotionSets.FirstOrDefault(s => s.Id == asset.MotionSet);
        var spec = TryController(asset.Controller.Kind);
        var clips = session.Assets.ClipsFor(asset.Model).OrderBy(c => c.Name).ToArray();
        var roles = EntityAuthoring.CommonRoles.Concat(selected?.Roles.Keys ?? Enumerable.Empty<string>()).Concat(asset.Roles.Keys).Distinct().ToArray();
        Ui.Help("Effective assignment: the entity's override, then the selected set.");
        foreach (var role in roles)
        {
            ImGui.PushID(role);
            var overridden = asset.Roles.TryGetValue(role, out var own); var fromSet = selected?.Roles.GetValueOrDefault(role);
            var effective = overridden ? own : fromSet;
            var required = spec?.RequiredRoles.Contains(role) == true;
            var label = $"{role}{(required ? " (required)" : "")}";
            if (effective is null && required) { ImGui.PushStyleColor(ImGuiCol.Text, Ui.Warning); ImGui.TextUnformatted(label + ": unassigned"); ImGui.PopStyleColor(); }
            else ImGui.TextUnformatted(label);
            if (overridden) { ImGui.SameLine(); ImGui.TextColored(Ui.Override, "entity"); ImGui.SameLine(); if (ImGui.SmallButton("Use set")) session.SetEntityRole(role, null); }
            else if (fromSet is not null) { ImGui.SameLine(); ImGui.TextDisabled("from " + selected!.Name); }
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##clip", effective is null ? "(unassigned)" : session.Assets.Clip(effective)?.Name ?? effective + " (missing)"))
            {
                foreach (var clip in clips) if (ImGui.Selectable(clip.Name + "##" + clip.Id, clip.Id == effective)) session.SetEntityRole(role, clip.Id);
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Choosing a clip here overrides the set for this entity only.");
            ImGui.PopID();
        }
    }

    private void Controller(AssetDocument<EntityAsset> document)
    {
        var config = document.Asset.Controller; var spec = TryController(config.Kind);
        Ui.Header("Controller");
        void Value(string label, float value, float max, Action<ControllerConfig, float> set)
        {
            if (Ui.Drag(label, ref value, .01f, 0, max)) session.Change(document, () => set(document.Asset.Controller, Math.Clamp(value, 0, max)));
        }
        if (spec?.Moves != false)
        {
            Value("Walk speed (units/s)", config.WalkSpeed, 100, (c, v) => c.WalkSpeed = v);
            Value("Run speed (0 never runs)", config.RunSpeed, 100, (c, v) => c.RunSpeed = v);
        }
        if (spec?.Jumps == true) Value("Jump speed", config.JumpSpeed, 100, (c, v) => c.JumpSpeed = v);
        Value("Attack range", config.Range, 100, (c, v) => c.Range = v);
        Value("Cooldown (s)", config.Cooldown, 60, (c, v) => c.Cooldown = v);
    }

    private void Movement(AssetDocument<EntityAsset> document)
    {
        var box = document.Asset.Movement;
        Ui.Header("Movement box");
        var size = new Vector2(box.Width, box.Height);
        if (Ui.Drag2("Width / height", ref size, .005f, .01f, 100)) session.Change(document, () => { document.Asset.Movement.Width = Math.Max(.01f, size.X); document.Asset.Movement.Height = Math.Max(.01f, size.Y); });
        var offset = box.OffsetX; if (Ui.Drag("Offset X", ref offset, .005f, -100, 100)) session.Change(document, () => document.Asset.Movement.OffsetX = offset);
        if (ImGui.SmallButton("Fit to rest pose")) session.FitMovement();
        Ui.Help("Stable and body-local: animation never changes it. Fit only on request.");
    }

    private void Hurt(AssetDocument<EntityAsset> document)
    {
        var hurt = document.Asset.Hurt; var basis = Base;
        Ui.Header("Hurt regions");
        if (basis is null) return;
        if (Ui.Combo("Layout", hurt.Layout ?? "(none)", basis.HurtLayouts.Select(l => l.Id).Prepend("(none)")) is { } layoutId)
            session.Edit(document, () => { document.Asset.Hurt.Layout = layoutId == "(none)" ? null : layoutId; document.Asset.Hurt.Regions.Clear(); });
        var layout = basis.HurtLayouts.FirstOrDefault(l => l.Id == hurt.Layout);
        if (layout is null) { Ui.Help(basis.HurtLayouts.Count == 0 ? $"'{basis.Name}' has no hurt layouts." : "No hurt regions: this entity cannot be hit."); return; }
        foreach (var region in layout.Regions)
        {
            ImGui.PushID(region.Id);
            var change = hurt.Regions.GetValueOrDefault(region.Id);
            var enabled = change?.Disabled != true;
            if (ImGui.Checkbox(region.Id, ref enabled)) session.Edit(document, () => Override(document, region.Id, o => o.Disabled = !enabled));
            if (enabled)
            {
                ImGui.SameLine(); ImGui.SetNextItemWidth(-60 * Ui.Scale);
                var pad = change?.Pad ?? region.Pad;
                if (ImGui.SliderFloat("##pad", ref pad, 0, 1, "pad %.3f")) session.Change(document, () => Override(document, region.Id, o => o.Pad = pad));
                if (change?.Pad is not null) { ImGui.SameLine(); if (ImGui.SmallButton("Reset")) session.Edit(document, () => Override(document, region.Id, o => o.Pad = null)); }
            }
            ImGui.PopID();
        }
        Ui.Help($"Geometry comes from '{basis.Name}''s layout and the final pose; changes here are this entity's overrides.");
    }

    private static void Override(AssetDocument<EntityAsset> document, string region, Action<HurtOverride> change)
    {
        var regions = document.Asset.Hurt.Regions;
        var o = regions.GetValueOrDefault(region) ?? new(); change(o);
        if (!o.Disabled && o.Pad is null) regions.Remove(region); else regions[region] = o;
    }

    private void Equipment(AssetDocument<EntityAsset> document)
    {
        var equipment = document.Asset.Equipment; var basis = Base;
        Ui.Header("Equipment");
        var sockets = basis?.Sockets.Select(s => s.Id).ToArray() ?? [];
        foreach (var binding in equipment.ToArray())
        {
            ImGui.PushID(binding.Prop);
            ImGui.TextUnformatted(session.Assets.Prop(binding.Prop)?.Name ?? binding.Prop + " (missing)");
            ImGui.SameLine(); if (ImGui.SmallButton("Remove"))
            {
                var used = document.Asset.Actions.SelectMany(a => a.Hits).Any(h => h.Prop == binding.Prop);
                if (used) session.Report($"'{binding.Prop}' anchors a hit window; re-anchor it first.", true);
                else session.Edit(document, () => document.Asset.Equipment.RemoveAll(e => e.Prop == binding.Prop));
            }
            if (Ui.Combo("Socket", binding.Socket, sockets) is { } socket) session.Edit(document, () => document.Asset.Equipment.First(e => e.Prop == binding.Prop).Socket = socket);
            ImGui.PopID();
        }
        var available = session.Assets.Props.Select(p => p.Id).Where(p => equipment.All(e => e.Prop != p)).Order(StringComparer.Ordinal).ToArray();
        if (available.Length == 0 || sockets.Length == 0) { Ui.Help(sockets.Length == 0 ? "The base has no sockets to hold props." : "Every prop is equipped."); return; }
        if (!available.Contains(_newProp)) _newProp = available[0];
        if (!sockets.Contains(_newSocket)) _newSocket = sockets[0];
        ImGui.SetNextItemWidth(110 * Ui.Scale);
        if (ImGui.BeginCombo("##prop", _newProp)) { foreach (var p in available) if (ImGui.Selectable(p, p == _newProp)) _newProp = p; ImGui.EndCombo(); }
        ImGui.SameLine(); ImGui.SetNextItemWidth(110 * Ui.Scale);
        if (ImGui.BeginCombo("##socket", _newSocket)) { foreach (var s in sockets) if (ImGui.Selectable(s, s == _newSocket)) _newSocket = s; ImGui.EndCombo(); }
        ImGui.SameLine(); if (ImGui.SmallButton("Equip")) session.Edit(document, () => document.Asset.Equipment.Add(new() { Prop = _newProp, Socket = _newSocket }), $"Equipped '{_newProp}' on '{_newSocket}'.");
    }

    // ---- Actions -------------------------------------------------------------------------------------------------

    private EntityActionDef Def(AssetDocument<EntityAsset> document, string id) => document.Asset.Actions.First(a => a.Id == id);

    private void ActionInspector(AssetDocument<EntityAsset> document, string id)
    {
        var action = Def(document, id); var basis = Base;
        Ui.Header("Action " + id);
        var byRole = action.Role is not null;
        if (ImGui.RadioButton("Plays a role", byRole) && !byRole) session.Edit(document, () => { var a = Def(document, id); a.Clip = null; a.Role = id; });
        ImGui.SameLine();
        if (ImGui.RadioButton("Plays a clip", !byRole) && byRole)
            session.Edit(document, () => { var a = Def(document, id); a.Clip = session.Entity?.Actions.GetValueOrDefault(id)?.Clip.Id ?? session.Assets.ClipsFor(document.Asset.Model).Select(c => c.Id).Order().FirstOrDefault() ?? id; a.Role = null; });
        if (byRole)
        {
            var roles = EntityAuthoring.CommonRoles.Concat(document.Asset.Roles.Keys).Concat(basis?.MotionSets.SelectMany(s => s.Roles.Keys) ?? []).Distinct().Order(StringComparer.Ordinal);
            if (Ui.Combo("Role", action.Role!, roles) is { } role) session.Edit(document, () => Def(document, id).Role = role);
        }
        else if (Ui.Combo("Clip", action.Clip!, session.Assets.ClipsFor(document.Asset.Model).Select(c => c.Id).Order(StringComparer.Ordinal), c => session.Assets.Clip(c)?.Name is { } n ? $"{n} ({c})" : c + " (missing)") is { } clip)
            session.Edit(document, () => Def(document, id).Clip = clip);
        var resolved = session.Entity?.Actions.GetValueOrDefault(id);

        var groups = basis?.Groups.Select(g => g.Id).ToArray() ?? [];
        if (id != EntityControllers.Jump)
        {
            if (Ui.Combo("Body", action.Mask ?? "(whole body)", groups.Prepend("(whole body)"), g => g == "(whole body)" ? "Whole body: replaces locomotion" : $"Over locomotion: owns '{g}'") is { } mask)
                session.Edit(document, () => { var a = Def(document, id); a.Mask = mask == "(whole body)" ? null : mask; if (a.Mask is null) a.BlendIn = a.BlendOut = 0; });
            if (action.Mask is not null)
            {
                var blend = new Vector2(action.BlendIn, action.BlendOut);
                if (Ui.Drag2("Blend in / out (seconds)", ref blend, .002f, 0, 5)) session.Change(document, () => { var a = Def(document, id); a.BlendIn = Math.Clamp(blend.X, 0, 5); a.BlendOut = Math.Clamp(blend.Y, 0, 5); });
                Ui.Help("The legs keep walking and keep their planted feet; the controller keeps moving the actor.");
                if (session.PreviewAction == id && ImGui.SmallButton($"Preview over: {session.PreviewRole}")) ImGui.OpenPopup("over");
                if (ImGui.BeginPopup("over"))
                {
                    foreach (var role in session.Entity?.Roles.Keys.Order() ?? Enumerable.Empty<string>()) if (ImGui.Selectable(role, role == session.PreviewRole)) session.PreviewRoleOf(role);
                    ImGui.EndPopup();
                }
            }
            if (groups.Length == 0) Ui.Help("The base has no control groups, so actions play on the whole body.");
        }

        Ui.Header("Hit windows");
        foreach (var hit in action.Hits)
            if (ImGui.Selectable($"{hit.Id}  {Describe(hit.Start)} to {Describe(hit.Finish)}  dmg {hit.Damage}", session.Selection.Hit == hit.Id)) session.Selection.Hit = hit.Id;
        if (ImGui.SmallButton("Add hit window"))
        {
            var hitId = ModelAuthoring.UniqueId("hit", action.Hits.Select(h => h.Id));
            var prop = document.Asset.Equipment.FirstOrDefault()?.Prop; var socket = prop is null ? basis?.Sockets.FirstOrDefault()?.Id : null;
            if (prop is null && socket is null) session.Report("Equip a prop or add a socket to the base first: a hit window needs an anchor.", true);
            else if (session.Edit(document, () => Def(document, id).Hits.Add(new() { Id = hitId, Prop = prop, Socket = socket, Start = new() { At = .4f }, Finish = new() { At = .6f } }))) session.Selection.Hit = hitId;
        }
        if (session.Selection.Hit is { } selectedHit && action.Hits.Any(h => h.Id == selectedHit)) HitFields(document, id, selectedHit, resolved);

        Ui.Header("Events");
        foreach (var cue in action.Events.ToArray())
        {
            var index = action.Events.IndexOf(cue);
            ImGui.PushID(index);
            ImGui.TextUnformatted(cue.Id); ImGui.SameLine(); ImGui.TextDisabled(Describe(cue.At) + (cue.Sound is { } sound ? $"  sound {sound}" : ""));
            ImGui.SameLine(); if (ImGui.SmallButton("Remove")) session.Edit(document, () => Def(document, id).Events.RemoveAt(index));
            TimeField("At", cue.At, resolved?.Clip, time => Def(document, id).Events[index].At = time, document);
            var soundId = cue.Sound ?? "";
            if (Ui.Text("Sound (empty for none)", ref soundId, 64)) session.Change(document, () => Def(document, id).Events[index].Sound = soundId.Length == 0 ? null : soundId);
            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(110 * Ui.Scale); ImGui.InputText("##event", ref _newEvent, 64); ImGui.SameLine();
        if (ImGui.SmallButton("Add event")) session.Edit(document, () => Def(document, id).Events.Add(new() { Id = _newEvent, At = new() { At = MathF.Round(session.Transport.Time / Math.Max(resolved?.Clip.Duration ?? 1, 1e-3f), 3) } }));
        Ui.Help("Events fire when simulation crosses them, never while scrubbing. The controller interprets their IDs, such as 'launch'.");
    }

    private void HitFields(AssetDocument<EntityAsset> document, string actionId, string hitId, ResolvedAction? resolved)
    {
        HitWindow Hit() => Def(document, actionId).Hits.First(h => h.Id == hitId);
        var hit = Hit(); var basis = Base;
        ImGui.Separator();
        ImGui.TextColored(Ui.Accent, "Hit " + hitId); ImGui.SameLine();
        if (ImGui.SmallButton("Remove hit")) { session.Edit(document, () => Def(document, actionId).Hits.RemoveAll(h => h.Id == hitId)); session.Selection.Hit = null; return; }
        TimeField("Opens", hit.Start, resolved?.Clip, time => Hit().Start = time, document);
        TimeField("Closes", hit.Finish, resolved?.Clip, time => Hit().Finish = time, document);
        var props = document.Asset.Equipment.Select(e => "prop:" + e.Prop); var sockets = basis?.Sockets.Select(s => "socket:" + s.Id) ?? [];
        var anchor = hit.Prop is not null ? "prop:" + hit.Prop : "socket:" + hit.Socket;
        if (Ui.Combo("Anchored to", anchor, props.Concat(sockets)) is { } chosen)
            session.Edit(document, () => { var h = Hit(); var (kind, value) = (chosen[..chosen.IndexOf(':')], chosen[(chosen.IndexOf(':') + 1)..]); h.Prop = kind == "prop" ? value : null; h.Socket = kind == "socket" ? value : null; });
        if (hit.Prop is not null && Ui.Combo("Prop point", hit.Point, PropAsset.PointNames) is { } point) session.Edit(document, () => Hit().Point = point);
        var along = hit.Along; if (Ui.Drag("Along the anchor's axis", ref along, .005f, -100, 100)) session.Change(document, () => Hit().Along = along);
        var size = new Vector2(hit.Width, hit.Height);
        if (Ui.Drag2("Width / height", ref size, .005f, .01f, 100)) session.Change(document, () => { Hit().Width = Math.Max(.01f, size.X); Hit().Height = Math.Max(.01f, size.Y); });
        var damage = hit.Damage; ImGui.TextUnformatted("Damage"); ImGui.SetNextItemWidth(-1);
        if (ImGui.DragInt("##damage", ref damage, .1f, 0, 10000)) session.Change(document, () => Hit().Damage = Math.Clamp(damage, 0, 10000));
    }

    /// <summary>A moment: a clip marker, or a normalized time over the clip. Markers keep hits on the visual moment when the clip is retimed.</summary>
    private void TimeField(string label, ActionTime time, MotionClip? clip, Action<ActionTime> set, AssetDocument<EntityAsset> document)
    {
        const string Normalized = "(normalized time)";
        var markers = clip?.Markers.Select(m => m.Id).ToArray() ?? [];
        if (Ui.Combo(label, time.Marker ?? Normalized, markers.Prepend(Normalized), m => m == Normalized ? m : $"marker '{m}'") is { } chosen)
            session.Edit(document, () => set(chosen == Normalized ? new() { At = clip is null || time.Marker is null ? time.At : (clip.Markers.FirstOrDefault(m => m.Id == time.Marker)?.Time ?? 0) / clip.Duration } : new() { Marker = chosen }));
        if (time.Marker is null)
        {
            var at = time.At; ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##" + label, ref at, 0, 1, clip is null ? "%.3f" : $"%.3f  ({at * clip.Duration:F3} s)")) session.Change(document, () => set(new() { At = at }));
        }
        else if (clip is not null && clip.Markers.All(m => m.Id != time.Marker)) Ui.Problem($"'{clip.Name}' has no marker '{time.Marker}'.");
    }

    private static string Describe(ActionTime time) => time.Marker ?? $"{time.At:0.###}";

    // ---- Viewport ------------------------------------------------------------------------------------------------

    public void Overlay(ViewportFrame frame)
    {
        if (session.EvaluateEntity() is not { } preview || frame.Primary is null) return;
        var draw = frame.Draw;
        Vector2 Screen(Vector2 p) => frame.Screen(new(p, 0));
        void Outline(EntityRegion region, uint color, float width)
        {
            var points = region.Points.Select(Screen).ToArray();
            for (var i = 0; i < points.Length; i++) draw.AddLine(points[i], points[(i + 1) % points.Length], color, width);
        }
        Outline(preview.Movement, Ui.Color(120, 120, 120), 1.5f);
        foreach (var region in preview.Hurt) Outline(region, Ui.Color(60, 120, 220), 2);
        foreach (var (hit, region) in preview.Attacks)
        {
            var points = region.Points.Select(Screen).ToArray();
            draw.AddQuadFilled(points[0], points[1], points[2], points[3], Ui.Color(235, 60, 50, 90));
            Outline(region, Ui.Color(220, 40, 30), 2.5f);
            draw.AddText(points[2] + new Vector2(4, -16), Ui.Color(200, 40, 30), hit.Window.Id);
        }
        foreach (var equipment in preview.Entity.Equipment)
        {
            var tip = ActorPose.PropPoint(preview.Pose.Socket(equipment.Socket), equipment.Prop, equipment.Prop.Tip);
            draw.AddCircleFilled(frame.Screen(tip), 4 * Ui.Scale, Ui.Color(240, 200, 40));
        }
        if (preview.Action is { Mask: not null } masked)
            draw.AddText(frame.Origin + new Vector2(12, frame.Size.Y - 28 * Ui.Scale), Ui.Color(70, 80, 90), $"{masked.Id} over {session.PreviewRole}: layer weight {preview.Weight:P0}");
        if (frame.Hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) session.Report("Collision overlays: grey movement box, blue hurt regions, red active hits, yellow prop tips.");
    }

    // ---- Timeline ------------------------------------------------------------------------------------------------

    public void Timeline()
    {
        var clip = session.TransportClip; var transport = session.Transport;
        var label = session.PreviewAction is { } action ? $"Action {action}" : $"Role {session.PreviewRole}";
        ImGui.TextUnformatted(label); ImGui.SameLine();
        if (clip is null) { ImGui.TextDisabled(session.EntityError ?? "Nothing assigned to preview."); return; }
        ImGui.TextDisabled($"({clip.Name})"); ImGui.SameLine();
        if (ImGui.Button(transport.Playing ? "Pause" : "Play", new(64 * Ui.Scale, 0))) session.TogglePlay();
        ImGui.SameLine(); if (ImGui.Button("|<")) session.Seek(0);
        ImGui.SameLine(); if (ImGui.Button(">|")) session.Seek(clip.Duration);
        ImGui.SameLine(); ImGui.SetNextItemWidth(Math.Max(120, ImGui.GetContentRegionAvail().X - 100 * Ui.Scale));
        var time = transport.Time;
        if (ImGui.SliderFloat("##time", ref time, 0, clip.Duration, $"%.3f / {clip.Duration:F3} s")) { transport.Pause(); session.Seek(time); }
        ImGui.SameLine(); ImGui.SetNextItemWidth(-1);
        var speed = transport.Speed; if (ImGui.SliderFloat("##speed", ref speed, .1f, 2, "%.2fx")) transport.Speed = speed;
        Phases(clip, session.Entity?.Actions.GetValueOrDefault(session.PreviewAction ?? ""));
    }

    /// <summary>Anticipation, active and recovery intervals over the clip, with markers, events and the masked layer's weight. Click to scrub.</summary>
    private void Phases(MotionClip clip, ResolvedAction? action)
    {
        var draw = ImGui.GetWindowDrawList(); var start = ImGui.GetCursorScreenPos() + new Vector2(0, 4 * Ui.Scale);
        var width = ImGui.GetContentRegionAvail().X; var height = 26 * Ui.Scale;
        float X(float t) => start.X + t / clip.Duration * width;
        draw.AddRectFilled(start, start + new Vector2(width, height), Ui.Color(222, 225, 214));
        if (action is not null)
        {
            var hits = action.Hits;
            if (hits.Count > 0)
            {
                var open = hits.Min(h => h.Start); var close = hits.Max(h => h.Finish);
                draw.AddRectFilled(start, new(X(open), start.Y + height), Ui.Color(240, 214, 150));
                draw.AddRectFilled(new(X(close), start.Y), start + new Vector2(width, height), Ui.Color(184, 204, 226));
                foreach (var hit in hits) draw.AddRectFilled(new(X(hit.Start), start.Y), new(X(hit.Finish), start.Y + height), Ui.Color(228, 96, 80));
                draw.AddText(start + new Vector2(4, 5 * Ui.Scale), Ui.Color(90, 70, 30), "anticipation");
                draw.AddText(new(X(close) + 4, start.Y + 5 * Ui.Scale), Ui.Color(40, 60, 90), "recovery");
            }
            if (action.Mask is not null)
                for (var i = 1; i <= 64; i++)
                {
                    float T(int k) => clip.Duration * k / 64;
                    draw.AddLine(new(X(T(i - 1)), start.Y + height * (1 - action.Weight(T(i - 1)))), new(X(T(i)), start.Y + height * (1 - action.Weight(T(i)))), Ui.Color(40, 110, 90), 1.5f);
                }
            foreach (var cue in action.Events) { var x = X(cue.Seconds); draw.AddTriangleFilled(new(x - 4, start.Y + height + 8), new(x + 4, start.Y + height + 8), new(x, start.Y + height), Ui.Color(40, 90, 160)); draw.AddText(new(x + 5, start.Y + height), Ui.Color(40, 90, 160), cue.Event.Id); }
        }
        foreach (var marker in clip.Markers) { var x = X(marker.Time); draw.AddLine(new(x, start.Y - 2), new(x, start.Y + height + 2), Ui.Color(70, 70, 70), 1); draw.AddText(new(x + 3, start.Y - 15 * Ui.Scale), Ui.Color(70, 70, 70), marker.Id); }
        var playhead = X(session.Transport.Time);
        draw.AddLine(new(playhead, start.Y - 4), new(playhead, start.Y + height + 4), Ui.Color(232, 169, 55), 2);
        ImGui.SetCursorScreenPos(start);
        ImGui.InvisibleButton("phases", new(width, height + 14 * Ui.Scale));
        if (ImGui.IsItemActive()) { session.Transport.Pause(); session.Seek(Math.Clamp((ImGui.GetMousePos().X - start.X) / width, 0, 1) * clip.Duration); }
        if (action is null) Ui.Help("Choose an action in the outline to see its anticipation, active hit windows and recovery.");
    }
}
