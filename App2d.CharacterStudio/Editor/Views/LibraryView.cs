using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Editing;
using App2d.CharacterStudio.Editor.Model;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>Independent Geometry, Material and Piece documents. Character fitting is an explicit Piece preview.</summary>
internal sealed class LibraryView(EditorSession session, Viewport viewport) : IWorkspaceView
{
    private readonly GeometryCanvas _canvas = new(session), _windowCanvas = new(session);
    private string? _windowGeometry;
    private bool _windowOpen;
    private bool _windowFocused;
    private bool _focusEquipment;
    private bool _showGameSize;
    private float _rotation, _scale = 1;
    private Vector2 _translation;
    private string _socketId = "", _socketControl = "", _socketToward = "";
    public Workspace Mode => Workspace.Library;
    public AssetDocument<GeometryAsset2D>? FocusedWindow => _windowOpen && _windowFocused ? session.Assets.Geometry(_windowGeometry) : null;
    public void ShowGeometryWindow(string id) { _windowGeometry = id; _windowOpen = true; }
    public void CloseGeometryWindow() => _windowOpen = false;
    public void FocusEquipment() => _focusEquipment = true;
    public float TimelineHeight => session.LibraryDocument is AssetDocument<Piece> && session.PreviewPartOnWearer ? 64 : 40;
    public void Timeline()
    {
        if (session.LibraryDocument is AssetDocument<Piece> && session.PreviewPartOnWearer) TransportBar.Draw(session, preview: true);
        else ImGui.TextDisabled("Local coordinates  |  Ctrl+S save  |  Ctrl+Z undo  |  Wheel zoom  |  Middle drag pan");
    }
    public void Overlay(ViewportFrame frame) { }

    public void Outline()
    {
        if (session.LibraryDocument is not { } doc) { Ui.Help("New > Geometry, Render material, or Piece."); return; }
        Ui.Header(AssetKinds.Label(doc.Kind));
        ImGui.TextWrapped(doc.Name);
        ImGui.TextDisabled(doc.Id);
        if (ImGui.CollapsingHeader("Saved location"))
            ImGui.TextWrapped(doc.Path ?? Path.Combine(session.Assets.Root, AssetKinds.Folder(doc.Kind), doc.Id + ".json") + (doc.IsNew ? " (not saved yet)" : ""));
        if (ImGui.Button("Save this asset")) session.Save(doc);
        if (doc is AssetDocument<Piece> && ImGui.Button("Equipment settings")) FocusEquipment();
        Ui.Help(doc is AssetDocument<Piece> { Asset.Game.Equipment: not null }
            ? "One inventory item equips both sides. Equipment settings control its placement and replacement behavior."
            : doc is AssetDocument<Piece>
            ? "A Piece attaches directly. Its geometry and named material stay shared. Instance placement is edited on the model."
            : "Editing this shared asset updates every Piece that references it.");
        foreach (var reference in session.Assets.UsedBy(doc.Id))
            if (ImGui.Selectable($"Used by {session.Assets.Find(reference.Id)?.Name ?? reference.Id} ({reference.Relation})")) session.Open(reference.Id);
        if (doc is AssetDocument<GeometryAsset2D> geometry)
        {
            var points = GeometryAssetEditing.Points(geometry.Asset);
            Ui.Header("Points");
            for (var i = 0; i < points.Count; i++)
                if (ImGui.Selectable($"{i + 1}: {points[i].X:0.###}, {points[i].Y:0.###}", _canvas.Selected == i)) _canvas.Selected = i;
        }
    }

    public void Inspector()
    {
        if (session.LibraryDocument is not { } doc) return;
        var name = doc.Name;
        if (Ui.Text("Name", ref name, 100) && !string.IsNullOrWhiteSpace(name))
            session.Change(doc, () =>
            {
                switch (doc)
                {
                    case AssetDocument<GeometryAsset2D> g: g.Asset.Name = name; break;
                    case AssetDocument<RenderMaterialAsset2D> m: m.Asset.Name = name; break;
                    case AssetDocument<Piece> p: p.Asset.Name = name; break;
                }
            });
        switch (doc)
        {
            case AssetDocument<GeometryAsset2D> geometry:
                if (ImGui.Button("Open geometry in its own window")) ShowGeometryWindow(geometry.Id);
                GeometryFields(geometry.Asset, (candidate, discrete) => SetGeometry(geometry, candidate, discrete), _canvas);
                break;
            case AssetDocument<RenderMaterialAsset2D> material:
                Ui.Header("Shared render material");
                MaterialFields(material.Asset.Material, value => session.Change(material, () => material.Asset.Material = value));
                break;
            case AssetDocument<Piece> piece:
                PieceFields(piece);
                break;
        }
        foreach (var problem in session.Assets.Problems(doc)) Ui.Problem(problem);
    }

    private void SetGeometry(AssetDocument<GeometryAsset2D> doc, GeometryAsset2D candidate, bool discrete = false)
    {
        try
        {
            candidate.Validate();
            if (discrete) session.Edit(doc, () => doc.Replace(candidate));
            else session.Change(doc, () => doc.Replace(candidate));
        }
        catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex)) { session.Report(ex.Message, true); }
    }

    private void GeometryFields(GeometryAsset2D asset, Action<GeometryAsset2D, bool> set, GeometryCanvas canvas)
    {
        Ui.Header("Local geometry");
        if (Ui.Combo("Geometry kind", asset.Kind, GeometryJson2D.DrawableKinds.Append("mesh")) is { } kind)
        {
            if (kind == "mesh")
                set(asset with { Geometry = null, SpatialCurve = null, Mesh = PartMesh.Extrude([new(-.2f, -.2f), new(.2f, -.2f), new(.2f, .2f), new(-.2f, .2f)], .02f) }, true);
            else set(asset with { Geometry = GeometryJson2D.CreateDefault(kind), Mesh = null, SpatialCurve = null }, true);
            return;
        }
        if (asset.Geometry is IShape2D shape && shape is not (SimplePolygon2D or ConvexPolygon2D or Triangle2D or HeightFieldShape2D))
            ShapeInspector.Draw(shape, value => set(asset with { Geometry = value }, false));
        var points = GeometryAssetEditing.Points(asset);
        for (var i = 0; i < points.Count; i++)
        {
            var index = i;
            var point = points[i];
            ImGui.PushID(i);
            if (Ui.Drag2($"Point {i + 1}", ref point, .005f, -10000, 10000))
            {
                var edited = points.ToArray(); edited[index] = point;
                set(GeometryAssetEditing.WithPoints(asset, edited), false);
            }
            if (ImGui.IsItemActivated()) canvas.Selected = i;
            ImGui.PopID();
        }
        canvas.Selected = Math.Clamp(canvas.Selected, 0, Math.Max(0, points.Count - 1));
        if (GeometryAssetEditing.CanInsert(asset) && points.Count > 0)
        {
            if (ImGui.Button("Insert after selected point")) set(GeometryAssetEditing.InsertAfter(asset, canvas.Selected), true);
            if (Ui.Button("Remove selected point", points.Count > GeometryAssetEditing.MinimumPoints(asset)))
                set(GeometryAssetEditing.RemovePoint(asset, canvas.Selected), true);
        }
        if (asset.Geometry is RoundedPolylineCurve2D rounded)
        {
            var softness = rounded.Softness;
            if (Ui.Slider("Bend softness", ref softness, 0, 1)) set(asset with { Geometry = rounded with { Softness = softness } }, false);
        }
        if (asset.Geometry is BSpline2D spline)
        {
            var degree = spline.Degree;
            if (ImGui.SliderInt("Degree", ref degree, 1, Math.Min(8, spline.ControlPoints.Length - 1)))
                set(asset with { Geometry = BSpline2D.Builder.Create(spline.ControlPoints.ToArray(), degree) }, false);
        }
        if (asset.Geometry is Arc2D arc)
        {
            var center = arc.Center; var radius = arc.Radius;
            var start = float.RadiansToDegrees(arc.StartAngleRadians); var sweep = float.RadiansToDegrees(arc.SweepAngleRadians);
            if (Ui.Drag2("Center", ref center)) set(asset with { Geometry = arc with { Center = center } }, false);
            if (Ui.Drag("Radius", ref radius, .005f, .001f, 10000)) set(asset with { Geometry = arc with { Radius = radius } }, false);
            if (Ui.Drag("Start degrees", ref start, 1, -36000, 36000)) set(asset with { Geometry = arc with { StartAngleRadians = float.DegreesToRadians(start) } }, false);
            if (Ui.Drag("Sweep degrees", ref sweep, 1, -360, 360)) set(asset with { Geometry = arc with { SweepAngleRadians = float.DegreesToRadians(sweep) } }, false);
        }
        if (asset.Geometry is HeightFieldShape2D heightfield)
        {
            var bottom = heightfield.YMin;
            if (Ui.Drag("Bottom Y", ref bottom, .005f, -10000, 10000)) set(asset with { Geometry = heightfield with { YMin = bottom } }, false);
        }
        if (asset.Mesh is { } mesh)
        {
            ImGui.TextDisabled($"{mesh.Vertices.Count} vertices, {mesh.Triangles.Count / 3} triangles");
            if (mesh.Outline is not null)
            {
                var thickness = mesh.Thickness;
                if (Ui.Drag("Extrusion thickness", ref thickness, .005f, .0001f, 32)) set(asset with { Mesh = mesh with { Thickness = thickness } }, false);
            }
            var vertices = mesh.Outline ?? mesh.Vertices;
            for (var i = 0; i < vertices.Count; i++)
            {
                var z = vertices[i].Z; var index = i;
                if (!Ui.Drag($"Point {i + 1} Z", ref z, .005f, -10000, 10000)) continue;
                var changed = vertices.ToList(); changed[index] = changed[index] with { Z = z };
                set(asset with { Mesh = mesh.Outline is null ? mesh with { Vertices = changed } : mesh with { Outline = changed } }, false);
            }
        }
        if (ImGui.Button("Import geometry from OBJ..."))
        {
            using var dialog = new OpenFileDialog { Filter = "Triangulated OBJ|*.obj", Title = "Import geometry" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (new FileInfo(dialog.FileName).Length > 16 * 1024 * 1024) throw new InvalidDataException("OBJ must be under 16 MB.");
                    var importedMesh = PartGeometryBuilder.ImportObj(File.ReadAllText(dialog.FileName), 1, "#b8bdc5").Mesh;
                    set(asset with { Geometry = null, SpatialCurve = null, Mesh = importedMesh }, true);
                }
                catch (Exception ex) when (ex is IOException || AuthoringWorkspace.IsAssetError(ex)) { session.Report(ex.Message, true); }
            }
        }
        if (asset.Geometry is IShape2D && ImGui.Button("Extrude outline to mesh"))
        {
            var outline = PartContours2D.LocalContour(asset.Entry(RenderMaterialDefinition2D.Ink));
            set(asset with { Geometry = null, Mesh = PartMesh.Extrude(outline.Select(p => new Vector3f(p.X, p.Y)), .02f) }, true);
        }
        if (points.Count > 0 && ImGui.CollapsingHeader("Transform all points around origin"))
        {
            Ui.Drag("Rotate degrees", ref _rotation, 1, -36000, 36000);
            Ui.Drag("Scale factor", ref _scale, .01f, .001f, 100);
            Ui.Drag2("Translate", ref _translation, .005f, -10000, 10000);
            if (ImGui.Button("Apply point transform"))
            {
                set(GeometryAssetEditing.TransformPoints(asset, Matrix3x2.CreateScale(_scale) * Matrix3x2.CreateRotation(float.DegreesToRadians(_rotation)) * Matrix3x2.CreateTranslation(_translation)), true);
                _rotation = 0; _scale = 1; _translation = default;
            }
        }
        Ui.Help("Click and drag numbered points on the canvas. Bézier handles show their control polygon. Invalid edits retain the last valid geometry. Coordinates use the asset origin.");
    }

    private static void MaterialFields(RenderMaterialDefinition2D material, Action<RenderMaterialDefinition2D> set)
    {
        var filled = material.Fill is not null;
        if (ImGui.Checkbox("Fill", ref filled)) set(material with { Fill = filled ? "#b8bdc5" : null });
        if (material.Fill is { } fill && Ui.ColorHex("Fill color", ref fill)) set(material with { Fill = fill });
        var outlined = material.Outline is not null;
        if (ImGui.Checkbox("Outline", ref outlined)) set(material with { Outline = outlined ? RenderOutlineDefinition2D.Ink : null });
        if (material.Outline is { } outline)
        {
            var color = outline.Color ?? "#222b32"; var width = outline.Width ?? .01f;
            if (Ui.ColorHex("Outline color", ref color)) set(material with { Outline = outline with { Color = color } });
            if (Ui.Drag("Outline width", ref width, .001f, 0, 1)) set(material with { Outline = outline with { Width = width } });
        }
        var stroke = material.StrokeWidth ?? .025f;
        if (Ui.Drag("Curve stroke width", ref stroke, .001f, .001f, 100)) set(material with { StrokeWidth = stroke });
        MaterialAnimationFields.Draw(new() { Material = material, Geometry = Rectangle2D.Builder.FromSize(.5f, .5f) }, edit =>
        {
            var entry = new PartGeometry { Material = material }; edit(entry); set(entry.RenderMaterial);
        });
    }

    private void PieceFields(AssetDocument<Piece> doc)
    {
        var piece = doc.Asset;
        Ui.Header("Geometry");
        var source = piece.InlineGeometry is not null ? "(inline)" : piece.Geometry ?? "(choose)";
        if (Ui.Combo("Geometry asset", source, session.Assets.Geometries.Select(g => g.Id).Order(StringComparer.Ordinal).Prepend("(inline)").Prepend("(choose)")) is { } geometry)
            session.Edit(doc, () =>
            {
                doc.Asset.Geometry = geometry.StartsWith('(') ? null : geometry;
                doc.Asset.InlineGeometry = geometry == "(inline)" ? new() { Id = "inline", Name = "Inline geometry", Geometry = GeometryJson2D.CreateDefault("rectangle") } : null;
            });
        if (piece.Geometry is { } geometryId && ImGui.Button("Edit shared geometry")) session.Open(geometryId);
        if (piece.InlineGeometry is { } inline)
            GeometryFields(inline, (value, discrete) =>
            {
                try
                {
                    value.Validate();
                    if (discrete) session.Edit(doc, () => doc.Asset.InlineGeometry = value);
                    else session.Change(doc, () => doc.Asset.InlineGeometry = value);
                }
                catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex)) { session.Report(ex.Message, true); }
            }, _canvas);
        Ui.Header("Render material");
        if (Ui.Combo("Material asset", piece.Material.Asset ?? "(inline)", session.Assets.RenderMaterials.Select(m => m.Id).Order(StringComparer.Ordinal).Prepend("(inline)")) is { } material)
            session.Edit(doc, () => doc.Asset.Material = material == "(inline)" ? new() { Fill = "#b8bdc5" } : new() { Asset = material });
        if (piece.Material.Asset is { } materialId)
        {
            Ui.Help("Shared material. Edit it to update every user; instance color overrides belong on the model.");
            if (ImGui.Button("Edit shared material")) session.Open(materialId);
        }
        else MaterialFields(piece.Material, value => session.Change(doc, () => doc.Asset.Material = value));
        EquipmentFields(doc);
        Ui.Header("Organization");
        var groups = string.Join(", ", piece.Groups);
        if (Ui.Text("Groups (comma separated)", ref groups, 256))
            session.Change(doc, () => doc.Asset.Groups = [.. groups.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase)]);
        Ui.Help("Use paths such as Clothes/Armor. Browser Group by supports groups and default attachments in either order.");
        if (piece.Game?.Equipment is { } equipment)
            ImGui.TextDisabled($"Default placement: {equipment.Slot} (paired)");
        else
        {
            var attachment = piece.Attachment ?? "";
            if (Ui.Text("Default attachment (socket ID)", ref attachment, 64))
                session.Change(doc, () => doc.Asset.Attachment = string.IsNullOrWhiteSpace(attachment) ? null : attachment.Trim());
        }
        Ui.Header("Piece transform in local space");
        var xy = new Vector2(piece.X, piece.Y); var scale = new Vector2(piece.ScaleX, piece.ScaleY);
        var degrees = float.RadiansToDegrees(piece.Angle); var layer = piece.Layer;
        if (Ui.Drag2("Offset", ref xy)) session.Change(doc, () => { doc.Asset.X = xy.X; doc.Asset.Y = xy.Y; });
        if (Ui.Drag2("Scale", ref scale, .01f, -100, 100)) session.Change(doc, () => { doc.Asset.ScaleX = scale.X; doc.Asset.ScaleY = scale.Y; });
        if (Ui.Drag("Angle degrees", ref degrees, 1, -36000, 36000)) session.Change(doc, () => doc.Asset.Angle = float.DegreesToRadians(degrees));
        if (Ui.Drag("Layer", ref layer, .005f, -16, 16)) session.Change(doc, () => doc.Asset.Layer = layer);
        Mounting(doc);
    }

    private void EquipmentFields(AssetDocument<Piece> doc)
    {
        Ui.Header("Game / Equipment");
        if (_focusEquipment) { ImGui.SetScrollHereY(0); _focusEquipment = false; }
        var enabled = doc.Asset.Game?.Equipment is not null;
        if (ImGui.Checkbox("Equippable item", ref enabled))
            session.Edit(doc, () => doc.Asset.Game = enabled ? new() { Equipment = new() { Slot = PersonEquipmentSlots.Shins } } : null);
        if (doc.Asset.Game?.Equipment is not { } settings) return;
        void Set(EquipmentSettings value) => session.Edit(doc, () => doc.Asset.Game = doc.Asset.Game! with { Equipment = value });
        if (Ui.Combo("Person equipment slot", settings.Slot, PersonEquipmentSlots.All) is { } slot) Set(settings with { Slot = slot });
        var replace = settings.Replace;
        if (ImGui.Checkbox("Replace base artwork", ref replace)) Set(settings with { Replace = replace });
        Ui.Help(replace ? "Hides the slot's original artwork while equipped. Unequip restores it; shin replacement keeps the thighs."
            : "Draws over the original body artwork. Equipping another item in this slot swaps the worn pair.");
        var mirror = settings.MirrorRight;
        if (ImGui.Checkbox("Mirror right side", ref mirror)) Set(settings with { MirrorRight = mirror });
        var (left, right) = PersonEquipment.Sockets(settings.Slot);
        ImGui.TextWrapped($"One inventory item, two placements: {left} and {right}.");
        Ui.Help("Save the Piece and restart the game to include it in the starter inventory. F7 opens equipment. No attachment to the shared Person model is needed.");
    }

    private void Mounting(AssetDocument<Piece> doc)
    {
        var equipment = doc.Asset.Game?.Equipment;
        Ui.Header(equipment is null ? "Attach Piece directly" : "Equipment fitting preview");
        var subjects = session.Assets.Models.Select(m => m.Id).Concat(session.Assets.Variants.Select(v => v.Id)).Order(StringComparer.Ordinal);
        if (Ui.Combo("Wearer", session.SubjectId ?? "", subjects) is { } wearer)
        {
            session.SetSubject(wearer);
            var model = session.SubjectModel?.Asset ?? session.Assets.Model(session.SubjectVariant?.Asset.Base)?.Asset;
            session.PreviewSocket = model?.Sockets.FirstOrDefault(s => s.Id == doc.Asset.Attachment)?.Id ?? model?.Sockets.FirstOrDefault()?.Id ?? "";
        }
        var basis = session.SubjectModel ?? session.Assets.Model(session.SubjectVariant?.Asset.Base);
        if (basis is null) { Ui.Help("Choose a wearer or create a model to attach this Piece."); return; }
        if (equipment is not null)
        {
            var (left, right) = PersonEquipment.Sockets(equipment.Slot);
            foreach (var required in new[] { left, right }.Where(id => basis.Asset.Sockets.All(s => s.Id != id)))
                Ui.Problem($"This model has no '{required}' equipment mount. Choose a standard Person model.");
            var preview = session.PreviewPartOnWearer;
            if (ImGui.Checkbox("Preview equipped pair (temporary)", ref preview)) session.PreviewPartOnWearer = preview;
            Ui.Help("This preview changes no model documents. Equip and unequip the saved item in the game's inventory.");
            return;
        }
        if (Ui.Combo("Socket", session.PreviewSocket, basis.Asset.Sockets.Select(s => s.Id)) is { } socket) session.PreviewSocket = socket;
        if (Ui.Button("Use default attachment", basis.Asset.Sockets.Any(s => s.Id == doc.Asset.Attachment))) session.PreviewSocket = doc.Asset.Attachment!;
        var onWearer = session.PreviewPartOnWearer;
        if (ImGui.Checkbox("Preview on wearer (temporary)", ref onWearer)) session.PreviewPartOnWearer = onWearer;
        var mirror = session.PreviewMirror;
        if (ImGui.Checkbox("Mirror this placement", ref mirror)) session.PreviewMirror = mirror;
        if (Ui.Button("Attach to " + basis.Name, basis.Asset.Sockets.Any(s => s.Id == session.PreviewSocket)))
            session.AttachPart(doc.Id, session.PreviewSocket, session.PreviewMirror);
        if (session.EntityId is { } entity && ImGui.Button("Equip on " + entity)) session.EquipAppearance(entity, doc.Id, session.PreviewSocket);
        Ui.Help(session.SubjectVariant is null ? "Attach writes an instance to this model. Shared geometry and material remain separate assets."
            : "Attach writes to the base model, so all its variants inherit the instance. Use Model to override it per variant.");
        if (ImGui.CollapsingHeader("Create a socket on a rig control"))
        {
            Ui.Text("Socket ID", ref _socketId, 64);
            var controls = basis.Asset.Controls.Select(c => c.Id).ToArray();
            if (Ui.Combo("Carried by control", _socketControl, controls) is { } control) _socketControl = control;
            if (Ui.Combo("Point toward (optional)", _socketToward, controls.Where(c => c != _socketControl).Prepend("")) is { } toward) _socketToward = toward;
            if (Ui.Button("Create socket", _socketId.Length > 0 && controls.Contains(_socketControl)))
                session.AddPieceSocket(_socketId, _socketControl, _socketToward.Length == 0 ? null : _socketToward);
            Ui.Help("For a shin, choose a control on that leg and point toward the other end of the shin. This edits the selected wearer's base model.");
        }
    }

    public void Stage()
    {
        try
        {
            switch (session.LibraryDocument)
            {
                case AssetDocument<GeometryAsset2D> geometry:
                    _canvas.Draw(geometry.Asset, value => SetGeometry(geometry, value));
                    break;
                case AssetDocument<RenderMaterialAsset2D> material:
                    ImGui.TextDisabled("Surface swatch: fill, outline and alpha");
                    var swatch = new GeometryAsset2D { Id = material.Id, Name = material.Name, Geometry = Rectangle2D.Builder.FromSize(.6f, .6f) };
                    _canvas.Draw(swatch, null, swatch.Entry(material.Asset.Material));
                    break;
                case AssetDocument<Piece> piece when session.PreviewPartOnWearer:
                    if (ImGui.SmallButton("Fit wearer")) viewport.Fit(session.Scene().FirstOrDefault());
                    ImGui.SameLine();
                    ImGui.Checkbox("Game-size inset", ref _showGameSize);
                    var previous = viewport.GameSize;
                    viewport.GameSize = _showGameSize;
                    try { viewport.Draw(ImGui.GetContentRegionAvail(), session.Scene(), session); }
                    finally { viewport.GameSize = previous; }
                    break;
                case AssetDocument<Piece> piece:
                    var part = session.Assets.PartOf(piece.Id)!;
                    var source = piece.Asset.InlineGeometry ?? session.Assets.Geometry(piece.Asset.Geometry)!.Asset;
                    _canvas.Draw(source with { Id = piece.Id }, piece.Asset.InlineGeometry is null ? null : value =>
                    {
                        try { value.Validate(); session.Change(piece, () => piece.Asset.InlineGeometry = value); }
                        catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex)) { session.Report(ex.Message, true); }
                    }, part.Geometry[0]);
                    break;
                default: Ui.Help("Create or open Geometry, Render material, or Piece from the browser."); break;
            }
        }
        catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex)) { Ui.Help(ex.Message); }
    }

    public void DrawWindows()
    {
        _windowFocused = false;
        if (!_windowOpen || session.Assets.Geometry(_windowGeometry) is not { } doc) return;
        var display = ImGui.GetIO().DisplaySize;
        var maximum = Vector2.Max(new(1, 1), display - new Vector2(24, 24) * Ui.Scale);
        ImGui.SetNextWindowSize(Vector2.Min(new Vector2(1000, 700) * Ui.Scale, maximum), ImGuiCond.Appearing);
        ImGui.SetNextWindowPos(display / 2, ImGuiCond.Appearing, new(.5f, .5f));
        ImGui.SetNextWindowSizeConstraints(Vector2.Min(new(360, 240), maximum), maximum);
        if (ImGui.Begin($"Geometry: {doc.Name}###geometry-window", ref _windowOpen))
        {
            _windowFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            if (ImGui.Button("Save geometry")) session.Save(doc);
            ImGui.SameLine();
            if (Ui.Button("Undo", doc.CanUndo)) doc.Undo();
            ImGui.SameLine();
            if (Ui.Button("Redo", doc.CanRedo)) doc.Redo();
            var size = ImGui.GetContentRegionAvail();
            var width = MathF.Min(300 * Ui.Scale, MathF.Max(1, size.X * .4f));
            ImGui.BeginChild("geometry-window-fields", new(width, size.Y));
            GeometryFields(doc.Asset, (candidate, discrete) => SetGeometry(doc, candidate, discrete), _windowCanvas);
            ImGui.EndChild();
            ImGui.SameLine();
            ImGui.BeginChild("geometry-window-canvas", new(0, size.Y), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            _windowCanvas.Draw(doc.Asset, value => SetGeometry(doc, value));
            ImGui.EndChild();
        }
        ImGui.End();
    }
}
