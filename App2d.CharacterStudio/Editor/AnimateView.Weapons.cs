using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

internal sealed partial class AnimateView
{
    private string? _weaponDrag;
    private float _weaponPointerAngle;
    private Vector3 _weaponStart;
    private float _objScale = 1;

    private void WeaponOutline(Subject primary)
    {
        if (primary.Model.Base.Sockets.Count == 0) return;
        Ui.Header("Attachment preview");
        var visible = session.PreviewWeapon;
        if (ImGui.Checkbox("Show attachment", ref visible)) session.PreviewWeapon = visible;
        if (Ui.Combo("Prop", session.PreviewProp, session.Assets.Props.Select(p => p.Id), id => session.Assets.Prop(id)?.Name ?? id) is { } prop)
        { session.PreviewProp = prop; session.EditWeapon = true; }
        var sockets = primary.Model.Base.Sockets.Select(s => s.Id).ToArray();
        if (!sockets.Contains(session.PreviewSocket)) session.PreviewSocket = sockets[0];
        if (Ui.Combo("Attachment socket", session.PreviewSocket, sockets) is { } socket) { session.PreviewSocket = socket; session.EditWeapon = true; }
        if (ImGui.Selectable("Edit attachment rotation", session.EditWeapon)) { session.EditWeapon = true; session.Selection.Clear(); }
        Ui.Help("Preview only. Equipment in Entity determines what is carried in game.");
    }

    private void WeaponInspector(AssetDocument<MotionClip> document, Subject subject)
    {
        if (session.WeaponFor(subject.Model) is not { } weapon) { Ui.Help("Enable a weapon preview and select its socket."); return; }
        var time = session.Transport.Time; var channel = new Channel(MotionClip.OrientKind, weapon.Socket.Id);
        var angles = subject.Pose.SocketAngles.GetValueOrDefault(weapon.Socket.Id);
        Ui.Header(weapon.Prop.Name + " rotation");
        Ui.Help("Rotation follows the attachment frame. Turn aims in the screen; tilt points toward/away; twist rolls the blade onto its edge.");
        var autokey = session.AutoKey; if (ImGui.Checkbox("Autokey", ref autokey)) session.AutoKey = autokey;
        var degrees = angles * (180 / MathF.PI);
        var changed = Ui.Drag("Turn (degrees)", ref degrees.Z, .5f, -2880, 2880, "%.1f");
        changed |= Ui.Drag("Tilt (degrees)", ref degrees.Y, .5f, -180, 180, "%.1f");
        changed |= Ui.Drag("Twist (degrees)", ref degrees.X, .5f, -2880, 2880, "%.1f");
        if (changed) session.PoseWeapon(degrees * (MathF.PI / 180));
        if (ImGui.Button("Broadside")) { session.PoseWeapon(angles with { X = 0, Y = 0 }); session.CommitAll(); }
        ImGui.SameLine(); if (ImGui.Button("Edge-on")) { session.PoseWeapon(angles with { X = MathF.PI / 2, Y = 0 }); session.CommitAll(); }
        if (ImGui.Button("Reverse face")) { session.PoseWeapon(angles with { X = MathF.PI }); session.CommitAll(); }
        ImGui.SameLine(); if (ImGui.Button("Reset rotation")) { session.PoseWeapon(Vector3.Zero); session.CommitAll(); }
        if (session.HasPendingPose) Ui.Problem("Unkeyed rotation. Key pose to keep it; seeking discards it.");
        if (ImGui.Button("Key pose")) { session.PoseWeapon(angles); session.KeyPose(); }
        var key = ClipAuthoring.Track(document.Asset, channel)?.Keys.FirstOrDefault(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
        if (key is not null)
        {
            ImGui.SameLine(); if (ImGui.Button("Delete rotation key")) session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, time, [channel]));
            if (Ui.Combo("Ease to next key", key.Ease, ClipEase.All) is { } ease) session.Edit(document, () => ClipAuthoring.SetEase(document.Asset, time, ease, [channel]));
        }
        Ui.Help("Gold: drag around the grip to turn. Blue: drag up/down to tilt. Green: drag left/right to twist. Shift slows adjustment. Use 0 to 360 for a full turn.");
        if (ImGui.Button("Save animation")) session.Save(document);

        if (ImGui.CollapsingHeader("Weapon asset / import OBJ")) PropFields(session.Assets.Prop(weapon.Prop.Id)!);
        foreach (var problem in session.Assets.Problems(document)) Ui.Problem(problem);
    }

    private void PropFields(AssetDocument<PropAsset> document)
    {
        Ui.Help("Shared asset: these changes affect every entity using this weapon. Save weapon separately, or use Save all.");
        var prop = document.Asset;
        var scale = prop.Scale; if (Ui.Drag("Size", ref scale, .005f, .001f, 100)) session.Change(document, () => document.Asset.Scale = Math.Clamp(scale, .001f, 100));
        var grip = prop.Grip.XYZ; if (Ui.Drag3("Grip (local XYZ)", ref grip)) session.Change(document, () => document.Asset.Grip = PuppetPoint.From(grip));
        var tip = prop.Tip.XYZ; if (Ui.Drag3("Tip (local XYZ)", ref tip)) session.Change(document, () => document.Asset.Tip = PuppetPoint.From(tip));
        if (prop.Muzzle is { } muzzle)
        {
            var point = muzzle.XYZ; if (Ui.Drag3("Muzzle (local XYZ)", ref point)) session.Change(document, () => document.Asset.Muzzle = PuppetPoint.From(point));
        }
        var ink = prop.LineWidth; if (Ui.Drag("Ink width", ref ink, .001f, .001f, .1f)) session.Change(document, () => document.Asset.LineWidth = Math.Clamp(ink, .001f, .1f));
        for (var i = 0; i < prop.Solids.Count; i++)
        {
            var index = i; var color = prop.Solids[i].RenderMaterial.Fill!;
            if (Ui.ColorHex("Color " + (i + 1), ref color)) session.Change(document, () =>
            { var solid = document.Asset.Solids[index]; solid.Material = solid.RenderMaterial with { Fill = color }; });
        }
        Ui.Drag("OBJ import scale", ref _objScale, .001f, .001f, 100);
        Ui.Help("Triangulated OBJ, outward face winding. +X along blade/barrel, +Y across broad face, Z thickness. Export applied transforms. Materials/textures are not imported.");
        if (ImGui.Button("Replace geometry from OBJ..."))
        {
            using var dialog = new OpenFileDialog { Filter = "Triangulated OBJ|*.obj", Title = "Import weapon geometry" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (new FileInfo(dialog.FileName).Length > 16 * 1024 * 1024) throw new InvalidDataException("OBJ must be under 16 MB.");
                    var mesh = PropGeometry.ImportObj(File.ReadAllText(dialog.FileName), _objScale, prop.Solids.FirstOrDefault()?.RenderMaterial.Fill ?? "#c8b18a");
                    session.Edit(document, () => { document.Asset.Shapes.Clear(); document.Asset.Solids = [mesh]; }, "Imported geometry. Check size, grip, tip and muzzle, then save weapon.");
                }
                catch (Exception ex) when (ex is IOException or ArgumentException) { session.Report(ex.Message, true); }
            }
        }
        if (Ui.Button("Undo weapon edit", document.CanUndo)) document.Undo();
        ImGui.SameLine(); if (Ui.Button("Redo", document.CanRedo)) document.Redo();
        if (ImGui.Button("Save weapon")) session.Save(document);
    }

    private bool WeaponOverlay(ViewportFrame frame, Subject subject)
    {
        if (session.WeaponFor(subject.Model) is not { } weapon) return false;
        var socket = new ActorPose(subject.Pose, Vector2.Zero, 1).Socket(weapon.Socket);
        var center = frame.Screen(socket.Origin); var draw = frame.Draw; var scale = Ui.Scale;
        var angles = subject.Pose.SocketAngles.GetValueOrDefault(weapon.Socket.Id);
        var tip = frame.Screen(ActorPose.PropPoint(socket, weapon.Prop, weapon.Prop.Tip));
        draw.AddLine(center, tip, Ui.Color(232, 169, 55), 1);
        draw.AddCircle(tip, 6 * scale, Ui.Color(232, 169, 55), 20, 2);
        draw.AddLine(center, frame.Screen(socket.At(0, .24f)), Ui.Color(55, 155, 100), 2);
        draw.AddLine(center, frame.Screen(socket.At(0, 0, .24f)), Ui.Color(70, 145, 225), 2);
        draw.AddCircle(center, 38 * scale, Ui.Color(232, 169, 55, 130), 48, 1);
        var screenAngle = new ActorPose(subject.Pose, Vector2.Zero, 1).SocketBaseAngle(weapon.Socket) + weapon.Socket.Angle + angles.Z;
        var turn = center + new Vector2(MathF.Cos(screenAngle), -MathF.Sin(screenAngle)) * 38 * scale;
        var tilt = center + new Vector2(-65, -30) * scale;
        var twist = center + new Vector2(-65, 25) * scale;
        foreach (var (name, point, color) in new[] { ("Turn", turn, Ui.Color(232, 169, 55)), ("Tilt", tilt, Ui.Color(70, 145, 225)), ("Twist", twist, Ui.Color(55, 155, 100)) })
        {
            draw.AddCircleFilled(point, 7 * scale, color);
            draw.AddText(point + new Vector2(10, -7) * scale, color, name);
            if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Vector2.Distance(ViewportFrame.Mouse, point) < 12 * scale)
            { _weaponDrag = name; _weaponPointerAngle = MathF.Atan2(center.Y - ViewportFrame.Mouse.Y, ViewportFrame.Mouse.X - center.X); _weaponStart = angles; session.BeginDrag(); }
        }
        if (_weaponDrag is not null)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { _weaponDrag = null; session.EndDrag(); }
            else if (ImGui.GetIO().MouseDelta != Vector2.Zero)
            {
                var speed = ImGui.GetIO().KeyShift ? .2f : 1;
                if (_weaponDrag == "Turn")
                {
                    var pointer = MathF.Atan2(center.Y - ViewportFrame.Mouse.Y, ViewportFrame.Mouse.X - center.X);
                    _weaponStart.Z += MathF.IEEERemainder(pointer - _weaponPointerAngle, MathF.Tau) * speed;
                    _weaponPointerAngle = pointer;
                }
                else if (_weaponDrag == "Tilt")
                {
                    _weaponStart.Y -= ImGui.GetIO().MouseDelta.Y * .01f / scale * speed;
                }
                else
                {
                    _weaponStart.X += ImGui.GetIO().MouseDelta.X * .01f / scale * speed;
                }

                session.PoseWeapon(_weaponStart);
            }
        }
        draw.AddText(frame.Origin + new Vector2(10, 8) * scale, Ui.Color(55, 105, 120), "WEAPON: " + weapon.Socket.Id + (session.AutoKey ? " | AUTOKEY" : " | key pose to keep changes"));
        return true;
    }
}
