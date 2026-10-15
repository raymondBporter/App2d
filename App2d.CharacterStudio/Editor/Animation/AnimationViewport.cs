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
                    draw.AddLine(frame.Screen(ghost.World(control.Parent!)), frame.Screen(ghost.World(control.Id)), Ui.Color(135, 128, 83, 110), 2);
                }
            }
        }

        foreach (var control in model.Order.Where(c => c.Parent is not null))
        {
            draw.AddLine(frame.Screen(pose.World(control.Parent!)), frame.Screen(pose.World(control.Id)), Ui.Color(104, 112, 122, 130), 1);
        }

        if (ShowSource)
        {
            var points = SourcePoints(subject, document.Asset).ToDictionary(p => p.Control, p => frame.Screen(p.Point), StringComparer.Ordinal);
            foreach (var control in model.Order.Where(c => c.Parent is not null && points.ContainsKey(c.Id) && points.ContainsKey(c.Parent)))
            {
                draw.AddLine(points[control.Parent!], points[control.Id], Ui.Color(240, 106, 50, 150), 1.5f);
            }

            foreach (var p in points.Values)
            {
                draw.AddCircleFilled(p, 3 * Ui.Scale, Ui.Color(240, 106, 50, 200));
            }
        }
        foreach (var control in model.Order)
        {
            var channel = ClipAuthoring.ChannelFor(model, control.Id);
            var p = frame.Screen(pose.World(control.Id));
            var selected = session.Selection.Control == control.Id;

            if (channel is null)
            {
                draw.AddCircle(p, 3 * Ui.Scale, Ui.Color(104, 112, 122), 12, 1);
                continue;
            }
            var planted = channel.Value.Kind == MotionClip.TargetKind && ClipAuthoring.ActiveContact(document.Asset, channel.Value.Target, session.Transport.Time) is not null;
            var color = selected ? Ui.Color(232, 169, 55) : planted ? Ui.Color(36, 140, 104) : channel.Value.Kind == MotionClip.TargetKind ? Ui.Color(50, 145, 160) : Ui.Color(230, 233, 229);
            var radius = (selected ? 6 : 4.5f) * Ui.Scale;
            if (planted)
            {
                draw.AddRectFilled(p - new Vector2(radius), p + new Vector2(radius), color);
            }
            else
            {
                draw.AddCircleFilled(p, radius, color);
            }

            draw.AddCircle(p, radius, Ui.Color(64, 78, 89), 16, 1);
            if (selected)
            {
                draw.AddText(p + new Vector2(9, -18) * Ui.Scale, Ui.Color(44, 53, 68), control.Id);
            }
        }
        draw.AddText(frame.Origin + new Vector2(10, 8) * Ui.Scale, session.AutoKey ? Ui.Color(200, 60, 50) : Ui.Color(90, 100, 96), session.AutoKey ? "AUTOKEY" : "autokey off");

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
