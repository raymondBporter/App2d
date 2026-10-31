using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>
/// A base model's declared parameters (P4a): each one's units, default, ranges and bindings, with add/remove for parameters
/// and bindings on the selected control or part. Editing a default re-resolves every preview that uses the defaults; the
/// values a variant chooses live in <see cref="VariantModelInspector"/>. Provenance beyond "bound from parameter X" is P4c.
/// </summary>
internal sealed class ParameterInspector(EditorSession session)
{
    private string _newId = "";
    private string _bindProperty = "";
    private string _bindKind = BindingKinds.Scale;
    private string _bindParameter = "";

    /// <summary>A one-line note under a property field when a parameter binding produces it, else nothing.</summary>
    public static void BoundNote(ResolvedModel? resolved, PropertyRef property)
    {
        if (resolved?.Parameters.BindingOf(property) is { } bound)
        {
            ImGui.TextDisabled($"  bound from parameter '{bound.Parameter.Id}': {bound.Binding.Describe(bound.Parameter.Id)}");
        }
    }

    public void Draw(AssetDocument<CharacterModel> document, ResolvedModel? resolved)
    {
        var model = document.Asset;
        Ui.Header("Parameters");
        if (model.Parameters.Count == 0)
        {
            Ui.Help("No exposed parameters. Declare one to drive bone lengths, layout offsets, part fitting and link positions from one value.");
        }

        foreach (var parameter in model.Parameters.ToArray())
        {
            ImGui.PushID("parameter-" + parameter.Id);
            var open = ImGui.CollapsingHeader($"{parameter.Label} ({parameter.Id}, {parameter.Units})");
            if (open)
            {
                ModelParameter Live() => document.Asset.Parameters.First(p => p.Id == parameter.Id);
                var name = parameter.Name ?? "";
                if (Ui.Text("Name", ref name, 60))
                {
                    session.Change(document, () => Live().Name = name.Trim().Length == 0 ? null : name);
                }

                var value = parameter.Default;
                if (Ui.Slider($"Default ({parameter.Units})", ref value, parameter.Range.SoftMin, parameter.Range.SoftMax))
                {
                    session.Change(document, () => { Live().Default = value; document.Asset.Validate(); });
                }

                ImGui.TextDisabled($"  slider spans the suggested range {parameter.Range.SoftMin:0.##}..{parameter.Range.SoftMax:0.##}; a file may hold {parameter.Min:0.##}..{parameter.Max:0.##}; neither is a joint limit.");

                var range = new System.Numerics.Vector2(parameter.Min, parameter.Max);
                if (Ui.Drag2("Hard range (min / max)", ref range, .01f, -10000, 10000))
                {
                    session.Change(document, () => { var live = Live(); live.Min = range.X; live.Max = range.Y; document.Asset.Validate(); });
                }

                var soft = new System.Numerics.Vector2(parameter.Range.SoftMin, parameter.Range.SoftMax);
                if (Ui.Drag2("Suggested range (min / max)", ref soft, .01f, -10000, 10000))
                {
                    session.Change(document, () => { var live = Live(); live.SoftMin = soft.X; live.SoftMax = soft.Y; document.Asset.Validate(); });
                }

                if (Ui.Combo("Units", parameter.Units, ParameterUnits.All) is { } units)
                {
                    session.Edit(document, () => { Live().Units = units; document.Asset.Validate(); });
                }

                if (resolved is not null && resolved.ParameterValues.TryGetValue(parameter.Id, out var used) && used != parameter.Default)
                {
                    ImGui.TextDisabled($"Previewing {used:0.###} (this variant's value).");
                }

                ImGui.TextUnformatted(parameter.Bindings.Count == 0 ? "Bindings: none" : "Bindings:");
                foreach (var binding in parameter.Bindings.ToArray())
                {
                    ImGui.PushID(binding.Target.Key);
                    if (ImGui.SmallButton("×"))
                    {
                        session.Edit(document, () => ModelAuthoring.RemoveBinding(document.Asset, binding.Target));
                    }

                    ImGui.SameLine();
                    ImGui.TextUnformatted($"{binding.Target}: {binding.Describe(parameter.Id)}");
                    ImGui.PopID();
                }
                if (ImGui.SmallButton("Remove parameter"))
                {
                    session.Edit(document, () => ModelAuthoring.RemoveParameter(document.Asset, parameter.Id));
                }
            }
            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(120 * Ui.Scale);
        ImGui.InputTextWithHint("##new-parameter", "parameter id", ref _newId, 40);
        ImGui.SameLine();
        if (Ui.Button("Add parameter", _newId.Trim().Length > 0))
        {
            var id = _newId.Trim();
            if (session.Edit(document, () => ModelAuthoring.AddParameter(document.Asset, id, id, ParameterUnits.Factor, 1, .5f, 2)))
            {
                _newId = "";
            }
        }

        if (model.Presets.Count > 0)
        {
            ImGui.TextDisabled("Presets: " + string.Join(", ", model.Presets.Select(p => $"{p.Name} ({string.Join(", ", p.Values.Select(v => $"{v.Key} {v.Value:0.##}"))})")));
        }

        BindSelection(document, resolved);
        Ui.Help("A parameter's bindings write bone lengths, layout offsets, part fitting and link positions when the model resolves; an explicit variant override of the same property wins over the bound value.");
    }

    /// <summary>Binds a property of the selected control or part to one of the declared parameters.</summary>
    private void BindSelection(AssetDocument<CharacterModel> document, ResolvedModel? resolved)
    {
        var model = document.Asset;
        if (model.Parameters.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> properties;
        Func<string, PropertyRef> target;
        string owner;
        if (session.Selection.Control is { } control && model.Controls.FirstOrDefault(c => c.Id == control) is { } selected)
        {
            properties = RigProperties.ForControl(selected);
            target = property => PropertyRef.OfControl(control, property);
            owner = "control " + control;
        }
        else if (session.Selection.Part is { } part && model.Parts.Any(p => p.Id == part))
        {
            properties = RigProperties.Part;
            target = property => PropertyRef.OfPart(part, property);
            owner = "part " + part;
        }
        else if (session.Selection.Attachment is { } attachment && model.Attachments.Any(a => a.Id == attachment))
        {
            properties = RigProperties.Attachment;
            target = property => PropertyRef.OfAttachment(attachment, property);
            owner = "attachment " + attachment;
        }
        else
        {
            Ui.Help("Select a control, part or attachment to bind one of its properties.");
            return;
        }

        if (properties.Count == 0)
        {
            Ui.Help($"The {owner} exposes no bindable properties.");
            return;
        }

        Ui.Header("Bind " + owner);
        var free = properties.Where(p => resolved?.Parameters.BindingOf(target(p)) is null).ToArray();
        foreach (var property in properties.Except(free))
        {
            var bound = resolved!.Parameters.BindingOf(target(property))!.Value;
            ImGui.TextDisabled($"{property}: bound from parameter '{bound.Parameter.Id}'");
        }

        if (free.Length == 0)
        {
            return;
        }

        if (!free.Contains(_bindProperty))
        {
            _bindProperty = free[0];
        }

        if (model.Parameters.All(p => p.Id != _bindParameter))
        {
            _bindParameter = model.Parameters[0].Id;
        }

        if (Ui.Combo("Property", _bindProperty, free) is { } property2)
        {
            _bindProperty = property2;
        }

        if (Ui.Combo("Parameter", _bindParameter, model.Parameters.Select(p => p.Id)) is { } parameter)
        {
            _bindParameter = parameter;
        }

        if (Ui.Combo("Kind", _bindKind, BindingKinds.All, k => k switch { BindingKinds.Scale => "scale (authored × parameter)", BindingKinds.Map => "map (parameter range → value range)", _ => "set (= parameter)" }) is { } kind)
        {
            _bindKind = kind;
        }

        if (ImGui.Button("Add binding"))
        {
            var binding = new ParameterBinding { Kind = _bindKind, Target = target(_bindProperty) };
            var id = _bindParameter;
            session.Edit(document, () => ModelAuthoring.AddBinding(document.Asset, id, binding));
        }
    }
}
