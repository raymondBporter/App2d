using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

internal sealed class LibraryPartFields(EditorSession session)
{
    private string _newLocation = "";
    private float _objScale = 1;

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
}
