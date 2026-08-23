using System.Numerics;
using App2d.Engine.Diagnostics;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using SkiaSharp;

namespace App2d.Engine;

public abstract class Game2D : IDisposable
{
    private readonly List<PhysicsWorld2D> _debugPhysicsWorlds = [];
    private readonly List<Func<IEnumerable<WorldObject2D>>> _debugAttackShapeProviders = [];
    private bool _drawGraphics = true;
    private bool _drawFps;
    private bool _drawCollisionShapes;
    private float _smoothedFrameSeconds;

    protected Game2D()
    {
        DeveloperConsole.RegisterVariable(
            "draw_graphics",
            () => _drawGraphics,
            value => _drawGraphics = value,
            "Draw the game's graphics. Disable for a collision-geometry-only view.");
        DeveloperConsole.RegisterVariable(
            "draw_fps",
            () => _drawFps,
            value => _drawFps = value,
            "Show a smoothed FPS and frame-time overlay.");
        DeveloperConsole.RegisterVariable(
            "draw_collision_shapes",
            () => _drawCollisionShapes,
            value => _drawCollisionShapes = value,
            "Overlay registered physics colliders and active attack hitboxes.");
    }

    public Camera2D Camera { get; } = new();
    public Scene2D Scene { get; } = [];
    public TextureCache2D Textures { get; } = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures"));
    public DeveloperConsole DeveloperConsole { get; } = new();
    public virtual string WindowTitle => "App2d";
    internal bool DrawGraphics => _drawGraphics;

    public virtual void Initialize() { }
    public abstract void Update(FrameTime time, InputState input);

    public virtual void Render(Renderer2D renderer)
    {
        renderer.Clear(new SKColor(24, 27, 36));
        renderer.DrawGrid();
        renderer.Draw(Scene);
    }

    public virtual void Dispose()
    {
        Textures.Dispose();
    }

    protected void RegisterDebugPhysicsWorld(PhysicsWorld2D physicsWorld)
    {
        ArgumentNullException.ThrowIfNull(physicsWorld);
        if (!_debugPhysicsWorlds.Contains(physicsWorld))
            _debugPhysicsWorlds.Add(physicsWorld);
    }

    protected void RegisterDebugAttackShapes(Func<IEnumerable<WorldObject2D>> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!_debugAttackShapeProviders.Contains(provider))
            _debugAttackShapeProviders.Add(provider);
    }

    internal void RenderDiagnostics(Renderer2D renderer, FrameTime time)
    {
        if (_drawCollisionShapes)
        {
            var fillColor = new SKColor(70, 245, 190, 55);
            var outlineColor = new SKColor(70, 245, 190, 235);
            foreach (var physicsWorld in _debugPhysicsWorlds)
            {
                foreach (var body in physicsWorld.Bodies)
                {
                    if (body.IsCollider)
                        renderer.DrawShapeOverlay(body.WorldObject, fillColor, outlineColor, 2f);
                }
            }

            var attackFillColor = new SKColor(255, 82, 92, 70);
            var attackOutlineColor = new SKColor(255, 105, 70, 245);
            foreach (var provider in _debugAttackShapeProviders)
            {
                foreach (var attackShape in provider())
                    renderer.DrawShapeOverlay(attackShape, attackFillColor, attackOutlineColor, 2f);
            }
        }

        if (!_drawFps)
            return;

        if (time.DeltaSeconds > 0f)
        {
            _smoothedFrameSeconds = _smoothedFrameSeconds <= 0f
                ? time.DeltaSeconds
                : float.Lerp(_smoothedFrameSeconds, time.DeltaSeconds, 0.1f);
        }

        if (_smoothedFrameSeconds <= 0f)
            return;

        var text = $"{1f / _smoothedFrameSeconds:0.0} FPS   {_smoothedFrameSeconds * 1000f:0.0} ms";
        renderer.DrawScreenLabel(
            text,
            new Vector2(Math.Max(24f, Camera.ViewportSize.X - 265f), 24f));
    }
}
