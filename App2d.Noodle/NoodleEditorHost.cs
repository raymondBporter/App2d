using App2d.Core;
using App2d.Core.Rendering;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal sealed partial class NoodleEditorHost : NoodleToolHost
{
    private readonly NoodlePerson2D _person = new();
    private readonly JointShowcase2D _jointShowcase = new();
    private bool _draggingTarget;
    private bool _showWeights;

    public NoodleEditorHost() : base("NoodleBRO — Full Body 2D Rig Lab", new Size(1180, 760),
        new Camera2D { Position = new Vector2(125, -55), Zoom = .85f })
    {
    }

    protected override void DrawScene(Renderer2D renderer, FrameTime time)
    {
        renderer.Clear(new XnaColor(17, 22, 33));
        renderer.DrawGrid(50, 5);
        var limb = _person.TargetLimb;
        renderer.DrawWorldCircle(limb.Shoulder, limb.UpperLength + limb.LowerLength, new XnaColor(117, 255, 178, 45), 1);
        _person.Render(renderer, _showWeights);
        if (!limb.Pose.ReachesTarget)
        {
            Span<Vector2> line = [limb.Pose.End, limb.Target];
            renderer.DrawWorldPolyline(line, new XnaColor(255, 118, 118, 150), 2);
        }
        _person.RenderControls(renderer);
        _jointShowcase.Render(renderer, Camera, time.TotalSeconds);
    }
    protected override void HandleSceneInput(bool mouseCaptured, bool keyboardCaptured)
    {
        if (!keyboardCaptured)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Space, false))
            {
                _person.TargetLimb.FlipBend();
            }

            if (ImGui.IsKeyPressed(ImGuiKey.W, false))
            {
                _showWeights = !_showWeights;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.R, false))
            {
                Reset();
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                Close();
            }
        }
        if (mouseCaptured || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _draggingTarget = false;
        }

        if (mouseCaptured)
        {
            return;
        }

        var world = Camera.DeviceToWorld(ImGui.GetIO().MousePos);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            _person.SelectNearestControl(world);
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _draggingTarget = true;
        }

        if (_draggingTarget)
        {
            _person.TargetLimb.SetTarget(world);
        }

        ZoomAtPointer();
    }
    private void Reset()
    {
        _person.Reset();
        Camera.Position = new Vector2(125, -55);
        Camera.Zoom = .85f;
    }
}
