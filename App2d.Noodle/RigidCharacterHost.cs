using App2d.Core;
using App2d.Core.Rendering;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal sealed partial class RigidCharacterHost : NoodleToolHost
{
    private static readonly XnaColor ActiveColor = new(116, 255, 180);
    private readonly RigidCharacterDemo2D _demo = new();
    private readonly PoseAuthoring2D _author = new();
    private readonly RigidPuppetRenderer2D _puppet = new(RigidPartSet2D.PrototypeSets[0]);
    private bool _authoring;
    private bool _draggingControl;
    private bool _showBones;
    private bool _customPlayback;
    private float _customPlaybackTime;
    private PoseClip2D? _customClip;
    private RigidControl2D _selectedControl = RigidControl2D.HandRight;
    private int _partSetIndex;
    private string _notice = "SplineMan turns from a three-quarter idle into a clear side profile while moving.";

    public RigidCharacterHost() : base("SplineBRO — Reusable Side-Scroller Rig", new Size(1280, 800),
        new Camera2D { Position = new Vector2(0, -66), Zoom = 1.15f })
    { }

    protected override void UpdateScene(FrameTime time, bool keyboardCaptured)
    {
        if (_authoring)
        {
            if (_customPlayback && _customClip is not null)
            {
                _customPlaybackTime += time.DeltaSeconds;
                _author.Pose = _customClip.Sample(_customPlaybackTime);
            }
        }
        else
        {
            var movement = keyboardCaptured ? 0 :
                (ImGui.IsKeyDown(ImGuiKey.D) || ImGui.IsKeyDown(ImGuiKey.RightArrow) ? 1 : 0) -
                (ImGui.IsKeyDown(ImGuiKey.A) || ImGui.IsKeyDown(ImGuiKey.LeftArrow) ? 1 : 0);
            _demo.Update(time.DeltaSeconds, movement);
            var desiredCameraX = _demo.Hips.X + _demo.Facing * 80;
            Camera.Position = new Vector2(float.Lerp(Camera.Position.X, desiredCameraX, .075f), -66);
        }
    }
    protected override void DrawScene(Renderer2D renderer, FrameTime time)
    {
        renderer.Clear(new XnaColor(13, 18, 29));
        DrawWorld(renderer);
        var hips = _authoring ? _demo.Hips : _demo.DisplayHips;
        var pose = _authoring ? _author.Pose : _demo.Pose;
        var solved = StandardSkeleton2D.Solve(hips, pose, _demo.Facing);
        _puppet.Render(renderer, solved, time.TotalSeconds, _authoring ? .65f : _demo.SideViewAmount);
        if (_showBones) DrawBones(renderer, solved);
        if (_authoring) DrawAuthoringControls(renderer, hips, pose);
        else DrawFootPlantStatus(renderer, solved);
    }
    protected override void HandleSceneInput(bool mouseCaptured, bool keyboardCaptured)
    {
        if (ImGui.IsKeyPressed(ImGuiKey.F1, false)) HandleCommand(Keys.F1);
        if (_authoring && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S, false)) HandleCommand(Keys.S, true);
        if (!keyboardCaptured)
        {
            foreach (var (imGuiKey, command) in Shortcuts)
                if (command is not Keys.F1 and not Keys.S && ImGui.IsKeyPressed(imGuiKey, false)) HandleCommand(command, ImGui.GetIO().KeyCtrl);
        }

        if (mouseCaptured || !ImGui.IsMouseDown(ImGuiMouseButton.Left)) _draggingControl = false;
        if (mouseCaptured) return;
        if (_authoring && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _selectedControl = FindNearestControl(Camera.DeviceToWorld(ImGui.GetIO().MousePos));
            _draggingControl = true; _customPlayback = false;
        }
        if (_draggingControl) MoveSelectedControl(Camera.DeviceToWorld(ImGui.GetIO().MousePos));
        ZoomAtPointer(.45f, 2.4f);
    }
    private static readonly (ImGuiKey, Keys)[] Shortcuts =
    [
        (ImGuiKey.Space, Keys.Space), (ImGuiKey.J, Keys.J), (ImGuiKey.H, Keys.H), (ImGuiKey.V, Keys.V),
        (ImGuiKey.B, Keys.B), (ImGuiKey.F1, Keys.F1), (ImGuiKey.Q, Keys.Q), (ImGuiKey.E, Keys.E),
        (ImGuiKey.Z, Keys.Z), (ImGuiKey.X, Keys.X), (ImGuiKey.K, Keys.K), (ImGuiKey.P, Keys.P),
        (ImGuiKey.S, Keys.S), (ImGuiKey.Delete, Keys.Delete), (ImGuiKey.R, Keys.R), (ImGuiKey.Escape, Keys.Escape)
    ];
    private string CurrentAnimationLabel => _authoring ? _customPlayback ? "user keyframe playback" : "editable IK pose" : _demo.AnimationLabel;
    internal void ShowAuthoringStudy() => HandleCommand(Keys.F1);
    private void HandleCommand(Keys key, bool control = false)
    {
        switch (key)
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
            case Keys.F1:
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
            case Keys.S when _authoring && control:
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
                Camera.Position = new Vector2(0f, -66f);
                _notice = "Reset character and procedural foot planner.";
                break;
            case Keys.Escape:
                Close();
                break;
            default:
                return;
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

    }

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

}
