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

    public void DrawVariant(AssetDocument<ModelVariant> document, CharacterModel basis, string id)
    {
        var instance = basis.Attachments.FirstOrDefault(a => a.Id == id);
        if (instance is null)
        {
            return;
        }

        var change = document.Asset.Attachments.GetValueOrDefault(id);
        var shown = instance.With(change);
        Ui.Header($"Attachment {id}");
        ImGui.TextDisabled($"Override on this variant; the base '{basis.Id}' owns the instance (asset '{instance.Asset}' on '{instance.Socket}').");
        void Set(Action<AttachmentOverride> set, bool discrete = false)
        {
            if (discrete)
            {
                session.Edit(document, () => ModelAuthoring.EditAttachmentOverride(document.Asset, basis, id, set));
            }
            else
            {
                session.Change(document, () => ModelAuthoring.EditAttachmentOverride(document.Asset, basis, id, set));
            }
        }
        var assets = session.Assets.Parts.Select(p => p.Id).Order(StringComparer.Ordinal).ToArray();
        if (Ui.OverrideLabel("Library part", change?.Asset is not null))
        {
            Set(o => o.Asset = null, true);
        }
        else if (Ui.Combo("##attachment-asset", shown.Asset, assets, a => session.Assets.Part(a)?.Name is { } n ? $"{n} ({a})" : a) is { } asset)
        {
            Set(o => o.Asset = asset == instance.Asset ? null : asset, true);
        }

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

        void Float(string label, float value, float? overridden, Action<AttachmentOverride, float?> write, float speed, float min, float max)
        {
            if (Ui.OverrideLabel(label, overridden is not null))
            {
                Set(o => write(o, null), true);
                return;
            }

            var v = value;
            if (Ui.Drag("##" + label, ref v, speed, min, max))
            {
                Set(o => write(o, v));
            }
        }
        Float("Offset X", shown.X, change?.X, (o, v) => o.X = v, .005f, -100, 100);
        Float("Offset Y", shown.Y, change?.Y, (o, v) => o.Y = v, .005f, -100, 100);
        Float("Angle (radians)", shown.Angle, change?.Angle, (o, v) => o.Angle = v, .01f, -1000, 1000);
        Float("Size", shown.Scale, change?.Scale, (o, v) => o.Scale = v, .01f, .001f, 100);
        Float("Layer offset", shown.Layer, change?.Layer, (o, v) => o.Layer = v, .005f, -16, 16);
        Float("Fit X", shown.FitX, change?.FitX, (o, v) => o.FitX = v, .01f, -100, 100);
        Float("Fit Y", shown.FitY, change?.FitY, (o, v) => o.FitY = v, .01f, -100, 100);
        if (Ui.OverrideLabel("Mirror", change?.Mirror is not null))
        {
            Set(o => o.Mirror = null, true);
        }
        else
        {
            var mirror = shown.Mirror;
            if (ImGui.Checkbox("##attachment-mirror", ref mirror))
            {
                Set(o => o.Mirror = mirror, true);
            }
        }

        if (Ui.OverrideLabel("Fill", change?.Material?.Fill is not null))
        {
            Set(o => o.Material = o.Material is null ? null : o.Material with { Fill = null }, true);
        }
        else
        {
            var color = shown.Material?.Fill ?? "#b97549";
            if (Ui.ColorHex("##attachment-fill", ref color))
            {
                Set(o => o.Material = (o.Material ?? new()) with { Fill = color });
            }
        }

        if (Ui.OverrideLabel("Hidden", change?.Hidden is not null))
        {
            Set(o => o.Hidden = null, true);
        }
        else
        {
            var hidden = shown.Hidden;
            if (ImGui.Checkbox("##attachment-hidden", ref hidden))
            {
                Set(o => o.Hidden = hidden, true);
            }
        }

        Ui.Help("Click a marked label to clear that override and follow the base again. Swapping the part here never edits the base or another variant.");
    }
}
