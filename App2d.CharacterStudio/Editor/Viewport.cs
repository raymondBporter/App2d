using App2d.Core.Characters.Editing;
using App2d.Rendering.Characters;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio.Editor;

/// <summary>A subject placed in the viewport. Compare pins sit side by side at one world scale.</summary>
internal sealed record SubjectView(Subject Subject, Vector3 Offset);

/// <summary>One frame of the viewport as the workspace views see it: where things are on screen and what the mouse is doing.</summary>
internal sealed class ViewportFrame(ImDrawListPtr draw, Vector2 origin, Vector2 size, Vector2 anchor, float ppu, IReadOnlyList<SubjectView> subjects, bool hovered)
{
    public ImDrawListPtr Draw { get; } = draw;
    public Vector2 Origin { get; } = origin;
    public Vector2 Size { get; } = size;
    public float Ppu { get; } = ppu;
    public IReadOnlyList<SubjectView> Subjects { get; } = subjects;
    public SubjectView? Primary => Subjects.Count > 0 ? Subjects[0] : null;
    public bool Hovered { get; } = hovered;
    public Vector2 Mouse => ImGui.GetMousePos();

    public Vector2 Screen(Vector3 world, int subject = 0)
    {
        var offset = subject < Subjects.Count ? Subjects[subject].Offset : Vector3.Zero;
        return Origin + anchor + new Vector2(world.X + offset.X, -(world.Y + offset.Y)) * Ppu;
    }

    /// <summary>The world position under a screen point, in a subject's own space, keeping <paramref name="z"/>.</summary>
    public Vector3 World(Vector2 screen, float z = 0, int subject = 0)
    {
        var offset = subject < Subjects.Count ? Subjects[subject].Offset : Vector3.Zero;
        var local = (screen - Origin - anchor) / Ppu;
        return new(local.X - offset.X, -local.Y - offset.Y, z);
    }
}

/// <summary>
/// The one shared viewport: renders evaluated subjects through the existing depth-tested character renderer, owns the camera
/// (session state, not asset data), and hands back a <see cref="ViewportFrame"/> for handles. Fit is explicit.
/// </summary>
internal sealed class Viewport(GraphicsDevice device, ImGuiHost gui, PointCharacterRenderer renderer) : IDisposable
{
    private const float GamePpu = 48; // arena scale at 1280x720
    private readonly List<PuppetDrawing> _drawings = [];
    private readonly CharacterMesh _ground = new(8192);
    private RenderTarget2D? _target, _inset;
    private nint _targetId, _insetId;
    private float _span = 3.2f, _centerY = 1.1f;
    private string? _fittedFor;

    public float Zoom { get; set; } = 1;
    public Vector2 Pan { get; set; }
    /// <summary>Keep the primary subject's travel centred while it moves.</summary>
    public bool Follow { get; set; } = true;
    /// <summary>Also show the scene at game size: enlarged authoring views alone are a poor readability test.</summary>
    public bool GameSize { get; set; } = true;

    public void Fit(Subject? subject)
    {
        Zoom = 1; Pan = default; _fittedFor = subject?.Id;
        if (subject is null) return;
        var ys = subject.Model.Rest.Values.Select(p => p.Y).DefaultIfEmpty(0).ToArray();
        var (min, max) = (MathF.Min(0, ys.Min()), MathF.Max(1, ys.Max()));
        _span = MathF.Max(1.5f, max - min + .9f); _centerY = (min + max) / 2;
    }

    public ViewportFrame Draw(Vector2 available, IReadOnlyList<Subject> scene)
    {
        if (scene.Count > 0 && _fittedFor != scene[0].Id) Fit(scene[0]);
        var width = Math.Max(1, (int)available.X); var height = Math.Max(1, (int)available.Y);
        Ensure(ref _target, ref _targetId, width, height);

        var spacing = scene.Select(s => s.Model.Rest.Values.Select(p => p.X).DefaultIfEmpty(0)).Select(xs => xs.Max() - xs.Min()).DefaultIfEmpty(0).Max() + 1.1f;
        var views = scene.Select((s, i) => new SubjectView(s, new(i * spacing, 0, 0))).ToArray();
        var ppu = MathF.Min(height / _span, width / (_span * 1.3f + (views.Length - 1) * spacing)) * Zoom;
        var followX = Follow && views.Length > 0 ? views[0].Subject.Pose.Locomotion.X : 0;
        var centerX = followX + (views.Length - 1) * spacing / 2;
        var anchor = new Vector2(width / 2f - centerX * ppu, height / 2f + _centerY * ppu) + Pan;

        while (_drawings.Count < views.Length) _drawings.Add(new());
        for (var i = 0; i < views.Length; i++) _drawings[i].Build(views[i].Subject.Model, views[i].Subject.Pose);
        Render(_target!, width, height, anchor, ppu, views, centerX);

        var start = ImGui.GetCursorScreenPos(); var size = new Vector2(width, height);
        ImGui.InvisibleButton("viewport", size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
        var hovered = ImGui.IsItemHovered();
        var draw = ImGui.GetWindowDrawList(); draw.AddImage(_targetId, start, start + size);
        if (GameSize && views.Length > 0) DrawInset(draw, start, size, views, centerX);
        if (hovered)
        {
            var io = ImGui.GetIO();
            if (io.MouseWheel != 0) Zoom = Math.Clamp(Zoom * MathF.Pow(1.1f, io.MouseWheel), .1f, 10);
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle)) Pan += io.MouseDelta;
        }
        return new(draw, start, size, anchor, ppu, views, hovered);
    }

    private void DrawInset(ImDrawListPtr draw, Vector2 start, Vector2 size, SubjectView[] views, float centerX)
    {
        var width = (int)MathF.Min(size.X * .45f, 520 * Ui.Scale); var height = (int)MathF.Min(size.Y * .28f, 150 * Ui.Scale);
        if (width < 40 || height < 40) return;
        Ensure(ref _inset, ref _insetId, width, height);
        Render(_inset!, width, height, new(width / 2f - centerX * GamePpu, height * .85f), GamePpu, views, centerX);
        var min = start + size - new Vector2(width + 8, height + 8);
        draw.AddImage(_insetId, min, min + new Vector2(width, height));
        draw.AddRect(min, min + new Vector2(width, height), Ui.Color(90, 100, 96), 0, ImDrawFlags.None, 1);
        draw.AddText(min + new Vector2(6, 4), Ui.Color(90, 100, 96), "game size");
    }

    private void Render(RenderTarget2D target, int width, int height, Vector2 anchor, float ppu, SubjectView[] views, float centerX)
    {
        device.SetRenderTarget(target); device.Clear(new Color(237, 238, 226));
        var projection = PointCharacterRenderer.Projection(width, height, anchor, ppu);
        BuildGround(centerX, width / ppu, ppu);
        renderer.Draw(_ground, projection, Matrix.Identity, writeDepth: false);
        for (var i = 0; i < views.Length; i++) renderer.Draw(_drawings[i].Mesh, projection, Matrix.CreateTranslation(views[i].Offset.X, views[i].Offset.Y, 0));
        device.SetRenderTarget(null);
    }

    private void BuildGround(float centerX, float span, float ppu)
    {
        _ground.Clear(); var ink = new Color(154, 169, 158); var pixel = 1 / ppu;
        _ground.Line(new(centerX - span, 0, 7), new(centerX + span, 0, 7), pixel, ink);
        var first = (int)MathF.Floor((centerX - span / 2 - 1) * 4); var last = (int)MathF.Ceiling((centerX + span / 2 + 1) * 4);
        for (var x = first; x <= last && x - first < 2000; x++) _ground.Line(new(x * .25f, 0, 7), new(x * .25f, -(x % 4 == 0 ? 9 : 4) * pixel, 7), pixel, ink);
    }

    private void Ensure(ref RenderTarget2D? target, ref nint id, int width, int height)
    {
        if (target is not null && target.Width == width && target.Height == height) return;
        if (target is not null) { gui.Unregister(id); target.Dispose(); }
        target = new(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        id = gui.Register(target);
    }

    public void Dispose() { _target?.Dispose(); _inset?.Dispose(); }
}
