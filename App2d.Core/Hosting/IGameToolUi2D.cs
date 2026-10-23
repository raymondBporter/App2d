using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Hosting;

/// <summary>Optional tool UI driven before simulation input is consumed and rendered after the world.</summary>
public interface IGameToolUi2D : IDisposable
{
    void Attach(Control surface);
    void BeginFrame(GraphicsDevice device, FrameTime time);
    bool CaptureMouse { get; }
    bool CaptureKeyboard { get; }
    void Render();
    void ReleaseDevice();
}
