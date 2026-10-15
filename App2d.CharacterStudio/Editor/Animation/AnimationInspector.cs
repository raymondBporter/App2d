using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Animation settings, keying, selected control or slot, faces, and markers from top to bottom.</summary>
internal sealed class AnimationInspector(EditorSession session, AnimationViewport viewport, AnimationWeaponEditor weapons)
{
    private readonly AnimationControlInspector _controls = new(session);
    private readonly AnimationSlotInspector _slots = new(session);
    private string _marker = "strike";

    public void Draw()
    {
        if (session.ClipDocument is not { } document)
        {
            Ui.Help("No animation open.");
            return;
        }
        var primary = session.Scene().FirstOrDefault();
        if (session.EditWeapon && primary is not null)
        {
            weapons.DrawInspector(document, primary);
            return;
        }

        AnimationFields(document);
        SourceFields(document.Asset);
        KeyingFields();

        if (primary is not null)
        {
            if (session.Selection.Control is { } control && primary.Model.Controls.ContainsKey(control))
            {
                _controls.Draw(document, primary, control);
            }

            if (session.Selection.Slot is { } slot)
            {
                _slots.Draw(document, primary, slot);
            }
        }
        Faces(document, primary);
        Markers(document);

        foreach (var problem in session.Assets.Problems(document))
        {
            Ui.Problem(problem);
        }

        References.Draw(session, document.Id);
    }

    private void AnimationFields(AssetDocument<MotionClip> document)
    {
        var clip = document.Asset;
        Ui.Header("Animation");
        var name = clip.Name;
        if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0)
        {
            session.Change(document, () => document.Asset.Name = name);
        }

        var builds = 1 + session.Assets.Variants.Count(v => v.Asset.Base == clip.Model);
        var entities = session.Assets.EntitiesOn(clip.Model).Count(e => session.Assets.CompileEntity(e.Id) is { } compiled && (compiled.Roles.Values.Any(r => r.Clip == clip) || compiled.Actions.Values.Any(a => a.Clip == clip)));
        ImGui.TextDisabled($"Shared: plays on {builds} build(s) of '{clip.Model}' and in {entities} entit{(entities == 1 ? "y" : "ies")}. Edits reach all of them.");
        if (ImGui.SmallButton("Duplicate to specialize"))
        {
            var id = session.Assets.SuggestId(clip.Id + "-copy");
            session.DuplicateClip(clip.Id, id, clip.Name + " copy");
        }
        var duration = clip.Duration;
        if (Ui.Drag("Duration (seconds, retimes keys)", ref duration, .005f, .05f, 60))
        {
            session.Change(document, () => ClipAuthoring.Retime(document.Asset, Math.Clamp(duration, .05f, 60)));
        }

        var loop = clip.Loop;
        if (ImGui.Checkbox("Loop", ref loop))
        {
            session.Edit(document, () => document.Asset.Loop = loop);
        }

        TravelFields(document);
    }

    private void TravelFields(AssetDocument<MotionClip> document)
    {
        var travel = document.Asset.Travel;
        if (ClipAuthoring.TryGetSteadyTravel(document.Asset, out var distance))
        {
            if (Ui.Drag($"Travel per cycle ({travel.Scale} units)", ref distance, .005f, -20, 20))
            {
                session.Change(document, () => ClipAuthoring.SetSteadyTravel(document.Asset, distance));
            }
        }
        else
        {
            ImGui.TextDisabled($"Travel: {travel.Keys.Count} keys, scaled by '{travel.Scale}'.");
        }
    }

    private void SourceFields(MotionClip clip)
    {
        if (clip.Source is not { } source)
        {
            return;
        }

        Ui.Header("Source");
        Ui.Help(source.Kind == AssetSource.Library
            ? $"Converted from imported library '{source.File}', clip '{source.Motion}', with '{source.Rest}' as the rest reference. The library is unchanged."
            : $"Converted from {source.File}, motion '{source.Motion}'. The file is unchanged.");
        if (source.Kind == AssetSource.Library)
        {
            var show = viewport.ShowSource;
            if (ImGui.Checkbox("Show source points", ref show))
            {
                viewport.ShowSource = show;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Orange: the mapped source points at the same time, scaled to this build. Compare them with the converted pose.");
            }
        }
    }

    private void KeyingFields()
    {
        Ui.Header("Keying");
        var autokey = session.AutoKey;
        if (ImGui.Checkbox("Autokey", ref autokey))
        {
            session.AutoKey = autokey;
        }

        ImGui.SameLine();
        if (ImGui.Button("Key pose"))
        {
            session.KeyPose();
        }

        if (session.HasPendingPose)
        {
            Ui.Problem("Unkeyed pose: Key pose keeps it; changing time discards it.");
            if (ImGui.SmallButton("Discard"))
            {
                session.DiscardPendingPose();
            }
        }
        var onion = viewport.Onion;
        if (ImGui.Checkbox("Onion skin", ref onion))
        {
            viewport.Onion = onion;
        }
    }

    private void Faces(AssetDocument<MotionClip> document, Subject? primary)
    {
        if (primary is null)
        {
            return;
        }

        var parts = primary.Model.Parts.Where(p => p.Geometry is Core.Geometry.Builders.ShapeBuilder2D && p.Face != "none").ToArray();
        if (parts.Length == 0)
        {
            return;
        }

        Ui.Header("Face");
        var time = session.Transport.Time;
        foreach (var part in parts)
        {
            var track = document.Asset.Faces.FirstOrDefault(f => f.Part == part.Id);
            var here = track?.Keys.FirstOrDefault(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
            var shown = PoseEvaluator.FaceAt(document.Asset, part.Id, time);
            ImGui.PushID(part.Id);
            if (Ui.Combo($"{part.Id}: {(here is not null ? "keyed here" : shown is null ? "model default" : "held from earlier key")}", here?.Expression ?? shown ?? part.Face,
                FaceExpressions.Names) is { } expression)
            {
                session.Edit(document, () => ClipAuthoring.SetFace(document.Asset, part.Id, time, expression));
            }

            if (here is not null && ImGui.SmallButton("Remove face key"))
            {
                session.Edit(document, () => ClipAuthoring.SetFace(document.Asset, part.Id, time, null));
            }

            ImGui.PopID();
        }
        Ui.Help("A gameplay expression (viewport toolbar) overrides these; limb keys never carry face data.");
    }

    private void Markers(AssetDocument<MotionClip> document)
    {
        Ui.Header("Markers");
        foreach (var marker in document.Asset.Markers.ToArray())
        {
            ImGui.PushID(marker.Id);
            var at = marker.Time;
            ImGui.SetNextItemWidth(120 * Ui.Scale);
            if (ImGui.DragFloat(marker.Id, ref at, .002f, 0, document.Asset.Duration, "%.3f s"))
            {
                session.Change(document, () => ClipAuthoring.SetMarker(document.Asset, marker.Id, at));
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Go"))
            {
                session.Seek(marker.Time);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                session.Edit(document, () => document.Asset.Markers.RemoveAll(m => m.Id == marker.Id));
            }

            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(130 * Ui.Scale);
        ImGui.InputText("##marker", ref _marker, 64);
        ImGui.SameLine();
        if (ImGui.SmallButton("Add at playhead"))
        {
            session.Edit(document, () => ClipAuthoring.SetMarker(document.Asset, _marker, session.Transport.Time));
        }

        Ui.Help("Markers name visual moments such as footsteps or a strike. Gameplay binds events to them later.");
    }
}
