using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>
/// The fit, material, layer and visibility fields shared by base parts and variant overrides. Feature inspectors (curve handles
/// and wave, cutout silhouette, face payload) are called unconditionally and decide their own applicability (P2c).
/// </summary>
internal sealed class ModelPartInspector(EditorSession session, ModelCutoutEditor cutout)
{
    private CharacterModel? Structure => session.SubjectModel?.Asset ?? session.Assets.Model(session.SubjectVariant?.Asset.Base)?.Asset;

    /// <summary>
    /// Draws a part's fields. On a base model (<paramref name="overrides"/> null) every field edits the part through
    /// <paramref name="change"/>; on a variant each field is labelled with its provenance, a marked label resets that
    /// property's override, and edits go through the same callback, which the variant turns into typed overrides.
    /// </summary>
    public void Draw(PuppetPart part, PartOverrideView? overrides, Action<Action<PuppetPart>> change)
    {
        var structure = Structure;
        var materialAsset = part.RenderMaterial.Asset;
        var sharedPaint = false;
        if (structure is not null)
        {
            var authoredMaterial = overrides is null ? part.RenderMaterial : structure.Parts.First(p => p.Id == part.Id).RenderMaterial.WithOverride(overrides.Material);
            sharedPaint = authoredMaterial.Asset is not null && authoredMaterial.Paint is null;
            part = part with { Material = part.RenderMaterial.Resolve(structure.Materials) };
            var defaultMaterial = overrides is null ? "(inline)" : "(base)";
            if (Ui.Combo("Material", materialAsset ?? defaultMaterial, structure.Materials.Keys.Order(StringComparer.Ordinal).Prepend(defaultMaterial)) is { } selected)
            {
                if (selected != defaultMaterial) change(p => p.Material = new() { Asset = selected });
                else if (overrides is null) change(p => p.Material = part.RenderMaterial with { Asset = null });
                else if (overrides.Material is not null) overrides.StripMaterial(m => m with { Asset = null });
                return;
            }
            if (materialAsset is null && ImGui.SmallButton("Share material"))
            {
                var id = ModelAuthoring.UniqueId(part.Id + "-surface", structure.Materials.Keys);
                var surface = part.RenderMaterial;
                if (session.Edit(session.Assets.Model(structure.Id), () => structure.Materials.Add(id, surface)))
                    change(p => p.Material = new() { Asset = id });
                return;
            }
        }
        bool Marked(string label, bool overridden, Action clear)
        {
            if (overrides is null)
            {
                ImGui.TextUnformatted(label);
                return false;
            }
            if (Ui.OverrideLabel(label, overridden))
            {
                clear();
                return true;
            }
            return false;
        }
        bool Property(string label, string property)
        {
            if (Marked(label, overrides?.Has(property) == true, () => overrides!.Reset(property)))
            {
                return true;
            }

            return false;
        }
        void Float(string label, string property, float value, float min, float max, Action<PuppetPart, float> set)
        {
            if (Property(label, property))
            {
                return;
            }

            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##" + label, ref value, min, max, "%.3f"))
            {
                change(p => set(p, value));
            }

            if (overrides is not null)
            {
                overrides.Note(property);
            }
            else
            {
                ParameterInspector.BoundNote(session.Assets.Resolve(session.SubjectId), PropertyRef.OfPart(part.Id, property));
            }
        }
        if (overrides is null && part.Geometry is IShape2D shape && shape is not SimplePolygon2D)
        {
            ShapeInspector.Draw(shape, geometry => change(p => p.Geometry = geometry));
        }
        Float("Scale X", RigProperties.ScaleX, part.ScaleX, -4, 4, (p, v) => p.ScaleX = v);
        Float("Scale Y", RigProperties.ScaleY, part.ScaleY, -4, 4, (p, v) => p.ScaleY = v);
        Float("Offset X", RigProperties.OffsetX, part.OffsetX, -2, 2, (p, v) => p.OffsetX = v);
        Float("Offset Y", RigProperties.OffsetY, part.OffsetY, -2, 2, (p, v) => p.OffsetY = v);
        var materialOverridden = overrides?.Material;
        if (part.Geometry is ICurve2D)
        {
            if (!Marked("Stroke width", materialOverridden?.StrokeWidth is not null, () => overrides!.StripMaterial(m => m with { StrokeWidth = null })))
            {
                var width = part.RenderMaterial.StrokeWidth ?? Structure?.LineWidth ?? .045f;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("##stroke-width", ref width, .005f, 2, "%.3f"))
                {
                    change(p => p.Material = p.RenderMaterial with { StrokeWidth = width });
                }
            }

        }
        if (overrides is null && structure is not null)
        {
            // Base-only feature inspectors, each checking the part's geometry itself: curve handles and the wave payload, the cutout silhouette.
            ModelCurveInspector.Draw(part, [.. structure.Controls.Select(c => c.Id)], change);
            cutout.Inspector(part, change);
        }
        if (!Marked("Fill", materialOverridden?.Fill is not null, () => overrides!.StripMaterial(m => m with { Fill = null })))
        {
            var fill = part.RenderMaterial.Fill ?? (part.Geometry is ICurve2D ? Structure?.Ink ?? "#222b32" : "#fff8e7");
            if (Ui.ColorHex("##fill", ref fill))
            {
                change(p => p.Material = p.RenderMaterial with { Fill = fill });
            }
        }
        if (!Marked("Outline width", materialOverridden?.Outline?.Width is not null, () => overrides!.StripMaterial(m => m with { Outline = m.Outline is null ? null : m.Outline with { Width = null } })))
        {
            var outlineWidth = part.RenderMaterial.Outline?.Width ?? .045f;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##outline-width", ref outlineWidth, 0, .15f, "%.3f"))
            {
                change(p => p.Material = p.RenderMaterial with { Outline = (p.RenderMaterial.Outline ?? new RenderOutlineDefinition2D()) with { Width = outlineWidth } });
            }
        }
        if (part.RenderMaterial.Outline is not null && !Marked("Outline color", materialOverridden?.Outline?.Color is not null,
            () => overrides!.StripMaterial(m => m with { Outline = m.Outline is null ? null : m.Outline with { Color = null } })))
        {
            var outlineColor = part.RenderMaterial.Outline?.Color ?? Structure?.Ink ?? "#222b32";
            if (Ui.ColorHex("##outline-color", ref outlineColor))
            {
                change(p => p.Material = p.RenderMaterial with { Outline = p.RenderMaterial.Outline! with { Color = outlineColor } });
            }

            if (part.RenderMaterial.Outline?.Color is not null && ImGui.SmallButton("Use model ink"))
            {
                change(p => p.Material = p.RenderMaterial with { Outline = (p.RenderMaterial.Outline ?? new()) with { Color = null } });
            }
        }
        if (overrides is not null)
        {
            overrides.Note(RigProperties.Material);
        }

        if (part.Geometry is IShape2D)
        {
            if (!Marked("Surface paint", materialOverridden?.Paint is not null, () => overrides!.StripMaterial(m => m with { Paint = null })) && ImGui.CollapsingHeader("Edit surface paint"))
            {
                Ui.Help(sharedPaint ? $"Editing shared material '{materialAsset}'. Paint changes reach every user; fill remains a local override." : "Paint belongs to this material and follows the body's shape and motion.");
                void Paint(Action<List<PartPaint>> edit)
                {
                    var patches = (part.RenderMaterial.Paint ?? []).ConvertAll(p => new PartPaint { Material = p.Material, Points = [.. p.Points] });
                    edit(patches);
                    try
                    {
                        PartPaint.Check(patches);
                        if (sharedPaint && materialAsset is { } id && structure is not null)
                        {
                            session.Change(session.Assets.Model(structure.Id), () => structure.Materials[id] = structure.Materials[id] with { Paint = patches });
                        }
                        else
                        {
                            change(p => p.Material = p.RenderMaterial with { Paint = patches });
                        }
                    }
                    catch (InvalidDataException) { /* Keep the last valid convex patch during a drag. */ }
                }
                if (ImGui.Button("Add paint patch"))
                {
                    Paint(p => p.Add(new() { Material = new() { Fill = "#754222" }, Points = [new(-.1f, -.1f), new(.1f, -.1f), new(.1f, .1f), new(-.1f, .1f)] }));
                }

                for (var i = 0; i < (part.RenderMaterial.Paint?.Count ?? 0); i++)
                {
                    var index = i;
                    var patch = part.RenderMaterial.Paint![i];
                    ImGui.PushID("paint-" + i);
                    if (ImGui.TreeNode("Patch " + (i + 1)))
                    {
                        var color = patch.Material!.Fill!;
                        if (Ui.ColorHex("Color", ref color))
                        {
                            Paint(p => p[index].Material = p[index].Material! with { Fill = color });
                        }

                        for (var j = 0; j < patch.Points.Count; j++)
                        {
                            var point = j;
                            var xy = patch.Points[j];
                            if (Ui.Drag2("Point " + (j + 1), ref xy, .005f, -2, 2))
                            {
                                Paint(p => p[index].Points[point] = xy);
                            }
                        }
                        if (ImGui.SmallButton("Delete patch"))
                        {
                            Paint(p => p.RemoveAt(index));
                        }

                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }
            }
        }
        // Feature inspectors decide their own applicability (plan section 3.10): a part shows a feature's rows only when its payload or geometry has it.
        ModelFaceInspector.Draw(part, overrides, change, Property, Float);
        if (overrides is null)
        {
            var layer = new CharacterLayer2D(part.Depth);
            if (CharacterUi.CharacterLayer("Shape layer offset", ref layer, -2, 2, offset: true))
            {
                change(p => p.Depth = layer.Order);
            }

            Ui.Help("Offsets the shape from its attached control's layer.");
        }
        if (!Property("Visibility", RigProperties.Hidden))
        {
            var hidden = part.Hidden;
            if (ImGui.Checkbox("Hidden", ref hidden))
            {
                change(p => p.Hidden = hidden);
            }

            overrides?.Note(RigProperties.Hidden);
        }
    }
}
