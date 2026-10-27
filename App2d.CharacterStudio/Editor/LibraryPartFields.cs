using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Physics;
using App2d.Core.Geometry.Shapes;

namespace App2d.CharacterStudio.Editor;

internal sealed class LibraryPartFields(EditorSession session)
{
    private string _newLocation = "";
    private float _objScale = 1;

    public void Physics(AssetDocument<Part> doc)
    {
        Ui.Header("Physics body");
        if (doc.Asset.Physics is not { } body)
        {
            if (Ui.Button("Add physics body", doc.Asset.Behavior is null && doc.Asset.Geometry.Any(g => g.Collision is { Enabled: true })))
                session.Edit(doc, () => { doc.Asset.Physics = new() { MotionType = doc.Asset.Motion is null ? BodyMotionType2D.Dynamic : BodyMotionType2D.Kinematic }; doc.Asset.Validate(); });
            Ui.Help("Enable collision on a piece first. All body pieces share one mass and collision mask.");
            return;
        }
        if (Ui.Combo("Motion type", body.MotionType.ToString(), Enum.GetNames<BodyMotionType2D>()) is { } type)
            session.Edit(doc, () => { body.MotionType = Enum.Parse<BodyMotionType2D>(type); doc.Asset.Validate(); });
        void Scalar(string label, float value, float min, float max, Action<float> set)
        {
            if (Ui.Drag(label, ref value, .05f, min, max))
                session.Change(doc, () => { set(value); doc.Asset.Validate(); });
        }
        Scalar("Mass", body.Mass, .001f, 100000, v => body.Mass = v);
        Scalar("Rotational inertia", body.MomentOfInertia, .001f, 1000000, v => body.MomentOfInertia = v);
        Scalar("Gravity scale", body.GravityScale, -100, 100, v => body.GravityScale = v);
        Scalar("Friction", body.Friction, 0, 10, v => body.Friction = v);
        Scalar("Restitution", body.Restitution, 0, 1, v => body.Restitution = v);
        var frozen = body.FreezeRotation;
        if (ImGui.Checkbox("Lock body rotation", ref frozen)) session.Edit(doc, () => body.FreezeRotation = frozen);
        var transfers = body.TransfersContactMotion;
        if (ImGui.Checkbox("Carry contacting bodies", ref transfers)) session.Edit(doc, () => body.TransfersContactMotion = transfers);
        if (ImGui.SmallButton("Remove physics body")) session.Edit(doc, () => { doc.Asset.Physics = null; doc.Asset.Surface = null; });
        Ui.Help("Preview samples animation only. The world simulates this body; its collision shape stays at the rig's bind pose.");
    }

    public void Participation(AssetDocument<Part> doc, int index)
    {
        var piece = doc.Asset.Geometry[index];
        void Edit(Action<PartGeometry> set) => session.Edit(doc, () => { set(doc.Asset.Geometry[index]); doc.Asset.Validate(); });
        var collision = piece.Collision is not null;
        ImGui.BeginDisabled(piece.Geometry is not IShape2D || piece.Bindings is { Count: > 0 } || piece.ScaleX <= 0 || piece.ScaleX != piece.ScaleY);
        if (ImGui.Checkbox("Collision geometry", ref collision))
            Edit(p => p.Collision = collision ? new() : null);
        ImGui.EndDisabled();
        if (piece.Collision is { } settings)
        {
            var enabled = settings.Enabled;
            if (ImGui.Checkbox("Collision enabled", ref enabled)) Edit(p => p.Collision = settings with { Enabled = enabled });
            var mask = settings.Mask.ToString("X8");
            if (Ui.Text("Collision mask (hex)", ref mask, 8) && uint.TryParse(mask, System.Globalization.NumberStyles.HexNumber, null, out var bits))
                Edit(p =>
                {
                    if (doc.Asset.Physics is null) p.Collision = settings with { Mask = bits };
                    else foreach (var entry in doc.Asset.Geometry.Where(g => g.Collision is not null)) entry.Collision = entry.Collision! with { Mask = bits };
                });
        }
        var rendered = piece.Material is not null;
        ImGui.BeginDisabled(piece.Collision is null);
        if (ImGui.Checkbox("Rendered", ref rendered)) Edit(p => p.Material = rendered ? new RenderMaterialDefinition2D { Fill = "#b97549" } : null);
        ImGui.EndDisabled();
    }

    public void Placement(AssetDocument<Part> doc)
    {
        var asset = doc.Asset;
        var scale = asset.Scale;
        if (Ui.Drag("Size", ref scale, .005f, .001f, 100)) session.Change(doc, () => asset.Scale = scale);
        if (Ui.Combo("Pivot location", asset.Pivot, asset.Locations.Select(l => l.Id).Prepend(PartLocation.Origin)) is { } pivot)
            session.Edit(doc, () => asset.Pivot = pivot);
        if (Ui.Combo("Back view", asset.BackView ?? "", session.Assets.Parts.Where(p => p.Id != doc.Id).Select(p => p.Id).Prepend(""), id => id.Length == 0 ? "Same artwork" : id) is { } back)
            session.Edit(doc, () => asset.BackView = back.Length == 0 ? null : back);
    }

    public void Locations(AssetDocument<Part> doc)
    {
        Ui.Header("Locations");
        foreach (var location in doc.Asset.Locations.ToArray())
        {
            ImGui.PushID(location.Id);
            var point = location.Point.XYZ;
            if (Ui.Drag3(location.Id + " (local XYZ)", ref point))
                session.Change(doc, () => { location.X = point.X; location.Y = point.Y; location.Z = point.Z; doc.Asset.Validate(); });
            var layer = location.Layer;
            if (Ui.Drag("Layer offset", ref layer, .005f, -16, 16)) session.Change(doc, () => location.Layer = layer);
            var angle = location.Angle;
            if (Ui.Drag("Angle (radians)", ref angle, .01f, -1000, 1000)) session.Change(doc, () => location.Angle = angle);
            if (doc.Asset.IsAnimated && Ui.Combo("Control", location.Control ?? "", doc.Asset.Controls.Select(c => c.Id).Prepend("")) is { } control)
                session.Edit(doc, () => location.Control = control.Length == 0 ? null : control);
            if (ImGui.SmallButton("Remove location"))
                session.Edit(doc, () => { doc.Asset.Locations.RemoveAll(l => l.Id == location.Id); doc.Asset.Validate(); });
            ImGui.PopID();
        }
        ImGui.InputTextWithHint("##new-location", "location id", ref _newLocation, 40);
        if (Ui.Button("Add location", _newLocation.Trim().Length > 0))
        {
            var id = _newLocation.Trim();
            if (session.Edit(doc, () => { doc.Asset.Locations.Add(new() { Id = id }); doc.Asset.Validate(); })) _newLocation = "";
        }
    }

    public void Import(AssetDocument<Part> doc)
    {
        Ui.Drag("OBJ import scale", ref _objScale, .001f, .001f, 100);
        if (!ImGui.Button("Replace geometry from OBJ...")) return;
        using var dialog = new OpenFileDialog { Filter = "Triangulated OBJ|*.obj", Title = "Import part geometry" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 16 * 1024 * 1024) throw new InvalidDataException("OBJ must be under 16 MB.");
            var entry = PartGeometryBuilder.ImportObj(File.ReadAllText(dialog.FileName), _objScale, doc.Asset.Geometry.FirstOrDefault()?.RenderMaterial.Fill ?? "#c8b18a");
            if (doc.Asset.IsAnimated) entry.A = entry.Frame = doc.Asset.Controls[0].Id;
            session.Edit(doc, () => { doc.Asset.Geometry = [entry]; doc.Asset.Validate(); }, "Imported geometry. Check size and named locations before saving.");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException) { session.Report(ex.Message, true); }
    }

    public void Motion(AssetDocument<Part> doc)
    {
        Ui.Header("Path motion");
        if (doc.Asset.Motion is not { } motion)
        {
            if (Ui.Button("Add path motion", doc.Asset.Physics is null || doc.Asset.Physics.MotionType == BodyMotionType2D.Kinematic))
                session.Edit(doc, () => doc.Asset.Motion = new() { Curve = CubicBezier2D.Builder.Create(Vector2.Zero, new(0, .5f), new(1, .5f), new(1, 0)) });
            Ui.Help("A path moves the whole part. A physical part must use a kinematic body.");
            return;
        }
        var period = (float)motion.PeriodSeconds;
        if (Ui.Drag("Path period (seconds)", ref period, .05f, .01f, 600))
            session.Change(doc, () => { doc.Asset.Motion = motion with { PeriodSeconds = period }; doc.Asset.Validate(); });
        var repeat = motion.Repeat;
        if (ImGui.Checkbox("Repeat path", ref repeat))
            session.Edit(doc, () => { doc.Asset.Motion = motion with { Repeat = repeat }; doc.Asset.Validate(); });
        ImGui.TextDisabled("Progress: " + string.Join(" → ", motion.Progress));
        if (motion.Curve is QuadraticBezier2D or CubicBezier2D or RoundedPolylineCurve2D)
        {
            var points = PartCurves2D.ControlPoints(motion.Curve);
            for (var i = 0; i < points.Count; i++)
            {
                var index = i;
                var point = points[i];
                if (Ui.Drag2("Path point " + i, ref point, .01f, -10000, 10000))
                    session.Change(doc, () =>
                    {
                        var changed = points.ToArray(); changed[index] = point;
                        doc.Asset.Motion = motion with { Curve = PartCurves2D.WithControlPoints(motion.Curve, changed) };
                        doc.Asset.Validate();
                    });
            }
        }
        if (ImGui.SmallButton("Remove path motion")) session.Edit(doc, () => doc.Asset.Motion = null);
        Ui.Help("Use the standalone preview to seek the path without stepping physics. The world applies its fixed-step velocity.");
    }
}
