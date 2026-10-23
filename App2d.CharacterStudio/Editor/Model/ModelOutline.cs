using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Lists drawing parts and rig structure, and issues commands for the selected model.</summary>
internal sealed class ModelOutline(EditorSession session, ModelStructuralEdits structural)
{
    private AssetDocument<CharacterModel>? Base => session.SubjectModel;
    private AssetDocument<ModelVariant>? Variant => session.SubjectVariant;
    private CharacterModel? Structure => Base?.Asset ?? session.Assets.Model(Variant?.Asset.Base)?.Asset;
    private ResolvedModel? Resolved => session.Assets.Resolve(session.SubjectId);

    public void Draw()
    {
        var rig = session.EditRig;
        if (ImGui.Checkbox("Edit rig", ref rig))
        {
            session.EditRig = rig;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Show control handles and IK setup. The viewport shows rest while the rig is edited.");
        }

        var structure = Structure;
        if (structure is null)
        {
            Ui.Help("Open a model or variant.");
            return;
        }
        if (session.EditRig)
        {
            Rig(structure);
        }
        else
        {
            Parts(structure);
        }

        structural.Draw();
    }

    private void Parts(CharacterModel structure)
    {
        Ui.Header("Drawing parts");
        var parts = Resolved?.Parts ?? structure.Parts;
        foreach (var part in parts)
        {
            if (part.Hidden)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            }

            if (ImGui.Selectable($"{part.Id}  ({part.Geometry!.Kind}){(part.Hidden ? "  hidden" : "")}", session.Selection.Part == part.Id))
            {
                session.Selection.Select(part: part.Id);
            }

            if (part.Hidden)
            {
                ImGui.PopStyleColor();
            }
        }
        var selectedBone = structure.Controls.FirstOrDefault(c => c.Id == session.Selection.Control && (c.Length > 0 || c.Node != RigNodeKinds.Point));
        if (Base is not null && Ui.Button(selectedBone is null ? "Add shape to selected control" : "Add shape to selected bone", session.Selection.Control is not null || structure.Controls.Count > 0))
        {
            ImGui.OpenPopup("add-part");
        }

        if (ImGui.BeginPopup("add-part"))
        {
            var anchor = session.Selection.Control ?? structure.Controls[0].Id;
            foreach (var kind in GeometryJson2D.DrawableKinds)
            {
                var geometry = GeometryJson2D.CreateDefault(kind);
                if (ImGui.MenuItem($"{kind} on {anchor}", "", false, geometry is not ICurve2D || structure.Controls.Count > 1) && Base is { } document)
                {
                    session.Edit(document, () =>
                    {
                        var other = structure.Controls.FirstOrDefault(c => c.Parent == anchor)?.Id ?? structure.Controls.FirstOrDefault(c => c.Id != anchor)?.Id;
                        session.Selection.Select(part: selectedBone is not null && geometry is IShape2D shape
                            ? ModelAuthoring.AddPartToBone(document.Asset, shape, anchor).Id
                            : ModelAuthoring.AddPart(document.Asset, geometry, anchor, geometry is ICurve2D ? other : null).Id);
                    });
                }
            }
            ImGui.EndPopup();
        }
        if (Variant is not null)
        {
            Ui.Help("Variants restyle, resize or hide parts. Adding shapes belongs to the base.");
        }

        if (structure.Slots.Count > 0)
        {
            Ui.Header("Slots (back to front)");
            foreach (var slot in structure.Slots)
            {
                ImGui.TextDisabled($"{slot.Name ?? slot.Id} on {slot.Bone}");
            }

            Ui.Help("Choose the skin above the viewport. Animate edits attachment choices and draw order.");
        }
    }

    private void Rig(CharacterModel structure)
    {
        Ui.Header("Bones and joints");
        void Node(ModelControl control)
        {
            var children = structure.Controls.Where(c => c.Parent == control.Id).ToArray();
            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth | (children.Length == 0 ? ImGuiTreeNodeFlags.Leaf : 0)
                | (session.Selection.Control == control.Id ? ImGuiTreeNodeFlags.Selected : 0);
            var ik = structure.IkChains.FirstOrDefault(c => c.Joint == control.Id || c.End == control.Id || c.Solver == ModelChain.BoneSolver && c.Root == control.Id);
            var label = control.Node switch
            {
                RigNodeKinds.Bone => $"{control.Id}  (bone {control.Length:0.##}{(control.Link is not null ? ", bound" : "")})",
                RigNodeKinds.Affine => $"{control.Id}  (affine{(control.Length > 0 ? $" {control.Length:0.##}" : "")})",
                _ => control.Length > 0 ? $"{control.Id}  (bone {control.Length:0.##})" : control.Id,
            };
            var open = ImGui.TreeNodeEx(control.Id, flags, label + (ik is null ? "" : ik.End == control.Id ? "  (IK end)" : ik.Root == control.Id ? "  (IK rotation)" : "  (IK bend)"));
            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            {
                session.Selection.Select(control: control.Id);
            }

            if (open)
            {
                foreach (var child in children)
                {
                    Node(child);
                }

                ImGui.TreePop();
            }
        }
        foreach (var root in structure.Controls.Where(c => c.Parent is null))
        {
            Node(root);
        }

        if (structure.Controls.Count == 0)
        {
            Ui.Help("An empty model. Add a root bone, then child bones and shapes.");
        }

        if (Base is { } model)
        {
            if (ImGui.Button("Add root bone"))
            {
                structural.Request(model, "add a root bone", () => session.Selection.Select(control: ModelAuthoring.AddBone(model.Asset, null).Id));
            }

            ImGui.SameLine();
            if (Ui.Button("Add child bone", session.Selection.Control is not null))
            {
                structural.Request(model, "add a child bone", () => session.Selection.Select(control: ModelAuthoring.AddBone(model.Asset, session.Selection.Control).Id));
            }

            if (ImGui.Button("Add control"))
            {
                structural.Request(model, "add a control", () =>
                {
                    var parent = session.Selection.Control;
                    var at = parent is null ? new Vector3(0, 1, 0) : (Resolved ?? throw new InvalidDataException("The model does not resolve; repair it before adding controls.")).Rest[parent] + new Vector3(.3f, 0, 0);
                    session.Selection.Select(control: ModelAuthoring.AddControl(model.Asset, parent, at).Id);
                });
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Adds a child of the selected control, or a root control when none is selected.");
            }

            ImGui.SameLine();
            var pointEnd = structure.Controls.FirstOrDefault(c => c.Id == session.Selection.Control);
            var pointJoint = structure.Controls.FirstOrDefault(c => c.Id == pointEnd?.Parent);
            var pointRoot = structure.Controls.FirstOrDefault(c => c.Id == pointJoint?.Parent);
            // A point chain moves positions only; it needs point or tip-bound bone controls. A bone constraint needs two lengths.
            var pointChain = pointEnd is { Node: not RigNodeKinds.Affine, Length: 0 } && pointJoint is { Node: not RigNodeKinds.Affine } && pointRoot is { Node: not RigNodeKinds.Affine }
                && (pointJoint.Length == 0 || pointEnd is { Node: RigNodeKinds.Bone, Link.IsTip: true }) && (pointRoot.Length == 0 || pointJoint is { Node: RigNodeKinds.Bone, Link.IsTip: true });
            var boneChain = pointEnd is not null && pointJoint is { Length: > 0 } && pointRoot is { Length: > 0 };
            if (Ui.Button("Make IK constraint", pointChain || boneChain))
            {
                structural.Request(model, "add an IK constraint", () => session.Selection.Select(chain: (pointChain
                    ? ModelAuthoring.AddChain(model.Asset, session.Selection.Control!)
                    : ModelAuthoring.AddBoneIk(model.Asset, session.Selection.Control!)).Id));
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(boneChain && pointChain
                    ? "Select an endpoint with a parent and grandparent. Lengths are declared on both segments: this makes a rigid bone constraint (frames turn). A point chain would need the lengths cleared."
                    : "Select an endpoint with a parent and grandparent. Two declared lengths make a rigid bone constraint; otherwise a point chain, which moves positions only.");
            }
        }
        Ui.Header("IK constraints");
        foreach (var chain in structure.IkChains)
        {
            if (ImGui.Selectable($"{chain.Id}  ({chain.Solver}: {chain.Root} > {chain.Joint} > {chain.End})", session.Selection.Chain == chain.Id))
            {
                session.Selection.Select(chain: chain.Id);
            }
        }

        if (!structure.IkChains.Any())
        {
            Ui.Help("None.");
        }

        Ui.Header("Measures");
        foreach (var measure in structure.Measures)
        {
            ImGui.TextUnformatted($"{measure.Id}: {Resolved?.Measure(measure.Id) ?? 0:F3}  ({string.Join(" > ", measure.Path)})");
        }

        if (structure.Measures.Count == 0)
        {
            Ui.Help("None: every channel scales in model units.");
        }

        if (Variant is not null)
        {
            Ui.Help("Structure belongs to the base. Variant drags write rest overrides.");
        }
    }
}
