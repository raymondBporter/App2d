using App2d.Core;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal sealed class RigidCharacterHost : IDisposable
{
    private static readonly XnaColor BackgroundColor = new(13, 18, 29);
    private static readonly XnaColor PanelColor = new(7, 12, 22, 232);
    private static readonly XnaColor MutedColor = new(157, 174, 199);
    private static readonly XnaColor ActiveColor = new(116, 255, 180);
    private readonly Camera2D _camera = new() { Position = new Vector2(0f, -66f), Zoom = 1.15f };
    private readonly GraphicsSurface2D _surface = new() { Dock = DockStyle.Fill, TabStop = true };
    private readonly RigidCharacterDemo2D _demo = new();
    private readonly PoseAuthoring2D _author = new();
    private readonly RigidPuppetRenderer2D _puppet = new(RigidPartSet2D.PrototypeSets[0]);
    private readonly HashSet<Keys> _keys = [];
    private readonly Form _window;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();

    private Renderer2D? _renderer;
    private FrameTime _frameTime;
    private double _previousTime;
    private bool _authoring;
    private bool _draggingControl;
    private bool _showBones;
    private bool _customPlayback;
    private float _customPlaybackTime;
    private PoseClip2D? _customClip;
    private RigidControl2D _selectedControl = RigidControl2D.HandRight;
    private int _partSetIndex;
    private string _notice = "SplineMan turns from a three-quarter idle into a clear side profile while moving.";
    private bool _disposed;

    public RigidCharacterHost()
    {
        _window = new Form
        {
            Text = "SplineBRO — Reusable Side-Scroller Rig",
            ClientSize = new Size(1280, 800),
            MinimumSize = new Size(920, 620),
            StartPosition = FormStartPosition.CenterScreen,
            KeyPreview = true
        };
        _window.Controls.Add(_surface);

        _surface.RenderFrame += Render;
        _surface.DeviceDisposing += ReleaseRenderer;
        _surface.MouseDown += OnMouseDown;
        _surface.MouseMove += OnMouseMove;
        _surface.MouseUp += OnMouseUp;
        _surface.MouseWheel += OnMouseWheel;
        _window.KeyDown += OnKeyDown;
        _window.KeyUp += OnKeyUp;
        _window.Resize += OnResize;
        _window.Shown += OnShown;
        _window.FormClosed += OnFormClosed;
        _timer.Tick += OnTick;
    }

    public void Run()
    {
        _clock.Start();
        _timer.Start();
        Application.Run(_window);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var totalTime = _clock.Elapsed.TotalSeconds;
        var elapsed = Math.Clamp(totalTime - _previousTime, 0d, 0.05d);
        _previousTime = totalTime;
        _frameTime = new FrameTime((float)elapsed, totalTime, _frameTime.FrameNumber + 1);

        if (_authoring)
        {
            if (_customPlayback && _customClip is not null)
            {
                _customPlaybackTime += (float)elapsed;
                _author.Pose = _customClip.Sample(_customPlaybackTime);
            }
        }
        else
        {
            var movement = (_keys.Contains(Keys.D) || _keys.Contains(Keys.Right) ? 1 : 0) -
                (_keys.Contains(Keys.A) || _keys.Contains(Keys.Left) ? 1 : 0);
            _demo.Update((float)elapsed, movement);
            var desiredCameraX = _demo.Hips.X + _demo.Facing * 80f;
            _camera.Position = new Vector2(float.Lerp(_camera.Position.X, desiredCameraX, 0.075f), -66f);
        }

        _surface.Refresh();
    }

    private void Render(GraphicsDevice device, int width, int height)
    {
        _renderer ??= new Renderer2D(_camera, device);
        _renderer.BeginFrame(width, height, _frameTime);
        try
        {
            _renderer.Clear(BackgroundColor);
            DrawWorld(_renderer);
            var hips = _authoring ? _demo.Hips : _demo.DisplayHips;
            var pose = _authoring ? _author.Pose : _demo.Pose;
            var solved = StandardSkeleton2D.Solve(hips, pose, _demo.Facing);
            _puppet.Render(
                _renderer,
                solved,
                _frameTime.TotalSeconds,
                _authoring ? 0.65f : _demo.SideViewAmount);
            if (_showBones)
                DrawBones(_renderer, solved);
            if (_authoring)
                DrawAuthoringControls(_renderer, hips, pose);
            else
                DrawFootPlantStatus(_renderer, solved);
            DrawHud(_renderer, width, height);
        }
        finally
        {
            _renderer.EndFrame();
        }
    }

    private static void DrawWorld(Renderer2D renderer)
    {
        renderer.DrawGrid(50f, 5);
        Span<Vector2> ground =
        [
            new(-5000f, RigidCharacterDemo2D.GroundY),
            new(5000f, RigidCharacterDemo2D.GroundY),
            new(5000f, RigidCharacterDemo2D.GroundY - 190f),
            new(-5000f, RigidCharacterDemo2D.GroundY - 190f)
        ];
        renderer.DrawWorldConvexPolygon(ground, new XnaColor(28, 39, 53));
        Span<Vector2> groundLine =
        [
            new(-5000f, RigidCharacterDemo2D.GroundY),
            new(5000f, RigidCharacterDemo2D.GroundY)
        ];
        renderer.DrawWorldPolyline(groundLine, new XnaColor(77, 105, 123), 4f);
    }

    private static void DrawBones(Renderer2D renderer, SolvedRigidPose2D pose)
    {
        foreach (var bone in pose.Bones.Values)
        {
            if (bone.Length <= 0.01f)
                continue;
            Span<Vector2> line = [bone.Start, bone.End];
            renderer.DrawWorldPolyline(line, new XnaColor(255, 245, 207, 220), 3f);
            renderer.DrawWorldCircle(bone.Start, 5f, new XnaColor(22, 26, 38), 3f);
        }
    }

    private void DrawFootPlantStatus(Renderer2D renderer, SolvedRigidPose2D pose)
    {
        var left = pose.Bones[StandardBones2D.FootLeft].Start;
        var right = pose.Bones[StandardBones2D.FootRight].Start;
        renderer.DrawWorldCircle(left, 7f,
            _demo.LeftFootPlanted ? ActiveColor : new XnaColor(255, 196, 91), 2f);
        renderer.DrawWorldCircle(right, 7f,
            _demo.RightFootPlanted ? ActiveColor : new XnaColor(255, 196, 91), 2f);
    }

    private void DrawAuthoringControls(Renderer2D renderer, Vector2 hips, RigidPose2D pose)
    {
        foreach (var control in Enum.GetValues<RigidControl2D>())
        {
            var world = StandardSkeleton2D.ToWorld(hips, pose.GetTarget(control), _demo.Facing);
            var selected = control == _selectedControl;
            var color = selected ? ActiveColor : new XnaColor(187, 193, 224, 220);
            renderer.DrawWorldCircle(world, selected ? 13f : 9f, color, selected ? 4f : 2f);
            if (!selected)
                continue;
            Span<Vector2> horizontal = [world - new Vector2(18f, 0f), world + new Vector2(18f, 0f)];
            Span<Vector2> vertical = [world - new Vector2(0f, 18f), world + new Vector2(0f, 18f)];
            renderer.DrawWorldPolyline(horizontal, color, 2f);
            renderer.DrawWorldPolyline(vertical, color, 2f);
        }
    }

    private void DrawHud(Renderer2D renderer, int width, int height)
    {
        var panelWidth = Math.Min(width - 36f, 790f);
        renderer.DrawScreenRoundedRectangle(new(18f, 18f, panelWidth, _authoring ? 236f : 210f), 12f, PanelColor);
        renderer.DrawScreenText("SPLINEBRO  /  ONE SKELETON + SWAPPABLE RIGID ART", new Vector2(36f, 49f), XnaColor.White);
        renderer.DrawScreenText($"Set: {_puppet.PartSet.Name}  [V]    Animation: {CurrentAnimationLabel}",
            new Vector2(36f, 79f), ActiveColor);
        renderer.DrawScreenText("A/D move    Space jump    J attack    H hit    V swap art treatment",
            new Vector2(36f, 109f), MutedColor);
        renderer.DrawScreenText("Tab pose editor    B bones    R reset    Wheel zoom    Esc close",
            new Vector2(36f, 137f), MutedColor);
        renderer.DrawScreenText(_notice, new Vector2(36f, 169f), new XnaColor(255, 214, 122));
        renderer.DrawScreenText(
            $"Feet: L {(_demo.LeftFootPlanted ? "PLANTED" : "swing")} / R {(_demo.RightFootPlanted ? "PLANTED" : "swing")}    View: 3/4 idle -> profile motion",
            new Vector2(36f, 197f), MutedColor);

        if (_authoring)
        {
            renderer.DrawScreenText(
                $"EDITOR  drag handles | Q/E torso | Z/X head | K snapshot ({_author.Frames.Count}) | P play | Ctrl+S save | Del clear",
                new Vector2(36f, 225f), ActiveColor);
        }

        renderer.DrawScreenRoundedRectangle(new(width - 278f, height - 73f, 260f, 55f), 10f, PanelColor);
        renderer.DrawScreenText(_authoring ? "POSE AUTHORING PAUSED" : "LIVE SIDE-SCROLLER TEST",
            new Vector2(width - 260f, height - 42f), _authoring ? new XnaColor(255, 196, 91) : ActiveColor);
    }

    private string CurrentAnimationLabel => _authoring
        ? _customPlayback ? "user keyframe playback" : "editable IK pose"
        : _demo.AnimationLabel;

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (!_authoring || e.Button != MouseButtons.Left)
            return;

        var world = _camera.DeviceToWorld(new Vector2(e.X, e.Y));
        _selectedControl = FindNearestControl(world);
        _draggingControl = true;
        _customPlayback = false;
        _surface.Capture = true;
        MoveSelectedControl(world);
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_draggingControl)
            return;
        MoveSelectedControl(_camera.DeviceToWorld(new Vector2(e.X, e.Y)));
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        _draggingControl = false;
        _surface.Capture = false;
    }

    private RigidControl2D FindNearestControl(Vector2 world)
    {
        var best = _selectedControl;
        var bestDistance = float.PositiveInfinity;
        foreach (var control in Enum.GetValues<RigidControl2D>())
        {
            var target = StandardSkeleton2D.ToWorld(_demo.Hips, _author.Pose.GetTarget(control), _demo.Facing);
            var distance = Vector2.DistanceSquared(world, target);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = control;
        }
        return best;
    }

    private void MoveSelectedControl(Vector2 world)
    {
        var local = StandardSkeleton2D.ToLocal(_demo.Hips, world, _demo.Facing);
        _author.Pose = _author.Pose.WithTarget(_selectedControl, local);
        _surface.Refresh();
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        var devicePoint = new Vector2(e.X, e.Y);
        var beforeZoom = _camera.DeviceToWorld(devicePoint);
        _camera.Zoom = Math.Clamp(_camera.Zoom * (e.Delta > 0 ? 1.1f : 1f / 1.1f), 0.45f, 2.4f);
        _camera.Position += beforeZoom - _camera.DeviceToWorld(devicePoint);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _keys.Add(e.KeyCode);
        switch (e.KeyCode)
        {
            case Keys.Space when !_authoring:
                _demo.Jump();
                break;
            case Keys.J when !_authoring:
                _demo.PlayAttack();
                break;
            case Keys.H when !_authoring:
                _demo.PlayHit();
                break;
            case Keys.V:
                _partSetIndex = (_partSetIndex + 1) % RigidPartSet2D.PrototypeSets.Count;
                _puppet.PartSet = RigidPartSet2D.PrototypeSets[_partSetIndex];
                _notice = "Same bones and animation; only the bone -> part mapping changed.";
                break;
            case Keys.B:
                _showBones = !_showBones;
                break;
            case Keys.Tab:
                _authoring = !_authoring;
                _customPlayback = false;
                if (_authoring)
                    _author.Pose = _demo.Pose;
                _notice = _authoring
                    ? "Drag a hand/foot target, then press K. Those few floats are the whole keyframe."
                    : "Gameplay resumed: locomotion is procedural; attacks are keyframes; curves are additive.";
                break;
            case Keys.Q when _authoring:
                _author.Pose = _author.Pose with { TorsoAngle = _author.Pose.TorsoAngle + 0.04f };
                break;
            case Keys.E when _authoring:
                _author.Pose = _author.Pose with { TorsoAngle = _author.Pose.TorsoAngle - 0.04f };
                break;
            case Keys.Z when _authoring:
                _author.Pose = _author.Pose with { HeadAngle = _author.Pose.HeadAngle + 0.04f };
                break;
            case Keys.X when _authoring:
                _author.Pose = _author.Pose with { HeadAngle = _author.Pose.HeadAngle - 0.04f };
                break;
            case Keys.K when _authoring:
                _author.Capture();
                _notice = $"Captured pose {_author.Frames.Count}: six values groups, no baked sprite frames.";
                break;
            case Keys.P when _authoring:
                _customClip = _author.CreateClip();
                _customPlayback = _customClip is not null;
                _customPlaybackTime = 0f;
                _notice = _customClip is null ? "Capture at least one pose first [K]." : "Playing your interpolated pose list.";
                break;
            case Keys.S when _authoring && e.Control:
                SaveAuthoredPoses();
                break;
            case Keys.Delete when _authoring:
                _author.Clear();
                _customPlayback = false;
                _notice = "Cleared the in-memory authored pose list.";
                break;
            case Keys.R:
                _demo.Reset();
                _author.Pose = RigidPose2D.Neutral;
                _camera.Position = new Vector2(0f, -66f);
                _notice = "Reset character and procedural foot planner.";
                break;
            case Keys.Escape:
                _window.Close();
                break;
            default:
                return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e) => _keys.Remove(e.KeyCode);

    private void SaveAuthoredPoses()
    {
        try
        {
            var path = Path.Combine(Environment.CurrentDirectory, "rigidbro-poses.json");
            _author.Save(path);
            _notice = $"Saved tiny pose list: {path}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _notice = $"Could not save poses: {exception.Message}";
        }
    }

    private void OnResize(object? sender, EventArgs e) =>
        _camera.SetViewport(Math.Max(1, _surface.ClientSize.Width), Math.Max(1, _surface.ClientSize.Height));

    private void OnShown(object? sender, EventArgs e)
    {
        OnResize(sender, e);
        _surface.Focus();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e) => _timer.Stop();

    private void ReleaseRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _surface.RenderFrame -= Render;
        _surface.DeviceDisposing -= ReleaseRenderer;
        _surface.MouseDown -= OnMouseDown;
        _surface.MouseMove -= OnMouseMove;
        _surface.MouseUp -= OnMouseUp;
        _surface.MouseWheel -= OnMouseWheel;
        _window.KeyDown -= OnKeyDown;
        _window.KeyUp -= OnKeyUp;
        _window.Resize -= OnResize;
        _window.Shown -= OnShown;
        _window.FormClosed -= OnFormClosed;
        ReleaseRenderer();
        _timer.Dispose();
        _surface.Dispose();
        _window.Dispose();
    }
}
