using App2d.Core.Mathematics;
using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.Demos;

internal sealed class HexWheelDemo : IDemo
{
    private float _tileRadius = HexWheelSimulation.DefaultTileRadius;
    private float _innerRadius = HexWheelSimulation.DefaultInnerRadius;
    private float _dropHeight = .4f;
    private float _motorSpeed = -.85f;
    private float _gravity = 9.81f;
    private float _frequency = 8f;
    private float _damping = .8f;
    private bool _motorOn = true;
    private bool _paused;
    private bool _showAnchors;
    private bool _releaseAfterStep;
    private double _elapsed;
    private float _cameraX;
    private HexWheelSimulation _simulation = new(HexWheelSimulation.DefaultTileRadius, HexWheelSimulation.DefaultInnerRadius, .4f);

    public string Name => "Hex wheel";
    public void Draw() => DemoCanvas.Layout(DrawCanvas, DrawControls);
    public void Deactivate() => EndGrab();

    public void Update(float seconds)
    {
        if (_paused)
            return;

        const float step = HexWheelSimulation.StepSeconds;
        _elapsed = Math.Min(_elapsed + seconds, 2d * step);
        while (_elapsed >= step)
        {
            _simulation.Step(_motorOn ? _motorSpeed : null);
            if (_releaseAfterStep)
            {
                EndGrab();
            }
            _elapsed -= step;
        }
    }

    private void DrawControls()
    {
        ImGui.TextWrapped("Drag a tile to stretch the wheel. The gold hub drives the inner ring.");
        ImGui.TextUnformatted($"{_simulation.Tiles.Count} tiles · {_simulation.Seams.Count / 2} shared edges");
        if (ImGui.Button("Retile and reset"))
        {
            EndGrab();
            _simulation = new(_tileRadius, _innerRadius, _dropHeight);
            _simulation.World.Gravity = new(0f, -_gravity);
            _simulation.SetSuspension(_frequency, _damping);
            _elapsed = 0d;
            _cameraX = 0f;
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("Pause", ref _paused) && _paused)
        {
            EndGrab();
        }
        if (_paused && ImGui.Button("Step"))
        {
            _simulation.Step(_motorOn ? _motorSpeed : null);
        }
        ImGui.Separator();
        ImGui.Checkbox("Motor", ref _motorOn);
        Ui.Slider("Motor speed (rad/s)", ref _motorSpeed, -2f, 2f);
        if (Ui.Slider("Gravity", ref _gravity, 0f, 18f))
        {
            _simulation.World.Gravity = new(0f, -_gravity);
        }
        if (Ui.Slider("Joint frequency (Hz)", ref _frequency, 2f, 18f) |
            Ui.Slider("Joint damping ratio", ref _damping, 0f, 1.5f))
        {
            _simulation.SetSuspension(_frequency, _damping);
        }
        ImGui.Checkbox("Show edge anchors", ref _showAnchors);
        ImGui.Separator();
        Ui.Slider("Tile radius", ref _tileRadius, .235f, .4f);
        Ui.Slider("Hole radius", ref _innerRadius, 1.65f, 3.4f);
        Ui.Slider("Drop height", ref _dropHeight, 0f, 1.8f);
    }

    private void DrawCanvas()
    {
        var hub = _simulation.Hub;
        if (_simulation.GrabbedTile is null)
        {
            _cameraX = hub.WorldCenterOfMass.X;
        }
        var canvas = DemoCanvas.Begin("wheel", new(_cameraX, 5.55f), new(15f, 13.5f));
        if (canvas.Activated)
        {
            _releaseAfterStep = false;
            _simulation.BeginGrab(canvas.Pointer());
        }
        if (_simulation.GrabbedTile is not null)
        {
            if (ImGui.IsMousePosValid() && (canvas.Active || !_paused))
            {
                _simulation.MoveGrab(canvas.Pointer());
                // Give the final cursor position one physics step, even on a quick release.
                _releaseAfterStep = !canvas.Active;
                if (canvas.Active)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                }
            }
            else
            {
                EndGrab();
            }
        }

        var draw = canvas.Draw;
        draw.PushClipRect(canvas.Min, canvas.Max, true);
        try
        {
            canvas.DrawGrid();
            var ground = canvas.ScreenAt(new(_cameraX, HexWheelSimulation.GroundY)).Y;
            draw.AddRectFilled(new(canvas.Min.X, ground), canvas.Max, Ui.Color(25, 33, 45));
            draw.AddLine(new(canvas.Min.X, ground), new(canvas.Max.X, ground), Ui.Color(120, 144, 161), 2f * Ui.Scale);
            DrawHub(canvas);
            DrawTiles(canvas);
            if (_showAnchors)
            {
                foreach (var joint in _simulation.Seams)
                {
                    var first = canvas.ScreenAt(joint.FirstWorldAnchor);
                    var second = canvas.ScreenAt(joint.SecondWorldAnchor);
                    var color = Ui.Color(255, 208, 126, 180);
                    draw.AddLine(first, second, color, Ui.Scale);
                    draw.AddCircleFilled(first, 1.6f * Ui.Scale, color);
                }
            }
        }
        finally
        {
            draw.PopClipRect();
        }
    }

    private void EndGrab()
    {
        _simulation.EndGrab();
        _releaseAfterStep = false;
    }

    private void DrawHub(DemoCanvas canvas)
    {
        var hub = _simulation.Hub;
        var center = hub.WorldCenterOfMass;
        var screenCenter = canvas.ScreenAt(center);
        var draw = canvas.Draw;
        var radius = _simulation.HubRadius * canvas.Scale;
        draw.AddCircleFilled(screenCenter, radius, Ui.Color(27, 37, 50), 64);
        foreach (var tile in _simulation.DrivenTiles)
        {
            draw.AddLine(screenCenter, canvas.ScreenAt(tile.WorldCenterOfMass), Ui.Color(216, 161, 96, 80), Ui.Scale);
        }
        draw.AddCircle(screenCenter, radius, Ui.Color(216, 161, 96), 64, 1.5f * Ui.Scale);
        var arm = Vector2f.FromAngle(hub.WorldObject.Transform.Rotation) * _simulation.HubRadius;
        draw.AddLine(screenCenter, canvas.ScreenAt(center + arm), Ui.Color(255, 192, 112), 3f * Ui.Scale);
        draw.AddCircleFilled(screenCenter, 5f * Ui.Scale, Ui.Color(255, 192, 112));
    }

    private void DrawTiles(DemoCanvas canvas)
    {
        var corners = _simulation.Hexagon.Vertices;
        Span<Vector2> screen = stackalloc Vector2[6];
        var fill = Ui.Color(80, 163, 161);
        var edge = Ui.Color(30, 79, 88);
        foreach (var tile in _simulation.Tiles)
        {
            var pose = tile.WorldObject.Transform.Matrix;
            for (var i = 0; i < corners.Length; i++)
            {
                screen[i] = canvas.ScreenAt(Vector2f.Transform(corners[i], pose));
            }

            // The screen transform flips Y. Reverse winding for ImGui's clockwise filled polygon.
            screen.Reverse();
            var grabbed = ReferenceEquals(tile, _simulation.GrabbedTile);
            canvas.Draw.AddConvexPolyFilled(ref screen[0], corners.Length, grabbed ? Ui.Color(241, 176, 95) : fill);
            canvas.Draw.AddPolyline(ref screen[0], corners.Length, edge, ImDrawFlags.Closed, Ui.Scale);
        }
    }
}
