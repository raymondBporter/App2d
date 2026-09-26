using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// What a character is made of. Drawing parts are selected by default; Edit rig exposes control handles and IK setup.
/// A base model edits structure and rest geometry. A variant edits build values and appearance as explicit overrides,
/// each with a reset, and never touches its base.
/// </summary>
internal sealed class ModelView(EditorSession session, Viewport viewport) : IWorkspaceView
{
    private string? _dragging;
    private Vector3 _grab;
    private (string Description, Action Apply)? _structural;
    private bool _confirmOpen;

    public Workspace Mode => Workspace.Model;
    public float TimelineHeight => 64;

    private AssetDocument<CharacterModel>? Base => session.SubjectModel;
    private AssetDocument<ModelVariant>? Variant => session.SubjectVariant;
    private CharacterModel? Structure => Base?.Asset ?? session.Assets.Model(Variant?.Asset.Base)?.Asset;
    private ResolvedModel? Resolved => session.Assets.Resolve(session.SubjectId);

    // ---- Outline -------------------------------------------------------------------------------------------------

    public void Outline()
    {
        var rig = session.EditRig; if (ImGui.Checkbox("Edit rig", ref rig)) session.EditRig = rig;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Show control handles and IK setup. The viewport shows rest while the rig is edited.");
        var structure = Structure;
        if (structure is null) { Ui.Help("Open a model or variant."); return; }
        if (session.EditRig) Rig(structure); else Parts(structure);
        DrawConfirm();
    }

    private void Parts(CharacterModel structure)
    {
        Ui.Header("Drawing parts");
        var parts = Resolved?.Parts ?? structure.Parts;
        foreach (var part in parts)
        {
            if (part.Hidden) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            if (ImGui.Selectable($"{part.Id}  ({part.Kind}){(part.Hidden ? "  hidden" : "")}", session.Selection.Part == part.Id)) Select(part: part.Id);
            if (part.Hidden) ImGui.PopStyleColor();
        }
        if (Base is not null && Ui.Button("Add shape to selected control", session.Selection.Control is not null || structure.Controls.Count > 0))
            ImGui.OpenPopup("add-part");
        if (ImGui.BeginPopup("add-part"))
        {
            var anchor = session.Selection.Control ?? structure.Controls[0].Id;
            foreach (var kind in new[] { "ellipse", "box", "stroke" })
                if (ImGui.MenuItem($"{kind} on {anchor}", "", false, kind != "stroke" || structure.Controls.Count > 1) && Base is { } document)
                    session.Edit(document, () =>
                    {
                        var other = structure.Controls.FirstOrDefault(c => c.Parent == anchor)?.Id ?? structure.Controls.FirstOrDefault(c => c.Id != anchor)?.Id;
                        Select(part: ModelAuthoring.AddPart(document.Asset, kind, anchor, kind == "stroke" ? other : null).Id);
                    });
            ImGui.EndPopup();
        }
        if (Variant is not null) Ui.Help("Variants restyle, resize or hide parts. Adding shapes belongs to the base.");
    }

    private void Rig(CharacterModel structure)
    {
        Ui.Header("Controls");
        void Node(ModelControl control)
        {
            var children = structure.Controls.Where(c => c.Parent == control.Id).ToArray();
            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth | (children.Length == 0 ? ImGuiTreeNodeFlags.Leaf : 0)
                | (session.Selection.Control == control.Id ? ImGuiTreeNodeFlags.Selected : 0);
            var ik = structure.Chains.FirstOrDefault(c => c.Joint == control.Id || c.End == control.Id);
            var open = ImGui.TreeNodeEx(control.Id, flags, control.Id + (ik is null ? "" : ik.End == control.Id ? "  (IK end)" : "  (IK bend)"));
            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Select(control: control.Id);
            if (open) { foreach (var child in children) Node(child); ImGui.TreePop(); }
        }
        foreach (var root in structure.Controls.Where(c => c.Parent is null)) Node(root);
        if (structure.Controls.Count == 0) Ui.Help("An empty model. Add a first control, then connect more to it.");
        if (Base is { } model)
        {
            if (ImGui.Button("Add control")) Structural("add a control", () =>
            {
                var parent = session.Selection.Control; var at = parent is null ? new Vector3(0, 1, 0) : Resolved!.Rest[parent] + new Vector3(.3f, 0, 0);
                Select(control: ModelAuthoring.AddControl(model.Asset, parent, at).Id);
            });
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Adds a child of the selected control, or a root control when none is selected.");
            ImGui.SameLine();
            if (Ui.Button("Make IK chain", session.Selection.Control is not null)) Structural("add an IK chain", () =>
                Select(chain: ModelAuthoring.AddChain(model.Asset, session.Selection.Control!).Id));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The selected control becomes the tip of a two-bone chain over its parent and grandparent.");
        }
        Ui.Header("IK chains");
        foreach (var chain in structure.Chains) if (ImGui.Selectable($"{chain.Id}  ({chain.Root} > {chain.Joint} > {chain.End})", session.Selection.Chain == chain.Id)) Select(chain: chain.Id);
        if (structure.Chains.Count == 0) Ui.Help("None.");
        Ui.Header("Measures");
        foreach (var measure in structure.Measures) ImGui.TextUnformatted($"{measure.Id}: {Resolved?.Measure(measure.Id) ?? 0:F3}  ({string.Join(" > ", measure.Path)})");
        if (structure.Measures.Count == 0) Ui.Help("None: every channel scales in model units.");
        if (Variant is not null) Ui.Help("Structure belongs to the base. Variant drags write rest overrides.");
    }

    private void Select(string? control = null, string? part = null, string? chain = null)
    {
        session.Selection.Clear(); session.Selection.Control = control; session.Selection.Part = part; session.Selection.Chain = chain;
    }

    // ---- Structural edits ----------------------------------------------------------------------------------------

    /// <summary>Applies a structural edit to the base, first listing its dependents when it has any.</summary>
    private void Structural(string description, Action apply)
    {
        if (Base is not { } model) return;
        var (variants, clips) = session.Assets.Dependents(model.Id);
        if (variants.Count + clips.Count == 0) { session.Edit(model, apply); return; }
        _structural = (description, apply); _confirmOpen = true;
    }

    private void DrawConfirm()
    {
        if (_structural is not { } pending || Base is not { } model) return;
        if (_confirmOpen) { ImGui.OpenPopup("Structural edit"); _confirmOpen = false; }
        if (!ImGui.BeginPopupModal("Structural edit", ImGuiWindowFlags.AlwaysAutoResize)) { _structural = null; return; }
        var (variants, clips) = session.Assets.Dependents(model.Id);
        ImGui.TextUnformatted($"This will {pending.Description} on '{model.Name}', changing its structure.");
        ImGui.TextUnformatted($"Dependents: {variants.Count} variant(s), {clips.Count} animation(s).");
        foreach (var clip in clips) ImGui.BulletText(clip.Name + " (" + clip.Id + ")");
        foreach (var variant in variants) ImGui.BulletText(variant.Name + " (" + variant.Id + ")");
        Ui.Help("Animations that stay compatible move to the new structure revision when the model is saved. Any that no longer fit are reported for repair; nothing is deleted. The edit is undoable.");
        if (ImGui.Button("Apply")) { session.Edit(model, pending.Apply); _structural = null; ImGui.CloseCurrentPopup(); }
        ImGui.SameLine(); if (ImGui.Button("Cancel")) { _structural = null; ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }

    // ---- Inspector -----------------------------------------------------------------------------------------------

    public void Inspector()
    {
        if (Variant is { } variant) VariantInspector(variant);
        else if (Base is { } model) BaseInspector(model);
        else { Ui.Help("Open a model or variant from the browser, or create one with New."); return; }
        foreach (var problem in session.ActiveDocument is { } document ? session.Assets.Problems(document) : []) Ui.Problem(problem);
        if (Variant is null && Base is { } opened) References.Draw(session, opened.Id);
    }

    private void BaseInspector(AssetDocument<CharacterModel> document)
    {
        var model = document.Asset;
        Ui.Header("Base model");
        var name = model.Name; if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0) session.Change(document, () => document.Asset.Name = name);
        var ink = model.Ink; if (Ui.ColorHex("Ink", ref ink)) session.Change(document, () => document.Asset.Ink = ink);
        var line = model.LineWidth; if (Ui.Slider("Outline width", ref line, .005f, .12f)) session.Change(document, () => document.Asset.LineWidth = line);
        if (model.Build is not null) Ui.Help($"Exposes '{model.Build}' build values to its variants.");
        Dependents(document);
        MotionSets(document);
        GroupsAndLooks(model);

        if (session.Selection.Control is { } id && model.Controls.FirstOrDefault(c => c.Id == id) is { } control)
        {
            Ui.Header("Control " + control.Id);
            var rest = control.Rest.XYZ;
            if (Ui.Drag3("Rest X / Y / depth", ref rest)) session.Change(document, () => ModelAuthoring.MoveRest(document.Asset, id, rest, session.MoveChildren));
            var children = session.MoveChildren; if (ImGui.Checkbox("Move children with it", ref children)) session.MoveChildren = children;
            var parents = model.Controls.Select(c => c.Id).Where(c => c != id).Prepend("(locomotion)");
            if (Ui.Combo("Parent", control.Parent ?? "(locomotion)", parents) is { } parent)
                Structural($"reparent '{id}' to '{parent}'", () => ModelAuthoring.Reparent(document.Asset, id, parent == "(locomotion)" ? null : parent));
            if (Ui.Combo("Translation scales with", control.Scale, model.Measures.Select(m => m.Id).Prepend(CharacterModel.Unit)) is { } scale)
                Structural($"scale '{id}' by '{scale}'", () => document.Asset.Controls.First(c => c.Id == id).Scale = scale);
            if (ImGui.Button("Delete control")) Structural($"delete '{id}'", () => { ModelAuthoring.RemoveControl(document.Asset, id); session.Selection.Clear(); });
            Ui.Help("Deleting removes shapes attached to it. Children, chains and measures must be changed first.");
        }
        if (session.Selection.Chain is { } chainId && model.Chains.FirstOrDefault(c => c.Id == chainId) is { } chain)
        {
            Ui.Header("IK chain " + chain.Id);
            ImGui.TextUnformatted($"{chain.Root} > {chain.Joint} > {chain.End}");
            if (ImGui.Button("Flip bend")) session.Edit(document, () => document.Asset.Chains.First(c => c.Id == chainId).Bend *= -1);
            var frames = model.Controls.Select(c => c.Id).Where(c => !model.Chains.Any(k => k.Joint == c || k.End == c)).Prepend(CharacterModel.Locomotion);
            if (Ui.Combo("Target frame", chain.Frame, frames) is { } frame) Structural($"key '{chainId}' in '{frame}'", () => document.Asset.Chains.First(c => c.Id == chainId).Frame = frame);
            if (Ui.Combo("Target scales with", chain.Scale, model.Measures.Select(m => m.Id).Prepend(CharacterModel.Unit)) is { } scale)
                Structural($"scale '{chainId}' by '{scale}'", () => document.Asset.Chains.First(c => c.Id == chainId).Scale = scale);
            if (!model.Measures.Any(m => m.Path.SequenceEqual([chain.Root, chain.Joint, chain.End])) && ImGui.Button("Add reach measure"))
                Structural($"add measure '{chainId}-reach'", () => ModelAuthoring.AddMeasure(document.Asset, ModelAuthoring.UniqueId(chainId + "-reach", document.Asset.Measures.Select(m => m.Id)), [chain.Root, chain.Joint, chain.End]));
            if (ImGui.Button("Remove chain")) Structural($"remove chain '{chainId}'", () => { ModelAuthoring.RemoveChain(document.Asset, chainId); session.Selection.Clear(); });
            Ui.Help("Feet usually key in the locomotion frame so they can plant; hands in a shoulder or chest frame.");
        }
        if (session.Selection.Part is { } partId && model.Parts.FirstOrDefault(p => p.Id == partId) is { } part)
        {
            Ui.Header($"Part {part.Id} ({part.Kind})");
            var controls = model.Controls.Select(c => c.Id).ToArray();
            if (Ui.Combo("Attach to", part.A, controls) is { } a) session.Edit(document, () => { Part(document, partId).A = a; document.Asset.Validate(); });
            if (Ui.Combo(part.Kind == "stroke" ? "End" : "Point toward", part.B ?? "(none)", part.Kind == "stroke" ? controls : controls.Prepend("(none)")) is { } b)
                session.Edit(document, () => { Part(document, partId).B = b == "(none)" ? null : b; document.Asset.Validate(); });
            PartFields(part, null, change => session.Change(document, () => change(Part(document, partId))));
            if (ImGui.Button("Delete part")) session.Edit(document, () => { ModelAuthoring.RemovePart(document.Asset, partId); session.Selection.Clear(); });
        }
    }

    private static PuppetPart Part(AssetDocument<CharacterModel> document, string id) => document.Asset.Parts.First(p => p.Id == id);

    private string _newSet = "";

    /// <summary>Role-to-clip tables on the base. A new set copies another's assignments; there is no set inheritance.</summary>
    private void MotionSets(AssetDocument<CharacterModel> document)
    {
        var model = document.Asset;
        Ui.Header("Motion sets");
        var clips = session.Assets.ClipsFor(model.Id).OrderBy(c => c.Name).ToArray();
        foreach (var set in model.MotionSets.ToArray())
        {
            ImGui.PushID(set.Id);
            var users = session.Assets.EntitiesOn(model.Id).Where(e => e.Asset.MotionSet == set.Id).ToArray();
            if (ImGui.TreeNodeEx($"{set.Name} ({set.Id})", ImGuiTreeNodeFlags.None, $"{set.Name}  ({set.Roles.Count} roles, {users.Length} entities)"))
            {
                var name = set.Name; if (Ui.Text("Set name", ref name, 60) && name.Trim().Length > 0) session.Change(document, () => document.Asset.MotionSets.First(s => s.Id == set.Id).Name = name);
                foreach (var role in EntityAuthoring.CommonRoles.Concat(set.Roles.Keys).Distinct())
                {
                    ImGui.PushID(role);
                    var assigned = set.Roles.GetValueOrDefault(role);
                    ImGui.TextUnformatted(role); ImGui.SameLine(90 * Ui.Scale); ImGui.SetNextItemWidth(-1);
                    if (ImGui.BeginCombo("##clip", assigned is null ? "(unassigned)" : session.Assets.Clip(assigned)?.Name ?? assigned + " (missing)"))
                    {
                        if (ImGui.Selectable("(unassigned)", assigned is null)) session.AssignRole(model.Id, set.Id, role, null);
                        foreach (var clip in clips) if (ImGui.Selectable(clip.Name + "##" + clip.Id, clip.Id == assigned)) session.AssignRole(model.Id, set.Id, role, clip.Id);
                        ImGui.EndCombo();
                    }
                    ImGui.PopID();
                }
                if (users.Length > 0) ImGui.TextDisabled("Selected by " + string.Join(", ", users.Select(u => u.Name)));
                if (ImGui.SmallButton("Copy to new set")) { var id = ModelAuthoring.UniqueId(set.Id + "-copy", model.MotionSets.Select(s => s.Id)); session.NewMotionSet(model.Id, id, set.Name + " copy", set.Id); }
                ImGui.SameLine(); if (ImGui.SmallButton("Remove set")) session.RemoveMotionSet(model.Id, set.Id);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(140 * Ui.Scale); ImGui.InputTextWithHint("##new-set", "new set name", ref _newSet, 60); ImGui.SameLine();
        if (Ui.Button("Add empty set", _newSet.Trim().Length > 0))
        {
            var id = ModelAuthoring.UniqueId(new string(_newSet.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-') is { Length: > 0 } slug ? slug : "set", model.MotionSets.Select(s => s.Id));
            if (session.NewMotionSet(model.Id, id, _newSet.Trim(), null)) _newSet = "";
        }
        Ui.Help("Sets describe an artistic choice such as Heavy; any build can use any set. Missing roles stay visibly unassigned.");
    }

    private static void GroupsAndLooks(CharacterModel model)
    {
        if (model.Groups.Count > 0)
        {
            Ui.Header("Control groups");
            foreach (var group in model.Groups) ImGui.TextWrapped($"{group.Id}: {string.Join(", ", group.Targets)}");
            Ui.Help("A masked action owns a group's channels over locomotion.");
        }
        if (model.Looks.Count > 0)
        {
            Ui.Header("Looks");
            ImGui.TextWrapped(string.Join(", ", model.Looks.Select(l => l.Name)));
            Ui.Help("Variants apply looks as overrides. Save a variant's look from its inspector.");
        }
    }

    private void Dependents(AssetDocument<CharacterModel> document)
    {
        var (variants, clips) = session.Assets.Dependents(document.Id);
        ImGui.TextDisabled($"Used by {variants.Count} variant(s) and {clips.Count} animation(s); revision {document.Asset.StructureRevision}.");
        if (!document.IsNew && session.Assets.StructureChanged(document))
            Ui.Problem($"Structure changed since saving: saving raises the revision to {document.Asset.StructureRevision + 1} and moves compatible animations with it.");
    }

    /// <summary>Shape fields shared by base parts and variant overrides. <paramref name="overrides"/> is null for a base part.</summary>
    private static void PartFields(PuppetPart part, PartOverride? overrides, Action<Action<PuppetPart>> change, Action<Action<PartOverride>>? reset = null)
    {
        bool Marked(string label, bool overridden, Action<PartOverride> clear)
        {
            if (overrides is null) { ImGui.TextUnformatted(label); return false; }
            if (Ui.OverrideLabel(label, overridden)) { reset!(clear); return true; }
            return false;
        }
        void Float(string label, float value, float min, float max, bool overridden, Action<PuppetPart, float> set, Action<PartOverride> clear)
        {
            if (Marked(label, overridden, clear)) return;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##" + label, ref value, min, max, "%.3f")) change(p => set(p, value));
        }
        Float(part.Kind == "stroke" ? "Thickness" : "Width", part.Width, .005f, 2, overrides?.Width is not null, (p, v) => p.Width = v, o => o.Width = null);
        if (part.Kind != "stroke")
        {
            Float("Height", part.Height, .005f, 2, overrides?.Height is not null, (p, v) => p.Height = v, o => o.Height = null);
            Float("Offset X", part.OffsetX, -2, 2, overrides?.OffsetX is not null, (p, v) => p.OffsetX = v, o => o.OffsetX = null);
            Float("Offset Y", part.OffsetY, -2, 2, overrides?.OffsetY is not null, (p, v) => p.OffsetY = v, o => o.OffsetY = null);
            if (overrides is null && part.Kind == "box") Float("Roundness", part.Roundness, 0, 1, false, (p, v) => p.Roundness = v, _ => { });
            if (!Marked("Fill", overrides?.Fill is not null, o => o.Fill = null))
            { var fill = part.Fill; if (Ui.ColorHex("##fill", ref fill)) change(p => p.Fill = fill); }
            if (!Marked("Default expression", overrides?.Face is not null, o => o.Face = null))
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##face", part.Face))
                {
                    foreach (var face in FaceExpressions.Names.Prepend("none")) if (ImGui.Selectable(face, face == part.Face)) change(p => p.Face = face);
                    ImGui.EndCombo();
                }
            }
            if (part.Face != "none") Float("Face offset", part.FaceX, -.4f, .4f, overrides?.FaceX is not null, (p, v) => p.FaceX = v, o => o.FaceX = null);
        }
        if (overrides is null) Float("Depth (positive is behind)", part.Depth, -2, 2, false, (p, v) => p.Depth = v, _ => { });
        if (!Marked("Visibility", overrides?.Hidden is not null, o => o.Hidden = null))
        { var hidden = part.Hidden; if (ImGui.Checkbox("Hidden", ref hidden)) change(p => p.Hidden = hidden); }
    }

    private void VariantInspector(AssetDocument<ModelVariant> document)
    {
        var variant = document.Asset; var basis = session.Assets.Model(variant.Base);
        Ui.Header("Variant");
        var name = variant.Name; if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0) session.Change(document, () => document.Asset.Name = name);
        ImGui.TextUnformatted("Base: " + (basis?.Name ?? variant.Base + " (missing)"));
        ImGui.SameLine(); if (Ui.Button("Open base", basis is not null)) session.Open(variant.Base);
        if (basis is null) return;

        if (BuildRules.For(basis.Asset) is { } rule)
        {
            Ui.Header("Build");
            foreach (var preset in rule.Presets)
            {
                if (ImGui.SmallButton(preset.Name))
                    session.Edit(document, () => { document.Asset.Build.Clear(); foreach (var (id, value) in preset.Values) if (value != rule.Values.First(v => v.Id == id).Default) document.Asset.Build[id] = value; });
                ImGui.SameLine();
            }
            ImGui.NewLine();
            foreach (var value in rule.Values)
            {
                var overridden = variant.Build.TryGetValue(value.Id, out var amount);
                if (Ui.OverrideLabel(value.Name, overridden)) { session.Edit(document, () => document.Asset.Build.Remove(value.Id)); continue; }
                amount = overridden ? amount : value.Default; ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("##build-" + value.Id, ref amount, value.Range.SoftMin, value.Range.SoftMax, "%.2fx", ImGuiSliderFlags.AlwaysClamp))
                    session.Change(document, () => document.Asset.Build[value.Id] = amount);
            }
            Ui.Help("Build values reshape the base's rest pose; animations keep playing and rescale with the measures they use.");
        }

        var resolved = Resolved;
        if (session.Selection.Part is { } partId && resolved?.Parts.FirstOrDefault(p => p.Id == partId) is { } part)
        {
            Ui.Header($"Part {part.Id} ({part.Kind})");
            var overrides = variant.Parts.GetValueOrDefault(partId) ?? new();
            PartFields(part, overrides,
                change => session.Change(document, () => Override(document, partId, part, change)),
                clear => session.Edit(document, () => { var o = document.Asset.Parts[partId]; clear(o); if (o.IsEmpty) document.Asset.Parts.Remove(partId); }));
        }
        if (session.Selection.Control is { } control && resolved is not null)
        {
            Ui.Header("Control " + control);
            var overridden = variant.Rest.ContainsKey(control);
            if (Ui.OverrideLabel("Rest X / Y / depth", overridden)) session.Edit(document, () => document.Asset.Rest.Remove(control));
            else
            {
                var rest = resolved.Rest[control]; ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat3("##rest", ref rest, .005f)) session.Change(document, () => ModelAuthoring.MoveRest(resolved, document.Asset, control, rest, session.MoveChildren));
            }
            var children = session.MoveChildren; if (ImGui.Checkbox("Move children with it", ref children)) session.MoveChildren = children;
        }
        Ui.Header("Look");
        foreach (var look in basis.Asset.Looks)
        {
            if (ImGui.SmallButton(look.Name + "##look-" + look.Id)) session.ApplyLook(look.Id);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(string.Join("\n", look.Parts.Select(p => $"{p.Key}: {string.Join(", ", new[] { p.Value.Fill, p.Value.Face, p.Value.Hidden is { } h ? (h ? "hidden" : "shown") : null }.OfType<string>())}")));
            ImGui.SameLine();
        }
        ImGui.NewLine();
        ImGui.SetNextItemWidth(140 * Ui.Scale); ImGui.InputTextWithHint("##look-name", "look name", ref _lookName, 60); ImGui.SameLine();
        if (Ui.Button("Save look to base", _lookName.Trim().Length > 0))
        {
            var id = new string(_lookName.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            if (session.SaveLook(id.Length > 0 ? id : "look", _lookName.Trim())) _lookName = "";
        }
        Ui.Help("Applying a look writes colors and faces as this variant's overrides. Saving one edits the base, which then needs saving.");

        Ui.Header("Overrides");
        ImGui.TextDisabled($"{variant.Build.Count} build value(s), {variant.Rest.Count} rest position(s), {variant.Parts.Count} part(s).");
        if (Ui.Button("Reset all rest positions", variant.Rest.Count > 0)) session.Edit(document, () => document.Asset.Rest.Clear());
        Ui.Help("Unoverridden values follow the base. Structural edits need the base: use Open base.");
        References.Draw(session, document.Id);
    }

    private string _lookName = "";

    /// <summary>Writes a part edit as an override: the edited field is compared against the resolved part and stored only where it now differs.</summary>
    private static void Override(AssetDocument<ModelVariant> document, string partId, PuppetPart resolved, Action<PuppetPart> change)
    {
        var edited = resolved with { }; change(edited);
        var o = document.Asset.Parts.GetValueOrDefault(partId) ?? new();
        if (edited.Width != resolved.Width) o.Width = edited.Width;
        if (edited.Height != resolved.Height) o.Height = edited.Height;
        if (edited.OffsetX != resolved.OffsetX) o.OffsetX = edited.OffsetX;
        if (edited.OffsetY != resolved.OffsetY) o.OffsetY = edited.OffsetY;
        if (edited.Fill != resolved.Fill) o.Fill = edited.Fill;
        if (edited.Face != resolved.Face) o.Face = edited.Face;
        if (edited.FaceX != resolved.FaceX) o.FaceX = edited.FaceX;
        if (edited.Hidden != resolved.Hidden) o.Hidden = edited.Hidden;
        document.Asset.Parts[partId] = o;
    }

    // ---- Viewport ------------------------------------------------------------------------------------------------

    public void Overlay(ViewportFrame frame)
    {
        if (frame.Primary is not { } primary) return;
        var pose = primary.Subject.Pose; var model = primary.Subject.Model; var draw = frame.Draw;
        if (session.EditRig)
        {
            foreach (var control in model.Order.Where(c => c.Parent is not null)) draw.AddLine(frame.Screen(pose.World(control.Parent!)), frame.Screen(pose.World(control.Id)), Ui.Color(104, 112, 122, 170), 1.5f);
            foreach (var control in model.Order)
            {
                var p = frame.Screen(pose.World(control.Id)); var selected = session.Selection.Control == control.Id;
                var end = model.Chains.Values.Any(c => c.End == control.Id);
                draw.AddCircleFilled(p, (selected ? 6 : 4.5f) * Ui.Scale, selected ? Ui.Color(232, 169, 55) : end ? Ui.Color(50, 145, 160) : Ui.Color(230, 233, 229));
                draw.AddCircle(p, (selected ? 6 : 4.5f) * Ui.Scale, Ui.Color(64, 78, 89), 16, 1);
                if (selected) draw.AddText(p + new Vector2(9, -18) * Ui.Scale, Ui.Color(44, 53, 68), control.Id);
            }
            DragControls(frame, model, pose);
            return;
        }
        if (session.Selection.Part is { } selectedPart && model.Parts.FirstOrDefault(p => p.Id == selectedPart) is { } part) Outline(frame, part, pose);
        if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var hit = model.Parts.Where(p => !p.Hidden).Select(p => (Part: p, Score: PartGeometry.Distance(p, pose.World, frame.World(frame.Mouse))))
                .Where(h => h.Score <= 1).OrderBy(h => h.Score).FirstOrDefault();
            if (hit.Part is not null) Select(part: hit.Part.Id); else session.Selection.Clear();
        }
        if (frame.Hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) viewport.Fit(primary.Subject);
    }

    private void DragControls(ViewportFrame frame, ResolvedModel model, EvaluatedPose pose)
    {
        if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragging = model.Order.Select(c => c.Id).OrderBy(id => Vector2.Distance(frame.Screen(pose.World(id)), frame.Mouse))
                .FirstOrDefault(id => Vector2.Distance(frame.Screen(pose.World(id)), frame.Mouse) < 12 * Ui.Scale);
            if (_dragging is not null) { Select(control: _dragging); _grab = pose.World(_dragging) - frame.World(frame.Mouse); session.BeginDrag(); }
            else session.Selection.Clear();
        }
        if (_dragging is null) return;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { _dragging = null; session.EndDrag(); return; }
        if (ImGui.GetIO().MouseDelta == Vector2.Zero) return;
        var target = frame.World(frame.Mouse) + _grab;
        session.DragControl(_dragging, target with { Z = pose.World(_dragging).Z });
    }

    private static void Outline(ViewportFrame frame, PuppetPart part, EvaluatedPose pose)
    {
        var outline = PartGeometry.Contour(part, pose.World).Select(p => frame.Screen(p)).ToArray();
        for (var i = 0; i < outline.Length; i++) frame.Draw.AddLine(outline[i], outline[(i + 1) % outline.Length], Ui.Color(232, 169, 55), 2);
    }

    // ---- Timeline: preview transport -----------------------------------------------------------------------------

    public void Timeline() => TransportBar.Draw(session, preview: true);
}
