using App2d.Core.Assets;
using App2d.Core.Input;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Core.Hosting;

public abstract class Game2D : IDisposable
{
    private readonly List<Func<IEnumerable<SpatialObject2D>>> _debugAttackShapeProviders = [];
    private float _smoothedFrameSeconds;

    protected Game2D() => DebugPanel.Section("Draw")
        .Toggle("Graphics", () => DrawGraphics, value => DrawGraphics = value, "Draw game graphics. World debug and UI remain visible when disabled.")
        .Toggle("Grid", () => DrawGrid, value => DrawGrid = value, "Overlay the world-space debug grid.")
        .Toggle("FPS", () => DrawFps, value => DrawFps = value, "Show a smoothed FPS and frame-time overlay.")
        .Toggle("Collision shapes", () => DrawCollisionShapes, value => DrawCollisionShapes = value, "Overlay registered physics colliders and active attack hitboxes.");

    public Camera2D Camera { get; } = new();
    public PhysicsWorld2D? PhysicsWorld { get; private set; }
    public Scene2D Scene { get; } = [];
    public TextureCache2D Textures { get; } = new(AssetPaths.Current.Runtime);
    /// <summary>Debug controls shown by the game's tool UI. Engine toggles register here; games add their own.</summary>
    public DebugPanel DebugPanel { get; } = new();
    /// <summary>Draw game graphics. World debug and UI stay visible when off.</summary>
    public bool DrawGraphics { get; set; } = true;
    public bool DrawGrid { get; set; }
    public bool DrawFps { get; set; }
    public bool DrawCollisionShapes { get; set; }
    public virtual string WindowTitle => "App2d";
    protected virtual XnaColor BackgroundColor => new(24, 27, 36);
    public virtual IGameToolUi2D? ToolUi => null;

    public virtual void Initialize()
    {
    }
    public abstract void Update(FrameTime time, InputState input);
    public virtual void AdvancePresentation(FrameTime time)
    {
    }

    /// <summary>Called between BeginFrame and EndFrame. Game graphics, then world debug, then UI.</summary>
    public void RenderFrame(Renderer2D renderer, FrameTime time)
    {
        renderer.Clear(DrawGraphics ? BackgroundColor : new XnaColor(24, 27, 36));
        if (DrawGraphics)
        {
            Render(renderer);
        }

        RenderWorldDebug(renderer);
        RenderUI(renderer, time);
    }

    /// <summary>Game graphics only. The frame is already cleared; debug and UI draw afterward.</summary>
    public virtual void Render(Renderer2D renderer) => renderer.Draw(Scene);

    public virtual void Dispose()
    {
        Textures.Dispose();
        GC.SuppressFinalize(this);
    }

    protected void AttachPhysicsWorld(PhysicsWorld2D physicsWorld)
    {
        ArgGuard.ThrowIfNull(physicsWorld);
        if (PhysicsWorld is not null && !ReferenceEquals(PhysicsWorld, physicsWorld))
        {
            throw new InvalidOperationException("A game can attach only one physics world.");
        }

        PhysicsWorld = physicsWorld;
    }

    protected void RegisterDebugAttackShapes(Func<IEnumerable<SpatialObject2D>> provider)
    {
        ArgGuard.ThrowIfNull(provider);
        if (!_debugAttackShapeProviders.Contains(provider))
        {
            _debugAttackShapeProviders.Add(provider);
        }
    }

    /// <summary>World-space diagnostics over the game. Call base to include the grid and collision toggles.</summary>
    public virtual void RenderWorldDebug(Renderer2D renderer)
    {
        if (DrawGrid)
        {
            renderer.DrawGrid();
        }

        if (DrawCollisionShapes)
        {
            var fillColor = new XnaColor(70, 245, 190, 55);
            var outlineColor = new XnaColor(70, 245, 190, 235);
            if (PhysicsWorld is { } physicsWorld)
            {
                foreach (var body in physicsWorld.Bodies)
                {
                    if (body.IsCollider)
                    {
                        renderer.DrawShapeOverlay(body.WorldObject, fillColor, outlineColor, 2f);
                    }
                }
            }

            var attackFillColor = new XnaColor(255, 82, 92, 70);
            var attackOutlineColor = new XnaColor(255, 105, 70, 245);
            foreach (var provider in _debugAttackShapeProviders)
            {
                foreach (var attackShape in provider())
                {
                    renderer.DrawShapeOverlay(attackShape, attackFillColor, attackOutlineColor, 2f);
                }
            }
        }
    }

    /// <summary>HUD, menus, and screen-space diagnostic labels. Call base to include the FPS display.</summary>
    public virtual void RenderUI(Renderer2D renderer, FrameTime time)
    {
        if (!DrawFps)
        {
            return;
        }

        if (time.DeltaSeconds > 0f)
        {
            _smoothedFrameSeconds = _smoothedFrameSeconds <= 0f
                ? time.DeltaSeconds
                : float.Lerp(_smoothedFrameSeconds, time.DeltaSeconds, 0.1f);
        }

        if (_smoothedFrameSeconds <= 0f)
        {
            return;
        }

        var text = $"{1f / _smoothedFrameSeconds:0.0} FPS   {_smoothedFrameSeconds * 1000f:0.0} ms";
        renderer.DrawScreenLabel(
            text,
            new Vector2(Math.Max(24f, Camera.ViewportSize.X - 265f), 24f));
    }
}
