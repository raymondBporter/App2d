using App2d.CharacterStudio.Editing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>
/// Edits a variant (P6c): its parameter values, the typed overrides of the selected control, part or attachment, its
/// animation-role bindings and saved looks. Every row says where its value comes from (base, parameter binding, this
/// variant's override or clear, the open clip), a marked label resets that one override so the property inherits again,
/// and a property no pass supports yet is shown as unsupported with the reason instead of a handle that does nothing.
/// </summary>
internal sealed class VariantModelInspector(EditorSession session, ModelPartInspector parts)
{
    private string _lookName = "";
    private readonly AttachmentInspector _attachments = new(session);
    private ResolvedModel? Resolved => session.Assets.Resolve(session.SubjectId);

    /// <summary>One line under a property row: its provenance, and the binding an override masks.</summary>
    public static void SourceNote(ResolvedModel resolved, PropertyRef property, MotionClip? clip = null)
    {
        var bound = resolved.Parameters.BindingOf(property);
        var note = resolved.Provenance(property) switch
        {
            PropertySource.Override => bound is { } masked ? $"override on this variant (masks parameter '{masked.Parameter.Id}': {masked.Binding.Describe(masked.Parameter.Id)})" : "override on this variant",
            PropertySource.Cleared => bound is { } masked ? $"cleared: the authored value, bypassing parameter '{masked.Parameter.Id}'" : "cleared on this variant",
            PropertySource.ParameterBinding => $"bound from parameter '{bound!.Value.Parameter.Id}': {bound.Value.Binding.Describe(bound.Value.Parameter.Id)}",
            _ => "from the base",
        };
        if (clip is not null && property.Control is { } control && AnimatedBy(clip, control, property.Property) is { } track)
        {
            note += $"; animated by '{clip.Id}' ({track} track)";
        }

        ImGui.TextDisabled("  " + note);
    }

    /// <summary>The open clip's track kind that moves a control property at pose time, if any.</summary>
    private static string? AnimatedBy(MotionClip clip, string control, string property)
    {
        var kind = property switch
        {
            RigProperties.LocalX or RigProperties.LocalY or RigProperties.LocalLayer => MotionClip.TranslateKind,
            RigProperties.LocalRotation => MotionClip.RotateKind,
            _ => null,
        };
        return kind is not null && clip.Tracks.Any(t => t.Target == control && t.Kind == kind) ? kind : null;
    }

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

        var resolved = Resolved;
        if (resolved is null)
        {
            Ui.Problem("The variant does not resolve: " + (session.Assets.Problems(document).FirstOrDefault() ?? "unknown problem") + " Reset the offending override below or repair the base.");
            Parameters(document, basis.Asset);
            Overrides(document, resolved: null);
            return;
        }

        // What is selected comes first; the variant-wide sections follow.
        if (session.Selection.Part is { } partId && resolved.Parts.FirstOrDefault(p => p.Id == partId) is { } part)
        {
            Ui.Header($"Part {part.Id} ({part.Kind})");
            ImGui.TextDisabled("Overrides on this variant; the base owns the shape.");
            var view = new PartOverrideView(document, basis.Asset, resolved, part.Id, session);
            parts.Draw(part, view, change => session.Change(document, () => ModelAuthoring.EditPartOverride(document.Asset, basis.Asset, part, change)));
        }
        if (session.Selection.Attachment is { } attachmentId)
        {
            _attachments.DrawVariant(document, resolved, attachmentId);
        }

        if (session.Selection.Control is { } control && resolved.Nodes.TryGetValue(control, out var node))
        {
            Control(document, basis.Asset, resolved, node);
        }

        Parameters(document, basis.Asset);
        Roles(document, basis.Asset, resolved);
        Looks(document, basis.Asset);
        Overrides(document, resolved);
        References.Draw(session, document.Id);
    }

    private void Parameters(AssetDocument<ModelVariant> document, CharacterModel basis)
    {
        var variant = document.Asset;
        if (basis.Parameters.Count == 0)
        {
            return;
        }

        Ui.Header("Parameters");
        foreach (var preset in basis.Presets)
        {
            if (ImGui.SmallButton(preset.Name))
            {
                session.Edit(document, () => ModelAuthoring.ApplyPreset(basis, document.Asset, preset.Id));
            }

            ImGui.SameLine();
        }
        ImGui.NewLine();
        foreach (var parameter in basis.Parameters)
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
            // The slider spans the suggested editor range; a value at the hard range (an extreme preset) is still shown and kept.
            var low = MathF.Min(parameter.Range.SoftMin, amount);
            var high = MathF.Max(parameter.Range.SoftMax, amount);
            if (ImGui.SliderFloat("##parameter-" + parameter.Id, ref amount, low, high, format, ImGuiSliderFlags.AlwaysClamp))
            {
                session.Change(document, () => document.Asset.Parameters[parameter.Id] = Math.Clamp(amount, parameter.Min, parameter.Max));
            }

            // Provenance, units and ranges (P4c): where the value came from, what the slider spans and what a file may hold.
            var preset = basis.Presets.FirstOrDefault(p => p.Values.TryGetValue(parameter.Id, out var v) ? v == amount : amount == parameter.Default && p.Values.Count == 0);
            var source = !overridden ? "base default" : preset is not null ? $"this variant, matching preset '{preset.Name}'" : "this variant";
            ImGui.TextDisabled($"  {parameter.Units}; from {source}; slider {parameter.Range.SoftMin:0.##}..{parameter.Range.SoftMax:0.##}, hard range {parameter.Min:0.##}..{parameter.Max:0.##}"
                + (amount < parameter.Range.SoftMin || amount > parameter.Range.SoftMax ? " (outside the suggested span)" : ""));
            if (parameter.Bindings.Count > 0)
            {
                ImGui.TextDisabled("  drives " + string.Join(", ", parameter.Bindings.Select(b => b.Target.ToString())));
            }
        }
        Ui.Help("Parameter values drive the base's bound lengths, layout, fitting and link positions; an explicit override below wins over a bound value. The slider span is an editing suggestion; the hard range is what a file may hold; neither is a joint limit.");
    }

    private void Control(AssetDocument<ModelVariant> document, CharacterModel basis, ResolvedModel resolved, RigNode node)
    {
        var id = node.Id;
        var declared = basis.Controls.First(c => c.Id == id);
        Ui.Header("Control " + id);
        ImGui.TextDisabled(node.Kind switch
        {
            RigNodeKinds.Bone when node.Link is { } link => $"Bone node bound to '{node.Parent}' at t = {link.T:0.##}; drag its tip in the viewport to override length and rotation.",
            RigNodeKinds.Bone => "Bone node: drag its tip in the viewport to override length and rotation; its origin moves through the local setup.",
            _ => "Point control: its rest moves through its parent-local setup overrides." + (declared.Length > 0 ? " Its legacy length is not a variant property; convert the run to a bone chain on the base." : ""),
        });
        if (node.Link is null)
        {
            var rest = LayeredPoint2D.From(resolved.Rest[id]);
            if (CharacterUi.LayeredPoint("Rest (character)", ref rest))
            {
                session.Change(document, () => ModelAuthoring.MoveRest(resolved, document.Asset, id, rest.ToVector3(), session.MoveChildren));
            }

            var children = session.MoveChildren;
            if (ImGui.Checkbox("Move children with it", ref children))
            {
                session.MoveChildren = children;
            }

            Ui.Help(children ? "The subtree follows through the hierarchy; only this control's setup is overridden." : "Unbound children get their own setup overrides so they stay in place; bound children follow their parent.");
        }
        else if (node.Link is { } link)
        {
            ImGui.TextDisabled($"Origin (character): {node.Rest.X:0.###}, {node.Rest.Y:0.###}, layer {node.Rest.Z:0.###}; override the parent's length or the link ({link.Orientation} axes).");
        }

        var clip = session.ClipDocument?.Asset;
        foreach (var declaration in RigProperties.Declarations(declared))
        {
            var target = PropertyRef.OfControl(id, declaration.Name);
            var source = resolved.Provenance(target);
            if (Ui.OverrideLabel(declaration.Name, source is PropertySource.Override or PropertySource.Cleared))
            {
                session.Edit(document, () => ModelAuthoring.ResetOverride(document.Asset, target));
                continue;
            }

            var value = resolved.Number(target);
            var degrees = declaration.Units == ParameterUnits.Radians;
            var shown = degrees ? float.RadiansToDegrees(value) : value;
            var range = declaration.Range ?? new Limit(-100, 100);
            ImGui.SetNextItemWidth(-1);
            if (Ui.Drag("##" + target.Key, ref shown, degrees ? 1f : .005f, degrees ? -720 : range.Min, degrees ? 720 : range.Max, degrees ? "%.1f°" : "%.3f"))
            {
                var next = degrees ? float.DegreesToRadians(shown) : shown;
                session.Change(document, () => ModelAuthoring.SetOverride(document.Asset, basis, PropertyOverride.Of(target, next)));
            }

            SourceNote(resolved, target, clip);
            if (source == PropertySource.ParameterBinding && ImGui.SmallButton("Pin the authored value##" + target.Key))
            {
                session.Edit(document, () => ModelAuthoring.ClearProperty(document.Asset, basis, target));
            }
        }
        foreach (var (property, reason) in RigProperties.Unsupported(declared))
        {
            ImGui.BeginDisabled();
            ImGui.TextUnformatted($"{property}: unsupported");
            ImGui.EndDisabled();
            ImGui.TextDisabled("  " + reason);
        }
        if (document.Asset.OverridesOf(PropertyRef.ControlKind, id).Any() && ImGui.SmallButton("Reset this control's overrides"))
        {
            session.Edit(document, () => ModelAuthoring.ResetOverrides(document.Asset, PropertyRef.ControlKind, id));
        }
    }

    private void Roles(AssetDocument<ModelVariant> document, CharacterModel basis, ResolvedModel resolved)
    {
        var roles = basis.MotionSets.SelectMany(s => s.Roles.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (roles.Length == 0)
        {
            return;
        }

        Ui.Header("Animation bindings");
        var clips = session.Assets.Clips.Where(c => c.Asset.Model == basis.Id).Select(c => c.Id).Order(StringComparer.Ordinal).ToArray();
        foreach (var role in roles)
        {
            var target = PropertyRef.OfRole(role);
            var overridden = resolved.RoleClips.TryGetValue(role, out var clip);
            if (Ui.OverrideLabel(role, overridden))
            {
                session.Edit(document, () => ModelAuthoring.ResetOverride(document.Asset, target));
                continue;
            }

            var sets = basis.MotionSets.Where(s => s.Roles.ContainsKey(role)).ToArray();
            var shown = clip ?? sets[0].Roles[role];
            ImGui.SetNextItemWidth(-1);
            if (Ui.Combo("##role-" + role, shown, clips) is { } chosen)
            {
                session.Edit(document, () => ModelAuthoring.SetOverride(document.Asset, basis, PropertyOverride.Of(target, chosen)));
            }

            ImGui.TextDisabled("  " + (overridden ? "override on this variant" : string.Join("; ", sets.Select(s => $"set '{s.Id}': {s.Roles[role]}"))));
        }
        Ui.Help("An entity on this variant plays the overridden clip for the role unless the entity assigns its own; the base's motion sets are untouched.");
    }

    private void Looks(AssetDocument<ModelVariant> document, CharacterModel basis)
    {
        Ui.Header("Look");
        foreach (var look in basis.Looks)
        {
            if (ImGui.SmallButton(look.Name + "##look-" + look.Id))
            {
                session.ApplyLook(look.Id);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(string.Join("\n", look.Parts.Select(p => $"{p.Key}: {string.Join(", ", new[] { p.Value.Material?.Fill, p.Value.Hidden is { } h ? (h ? "hidden" : "shown") : null }.OfType<string>())}")
                    .Concat(look.Expressions.Select(e => $"{e.Key}: {e.Value}"))));
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
        Ui.Help("Applying a look writes materials and instance expressions as this variant's overrides. Saving one edits the base, which then needs saving.");
    }

    private void Overrides(AssetDocument<ModelVariant> document, ResolvedModel? resolved)
    {
        var variant = document.Asset;
        Ui.Header("Overrides");
        ImGui.TextDisabled($"{variant.Parameters.Count} parameter value(s), {variant.Overrides.Count} property override(s).");
        foreach (var change in variant.Overrides.ToArray())
        {
            var masked = resolved?.Masks(change.Target) == true;
            ImGui.TextUnformatted(change.Describe() + (masked ? "  (masks a parameter binding)" : ""));
            if (ImGui.IsItemClicked())
            {
                session.Selection.Select(control: change.Target.Control, part: change.Target.Part, attachment: change.Target.Attachment);
            }

            if (Ui.OverrideMarker(true, "override-" + change.Key))
            {
                session.Edit(document, () => ModelAuthoring.ResetOverride(document.Asset, change.Target));
            }
        }
        if (Ui.Button("Reset all overrides", variant.Overrides.Count > 0))
        {
            session.Edit(document, () => document.Asset.Overrides.Clear());
        }

        Ui.Help("Unoverridden values follow the base, including its parameter bindings. Structural edits need the base: use Open base. Duplicate makes a sibling of the same base; Make independent flattens the resolved recipe into a new base.");
    }
}

/// <summary>A variant's view of one part's overrides for the shared part inspector: which properties are overridden, the material override, and how to reset.</summary>
internal sealed class PartOverrideView(AssetDocument<ModelVariant> document, CharacterModel basis, ResolvedModel resolved, string part, EditorSession session)
{
    public ResolvedModel Resolved => resolved;
    private PropertyRef Target(string property) => PropertyRef.OfPart(part, property);
    public bool Has(string property) => document.Asset.HasOverride(Target(property));
    public RenderMaterialDefinition2D? Material => document.Asset.Override(Target(RigProperties.Material))?.Material;
    public void Note(string property) => VariantModelInspector.SourceNote(resolved, Target(property));
    public void Reset(string property) => session.Edit(document, () => ModelAuthoring.ResetOverride(document.Asset, Target(property)));

    /// <summary>Strips members from the material override; an override left with nothing set is removed.</summary>
    public void StripMaterial(Func<RenderMaterialDefinition2D, RenderMaterialDefinition2D> strip) => session.Edit(document, () =>
    {
        var target = Target(RigProperties.Material);
        if (document.Asset.Override(target)?.Material is not { } material)
        {
            return;
        }

        var stripped = strip(material);
        if (stripped.Outline is { Width: null, Color: null })
        {
            stripped = stripped with { Outline = null };
        }

        if (stripped.Asset is null && stripped.Fill is null && stripped.Paint is null && stripped.StrokeWidth is null && stripped.Outline is null && stripped.Texture is null && stripped.Tint is null && stripped.Alpha is null && stripped.Source is null && stripped.TextureAnimation is null)
        {
            ModelAuthoring.ResetOverride(document.Asset, target);
        }
        else
        {
            ModelAuthoring.SetOverride(document.Asset, basis, PropertyOverride.Of(target, stripped));
        }
    });
}
