using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using SkiaSharp;

namespace App2d.Engine;

public abstract class Game2D : IDisposable
{
    public Camera2D Camera { get; } = new();
    public Scene2D Scene { get; } = [];
    public TextureCache2D Textures { get; } = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures"));
    public virtual string WindowTitle => "App2d";

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
}
