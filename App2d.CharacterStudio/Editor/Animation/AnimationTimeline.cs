using App2d.Core.Mathematics.Progress;
using App2d.Core.Animation;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Transport, ruler, pose rows, contact intervals, appearance rows, and playhead in display order.</summary>
internal sealed class AnimationTimeline(EditorSession session)
{
    private const float RowHeight = 20, LabelWidth = 118;
    private AssetDocument<MotionClip>? _document;
    // Rows are rebuilt each frame, so pointer state names a row by label.
    private (string Row, float From, float To)? _keyDrag;
    private (string Row, IReadOnlyCollection<Channel>? Channels)? _keyRow;
    private (IReadOnlyCollection<Channel>? Channels, float Time)? _keyMenu;

    public void Draw()
    {
        TransportBar.Draw(session, preview: false);
        if (session.ClipDocument is not { } document || session.Scene().FirstOrDefault() is not { } primary)
        {
            return;
        }

        if (_document != document)
        {
            _document = document;
            _keyDrag = null;
            _keyRow = null;
            _keyMenu = null;
            session.Selection.Key = null;
        }
        var clip = document.Asset;
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var scale = Ui.Scale;
        var left = origin.X + LabelWidth * scale;
        var right = origin.X + width - 8 * scale;
        float X(float time) => left + (right - left) * time / clip.Duration;
        float Time(float x) => Math.Clamp((x - left) / (right - left) * clip.Duration, 0, clip.Duration);

        var rows = new List<(string Label, IReadOnlyCollection<Channel>? Channels)> { ("Pose (all)", null) };
        if (session.EditWeapon)
        {
            rows.Add(("Weapon rotation", new[] { new Channel(MotionClip.OrientKind, session.PreviewSocket) }));
        }

        if (session.Selection.Part is { } piece && AnimationInspector.Keyable(primary.Model, piece))
        {
            rows.Add((piece + " geometry", MotionClip.PartKinds.Select(k => new Channel(k, piece)).ToArray()));
        }
        else if (session.Selection.Control is { } control && AnimationInspector.Keyable(primary.Model, control) && ClipAuthoring.ChannelsForControl(primary.Model, control) is { Count: > 0 } selected)
        {
            rows.Add((control, selected));
        }

        var y = origin.Y;
        Ruler(draw, clip, left, right, y, X, Time);
        y += RowHeight * scale + 2 * scale;

        foreach (var (label, channels) in rows)
        {
            KeyRow(draw, document, label, channels, origin.X, y, X, Time);
            y += RowHeight * scale;
        }
        ContactRows(draw, document, primary.Model, origin.X, ref y, X);
        PointRow(draw, "Markers", clip.Markers.Select(m => (m.Time, m.Id)), origin.X, y, X, StudioTheme.TimelineMarker.PackedValue);
        y += RowHeight * scale;
        foreach (var play in clip.Plays)
        {
            PointRow(draw, "Part " + play.Instance + (primary.Model.MotionOwner?.Id == play.Instance ? " (root motion)" : ""), play.Keys.Select(k => (k.Time, k.Expression)), origin.X, y, X, StudioTheme.TimelineFaceKey.PackedValue);
            y += RowHeight * scale;
        }
        foreach (var slot in clip.Attachments)
        {
            PointRow(draw, "Slot " + slot.Slot, slot.Keys.Select(k => (k.Time, k.Attachment ?? "hidden")), origin.X, y, X, StudioTheme.TimelineDiscreteKey.PackedValue);
            y += RowHeight * scale;
        }
        foreach (var colors in clip.Colors)
        {
            PointRow(draw, colors.Slot + " " + colors.Kind, colors.Keys.Select(k => (k.Time, "tint")), origin.X, y, X, StudioTheme.TimelineTintKey.PackedValue);
            y += RowHeight * scale;
        }
        if (clip.DrawOrder.Count > 0)
        {
            PointRow(draw, "Draw order", clip.DrawOrder.Select(k => (k.Time, "order")), origin.X, y, X, StudioTheme.TimelineDiscreteKey.PackedValue);
            y += RowHeight * scale;
        }

        var head = X(session.Transport.Time);
        draw.AddLine(new(head, origin.Y), new(head, y), StudioTheme.Selection.PackedValue, 2);
        KeyMenu(document);
        if (!ImGui.GetIO().WantTextInput && session.Selection.Key is { } selectedKey && ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, selectedKey, _keyRow?.Channels));
        }

        ImGui.SetCursorScreenPos(new(origin.X, y + 4 * scale));
        ImGui.TextDisabled("Click a key to jump  |  drag to move, Ctrl+drag to copy  |  right-click for easing  |  Delete removes the selected key");
    }

    private void Ruler(ImDrawListPtr draw, MotionClip clip, float left, float right, float y, Func<float, float> X, Func<float, float> Time)
    {
        var height = RowHeight * Ui.Scale;
        draw.AddRectFilled(new(left, y), new(right, y + height), StudioTheme.TimelineRulerBackground.PackedValue);
        for (var i = 0; i <= 10; i++)
        {
            var time = clip.Duration * i / 10;
            var x = X(time);
            draw.AddLine(new(x, y + height * .55f), new(x, y + height), StudioTheme.TimelineRulerText.PackedValue);
            draw.AddText(new(x + 2, y), StudioTheme.TimelineRulerText.PackedValue, $"{time:0.##}");
        }
        ImGui.SetCursorScreenPos(new(left, y));
        ImGui.InvisibleButton("ruler", new(Math.Max(1, right - left), height));
        if (ImGui.IsItemActive())
        {
            session.Transport.Pause();
            session.Seek(Time(ImGui.GetMousePos().X));
        }
    }

    private void KeyRow(ImDrawListPtr draw, AssetDocument<MotionClip> document, string label, IReadOnlyCollection<Channel>? channels, float x0, float y, Func<float, float> X, Func<float, float> Time)
    {
        var scale = Ui.Scale;
        var height = RowHeight * scale;
        var center = y + height / 2;
        var clip = document.Asset;
        draw.AddText(new(x0 + 4, y + 2), StudioTheme.TimelineText.PackedValue, label);
        draw.AddLine(new(X(0), center), new(X(clip.Duration), center), StudioTheme.TimelineGuide.PackedValue);
        var times = ClipAuthoring.KeyTimes(clip, channels);
        var row = (label, channels);
        foreach (var time in times)
        {
            var shown = _keyDrag is { } d && d.Row == label && MathF.Abs(d.From - time) < ClipAuthoring.SameTime ? d.To : time;
            var x = X(shown);
            var r = 5 * scale;
            var selected = session.Selection.Key is { } k && MathF.Abs(k - time) < ClipAuthoring.SameTime && _keyRow?.Row == label;
            draw.AddQuadFilled(new(x, center - r), new(x + r, center), new(x, center + r), new(x - r, center), selected ? StudioTheme.Selection.PackedValue : StudioTheme.TimelineKey.PackedValue);
            ImGui.SetCursorScreenPos(new(x - r, center - r));
            ImGui.InvisibleButton($"key-{label}-{time:R}", new(2 * r, 2 * r));
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                session.Transport.Pause();
                session.Seek(time);
                session.Selection.Key = time;
                _keyRow = row;
                _keyDrag = (label, time, time);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                _keyMenu = (channels, time);
                session.Selection.Key = time;
                _keyRow = row;
                ImGui.OpenPopup("key-menu");
            }
        }
        if (_keyDrag is { } active && active.Row == label)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _keyDrag = active with
                {
                    To = Time(ImGui.GetMousePos().X)
                };
            }
            else
            {
                _keyDrag = null;
                if (MathF.Abs(active.To - active.From) > ClipAuthoring.SameTime)
                {
                    var copy = ImGui.GetIO().KeyCtrl;
                    session.Edit(document, () => ClipAuthoring.MoveKeys(document.Asset, active.From, active.To, channels, copy));
                    session.Selection.Key = active.To;
                    session.Seek(active.To);
                }
            }
        }
    }

    private void KeyMenu(AssetDocument<MotionClip> document)
    {
        if (!ImGui.BeginPopup("key-menu"))
        {
            return;
        }

        if (_keyMenu is { } menu)
        {
            ImGui.TextDisabled($"Key at {menu.Time:F3}s");
            foreach (var ease in AnimationCurveEditor.EasingKinds)
            {
                if (ImGui.MenuItem("Ease: " + ease))
                {
                    session.Edit(document, () => ClipAuthoring.SetEase(document.Asset, menu.Time, ease, menu.Channels));
                }
            }

            if (ImGui.MenuItem("Copy to playhead"))
            {
                session.Edit(document, () => ClipAuthoring.MoveKeys(document.Asset, menu.Time, session.Transport.Time, menu.Channels, copy: true));
            }

            if (ImGui.MenuItem("Delete"))
            {
                session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, menu.Time, menu.Channels));
            }
        }
        ImGui.EndPopup();
    }

    private void ContactRows(ImDrawListPtr draw, AssetDocument<MotionClip> document, ResolvedModel model, float x0, ref float y, Func<float, float> X)
    {
        var scale = Ui.Scale;
        var height = RowHeight * scale;
        foreach (var chain in model.Chains.Values.Where(c => c.Frame == CharacterModel.Locomotion))
        {
            draw.AddText(new(x0 + 4, y + 2), StudioTheme.TimelinePlantText.PackedValue, "Plant " + chain.Id);
            draw.AddLine(new(X(0), y + height / 2), new(X(document.Asset.Duration), y + height / 2), StudioTheme.TimelinePlantGuide.PackedValue);
            foreach (var contact in document.Asset.Contacts.Where(c => c.Chain == chain.Id))
            {
                var min = new Vector2(X(contact.Start), y + 4 * scale);
                var max = new Vector2(X(contact.Finish), y + height - 4 * scale);
                draw.AddRectFilled(min, max, StudioTheme.TimelinePlantFill.PackedValue, 3);
                ImGui.SetCursorScreenPos(min);
                ImGui.InvisibleButton($"contact-{chain.Id}-{contact.Start:R}", Vector2.Max(max - min, Vector2.One));
                if (ImGui.IsItemClicked())
                {
                    session.Transport.Pause();
                    session.Seek(contact.Start);
                    session.Selection.Clear();
                    session.Selection.Control = chain.End;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"{chain.Id} planted {contact.Start:F3}s to {contact.Finish:F3}s");
                }
            }
            y += height;
        }
    }

    private static void PointRow(ImDrawListPtr draw, string label, IEnumerable<(float Time, string Name)> points, float x0, float y, Func<float, float> X, uint color)
    {
        var scale = Ui.Scale;
        var height = RowHeight * scale;
        draw.AddText(new(x0 + 4, y + 2), StudioTheme.TimelineText.PackedValue, label);
        foreach (var (time, name) in points)
        {
            var x = X(time);
            draw.AddTriangleFilled(new(x - 5 * scale, y + 3 * scale), new(x + 5 * scale, y + 3 * scale), new(x, y + height - 4 * scale), color);
            draw.AddText(new(x + 6 * scale, y + 2), color, name);
        }
    }
}
