using App2d.Core.Characters;
using App2d.Rendering.Characters;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame
{
    private bool _headEditorOpen, _headFit = true, _headGuides = true, _headFaceMode;
    private int _headPoint = 2, _headDrag = -1;
    private HeadShape _headDraft = new();
    private readonly List<HeadShape> _headVariations = [new(), new() { Width = .7f, Height = 1.3f, Muzzle = 1.25f, Depth = .3f, Drop = .12f, Jaw = .1f }, new() { Width = 1.6f, Height = .6f, Muzzle = .2f, Depth = .65f, Jaw = .55f }, new() { Width = .8f, Height = .8f, Muzzle = .9f, Depth = .8f, Brow = .35f, Jaw = .8f, Roundness = .4f }];
    private readonly List<HeadShape> _headProposals = [];
    private readonly HeadDrawing _headDrawing = new();
    private readonly CharacterMesh _headBody = new(), _headFace = new(16);
    private RenderTarget2D? _headPreview;
    private nint _headPreviewId;
    private Vector2 _headCenter = new(.5f, 0);
    private float _headSpan = 3.5f;
    private string _headError = "";

    private void SetHead(HeadShape shape)
    {
        try
        {
            shape.Validate();
            // Validate the curved fill as well as the control polygon before accepting a drag.
            _headBody.Clear(); _headFace.Clear();
            _headDrawing.Build(_headBody, _headFace, shape, Vector3.Zero, Vector3.UnitX, -Vector3.UnitY, .035f, new Color(22, 25, 29));
            _document.Appearance.CustomHead = _headDraft = shape; _headError = "";
        }
        catch (InvalidDataException ex) { _headError = ex.Message; }
    }
    private void DrawHeadEditor()
    {
        if (!_headEditorOpen || !EntityVocabulary.EntityAnatomies.Contains(_document.Library.Anatomy)) return;
        var display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowSize(Vector2.Min(new(930 * Scale, 760 * Scale), display * .92f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(display * .5f, ImGuiCond.FirstUseEver, new(.5f));
        if (!ImGui.Begin("Head editor", ref _headEditorOpen)) { ImGui.End(); return; }
        var a = _document.Appearance;
        var enabled = a.CustomHead is not null;
        if (ImGui.Checkbox("Use edited head", ref enabled)) { if (enabled) SetHead(_headDraft); else a.CustomHead = null; }
        ImGui.SameLine(); ImGui.TextDisabled(_document.Library.Label + " / live animation in the main preview");
        if (ImGui.Button("Import head")) Attempt(() =>
        {
            using var dialog = new OpenFileDialog { Filter = "Head configuration (*.json)|*.json" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            using var data = JsonDocument.Parse(File.ReadAllText(dialog.FileName));
            if (!data.RootElement.TryGetProperty("offsets", out var offsets) || offsets.ValueKind != JsonValueKind.Array || !data.RootElement.TryGetProperty("version", out var version) || version.GetInt32() != 1)
                throw new InvalidDataException("Import a version 1 head configuration from the head workshop.");
            var shape = data.RootElement.Deserialize<HeadShape>(AuthoredJson.Tolerant) ?? throw new InvalidDataException("Empty head configuration.");
            shape.Validate(); SetHead(shape); _headFit = true;
        });
        ImGui.SameLine();
        if (ImGui.Button("Export head")) Attempt(() =>
        {
            using var dialog = new SaveFileDialog { Filter = "Head configuration (*.json)|*.json", FileName = "head.json", DefaultExt = "json", AddExtension = true };
            if (dialog.ShowDialog() == DialogResult.OK) File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(a.CustomHead ?? _headDraft, AuthoredJson.Options));
        });
        ImGui.SameLine(); if (ImGui.Button("Reset head")) { SetHead(new()); _headFit = true; }
        ImGui.Separator();
        var area = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("Head canvas panel", new(MathF.Max(230 * Scale, area.X - 300 * Scale), 0), ImGuiChildFlags.None);
        if (ImGui.RadioButton("Outline", !_headFaceMode)) _headFaceMode = false;
        ImGui.SameLine(); if (ImGui.RadioButton("Face", _headFaceMode)) _headFaceMode = true;
        ImGui.SameLine(); ImGui.Checkbox("Guides", ref _headGuides);
        ImGui.SameLine(); if (ImGui.SmallButton("Fit")) _headFit = true;
        HeadCanvas(a.CustomHead ?? _headDraft, new(ImGui.GetContentRegionAvail().X, MathF.Max(220 * Scale, ImGui.GetContentRegionAvail().Y - 170 * Scale)));
        ImGui.TextWrapped(_headFaceMode ? "Drag the face box. Size and tilt are independent of the outline." : "Drag a point, or select it and use arrow keys. Shift moves in larger steps.");
        ImGui.TextUnformatted("Session variations");
        for (var i = 0; i < _headVariations.Count; i++)
        {
            ImGui.PushID(i);
            if (ImGui.Button((i + 1).ToString())) { SetHead(_headVariations[i]); _headFit = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Apply variation. Right-click to remove.");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) { _headVariations.RemoveAt(i--); }
            ImGui.PopID(); ImGui.SameLine();
        }
        ImGui.BeginDisabled(_headVariations.Count >= 8);
        if (ImGui.Button("Keep variation")) _headVariations.Add(a.CustomHead ?? _headDraft);
        ImGui.EndDisabled();
        if (ImGui.Button("Explore four shapes"))
        {
            _headProposals.Clear(); var source = a.CustomHead ?? _headDraft;
            float Between(Limit limit) => limit.Min + Random.Shared.NextSingle() * (limit.Max - limit.Min);
            for (var tries = 0; tries < 80 && _headProposals.Count < 4; tries++)
            {
                var proposal = source with { Width = Between(HeadShape.Limits.Width), Height = Between(HeadShape.Limits.Height), Muzzle = Between(HeadShape.Limits.Muzzle), Depth = Between(HeadShape.Limits.Depth), Drop = Between(HeadShape.Limits.Drop), Brow = Between(HeadShape.Limits.Brow), Jaw = Between(HeadShape.Limits.Jaw), Offsets = default };
                if (proposal.IsSimple()) _headProposals.Add(proposal);
            }
        }
        for (var i = 0; i < _headProposals.Count; i++) { if (i > 0) ImGui.SameLine(); if (ImGui.Button("Try " + (i + 1))) { SetHead(_headProposals[i]); _headFit = true; } }
        if (_headError.Length > 0) ImGui.TextWrapped(_headError);
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild(_headFaceMode ? "Head face fields" : "Head outline fields", new(0, 0), ImGuiChildFlags.Borders);
        void Field(string name, Func<HeadShape, float> read, Limit limit, Func<HeadShape, float, HeadShape> write)
        {
            var shape = a.CustomHead ?? _headDraft;
            Slider(name, read(shape), limit, v => SetHead(write(shape, v)));
        }
        if (!_headFaceMode)
        {
        Field("Skull width", s => s.Width, HeadShape.Limits.Width, (s, v) => s with { Width = v });
        Field("Skull height", s => s.Height, HeadShape.Limits.Height, (s, v) => s with { Height = v });
        Field("Muzzle length", s => s.Muzzle, HeadShape.Limits.Muzzle, (s, v) => s with { Muzzle = v });
        Field("Muzzle thickness", s => s.Depth, HeadShape.Limits.Depth, (s, v) => s with { Depth = v });
        Field("Muzzle up / down", s => s.Drop, HeadShape.Limits.Drop, (s, v) => s with { Drop = v });
        Field("Forehead slope", s => s.Brow, HeadShape.Limits.Brow, (s, v) => s with { Brow = v });
        Field("Jaw fullness", s => s.Jaw, HeadShape.Limits.Jaw, (s, v) => s with { Jaw = v });
        Field("Corner softness", s => s.Roundness, HeadShape.Limits.Roundness, (s, v) => s with { Roundness = v });
        ImGui.Separator();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##Head point", HeadShape.PointNames[_headPoint]))
        { for (var i = 0; i < 10; i++) if (ImGui.Selectable(HeadShape.PointNames[i], i == _headPoint)) _headPoint = i; ImGui.EndCombo(); }
        Field("Point horizontal", s => s.Offsets[_headPoint].X, HeadShape.Limits.Offset, (s, v) => s with { Offsets = s.Offsets.With(_headPoint, s.Offsets[_headPoint] with { X = v }) });
        Field("Point vertical", s => s.Offsets[_headPoint].Y, HeadShape.Limits.Offset, (s, v) => s with { Offsets = s.Offsets.With(_headPoint, s.Offsets[_headPoint] with { Y = v }) });
        if (ImGui.Button("Reset point")) { var shape = a.CustomHead ?? _headDraft; SetHead(shape with { Offsets = shape.Offsets.With(_headPoint, default) }); }
        ImGui.SameLine(); if (ImGui.Button("Clear hand edits")) SetHead((a.CustomHead ?? _headDraft) with { Offsets = default });
        }
        ImGui.Separator();
        if (_headFaceMode)
        {
        Field("Face left / right", s => s.FaceX, HeadShape.Limits.FaceX, (s, v) => s with { FaceX = v });
        Field("Face up / down", s => s.FaceY, HeadShape.Limits.FaceY, (s, v) => s with { FaceY = v });
        Field("Face size", s => s.FaceSize, HeadShape.Limits.FaceSize, (s, v) => s with { FaceSize = v });
        Field("Face tilt", s => s.FaceAngle, HeadShape.Limits.FaceAngle, (s, v) => s with { FaceAngle = v });
        var current = a.CustomHead ?? _headDraft;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##Head expression", current.Face)) { foreach (var id in new[] { "none", "grumpy" }.Concat(FaceExpressions.Names)) if (ImGui.Selectable(id, current.Face == id)) SetHead(current with { Face = id }); ImGui.EndCombo(); }
        }
        var colored = a.CustomHead ?? _headDraft;
        var color = new Vector3(Convert.ToInt32(colored.Color.Substring(1, 2), 16), Convert.ToInt32(colored.Color.Substring(3, 2), 16), Convert.ToInt32(colored.Color.Substring(5, 2), 16)) / 255;
        if (ImGui.ColorEdit3("Head color", ref color, ImGuiColorEditFlags.NoInputs)) SetHead(colored with { Color = $"#{(int)(color.X * 255):x2}{(int)(color.Y * 255):x2}{(int)(color.Z * 255):x2}" });
        ImGui.TextWrapped("Save look keeps the edited head and weapons with this character. Export head makes a reusable head-only file.");
        ImGui.EndChild();
        _document.RecordEdit(ImGui.IsAnyItemActive());
        ImGui.End();
    }
    private void HeadCanvas(HeadShape shape, Vector2 size)
    {
        var width = Math.Max(1, (int)size.X); var height = Math.Max(1, (int)size.Y);
        if (_headPreview is null || _headPreview.Width != width || _headPreview.Height != height)
        {
            if (_headPreview is not null) { _gui.Unregister(_headPreviewId); _headPreview.Dispose(); }
            _headPreview = new(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
            _headPreviewId = _gui.Register(_headPreview);
        }
        if (_headFit)
        {
            var bounds = shape.Points().ToList();
            if (shape.Face != "none") { var r = shape.FaceSize * .71f; bounds.Add(new(shape.FaceX - r, shape.FaceY - r)); bounds.Add(new(shape.FaceX + r, shape.FaceY + r)); }
            var min = bounds.Aggregate(Vector2.Min); var max = bounds.Aggregate(Vector2.Max);
            _headCenter = (min + max) / 2; _headSpan = MathF.Max(2.5f, MathF.Max(max.X - min.X, max.Y - min.Y) + 1); _headFit = false;
        }
        var ppu = MathF.Min(width, height) / _headSpan;
        var anchor = new Vector2(width / 2f, height / 2f) - _headCenter * ppu;
        _headBody.Clear(); _headFace.Clear();
        _headDrawing.Build(_headBody, _headFace, shape, Vector3.Zero, Vector3.UnitX, -Vector3.UnitY, .035f, new Color(22, 25, 29));
        GraphicsDevice.SetRenderTarget(_headPreview); GraphicsDevice.Clear(new Color(230, 234, 227));
        var projection = PointCharacterRenderer.Projection(width, height, anchor, ppu);
        _renderer.Draw(_headBody, projection, Matrix.Identity);
        if (_faces.TryGetValue(shape.Face, out var face)) _renderer.Draw(_headFace, projection, Matrix.Identity, face, false);
        GraphicsDevice.SetRenderTarget(null);
        var start = ImGui.GetCursorScreenPos(); var draw = ImGui.GetWindowDrawList();
        ImGui.InvisibleButton("Head editing canvas", new(width, height));
        draw.AddImage(_headPreviewId, start, start + new Vector2(width, height));
        Vector2 Screen(Vector2 p) => start + anchor + p * ppu;
        var points = shape.Points(); var mouse = ImGui.GetIO().MousePos;
        draw.PushClipRect(start, start + new Vector2(width, height), true);
        if (_headGuides && !_headFaceMode)
        {
            for (var i = 0; i < points.Length; i++)
            {
                draw.AddLine(Screen(points[i]), Screen(points[(i + 1) % points.Length]), 0x88725b34, Scale);
                draw.AddCircleFilled(Screen(points[i]), (i == _headPoint ? 6 : 4) * Scale, i == _headPoint ? 0xff4386cb : 0xff8a6451);
            }
        }
        if (_headGuides && _headFaceMode && shape.Face != "none")
        {
            var angle = shape.FaceAngle * MathF.PI / 180;
            var corners = new[] { new Vector2(-.5f, -.5f), new(.5f, -.5f), new(.5f, .5f), new(-.5f, .5f) }.Select(p => Screen(new(shape.FaceX + (p.X * MathF.Cos(angle) - p.Y * MathF.Sin(angle)) * shape.FaceSize, shape.FaceY + (p.X * MathF.Sin(angle) + p.Y * MathF.Cos(angle)) * shape.FaceSize))).ToArray();
            for (var i = 0; i < 4; i++) draw.AddLine(corners[i], corners[(i + 1) % 4], 0xff4386cb, 2 * Scale);
        }
        draw.PopClipRect();
        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _headDrag = -1;
            if (_headFaceMode && shape.Face != "none")
            {
                var p = (mouse - Screen(new(shape.FaceX, shape.FaceY))) / ppu;
                var angle = -shape.FaceAngle * MathF.PI / 180;
                var rotated = new Vector2(p.X * MathF.Cos(angle) - p.Y * MathF.Sin(angle), p.X * MathF.Sin(angle) + p.Y * MathF.Cos(angle));
                if (MathF.Abs(rotated.X) <= shape.FaceSize / 2 && MathF.Abs(rotated.Y) <= shape.FaceSize / 2) _headDrag = 10;
            }
            else if (_headGuides)
                for (var i = 0; i < 10; i++) if (Vector2.Distance(mouse, Screen(points[i])) < 12 * Scale) { _headDrag = _headPoint = i; break; }
        }
        void Move(Vector2 delta)
        {
            if (_headFaceMode) SetHead(shape with { FaceX = HeadShape.Limits.FaceX.Clamp(shape.FaceX + delta.X), FaceY = HeadShape.Limits.FaceY.Clamp(shape.FaceY + delta.Y) });
            else { var p = shape.Offsets[_headPoint]; SetHead(shape with { Offsets = shape.Offsets.With(_headPoint, new(HeadShape.Limits.Offset.Clamp(p.X + delta.X), HeadShape.Limits.Offset.Clamp(p.Y + delta.Y))) }); }
        }
        if (ImGui.IsItemActive() && _headDrag >= 0 && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) Move(ImGui.GetIO().MouseDelta / ppu);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _headDrag = -1;
        if (ImGui.IsItemFocused() && !ImGui.GetIO().WantTextInput)
        {
            var delta = Vector2.Zero;
            if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow)) delta.X--;
            if (ImGui.IsKeyPressed(ImGuiKey.RightArrow)) delta.X++;
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) delta.Y--;
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) delta.Y++;
            if (delta != Vector2.Zero) Move(delta * (ImGui.GetIO().KeyShift ? .1f : .01f));
        }
        if (ImGui.IsItemHovered() && ImGui.GetIO().MouseWheel != 0) _headSpan = Math.Clamp(_headSpan / MathF.Pow(1.1f, ImGui.GetIO().MouseWheel), 1, 12);
    }
}
