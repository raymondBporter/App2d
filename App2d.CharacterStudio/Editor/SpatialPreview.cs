using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>Orbit, mesh picking and plane dragging over a transient scene, using the generic Core 3D path.</summary>
internal sealed class SpatialPreview : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly ImGuiHost _gui;
    private readonly Renderer3D _renderer;
    private readonly SpatialPreviewScene _scene;
    private RenderTarget2D? _target;
    private nint _image;
    private float _yaw = .55f, _pitch = .32f, _distance = 3.9f;
    private Vector3 _lookAt;
    private bool _perspective = true, _dragging, _orbiting;
    private int _selected;
    private string _dragMode = "View plane";
    private Plane _dragPlane;
    private Vector3 _dragOffset;

    public SpatialPreview(GraphicsDevice device, ImGuiHost gui, Part? sword, Part? curve = null)
    {
        _device = device;
        _gui = gui;
        _renderer = new(device);
        _scene = new(sword, curve);
    }

    public void Draw(Vector2 available)
    {
        Ui.Row(
            () => Ui.Panel("spatial-tools", Inspector, size: new(MathF.Min(280 * Ui.Scale, available.X * .28f), available.Y)),
            () => Ui.Panel("spatial-stage", () => Stage(ImGui.GetContentRegionAvail()),
                size: new(MathF.Max(100, available.X - MathF.Min(280 * Ui.Scale, available.X * .28f) - ImGui.GetStyle().ItemSpacing.X), available.Y),
                flags: ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse));
    }

    private void Inspector()
    {
        ImGui.TextColored(UiTheme.Accent, "3D PREVIEW");
        ImGui.TextWrapped("Spatial meshes share one camera and depth buffer. Choose an object and inspect it from any angle.");
        ImGui.Separator();
        ImGui.Checkbox("Perspective", ref _perspective);
        if (ImGui.Button("3/4 view")) { _yaw = .55f; _pitch = .32f; _distance = 3.9f; _lookAt = default; }
        ImGui.SameLine();
        if (ImGui.Button("Front")) { _yaw = 0; _pitch = 0; }
        ImGui.SameLine();
        if (ImGui.Button("Side")) { _yaw = MathF.PI / 2; _pitch = 0; }
        ImGui.Spacing();
        ImGui.TextWrapped("Drag empty space to orbit. Wheel to zoom. Middle drag to pan.");
        ImGui.TextWrapped("Click an object to select it; drag it on the chosen plane.");
        ImGui.Separator();
        var item = _scene.Objects[_selected];
        if (Ui.Combo("Object", item.Name, _scene.Objects.Select(o => o.Name)) is { } name)
            _selected = _scene.Objects.ToList().FindIndex(o => o.Name == name);
        item = _scene.Objects[_selected];
        if (Ui.Combo("Drag on", _dragMode, ["View plane", "XY plane", "XZ plane", "YZ plane"]) is { } mode) _dragMode = mode;
        var position = item.Position;
        if (ImGui.DragFloat3("Position", ref position, .01f)) item.Position = position;
        var angles = item.Rotation * (180f / MathF.PI);
        if (ImGui.DragFloat3("Rotation", ref angles, .5f)) item.Rotation = angles * (MathF.PI / 180f);
        if (ImGui.Button("Reset object")) item.Reset();
        ImGui.Spacing();
        ImGui.TextDisabled($"{item.Mesh.TriangleCount} triangles");
        ImGui.TextDisabled($"Mesh uploads: {_renderer.MeshUploadCount}");
        ImGui.Separator();
        ImGui.TextWrapped("Preview transforms are temporary. The source assets and rig layers stay unchanged.");
    }

    private Camera3D Camera(Vector2 size)
    {
        var direction = new Vector3(MathF.Sin(_yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch), MathF.Cos(_yaw) * MathF.Cos(_pitch));
        var position = _lookAt + direction * _distance;
        var height = 2f * _distance * MathF.Tan(MathF.PI / 8);
        return _perspective ? Camera3D.Perspective(position, _lookAt, Vector3.UnitY, size.X / size.Y)
            : Camera3D.Orthographic(position, _lookAt, Vector3.UnitY, height * size.X / size.Y, height);
    }

    private void Stage(Vector2 available)
    {
        var size = new Vector2(Math.Max(1, (int)available.X), Math.Max(1, (int)available.Y));
        if (_target is null || _target.Width != (int)size.X || _target.Height != (int)size.Y)
        {
            if (_target is not null) { _gui.Unregister(_image); _target.Dispose(); }
            _target = RenderCapture2D.CreateTarget(_device, (int)size.X, (int)size.Y);
            _image = _gui.Register(_target);
        }
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("spatial-viewport", size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
        var hovered = ImGui.IsItemHovered();
        var io = ImGui.GetIO();
        var camera = Camera(size);
        var mouse = ImGui.GetMousePos() - origin;
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var picked = _scene.Pick(camera.ScreenRay(mouse, size), out var hit);
            _orbiting = picked is null;
            _dragging = picked is not null;
            if (picked is { } index)
            {
                _selected = index;
                var normal = _dragMode switch { "XY plane" => Vector3.UnitZ, "XZ plane" => Vector3.UnitY, "YZ plane" => Vector3.UnitX, _ => camera.Forward };
                _dragPlane = new(normal, -Vector3.Dot(normal, hit));
                _dragOffset = _scene.Objects[index].Position - hit;
            }
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { _dragging = false; _orbiting = false; }
        if (_dragging && camera.ScreenRay(mouse, size).TryIntersect(_dragPlane, out var distance))
            _scene.Objects[_selected].Position = camera.ScreenRay(mouse, size).At(distance) + _dragOffset;
        if (_orbiting || hovered && ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            _yaw -= io.MouseDelta.X * .008f;
            _pitch = Math.Clamp(_pitch + io.MouseDelta.Y * .008f, -1.45f, 1.45f);
        }
        if (hovered)
        {
            _distance = Math.Clamp(_distance * MathF.Pow(1.12f, -io.MouseWheel), 1f, 16f);
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
            {
                var right = Vector3.Normalize(Vector3.Cross(camera.Forward, Vector3.UnitY));
                var up = Vector3.Normalize(Vector3.Cross(right, camera.Forward));
                _lookAt += (-right * io.MouseDelta.X + up * io.MouseDelta.Y) * (_distance * .0015f);
            }
        }
        camera = Camera(size);
        using (_device.UseRenderTarget(_target)) _scene.Draw(_renderer, camera);
        var draw = ImGui.GetWindowDrawList();
        draw.AddImage(_image, origin, origin + size);
        draw.PushClipRect(origin, origin + size, true);
        foreach (var item in _scene.Objects)
        {
            if (!camera.TryProject(item.Position, size, out var screen)) continue;
            var at = origin + new Vector2(screen.X, screen.Y);
            var selected = ReferenceEquals(item, _scene.Objects[_selected]);
            var color = selected ? new Color4b(255, 200, 105).PackedValue : new Color4b(167, 187, 210).PackedValue;
            draw.AddCircle(at, selected ? 7f : 4f, color, 20, selected ? 2f : 1f);
            draw.AddText(at + new Vector2(12, -8), color, item.Name);
        }
        draw.AddText(origin + new Vector2(16, 16), new Color4b(180, 202, 220).PackedValue, _perspective ? "PERSPECTIVE  /  Y UP" : "ORTHOGRAPHIC  /  Y UP");
        draw.PopClipRect();
    }

    public void Dispose()
    {
        if (_target is not null) { _gui.Unregister(_image); _target.Dispose(); }
        _renderer.Dispose();
    }
}
