using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>
/// The face payload's inspector (P2b/P2c): it decides its own applicability. A shape part without a face offers "Add face" on
/// the base only; the expression, offset and "Remove face" rows appear only while the part carries the payload. On a variant
/// the rows carry provenance and reset through the shared part-inspector helpers, and the payload cannot be added or removed.
/// </summary>
internal static class ModelFaceInspector
{
    public static void Draw(PuppetPart part, PartOverrideView? overrides, Action<Action<PuppetPart>> change, Func<string, string, bool> property, Action<string, string, float, float, float, Action<PuppetPart, float>> number)
    {
        if (part.Face is not { } face)
        {
            if (overrides is null && FaceAppearance.Supports(part.Geometry) && ImGui.SmallButton("Add face"))
            {
                change(p => p.Face = FaceAppearance.Starter);
            }

            return;
        }

        if (!property("Default expression", RigProperties.Face))
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##face", face.Expression))
            {
                foreach (var expression in FaceExpressions.Names.Prepend(FaceAppearance.None))
                {
                    if (ImGui.Selectable(expression, expression == face.Expression))
                    {
                        change(p => p.Face = p.Face! with { Expression = expression });
                    }
                }

                ImGui.EndCombo();
            }

            overrides?.Note(RigProperties.Face);
        }
        number("Face offset", RigProperties.FaceX, face.OffsetX, -.4f, .4f, (p, v) => p.Face = p.Face! with { OffsetX = v });
        if (overrides is null)
        {
            if (ImGui.SmallButton("Remove face"))
            {
                change(p => p.Face = null);
            }

            Ui.Help("The procedural face draws its expression over this shape. Clips and variants keep their own expression; removing the payload reports any clip still animating it.");
        }
    }
}
