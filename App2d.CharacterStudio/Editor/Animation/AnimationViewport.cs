using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Onion skin, source comparison, control handles, and animation viewport input.</summary>
internal sealed class AnimationViewport(EditorSession session, Viewport viewport, AnimationWeaponEditor weapons)
{
    private readonly AnimationControlDrag _controls = new(session, viewport);
    private readonly SourceMotionPreview _source = new(session.Sources);
    public bool Onion { get; set; } = true;
    public bool ShowSource { get; set; } = true;

    public void Draw(ViewportFrame frame)
    {
        if (frame.Primary is not { } view || session.ClipDocument is not { } document)
        {
            return;
        }

        var subject = view.Subject;
        var model = subject.Model;
        var pose = subject.Pose;
        var draw = frame.Draw;

        if (session.EditWeapon && weapons.DrawOverlay(frame, subject))
        {
            return;
        }
        if (Onion && !session.Transport.Playing && subject.Clip is { } shown)
        {
            var times = ClipAuthoring.KeyTimes(shown);
            var now = session.Transport.Time;
            foreach (var time in new[] { times.LastOrDefault(t => t < now - .001f, -1), times.FirstOrDefault(t => t > now + .001f, -1) }.Where(t => t >= 0))
            {
                var ghost = PoseEvaluator.Sample(model, shown, time);
                foreach (var control in model.Order.Where(c => c.Parent is not null))
                {
                    draw.AddLine(frame.Screen(ghost.World(control.Parent!)), frame.Screen(ghost.World(control.Id)), StudioTheme.OnionSkin.PackedValue, 2);
                }
            }
        }

        foreach (var control in model.Order.Where(c => c.Parent is not null))
        {
            draw.AddLine(frame.Screen(pose.World(control.Parent!)), frame.Screen(pose.World(control.Id)), StudioTheme.AnimationBoneLink.PackedValue, 1);
        }

        if (ShowSource)
        {
            var points = SourcePoints(subject, document.Asset).ToDictionary(p => p.Control, p => frame.Screen(p.Point), StringComparer.Ordinal);
            foreach (var control in model.Order.Where(c => c.Parent is not null && points.ContainsKey(c.Id) && points.ContainsKey(c.Parent)))
            {
                draw.AddLine(points[control.Parent!], points[control.Id], StudioTheme.SourceMotionLink.PackedValue, 1.5f);
            }

            foreach (var p in points.Values)
            {
                draw.AddCircleFilled(p, 3 * Ui.Scale, StudioTheme.SourceMotionPoint.PackedValue);
            }
        }
        foreach (var control in model.Order)
        {
            var channel = ClipAuthoring.ChannelFor(model, control.Id);
            var p = frame.Screen(pose.World(control.Id));
            var selected = session.Selection.Control == control.Id;

            if (channel is null)
            {
                draw.AddCircle(p, 3 * Ui.Scale, StudioTheme.InactiveControl.PackedValue, 12, 1);
                continue;
            }
            var planted = channel.Value.Kind == MotionClip.TargetKind && ClipAuthoring.ActiveContact(document.Asset, channel.Value.Target, session.Transport.Time) is not null;
            var color = selected ? StudioTheme.Selection.PackedValue : planted ? StudioTheme.ConstraintSatisfied.PackedValue : channel.Value.Kind == MotionClip.TargetKind ? StudioTheme.TargetControl.PackedValue : StudioTheme.ControlFill.PackedValue;
            var radius = (selected ? 6 : 4.5f) * Ui.Scale;
            if (planted)
            {
                draw.AddRectFilled(p - new Vector2(radius), p + new Vector2(radius), color);
            }
            else
            {
                draw.AddCircleFilled(p, radius, color);
            }

            draw.AddCircle(p, radius, StudioTheme.ControlOutline.PackedValue, 16, 1);
            if (selected)
            {
                draw.AddText(p + new Vector2(9, -18) * Ui.Scale, StudioTheme.ControlLabel.PackedValue, control.Id);
            }
        }
        // Animated part instances (P7a): their rig links and exposed controls, keyed through namespaced channels.
        foreach (var evaluated in pose.Attachments.Values.Where(e => !e.Hidden))
        {
            foreach (var control in evaluated.Rig.Order.Where(c => c.Parent is not null))
            {
                draw.AddLine(frame.Screen(evaluated.World(control.Parent!)), frame.Screen(evaluated.World(control.Id)), StudioTheme.AnimationBoneLink.PackedValue, 1);
            }

            foreach (var control in evaluated.Rig.Order)
            {
                var id = evaluated.Qualify(control.Id);
                var p = frame.Screen(evaluated.World(control.Id));
                var selected = session.Selection.Control == id;
                if (!evaluated.Attachment.Asset.Exposes(control.Id))
                {
                    draw.AddCircle(p, 2.5f * Ui.Scale, StudioTheme.InactiveControl.PackedValue, 10, 1);
                    continue;
                }

                var radius = (selected ? 5 : 3.5f) * Ui.Scale;
                draw.AddCircleFilled(p, radius, selected ? StudioTheme.Selection.PackedValue : StudioTheme.ControlFill.PackedValue);
                draw.AddCircle(p, radius, StudioTheme.ControlOutline.PackedValue, 12, 1);
                if (selected)
                {
                    draw.AddText(p + new Vector2(9, -18) * Ui.Scale, StudioTheme.ControlLabel.PackedValue, id);
                }
            }
        }
        draw.AddText(frame.Origin + new Vector2(10, 8) * Ui.Scale, session.AutoKey ? StudioTheme.AutoKey.PackedValue : StudioTheme.MutedText.PackedValue, session.AutoKey ? "AUTOKEY" : "autokey off");

        _controls.Draw(frame, subject);
    }

    private IEnumerable<(string Control, Vector3 Point)> SourcePoints(Subject subject, MotionClip clip)
    {
        try
        {
            return _source.Sample(subject, clip, session.Transport.Time);
        }
        catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex) || ex is IOException)
        {
            session.Report("Source unavailable: " + ex.Message, true);
            ShowSource = false;
            return [];
        }
    }
}
