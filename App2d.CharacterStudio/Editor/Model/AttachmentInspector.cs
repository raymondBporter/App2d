using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>
/// One library part instance on a character (P5c). On the base model it edits the instance: which asset, which socket, its
/// placement, mirror, fit, material and visibility, and removes it. On a variant it edits that wearer's override of the same
/// instance with reset per field. Either way the shared asset is one click away and labelled as shared: editing it reaches
/// every wearer, editing the instance reaches this character and its variants, editing the override reaches this variant.
/// </summary>
internal sealed class AttachmentInspector(EditorSession session)
{
    private ResolvedModel? Resolved => session.Assets.Resolve(session.SubjectId);

    public void DrawBase(AssetDocument<CharacterModel> document, string id)
    {
        var instance = document.Asset.Attachments.FirstOrDefault(a => a.Id == id);
        if (instance is null)
        {
            return;
        }

        Ui.Header($"Attachment {id}");
        ImGui.TextDisabled("Instance on this character: every variant wears it unless it overrides this instance.");
        PartInstance Live() => document.Asset.Attachments.First(a => a.Id == id);
        var assets = session.Assets.Parts.Select(p => p.Id).Order(StringComparer.Ordinal).ToArray();
        if (Ui.Combo("Library part", instance.Asset, assets, a => session.Assets.Part(a)?.Name is { } n ? $"{n} ({a})" : a) is { } asset)
        {
            session.Edit(document, () => ModelAuthoring.SetAttachmentAsset(document.Asset, id, asset, session.Assets.PartOf));
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Edit shared part"))
        {
            session.Open(instance.Asset);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Opens the library asset. Edits there reach every character and variant that wears it.");
        }

        if (Ui.Combo("Socket", instance.Socket, document.Asset.Sockets.Select(s => s.Id)) is { } socket)
        {
            session.Edit(document, () => { Live().Socket = socket; document.Asset.Validate(); });
        }

        // P10c: the connection (a socket on the wearer's control or on another instance's control) and root-motion ownership.
        if (document.Asset.Sockets.FirstOrDefault(s => s.Id == instance.Socket) is { } mountSocket)
        {
            var where = ResolvedModel.NestedSocket(mountSocket) is { } nested ? $"attachment '{nested.Instance}' control '{nested.Control}' (evaluated after that instance)" : $"control '{mountSocket.Control}'";
            ImGui.TextDisabled($"Connection: socket '{mountSocket.Id}' on {where}{(mountSocket.Usage is { } usage ? $", takes '{usage}'" : "")}.");
        }

        var owns = document.Asset.MotionOwner == id;
        if (ImGui.Checkbox("Owns the root motion", ref owns))
        {
            session.Edit(document, () => { document.Asset.MotionOwner = owns ? id : null; document.Asset.Validate(); });
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(owns ? "This instance's clips carry the wearer's travel; the wearer's own clips may neither travel nor plant." : document.Asset.MotionOwner is { } other ? $"'{other}' owns the root motion; one instance at most." : "The wearer's own clips carry the travel.");
        }

        var offset = new Vector2(instance.X, instance.Y);
        if (Ui.Drag2("Offset in socket axes", ref offset, .005f, -100, 100))
        {
            session.Change(document, () => { var live = Live(); live.X = offset.X; live.Y = offset.Y; document.Asset.Validate(); });
        }

        ParameterInspector.BoundNote(Resolved, PropertyRef.OfAttachment(id, RigProperties.X));
        ParameterInspector.BoundNote(Resolved, PropertyRef.OfAttachment(id, RigProperties.Y));
        var angle = float.RadiansToDegrees(instance.Angle);
        if (Ui.Drag("Angle (degrees)", ref angle, 1f, -720, 720))
        {
            session.Change(document, () => { Live().Angle = float.DegreesToRadians(angle); document.Asset.Validate(); });
        }

        var scale = instance.Scale;
        if (Ui.Drag("Size", ref scale, .01f, .001f, 100))
        {
            session.Change(document, () => { Live().Scale = scale; document.Asset.Validate(); });
        }

        ParameterInspector.BoundNote(Resolved, PropertyRef.OfAttachment(id, RigProperties.Scale));
        var mirror = instance.Mirror;
        if (ImGui.Checkbox("Mirror across the socket", ref mirror))
        {
            session.Edit(document, () => { Live().Mirror = mirror; document.Asset.Validate(); });
        }

        var layer = instance.Layer;
        if (Ui.Drag("Layer offset from socket", ref layer, .005f, -16, 16))
        {
            session.Change(document, () => { Live().Layer = layer; document.Asset.Validate(); });
        }

        ParameterInspector.BoundNote(Resolved, PropertyRef.OfAttachment(id, RigProperties.Layer));
        var fit = new Vector2(instance.FitX, instance.FitY);
        if (Ui.Drag2("Fit X / Y (artwork only)", ref fit, .01f, -100, 100))
        {
            session.Change(document, () => { var live = Live(); live.FitX = fit.X; live.FitY = fit.Y; document.Asset.Validate(); });
        }

        ParameterInspector.BoundNote(Resolved, PropertyRef.OfAttachment(id, RigProperties.FitX));
        ParameterInspector.BoundNote(Resolved, PropertyRef.OfAttachment(id, RigProperties.FitY));
        var fill = instance.Material?.Fill;
        if (Ui.OverrideLabel("Fill (instance override)", fill is not null))
        {
            session.Edit(document, () => { var live = Live(); live.Material = live.Material is null ? null : live.Material with { Fill = null }; document.Asset.Validate(); });
        }
        else
        {
            var color = fill ?? "#b97549";
            if (Ui.ColorHex("##instance-fill", ref color))
            {
                session.Change(document, () => { var live = Live(); live.Material = (live.Material ?? new()) with { Fill = color }; document.Asset.Validate(); });
            }
        }

        var hidden = instance.Hidden;
        if (ImGui.Checkbox("Hidden", ref hidden))
        {
            session.Edit(document, () => { Live().Hidden = hidden; document.Asset.Validate(); });
        }

        if (session.Assets.PartOf(instance.Asset) is { IsAnimated: true } animated)
        {
            // An animated part (P7a): which bound expression this instance shows before any clip or gameplay request.
            var words = animated.Expressions.Keys.Append(Part.None).Prepend("").ToArray();
            if (Ui.Combo("Default expression", instance.Expression ?? "", words, w => w.Length == 0 ? $"asset default ({animated.Expression ?? "nothing"})" : w) is { } expression)
            {
                session.Edit(document, () => { Live().Expression = expression.Length == 0 ? null : expression; document.Asset.Validate(); });
            }

            Inputs(document, id, instance, animated);
            Animated(animated);
        }

        if (ImGui.Button("Remove instance"))
        {
            session.Edit(document, () =>
            {
                ModelAuthoring.RemoveAttachment(document.Asset, id);
                session.Selection.Clear();
            });
        }

        Ui.Help("Fit stretches this instance's artwork only; nothing inherits it. A variant overrides any of these per instance without editing this character or the shared part.");
    }

    /// <summary>
    /// The module's dimension inputs on this instance (P10b): each parameter the part declares, with this instance's value
    /// (bindable as the attachment property input.&lt;id&gt;, so a wearer parameter or a variant override may drive it) or the
    /// part's default when the instance sets none.
    /// </summary>
    private void Inputs(AssetDocument<CharacterModel> document, string id, PartInstance instance, Part asset)
    {
        if (asset.Parameters.Count == 0)
        {
            return;
        }

        ImGui.TextDisabled("Module inputs (this instance's values; the part declares each range)");
        PartInstance Live() => document.Asset.Attachments.First(a => a.Id == id);
        foreach (var parameter in asset.Parameters)
        {
            var set = instance.Inputs.TryGetValue(parameter.Id, out var current);
            var value = set ? current : parameter.Default;
            ImGui.SetNextItemWidth(160 * Ui.Scale);
            if (Ui.Drag($"{parameter.Label} ({parameter.Units})##input-{parameter.Id}", ref value, .005f, parameter.Min, parameter.Max))
            {
                session.Change(document, () => { Live().Inputs[parameter.Id] = value; document.Asset.Validate(); });
            }

            if (set)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"Part default##reset-input-{parameter.Id}"))
                {
                    session.Edit(document, () => { Live().Inputs.Remove(parameter.Id); document.Asset.Validate(); });
                }

                ParameterInspector.BoundNote(Resolved, PropertyRef.OfAttachment(id, RigProperties.Input + parameter.Id));
            }
            else if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Shows the part's default; dragging sets this instance's own value.");
            }
        }
    }

    /// <summary>What an animated asset brings to the instance (P7a): its layers with their clocks, the targets a clip may key and the words it binds.</summary>
    private static void Animated(Part asset)
    {
        foreach (var line in asset.Contract())
        {
            ImGui.TextDisabled("  " + line);
        }

        ImGui.TextDisabled($"Animated part: {asset.Controls.Count} control(s), {asset.Clips.Count} clip(s), fitting '{asset.Fitting}'.");
        foreach (var layer in asset.Layers)
        {
            ImGui.TextDisabled($"  layer '{layer.Id}': {(layer.Mask is null ? "everything" : "mask '" + layer.Mask + "'")}, clock {layer.Clock}, plays {layer.Clip ?? "nothing"} by default");
        }

        ImGui.TextDisabled($"  exposed to the wearer's clips: {(asset.Exposed.Count == 0 ? "nothing" : string.Join(", ", asset.Exposed))}");
        ImGui.TextDisabled($"  expressions: {(asset.Expressions.Count == 0 ? "none" : string.Join(", ", asset.Expressions.Keys))}");
    }

    /// <summary>A variant's override of one instance: every field labelled with its provenance, a marked label resets that property, and edits become typed overrides (P6c).</summary>
    public void DrawVariant(AssetDocument<ModelVariant> document, ResolvedModel resolved, string id)
    {
        var basis = resolved.Base;
        var instance = basis.Attachments.FirstOrDefault(a => a.Id == id);
        var shown = resolved.Attachment(id)?.Instance;
        if (instance is null || shown is null)
        {
            return;
        }

        Ui.Header($"Attachment {id}");
        ImGui.TextDisabled($"Override on this variant; the base '{basis.Id}' owns the instance (asset '{instance.Asset}' on '{instance.Socket}').");
        PropertyRef Target(string property) => PropertyRef.OfAttachment(id, property);
        bool Overridden(string property) => document.Asset.HasOverride(Target(property));
        void Reset(string property) => session.Edit(document, () => ModelAuthoring.ResetOverride(document.Asset, Target(property)));
        void Set(Action<PartInstance> set, bool discrete = false)
        {
            if (discrete)
            {
                session.Edit(document, () => ModelAuthoring.EditAttachmentOverride(document.Asset, resolved, id, set));
            }
            else
            {
                session.Change(document, () => ModelAuthoring.EditAttachmentOverride(document.Asset, resolved, id, set));
            }
        }
        var assets = session.Assets.Parts.Select(p => p.Id).Order(StringComparer.Ordinal).ToArray();
        if (Ui.OverrideLabel("Library part", Overridden(RigProperties.Asset)))
        {
            Reset(RigProperties.Asset);
        }
        else if (Ui.Combo("##attachment-asset", shown.Asset, assets, a => session.Assets.Part(a)?.Name is { } n ? $"{n} ({a})" : a) is { } asset)
        {
            Set(i => i.Asset = asset, true);
        }

        VariantModelInspector.SourceNote(resolved, Target(RigProperties.Asset));
        ImGui.SameLine();
        if (ImGui.SmallButton("Edit shared part"))
        {
            session.Open(shown.Asset);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Opens the library asset this variant shows. Edits there reach every wearer.");
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Edit base instance"))
        {
            session.Open(basis.Id);
            session.Selection.Select(attachment: id);
        }

        void Float(string label, string property, float value, Action<PartInstance, float> write, float speed, float min, float max)
        {
            if (Ui.OverrideLabel(label, Overridden(property)))
            {
                Reset(property);
                return;
            }

            var v = value;
            ImGui.SetNextItemWidth(-1);
            if (Ui.Drag("##" + label, ref v, speed, min, max))
            {
                Set(i => write(i, v));
            }

            VariantModelInspector.SourceNote(resolved, Target(property));
        }
        Float("Offset X", RigProperties.X, shown.X, (i, v) => i.X = v, .005f, -100, 100);
        Float("Offset Y", RigProperties.Y, shown.Y, (i, v) => i.Y = v, .005f, -100, 100);
        Float("Angle (radians)", RigProperties.Angle, shown.Angle, (i, v) => i.Angle = v, .01f, -1000, 1000);
        Float("Size", RigProperties.Scale, shown.Scale, (i, v) => i.Scale = v, .01f, .001f, 100);
        Float("Layer offset", RigProperties.Layer, shown.Layer, (i, v) => i.Layer = v, .005f, -16, 16);
        Float("Fit X", RigProperties.FitX, shown.FitX, (i, v) => i.FitX = v, .01f, -100, 100);
        Float("Fit Y", RigProperties.FitY, shown.FitY, (i, v) => i.FitY = v, .01f, -100, 100);
        if (Ui.OverrideLabel("Mirror", Overridden(RigProperties.Mirror)))
        {
            Reset(RigProperties.Mirror);
        }
        else
        {
            var mirror = shown.Mirror;
            if (ImGui.Checkbox("##attachment-mirror", ref mirror))
            {
                Set(i => i.Mirror = mirror, true);
            }
        }

        var materialOverride = document.Asset.Override(Target(RigProperties.Material));
        if (Ui.OverrideLabel("Fill", materialOverride is not null))
        {
            Reset(RigProperties.Material);
        }
        else
        {
            var color = shown.Material?.Fill ?? "#b97549";
            if (Ui.ColorHex("##attachment-fill", ref color))
            {
                Set(i => i.Material = (i.Material ?? new()) with { Fill = color });
            }

            if (shown.Material is not null && ImGui.SmallButton("Clear to the asset's materials"))
            {
                Set(i => i.Material = null, true);
            }
        }

        VariantModelInspector.SourceNote(resolved, Target(RigProperties.Material));
        if (Ui.OverrideLabel("Hidden", Overridden(RigProperties.Hidden)))
        {
            Reset(RigProperties.Hidden);
        }
        else
        {
            var hidden = shown.Hidden;
            if (ImGui.Checkbox("##attachment-hidden", ref hidden))
            {
                Set(i => i.Hidden = hidden, true);
            }
        }

        if (resolved.Attachment(id)?.Asset is { IsAnimated: true } animated)
        {
            if (Ui.OverrideLabel("Default expression", Overridden(RigProperties.Expression)))
            {
                Reset(RigProperties.Expression);
            }
            else
            {
                var words = animated.Expressions.Keys.Append(Part.None).Prepend("").ToArray();
                if (Ui.Combo("##attachment-expression", shown.Expression ?? "", words, w => w.Length == 0 ? $"asset default ({animated.Expression ?? "nothing"})" : w) is { } expression)
                {
                    Set(i => i.Expression = expression.Length == 0 ? null : expression, true);
                }
            }

            VariantModelInspector.SourceNote(resolved, Target(RigProperties.Expression));
            Animated(animated);
        }

        Ui.Help("Click a marked label to reset that override and follow the base again. Swapping the part here never edits the base or another variant.");
    }
}
