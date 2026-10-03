using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>Reusable cutout art, edited on its wearer with the same sockets and character layers as the game.</summary>
internal sealed class AppearanceView(EditorSession session) : IWorkspaceView
{
    private int _piece, _drag = -1;
    private string? _asset;
    public Workspace Mode => Workspace.Appearance;
    public float TimelineHeight => 80 * Ui.Scale;
    public void Timeline() => TransportBar.Draw(session, true);

    public void Outline()
    {
        Ui.Header("Hair and clothing");
        Ui.Help("New > Appearance creates a reusable asset. Right-click an asset to duplicate it.");
        if (Ui.Combo("Appearance", session.PreviewProp, session.Assets.Props.Where(p => p.Asset.Usage != "prop").Select(p => p.Id)) is { } id) session.Open(id);
        if (session.AppearanceDocument is not { } doc || doc.Asset.Usage == "prop") return;
        if (_asset != doc.Id) { _asset = doc.Id; _piece = 0; _drag = -1; }
        for (var i = 0; i < doc.Asset.Solids.Count; i++)
            if (ImGui.Selectable($"Piece {i + 1}", i == _piece)) _piece = i;
        if (ImGui.Button("Add piece"))
        {
            session.Edit(doc, () =>
            {
                doc.Asset.Solids.Add(PropGeometry.Extrude([new(-.1f, 0), new(.1f, 0), new(.1f, .2f), new(-.1f, .2f)], .02f, "#8c4d2c"));
                _piece = doc.Asset.Solids.Count - 1;
            });
        }
    }

    public void Inspector()
    {
        if (session.AppearanceDocument is not { } doc || doc.Asset.Usage == "prop") { Ui.Help("Create or open hair or clothing from the asset browser."); return; }
        var art = doc.Asset;
        Ui.Header("Appearance asset");
        var name = art.Name; if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0) session.Change(doc, () => doc.Asset.Name = name);
        if (Ui.Combo("Type", art.Usage, ["hair", "clothing"]) is { } usage) session.Edit(doc, () => doc.Asset.Usage = usage);
        if (Ui.Combo("Wearer", session.EntityId ?? "", session.Assets.Entities.Select(e => e.Id)) is { } wearer) session.SetEntity(wearer);
        var model = session.Assets.Resolve(session.SubjectId);
        if (model is not null)
        {
            if (Ui.Combo("Socket", session.PreviewSocket, model.Base.Sockets.Select(s => s.Id)) is { } socket)
            { session.PreviewSocket = socket; session.Edit(doc, () => doc.Asset.Attachment = socket); }
            if (Ui.Button("Equip on wearer", session.EntityId is not null)) session.EquipAppearance(session.EntityId!, doc.Id, session.PreviewSocket);
        }
        Ui.Help("Equipping adds this piece. Remove unwanted hair or clothes in Entity > Equipment. Shared edits affect all wearers.");
        var rest = session.ShowRest; if (ImGui.Checkbox("Rest pose", ref rest)) session.ShowRest = rest;
        var size = art.Scale; if (Ui.Drag("Size", ref size, .005f, .001f, 100)) session.Change(doc, () => doc.Asset.Scale = Math.Clamp(size, .001f, 100));
        var grip = art.Grip.Layered; if (Ui.LayeredPoint("Attachment origin", ref grip)) session.Change(doc, () => doc.Asset.Grip = PuppetPoint.From(grip));
        var ink = art.Ink; if (Ui.ColorHex("Ink", ref ink)) session.Change(doc, () => doc.Asset.Ink = ink);
        var line = art.LineWidth; if (Ui.Drag("Line width", ref line, .001f, .001f, .2f)) session.Change(doc, () => doc.Asset.LineWidth = Math.Clamp(line, .001f, .2f));
        if (model is not null && ImGui.Button("Match body ink")) session.Edit(doc, () => { doc.Asset.Ink = model.Base.Ink; doc.Asset.LineWidth = model.Base.LineWidth; });
        if (Ui.Combo("Back view", art.BackView ?? "", session.Assets.Props.Where(p => p.Id != doc.Id && p.Asset.Usage != "prop").Select(p => p.Id).Prepend(""), id => id.Length == 0 ? "Same artwork" : id) is { } back)
            session.Edit(doc, () => doc.Asset.BackView = back.Length == 0 ? null : back);
        Ui.Help("Back view follows the player's view-back animation markers.");
        _piece = Math.Clamp(_piece, 0, Math.Max(0, art.Solids.Count - 1));
        if (art.Solids.Count > 0) Piece(doc);
        if (ImGui.Button("Save all")) session.SaveAll();
        foreach (var problem in session.Assets.Problems(doc)) Ui.Problem(problem);
    }

    private void Piece(AssetDocument<PropAsset> doc)
    {
        var piece = doc.Asset.Solids[_piece];
        Ui.Header($"Piece {_piece + 1}");
        var fill = piece.Fill; if (Ui.ColorHex("Fill", ref fill)) session.Change(doc, () => doc.Asset.Solids[_piece].Fill = fill);
        var outlined = piece.Outlined; if (ImGui.Checkbox("Outline", ref outlined)) session.Edit(doc, () => doc.Asset.Solids[_piece].Outlined = outlined);
        if (outlined)
        {
            var outlineColor = piece.OutlineColor ?? doc.Asset.Ink;
            if (Ui.ColorHex("Outline color", ref outlineColor)) session.Change(doc, () => doc.Asset.Solids[_piece].OutlineColor = outlineColor);
            if (piece.OutlineColor is not null && ImGui.SmallButton("Use prop ink")) session.Change(doc, () => doc.Asset.Solids[_piece].OutlineColor = null);
        }
        if (Ui.Button("Remove piece", doc.Asset.Solids.Count > 1)) { session.Edit(doc, () => doc.Asset.Solids.RemoveAt(_piece)); _piece = 0; return; }
        if (piece.Outline is not { } points) { Ui.Help("This imported mesh has no editable cutout outline."); return; }
        var thickness = piece.Thickness;
        if (Ui.Drag("Thickness", ref thickness, .001f, .0001f, 2)) Cut(doc, points, Math.Clamp(thickness, .0001f, 2));
        var layer = new CharacterLayer2D(points.Average(p => p.Z));
        if (Ui.CharacterLayer("Piece layer", ref layer, -2, 2))
        { var delta = layer.Order - points.Average(p => p.Z); Cut(doc, points.Select(p => p with { Z = p.Z + delta }), thickness); }
        Ui.Help("Drag the gold points in the viewport, or edit coordinates below. Clothing can span both legs in layer order; keep the near arm in front of the belt.");
        if (session.PreviewSocket == PersonWardrobe.BodySocket && session.Assets.Resolve(session.SubjectId) is { } model && model.Rest.ContainsKey("left-hip") && model.Rest.ContainsKey("right-hip"))
        {
            try
            {
                var layers = PersonWardrobeDepths.From(model);
                Ui.Help($"Layer guide: near arm {layers.NearArm:F3}, belt {layers.FrontDetail:F3}, wrap front {layers.WrapFront:F3}, near leg {layers.NearLeg:F3}, far leg {layers.FarLeg:F3}, wrap back {layers.WrapBack:F3}.");
            }
            catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException) { Ui.Help("This rig has custom layer spacing; place the garment using its layer and thickness."); }
        }
        if (ImGui.CollapsingHeader("Outline points"))
        {
            for (var i = 0; i < points.Count; i++)
            {
                ImGui.PushID(i); var p = points[i].XY;
                if (Ui.Drag2("XY", ref p)) { var changed = points.ToArray(); changed[i] = new(p.X, p.Y, points[i].Z); Cut(doc, changed, thickness); }
                if (ImGui.SmallButton("Insert after")) { var changed = points.ToList(); changed.Insert(i + 1, PuppetPoint.From((points[i].XYZ + points[(i + 1) % points.Count].XYZ) / 2)); Cut(doc, changed, thickness); }
                ImGui.SameLine();
                if (Ui.Button("Remove", points.Count > 3)) { var changed = points.ToList(); changed.RemoveAt(i); Cut(doc, changed, thickness); }
                ImGui.PopID();
            }
        }
    }

    private void Cut(AssetDocument<PropAsset> doc, IEnumerable<PuppetPoint> points, float thickness)
    {
        var copy = points.ToArray();
        session.Change(doc, () => AppearanceAuthoring.Cutout(doc.Asset, _piece, copy, thickness));
    }

    public void Overlay(ViewportFrame frame)
    {
        if (frame.Primary is not { } primary || session.AppearanceDocument is not { } doc ||
            session.WeaponFor(primary.Subject.Model) is not { } worn || _piece >= doc.Asset.Solids.Count ||
            doc.Asset.Solids[_piece].Outline is not { } points)
        {
            return;
        }

        var socket = new ActorPose(primary.Subject.Pose, Vector2.Zero, 1).Socket(worn.Socket);
        var gold = Ui.Color(220, 157, 39);
        for (var i = 0; i < points.Count; i++)
        {
            var p = frame.Screen(ActorPose.PropPoint(socket, doc.Asset, points[i]));
            var next = frame.Screen(ActorPose.PropPoint(socket, doc.Asset, points[(i + 1) % points.Count]));
            frame.Draw.AddLine(p, next, gold, 1);
            frame.Draw.AddCircleFilled(p, 4 * Ui.Scale, gold);
            if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Vector2.Distance(p, ViewportFrame.Mouse) < 8 * Ui.Scale)
            { _drag = i; session.Transport.Pause(); session.BeginDrag(); }
        }
        if (_drag < 0) return;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { _drag = -1; session.EndDrag(); return; }
        if (_drag >= points.Count || ImGui.GetIO().MouseDelta == Vector2.Zero) return;
        var origin = ActorPose.PropPoint(socket, doc.Asset, points[_drag]);
        var x = ActorPose.PropPoint(socket, doc.Asset, points[_drag] with { X = points[_drag].X + 1 }) - origin;
        var y = ActorPose.PropPoint(socket, doc.Asset, points[_drag] with { Y = points[_drag].Y + 1 }) - origin;
        var det = x.X * y.Y - x.Y * y.X;
        if (MathF.Abs(det) < 1e-6f) return;
        var delta = frame.World(ViewportFrame.Mouse, origin.Z) - origin;
        var edited = points.ToArray();
        edited[_drag] = points[_drag] with { X = points[_drag].X + (delta.X * y.Y - delta.Y * y.X) / det, Y = points[_drag].Y + (x.X * delta.Y - x.Y * delta.X) / det };
        Cut(doc, edited, doc.Asset.Solids[_piece].Thickness);
    }
}
