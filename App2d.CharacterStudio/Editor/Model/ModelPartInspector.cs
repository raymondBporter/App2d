using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Shape and appearance fields shared by base parts and variant overrides.</summary>
internal sealed class ModelPartInspector(EditorSession session, ModelCutoutEditor cutout)
{
    private CharacterModel? Structure => session.SubjectModel?.Asset ?? session.Assets.Model(session.SubjectVariant?.Asset.Base)?.Asset;

    public void Draw(PuppetPart part, PartOverride? overrides, Action<Action<PuppetPart>> change, Action<Action<PartOverride>>? reset = null)
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
                else if (overrides.Material is not null) reset!(o => o.Material = null);
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
        bool Marked(string label, bool overridden, Action<PartOverride> clear)
        {
            if (overrides is null)
            {
                ImGui.TextUnformatted(label);
                return false;
            }
            if (Ui.OverrideLabel(label, overridden))
            {
                reset!(clear);
                return true;
            }
            return false;
        }
        void Float(string label, float value, float min, float max, bool overridden, Action<PuppetPart, float> set, Action<PartOverride> clear)
        {
            if (Marked(label, overridden, clear))
            {
                return;
            }

            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##" + label, ref value, min, max, "%.3f"))
            {
                change(p => set(p, value));
            }
        }
        if (overrides is null && part.Geometry is IShape2D shape && shape is not SimplePolygon2D)
        {
            ShapeInspector.Draw(shape, geometry => change(p => p.Geometry = geometry));
        }
        Float("Scale X", part.ScaleX, -4, 4, overrides?.ScaleX is not null, (p, v) => p.ScaleX = v, o => o.ScaleX = null);
        Float("Scale Y", part.ScaleY, -4, 4, overrides?.ScaleY is not null, (p, v) => p.ScaleY = v, o => o.ScaleY = null);
        Float("Offset X", part.OffsetX, -2, 2, overrides?.OffsetX is not null, (p, v) => p.OffsetX = v, o => o.OffsetX = null);
        Float("Offset Y", part.OffsetY, -2, 2, overrides?.OffsetY is not null, (p, v) => p.OffsetY = v, o => o.OffsetY = null);
        if (part.Geometry is ICurve2D)
        {
            Float("Stroke width", part.RenderMaterial.StrokeWidth ?? Structure?.LineWidth ?? .045f, .005f, 2, overrides?.Material?.StrokeWidth is not null,
                (p, v) => p.Material = p.RenderMaterial with { StrokeWidth = v },
                o => o.Material = o.Material is null ? null : o.Material with { StrokeWidth = null });
            if (overrides is null && structure is not null)
                ModelCurveInspector.Draw(part, structure.Controls.Select(c => c.Id).ToArray(), change);
        }
        else if (overrides is null)
        {
            cutout.Inspector(part, change);
        }
        if (!Marked("Fill", overrides?.Material?.Fill is not null, o => o.Material = o.Material is null ? null : o.Material with { Fill = null }))
        {
            var fill = part.RenderMaterial.Fill ?? (part.Geometry is ICurve2D ? Structure?.Ink ?? "#222b32" : "#fff8e7");
            if (Ui.ColorHex("##fill", ref fill))
            {
                change(p => p.Material = p.RenderMaterial with { Fill = fill });
            }
        }
        Float("Outline width", part.RenderMaterial.Outline?.Width ?? .045f, 0, .15f, overrides?.Material?.Outline?.Width is not null,
            (p, v) => p.Material = p.RenderMaterial with { Outline = (p.RenderMaterial.Outline ?? new RenderOutlineDefinition2D()) with { Width = v } },
            o => o.Material = o.Material is null ? null : o.Material with
            {
                Outline = o.Material.Outline?.Color is { } color ? new RenderOutlineDefinition2D { Color = color } : null
            });
        if (part.RenderMaterial.Outline is not null && !Marked("Outline color", overrides?.Material?.Outline?.Color is not null,
            o => o.Material = o.Material is null ? null : o.Material with
            {
                Outline = o.Material.Outline?.Width is { } width ? new RenderOutlineDefinition2D { Width = width } : null
            }))
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
        if (part.Geometry is IShape2D)
        {
            if (!Marked("Surface paint", overrides?.Material?.Paint is not null, o => o.Material = o.Material! with { Paint = null }) && ImGui.CollapsingHeader("Edit surface paint"))
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
            if (!Marked("Default expression", overrides?.Face is not null, o => o.Face = null))
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##face", part.Face))
                {
                    foreach (var face in FaceExpressions.Names.Prepend("none"))
                    {
                        if (ImGui.Selectable(face, face == part.Face))
                        {
                            change(p => p.Face = face);
                        }
                    }

                    ImGui.EndCombo();
                }
            }
            if (part.Face != "none")
            {
                Float("Face offset", part.FaceX, -.4f, .4f, overrides?.FaceX is not null, (p, v) => p.FaceX = v, o => o.FaceX = null);
            }
        }
        if (overrides is null)
        {
            var layer = new CharacterLayer2D(part.Depth);
            if (CharacterUi.CharacterLayer("Shape layer offset", ref layer, -2, 2, offset: true))
            {
                change(p => p.Depth = layer.Order);
            }

            Ui.Help("Offsets the shape from its attached control's layer.");
        }
        if (!Marked("Visibility", overrides?.Hidden is not null, o => o.Hidden = null))
        {
            var hidden = part.Hidden;
            if (ImGui.Checkbox("Hidden", ref hidden))
            {
                change(p => p.Hidden = hidden);
            }
        }
    }
}
