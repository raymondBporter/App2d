using App2d.Core;
using App2d.Core.Hosting;
using App2d.Core.Input;
using App2d.Core.Physics;
using App2d.Core.Validation;
using App2d.Rendering;
using System.Numerics;

namespace App2d.Trials;

/// <summary>A small trials playground with a telescoping fork and a rear swingarm/shock.</summary>
public sealed partial class TrialsGame2D : Game2D
{
    private const float CameraHeight = 160f;
    private static readonly Vector2 SpawnPosition = new(0f, 180f);
    private readonly PhysicsWorld2D _world = new()
    {
        Gravity = new Vector2(0f, -980f),
        MaxSubstepSeconds = 1f / 120f,
        PositionIterations = 12,
        VelocityIterations = 12
    };

    public TrialsGame2D() : this(TrialsCourse2D.Default) { }

    public TrialsGame2D(TrialsCourse2D course)
    {
        Course = ArgGuard.RequireNotNull(course);
        AttachPhysicsWorld(_world);
        Camera.ReferenceViewportHeight = 720f;
        CreateBackground();

        Terrain = new TrialsTerrain2D(_world, course);
        Bike = new TrialsBike2D(_world);
        ResetBike();
    }

    public TrialsBike2D Bike { get; }
    public TrialsCourse2D Course { get; }
    public TrialsTerrain2D Terrain { get; }
    public override string WindowTitle => "App2d Trials";

    public override void Initialize() => ResetBike();

    public override void Update(FrameTime time, InputState input)
    {
        if (input.Keyboard.WasPressed(Keys.R))
        {
            ResetBike();
        }
        else
        {
            var left = input.Keyboard.IsDown(Keys.A) || input.Keyboard.IsDown(Keys.Left);
            var right = input.Keyboard.IsDown(Keys.D) || input.Keyboard.IsDown(Keys.Right);
            var direction = (right ? 1f : 0f) - (left ? 1f : 0f);
            var lean = (input.Keyboard.IsDown(Keys.W) ? 1f : 0f) - (input.Keyboard.IsDown(Keys.S) ? 1f : 0f);
            Bike.Drive(direction, lean, input.Keyboard.IsDown(Keys.Space));
        }

        // GameHost supplies fixed 120 Hz updates. Smaller substeps also support larger external updates.
        _world.Step(time.DeltaSeconds);
    }

    public override void AdvancePresentation(FrameTime time)
    {
        var x = Bike.Frame.WorldObject.Transform.Position.X;
        var target = new Vector2(x, Terrain.HeightAt(x) + CameraHeight);
        Camera.Position = Vector2.Lerp(Camera.Position, target, 1f - MathF.Exp(-8f * time.DeltaSeconds));
    }

    public override void Render(Renderer2D renderer)
    {
        Terrain.Render(renderer, Camera.VisibleWorldBounds);
        base.Render(renderer);
        Bike.Render(renderer);
    }

    public override void RenderUI(Renderer2D renderer, FrameTime time)
    {
        renderer.DrawScreenText("TRIALS / PROTOTYPE", new Vector2(24f, 32f), Color4b.White);
        renderer.DrawScreenText("A/D or arrows: drive    W/S: lean    Space: brake    R: reset    Esc: exit", new Vector2(24f, 56f), new Color4b(176, 187, 204));
        base.RenderUI(renderer, time);
    }

    private void ResetBike()
    {
        Bike.Reset(SpawnPosition);
        Camera.Position = new Vector2(SpawnPosition.X, CameraHeight);
    }
}
