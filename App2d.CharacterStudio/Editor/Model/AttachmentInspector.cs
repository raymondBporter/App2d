using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
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

        Ui.Help("Click a marked label to reset that override and follow the base again. Swapping the part here never edits the base or another variant.");
    }
}
