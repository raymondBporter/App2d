using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Edits variant build values, explicit overrides, and saved looks.</summary>
internal sealed class VariantModelInspector(EditorSession session, ModelPartInspector parts)
{
    private string _lookName = "";
    private readonly AttachmentInspector _attachments = new(session);
    private ResolvedModel? Resolved => session.Assets.Resolve(session.SubjectId);

    public void Draw(AssetDocument<ModelVariant> document)
    {
        var variant = document.Asset;
        var basis = session.Assets.Model(variant.Base);
        Ui.Header("Variant");
        var name = variant.Name;
        if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0)
        {
            session.Change(document, () => document.Asset.Name = name);
        }

        ImGui.TextUnformatted("Base: " + (basis?.Name ?? variant.Base + " (missing)"));
        ImGui.SameLine();
        if (Ui.Button("Open base", basis is not null))
        {
            session.Open(variant.Base);
        }

        if (basis is null)
        {
            return;
        }

        if (basis.Asset.Parameters.Count > 0)
        {
            Ui.Header("Parameters");
            foreach (var preset in basis.Asset.Presets)
            {
                if (ImGui.SmallButton(preset.Name))
                {
                    session.Edit(document, () => ModelAuthoring.ApplyPreset(basis.Asset, document.Asset, preset.Id));
                }

                ImGui.SameLine();
            }
            ImGui.NewLine();
            foreach (var parameter in basis.Asset.Parameters)
            {
                var overridden = variant.Parameters.TryGetValue(parameter.Id, out var amount);
                if (Ui.OverrideLabel(parameter.Label, overridden))
                {
                    session.Edit(document, () => document.Asset.Parameters.Remove(parameter.Id));
                    continue;
                }
                amount = overridden ? amount : parameter.Default;
                ImGui.SetNextItemWidth(-1);
                var format = parameter.Units == ParameterUnits.Factor ? "%.2fx" : $"%.3f {parameter.Units}";
                if (ImGui.SliderFloat("##parameter-" + parameter.Id, ref amount, parameter.Range.SoftMin, parameter.Range.SoftMax, format, ImGuiSliderFlags.AlwaysClamp))
                {
                    session.Change(document, () => document.Asset.Parameters[parameter.Id] = amount);
                }

                if (parameter.Bindings.Count > 0)
                {
                    ImGui.TextDisabled("  drives " + string.Join(", ", parameter.Bindings.Select(b => b.Target.ToString())));
                }
            }
            Ui.Help("Parameter values drive the base's bound lengths, layout, fitting and link positions; animations keep playing and rescale with the measures they use.");
        }

        var resolved = Resolved;
        if (session.Selection.Part is { } partId && resolved?.Parts.FirstOrDefault(p => p.Id == partId) is { } part)
        {
            Ui.Header($"Part {part.Id} ({part.Geometry!.Kind})");
            var overrides = variant.Parts.GetValueOrDefault(partId) ?? new();
            parts.Draw(part, overrides,
                change => session.Change(document, () => ModelAuthoring.EditPartOverride(document.Asset, part, change)),
                clear => session.Edit(document, () =>
                {
                    var o = document.Asset.Parts[partId];
                    clear(o);
                    if (o.IsEmpty)
                    {
                        document.Asset.Parts.Remove(partId);
                    }
                }));
        }
        if (session.Selection.Attachment is { } attachmentId)
        {
            _attachments.DrawVariant(document, basis.Asset, attachmentId);
        }

        if (session.Selection.Control is { } control && resolved is not null)
        {
            Ui.Header("Control " + control);
            var overridden = variant.Rest.ContainsKey(control);
            if (Ui.OverrideLabel("Rest position and layer", overridden))
            {
                session.Edit(document, () => document.Asset.Rest.Remove(control));
            }
            else
            {
                var rest = LayeredPoint2D.From(resolved.Rest[control]);
                if (CharacterUi.LayeredPoint("Rest", ref rest))
                {
                    session.Change(document, () => ModelAuthoring.MoveRest(resolved, document.Asset, control, rest.ToVector3(), session.MoveChildren));
                }
            }
            var children = session.MoveChildren;
            if (ImGui.Checkbox("Move children with it", ref children))
            {
                session.MoveChildren = children;
            }
        }
        Ui.Header("Look");
        foreach (var look in basis.Asset.Looks)
        {
            if (ImGui.SmallButton(look.Name + "##look-" + look.Id))
            {
                session.ApplyLook(look.Id);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(string.Join("\n", look.Parts.Select(p => $"{p.Key}: {string.Join(", ", new[] { p.Value.Material?.Fill, p.Value.Face, p.Value.Hidden is { } h ? (h ? "hidden" : "shown") : null }.OfType<string>())}")));
            }

            ImGui.SameLine();
        }
        ImGui.NewLine();
        ImGui.SetNextItemWidth(140 * Ui.Scale);
        ImGui.InputTextWithHint("##look-name", "look name", ref _lookName, 60);
        ImGui.SameLine();
        if (Ui.Button("Save look to base", _lookName.Trim().Length > 0))
        {
            var id = new string([.. _lookName.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]).Trim('-');
            if (session.SaveLook(id.Length > 0 ? id : "look", _lookName.Trim()))
            {
                _lookName = "";
            }
        }
        Ui.Help("Applying a look writes colors and faces as this variant's overrides. Saving one edits the base, which then needs saving.");

        Ui.Header("Overrides");
        ImGui.TextDisabled($"{variant.Parameters.Count} parameter value(s), {variant.Rest.Count} rest position(s), {variant.Parts.Count} part(s).");
        if (Ui.Button("Reset all rest positions", variant.Rest.Count > 0))
        {
            session.Edit(document, () => document.Asset.Rest.Clear());
        }

        Ui.Help("Unoverridden values follow the base. Structural edits need the base: use Open base.");
        References.Draw(session, document.Id);
    }
}
