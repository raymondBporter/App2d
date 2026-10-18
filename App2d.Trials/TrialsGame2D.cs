using App2d.Core;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Hosting;
using App2d.Core.Input;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Trials;

/// <summary>The starting scene for a trials game, using a circle until the bike is built.</summary>
public sealed partial class TrialsGame2D : Game2D
{
    private const float PlayerRadius = 24f;
    private const float MoveSpeed = 320f;
    private const float JumpSpeed = 420f;
    private const float CameraHeight = 160f;
    private static readonly Vector2 SpawnPosition = new(0f, 180f);
    private static readonly XnaColor GroundColor = new(117, 193, 150);
    private readonly PhysicsWorld2D _world = new() { Gravity = new Vector2(0f, -980f) };
    private readonly WorldObject2D _ground;

    public TrialsGame2D()
    {
        AttachPhysicsWorld(_world);
        Camera.ReferenceViewportHeight = 720f;
        CreateBackground();

        _ground = new WorldObject2D(HalfSpace2D.Builder.Create(Vector2.UnitY, 0f),
            new SolidColorShader(new XnaColor(18, 23, 28)));
        _world.AddBody(_ground, BodyMotionType2D.Static).Restitution = 0f;

        var player = new WorldObject2D(Circle2D.Builder.Create(PlayerRadius), new SolidColorShader(new XnaColor(255, 197, 102)));
        Scene.Add(player);
        Player = _world.AddBody(player, BodyMotionType2D.Dynamic);
        Player.Restitution = 0f;
        ResetPlayer();
    }

    public PhysicsBody2D Player { get; }
    public override string WindowTitle => "App2d Trials";

    public override void Initialize() => ResetPlayer();

    public override void Update(FrameTime time, InputState input)
    {
        if (input.Keyboard.WasPressed(Keys.R))
        {
            ResetPlayer();
        }
        else
        {
            var left = input.Keyboard.IsDown(Keys.A) || input.Keyboard.IsDown(Keys.Left);
            var right = input.Keyboard.IsDown(Keys.D) || input.Keyboard.IsDown(Keys.Right);
            var direction = (right ? 1f : 0f) - (left ? 1f : 0f);
            var velocity = Player.LinearVelocity;
            velocity.X = float.Lerp(velocity.X, direction * MoveSpeed, 1f - MathF.Exp(-12f * time.DeltaSeconds));
            if (input.Keyboard.WasPressed(Keys.Space) && _world.IsTouching(Player, Vector2.UnitY))
            {
                velocity.Y = JumpSpeed;
            }

            Player.LinearVelocity = velocity;
        }

        // GameHost supplies fixed 120 Hz updates. Collision keeps the circle above y = 0.
        _world.Step(time.DeltaSeconds);
    }

    public override void AdvancePresentation(FrameTime time)
    {
        var target = new Vector2(Player.WorldObject.Transform.Position.X, CameraHeight);
        Camera.Position = Vector2.Lerp(Camera.Position, target, 1f - MathF.Exp(-8f * time.DeltaSeconds));
    }

    public override void Render(Renderer2D renderer)
    {
        renderer.Draw(_ground);
        renderer.DrawWorldLine(Line2D.Horizontal(0f), GroundColor, 3f);

        // Marks make sideways movement visible even along an infinite, flat floor.
        var bounds = Camera.VisibleWorldBounds;
        for (var x = MathF.Floor(bounds.Min.X / 100f) * 100f; x <= bounds.Max.X; x += 100f)
        {
            renderer.DrawWorldSegment(new Vector2(x, 0f), new Vector2(x, -8f), GroundColor);
        }

        base.Render(renderer);
    }

    public override void RenderUI(Renderer2D renderer, FrameTime time)
    {
        renderer.DrawScreenText("TRIALS / PROTOTYPE", new Vector2(24f, 32f), XnaColor.White);
        renderer.DrawScreenText("A/D or arrows: move    Space: jump    R: reset    Esc: exit", new Vector2(24f, 56f), new XnaColor(176, 187, 204));
        base.RenderUI(renderer, time);
    }

    private void ResetPlayer()
    {
        Player.WorldObject.Transform.Position = SpawnPosition;
        Player.WorldObject.Transform.Rotation = 0f;
        Player.LinearVelocity = Vector2.Zero;
        Player.AngularVelocity = 0f;
        Player.ClearAccumulators();
        Camera.Position = new Vector2(SpawnPosition.X, CameraHeight);
    }
}
