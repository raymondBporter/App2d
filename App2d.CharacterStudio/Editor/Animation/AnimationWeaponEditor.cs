using App2d.Core.Mathematics.Progress;
using App2d.Core.Animation;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Attachment preview controls, weapon asset inspector, and rotation handles share one drag state.</summary>
internal sealed class AnimationWeaponEditor(EditorSession session)
{
    private string? _weaponDrag;
    private float _weaponPointerAngle;
    private Vector3 _weaponStart;
    private readonly LibraryPartFields _partFields = new(session);
    private readonly Model.ModelPartInspector _geometryInspector = new(session, new(session));

    public void DrawOutline(Subject primary)
    {
        if (primary.Model.Base.Sockets.Count == 0)
        {
            return;
        }

        Ui.Header("Attachment preview");
        var visible = session.PreviewWeapon;
        if (ImGui.Checkbox("Show attachment", ref visible))
        {
            session.PreviewWeapon = visible;
        }

        if (Ui.Combo("Prop", session.PreviewProp, session.Assets.Parts.Select(p => p.Id), id => session.Assets.Part(id)?.Name ?? id) is { } prop)
        {
            session.PreviewProp = prop;
            session.EditWeapon = true;
        }
        var sockets = primary.Model.Base.Sockets.Select(s => s.Id).ToArray();
        if (!sockets.Contains(session.PreviewSocket))
        {
            session.PreviewSocket = sockets[0];
        }

        if (Ui.Combo("Attachment socket", session.PreviewSocket, sockets) is { } socket)
        {
            session.PreviewSocket = socket;
            session.EditWeapon = true;
        }
        if (ImGui.Selectable("Edit attachment rotation", session.EditWeapon))
        {
            session.EditWeapon = true;
            session.Selection.Clear();
        }
        Ui.Help("Preview only. Equipment in Entity determines what is carried in game.");
    }

    public void DrawInspector(AssetDocument<MotionClip> document, Subject subject)
    {
        if (session.WeaponFor(subject.Model) is not { } weapon)
        {
            Ui.Help("Enable a weapon preview and select its socket.");
            return;
        }
        var time = session.Transport.Time;
        var channel = new Channel(MotionClip.OrientKind, weapon.Socket.Id);
        var angles = subject.Pose.SocketAngles.GetValueOrDefault(weapon.Socket.Id);
        Ui.Header(weapon.Prop.Name + " rotation");
        Ui.Help("Rotation follows the attachment frame. Turn aims in the screen; tilt points toward/away; twist rolls the blade onto its edge.");
        var autokey = session.AutoKey;
        if (ImGui.Checkbox("Autokey", ref autokey))
        {
            session.AutoKey = autokey;
        }

        var degrees = angles.RadiansToDegrees();
        var changed = Ui.Drag("Turn (degrees)", ref degrees.Z, .5f, -2880, 2880, "%.1f");
        changed |= Ui.Drag("Tilt (degrees)", ref degrees.Y, .5f, -180, 180, "%.1f");
        changed |= Ui.Drag("Twist (degrees)", ref degrees.X, .5f, -2880, 2880, "%.1f");
        if (changed)
        {
            session.PoseWeapon(degrees.DegreesToRadians());
        }

        if (ImGui.Button("Broadside"))
        {
            session.PoseWeapon(angles with
            {
                X = 0,
                Y = 0
            });
            session.CommitAll();
        }
        ImGui.SameLine();
        if (ImGui.Button("Edge-on"))
        {
            session.PoseWeapon(angles with
            {
                X = MathF.PI / 2,
                Y = 0
            });
            session.CommitAll();
        }
        if (ImGui.Button("Reverse face"))
        {
            session.PoseWeapon(angles with
            {
                X = MathF.PI
            });
            session.CommitAll();
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset rotation"))
        {
            session.PoseWeapon(Vector3.Zero);
            session.CommitAll();
        }
        if (session.HasPendingPose)
        {
            Ui.Problem("Unkeyed rotation. Key pose to keep it; seeking discards it.");
        }

        if (ImGui.Button("Key pose"))
        {
            session.PoseWeapon(angles);
            session.KeyPose();
        }
        var key = ClipAuthoring.Track(document.Asset, channel)?.Keys.FirstOrDefault(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
        if (key is not null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Delete rotation key"))
            {
                session.Edit(document, () => ClipAuthoring.DeleteKeys(document.Asset, time, [channel]));
            }

            if (Ui.EnumCombo("Ease to next key", key.Ease, AnimationCurveEditor.EasingKinds) is { } ease)
            {
                session.Edit(document, () => ClipAuthoring.SetEase(document.Asset, time, ease, [channel]));
            }
        }
        Ui.Help("Gold: drag around the grip to turn. Blue: drag up/down to tilt. Green: drag left/right to twist. Shift slows adjustment. Use 0 to 360 for a full turn.");
        if (ImGui.Button("Save animation"))
        {
            session.Save(document);
        }

        if (ImGui.CollapsingHeader("Weapon asset / import OBJ"))
        {
            PropFields(session.Assets.Part(weapon.Prop.Id)!);
        }

        foreach (var problem in session.Assets.Problems(document))
        {
            Ui.Problem(problem);
        }
    }

    private void PropFields(AssetDocument<Part> document)
    {
        _partFields.Placement(document);
        _partFields.Locations(document);
        _partFields.Import(document);
        foreach (var piece in document.Asset.Geometry)
        {
            ImGui.PushID(piece.Id);
            if (ImGui.CollapsingHeader(piece.Id))
                _geometryInspector.Draw(piece, null, change => session.Change(document, () => { change(piece); document.Asset.Validate(); }), document.Asset);
            ImGui.PopID();
        }
        if (ImGui.Button("Open part geometry editor")) session.Open(document.Id);
        if (Ui.Button("Undo weapon edit", document.CanUndo))
        {
            document.Undo();
        }

        ImGui.SameLine();
        if (Ui.Button("Redo", document.CanRedo))
        {
            document.Redo();
        }

        if (ImGui.Button("Save weapon"))
        {
            session.Save(document);
        }
    }

    public bool DrawOverlay(ViewportFrame frame, Subject subject)
    {
        if (session.WeaponFor(subject.Model) is not { } weapon)
        {
            return false;
        }

        var socket = new ActorPose(subject.Pose, Vector2.Zero, 1).Socket(weapon.Socket);
        var center = frame.Screen(socket.Origin);
        var draw = frame.Draw;
        var scale = Ui.Scale;
        var angles = subject.Pose.SocketAngles.GetValueOrDefault(weapon.Socket.Id);
        var tip = frame.Screen(PartPlacement3D.Point(socket, weapon.Prop, weapon.Prop.Point(EquipmentPoints.Tip) ?? default));
        draw.AddLine(center, tip, StudioTheme.Selection.PackedValue, 1);
        draw.AddCircle(tip, 6 * scale, StudioTheme.Selection.PackedValue, 20, 2);
        draw.AddLine(center, frame.Screen(socket.TransformPoint(new(0, .24f, 0))), StudioTheme.WeaponAxisY.PackedValue, 2);
        draw.AddLine(center, frame.Screen(socket.TransformPoint(new(0, 0, .24f))), StudioTheme.WeaponAxisZ.PackedValue, 2);
        draw.AddCircle(center, 38 * scale, StudioTheme.RotationGuide.PackedValue, 48, 1);
        var screenAngle = new ActorPose(subject.Pose, Vector2.Zero, 1).SocketTurn(weapon.Socket);
        var turn = center + new Vector2(MathF.Cos(screenAngle), -MathF.Sin(screenAngle)) * 38 * scale;
        var tilt = center + new Vector2(-65, -30) * scale;
        var twist = center + new Vector2(-65, 25) * scale;
        foreach (var (name, point, color) in new[] { ("Turn", turn, StudioTheme.Selection.PackedValue), ("Tilt", tilt, StudioTheme.WeaponAxisZ.PackedValue), ("Twist", twist, StudioTheme.WeaponAxisY.PackedValue) })
        {
            draw.AddCircleFilled(point, 7 * scale, color);
            draw.AddText(point + new Vector2(10, -7) * scale, color, name);
            if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Vector2.Distance(ViewportFrame.Mouse, point) < 12 * scale)
            {
                _weaponDrag = name;
                _weaponPointerAngle = MathF.Atan2(center.Y - ViewportFrame.Mouse.Y, ViewportFrame.Mouse.X - center.X);
                _weaponStart = angles;
                session.BeginDrag();
            }
        }
        if (_weaponDrag is not null)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _weaponDrag = null;
                session.EndDrag();
            }
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
        draw.AddText(frame.Origin + new Vector2(10, 8) * scale, StudioTheme.WeaponCaption.PackedValue, "WEAPON: " + weapon.Socket.Id + (session.AutoKey ? " | AUTOKEY" : " | key pose to keep changes"));
        return true;
    }
}
