using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Mathematics.Transforms;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Edits a base model, its selected rig element or part, and its motion-set assignments.</summary>
internal sealed class BaseModelInspector(EditorSession session, ModelStructuralEdits structural, ModelPartInspector parts)
{
    private string _newSet = "";
    private ResolvedModel? Resolved => session.Assets.Resolve(session.SubjectId);

    public void Draw(AssetDocument<CharacterModel> document)
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
            Ui.Header((control.Length > 0 ? "Bone " : "Control ") + control.Id);
            var rest = control.Rest.Layered;
            if (control.Transform is not null && Resolved is { } resolved) rest = LayeredPoint2D.From(resolved.Rest[id]);
            if (CharacterUi.LayeredPoint(control.Length > 0 ? "Origin" : "Rest", ref rest)) session.Change(document, () => ModelAuthoring.MoveRest(document.Asset, id, rest.ToVector3(), session.MoveChildren));
            if (control.Length > 0)
            {
                var length = control.Length;
                if (Ui.Drag("Bone length", ref length, .01f, .01f, 100)) session.Change(document, () => ModelAuthoring.ResizeBone(document.Asset, id, length));
                var degrees = control.RestAngle * 180f / MathF.PI;
                if (Ui.Drag("Rest angle (degrees)", ref degrees, 1f, -360, 360))
                    session.Change(document, () => ModelAuthoring.RotateRestBone(document.Asset, id, degrees * MathF.PI / 180f));
                Ui.Help("The bone starts at this control and points along its local +X axis. Its children and attached shapes follow rotation.");
            }
            if (control.Transform is { } transform)
            {
                Ui.Header("Local affine transform");
                void Number(string label, float value, Action<Affine2D, float> set, float min = -100, float max = 100)
                {
                    if (Ui.Drag(label, ref value, .01f, min, max))
                    {
                        session.Change(document, () =>
                        { set(document.Asset.Controls.First(c => c.Id == id).Transform!, value); ModelAuthoring.SyncAffineRest(document.Asset); });
                    }
                }
                Number("Local X", transform.X, (t, v) => t.X = v);
                Number("Local Y", transform.Y, (t, v) => t.Y = v);
                Number("Local rotation (degrees)", transform.Rotation * 180 / MathF.PI, (t, v) => t.Rotation = v * MathF.PI / 180, -720, 720);
                Number("Scale X", transform.ScaleX, (t, v) => t.ScaleX = v);
                Number("Scale Y", transform.ScaleY, (t, v) => t.ScaleY = v);
                Number("Shear X (degrees)", transform.ShearX * 180 / MathF.PI, (t, v) => t.ShearX = v * MathF.PI / 180, -720, 720);
                Number("Shear Y (degrees)", transform.ShearY * 180 / MathF.PI, (t, v) => t.ShearY = v * MathF.PI / 180, -720, 720);
            }
            var children = session.MoveChildren; if (ImGui.Checkbox("Move children with it", ref children)) session.MoveChildren = children;
            var parents = model.Controls.Select(c => c.Id).Where(c => c != id).Prepend("(locomotion)");
            if (Ui.Combo("Parent", control.Parent ?? "(locomotion)", parents) is { } parent)
                structural.Request(document, $"reparent '{id}' to '{parent}'", () => ModelAuthoring.Reparent(document.Asset, id, parent == "(locomotion)" ? null : parent));
            if (Ui.Combo("Translation scales with", control.Scale, model.Measures.Select(m => m.Id).Prepend(CharacterModel.Unit)) is { } scale)
                structural.Request(document, $"scale '{id}' by '{scale}'", () => document.Asset.Controls.First(c => c.Id == id).Scale = scale);
            if (ImGui.Button("Delete control")) structural.Request(document, $"delete '{id}'", () => { ModelAuthoring.RemoveControl(document.Asset, id); session.Selection.Clear(); });
            Ui.Help("Deleting removes shapes attached to it. Children, chains and measures must be changed first.");
        }
        if (session.Selection.Chain is { } chainId && model.IkChains.FirstOrDefault(c => c.Id == chainId) is { } chain)
        {
            Ui.Header("IK constraint " + chain.Id);
            ImGui.TextUnformatted($"{chain.Root} > {chain.Joint} > {chain.End}");
            ImGui.TextDisabled($"Solver: {chain.Solver}");
            if (ImGui.Button("Flip bend")) session.Edit(document, () => document.Asset.IkChains.First(c => c.Id == chainId).Bend *= -1);
            bool FreeFrame(ModelControl c)
            {
                for (ModelControl? node = c; node is not null; node = model.Controls.FirstOrDefault(p => p.Id == node.Parent))
                    if (model.IkChains.Any(k => k.Joint == node.Id || k.End == node.Id || k.Solver == ModelChain.BoneSolver && k.Root == node.Id)) return false;
                return true;
            }
            var frames = model.Controls.Where(FreeFrame).Select(c => c.Id).Prepend(CharacterModel.Locomotion);
            if (Ui.Combo("Target frame", chain.Frame, frames) is { } frame) structural.Request(document, $"key '{chainId}' in '{frame}'", () => document.Asset.IkChains.First(c => c.Id == chainId).Frame = frame);
            if (Ui.Combo("Target scales with", chain.Scale, model.Measures.Select(m => m.Id).Prepend(CharacterModel.Unit)) is { } scale)
                structural.Request(document, $"scale '{chainId}' by '{scale}'", () => document.Asset.IkChains.First(c => c.Id == chainId).Scale = scale);
            if (!model.Measures.Any(m => m.Path.SequenceEqual([chain.Root, chain.Joint, chain.End])) && ImGui.Button("Add reach measure"))
                structural.Request(document, $"add measure '{chainId}-reach'", () => ModelAuthoring.AddMeasure(document.Asset, ModelAuthoring.UniqueId(chainId + "-reach", document.Asset.Measures.Select(m => m.Id)), [chain.Root, chain.Joint, chain.End]));
            if (ImGui.Button("Remove chain")) structural.Request(document, $"remove chain '{chainId}'", () => { ModelAuthoring.RemoveChain(document.Asset, chainId); session.Selection.Clear(); });
            Ui.Help("The locomotion frame supports contact pins. A free control frame makes the target follow that control.");
            if (chain.Solver == ModelChain.BoneSolver) Ui.Help("Bone IK turns both segments and their attachments. Uniform scale and reflection are supported; shear and nonuniform segment frames are not.");
        }
        if (session.Selection.Part is { } partId && model.Parts.FirstOrDefault(p => p.Id == partId) is { } part)
        {
            Ui.Header($"Part {part.Id} ({part.Kind})");
            var controls = model.Controls.Select(c => c.Id).ToArray();
            if (Ui.Combo("Shape", part.Kind, PuppetPartKinds.All.Where(kind => !PuppetPartKinds.IsStroke(kind) || controls.Length > 1)) is { } kind)
            {
                session.Edit(document, () => ModelAuthoring.SetPartKind(document.Asset, partId, kind));
                return;
            }
            if (Ui.Combo("Attach to", part.A, controls) is { } a) session.Edit(document, () => { Part(document, partId).A = a; document.Asset.Validate(); });
            if (!PuppetPartKinds.IsStroke(part.Kind) && Ui.Combo("Bone frame", part.Frame ?? "(none)", controls.Where(id => model.Controls.Any(c => c.Id == id && (c.Length > 0 || c.Transform is not null))).Prepend("(none)")) is { } bone)
                session.Edit(document, () => { var edited = Part(document, partId); edited.Frame = bone == "(none)" ? null : bone; if (edited.Frame is not null) edited.B = null; document.Asset.Validate(); });
            if (Ui.Combo(PuppetPartKinds.IsStroke(part.Kind) ? "End" : "Point toward", part.B ?? "(none)", PuppetPartKinds.IsStroke(part.Kind) ? controls : controls.Prepend("(none)")) is { } b)
                session.Edit(document, () => { var edited = Part(document, partId); edited.B = b == "(none)" ? null : b; if (edited.B is not null) edited.Frame = null; document.Asset.Validate(); });
            if (part.Frame is not null)
            {
                var degrees = part.Angle * 180f / MathF.PI;
                if (Ui.Drag("Shape angle (degrees)", ref degrees, 1f, -360, 360)) session.Change(document, () => Part(document, partId).Angle = degrees * MathF.PI / 180f);
                if (part.Material?.Texture is { } texture)
                {
                    ImGui.TextDisabled("Image: " + texture);
                    var scaleX = part.ScaleX; var scaleY = part.ScaleY;
                    if (Ui.Drag("Attachment scale X", ref scaleX, .01f, -100, 100)) session.Change(document, () => Part(document, partId).ScaleX = scaleX);
                    if (Ui.Drag("Attachment scale Y", ref scaleY, .01f, -100, 100)) session.Change(document, () => Part(document, partId).ScaleY = scaleY);
                }
            }
            parts.Draw(part, null, change => session.Change(document, () => change(Part(document, partId))));
            if (ImGui.Button("Delete part")) session.Edit(document, () => { ModelAuthoring.RemovePart(document.Asset, partId); session.Selection.Clear(); });
        }
    }

    private static PuppetPart Part(AssetDocument<CharacterModel> document, string id) => document.Asset.Parts.First(p => p.Id == id);

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
                        foreach (var clip in clips)
                        {
                            if (ImGui.Selectable(clip.Name + "##" + clip.Id, clip.Id == assigned))
                                session.AssignRole(model.Id, set.Id, role, clip.Id);
                        }

                        ImGui.EndCombo();
                    }
                    ImGui.PopID();
                }
                if (users.Length > 0) ImGui.TextDisabled("Selected by " + string.Join(", ", users.Select(u => u.Name)));
                if (ImGui.SmallButton("Copy to new set"))
                {
                    var id = ModelAuthoring.UniqueId(set.Id + "-copy", model.MotionSets.Select(s => s.Id)); 
                    session.NewMotionSet(model.Id, id, set.Name + " copy", set.Id); 
                }
                ImGui.SameLine(); if (ImGui.SmallButton("Remove set")) session.RemoveMotionSet(model.Id, set.Id);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(140 * Ui.Scale); ImGui.InputTextWithHint("##new-set", "new set name", ref _newSet, 60); ImGui.SameLine();
        if (Ui.Button("Add empty set", _newSet.Trim().Length > 0))
        {
            var id = ModelAuthoring.UniqueId(new string([.. _newSet.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]).Trim('-') is { Length: > 0 } slug ? slug : "set", model.MotionSets.Select(s => s.Id));
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
}
