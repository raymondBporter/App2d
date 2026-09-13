using Microsoft.Xna.Framework.Graphics;

namespace App2d.Tests.Rendering;

[CollectionDefinition("Graphics", DisableParallelization = true)]
public sealed class GraphicsCollection { }

internal sealed class GraphicsTestContext : IDisposable
{
    private readonly System.Windows.Forms.Form _window = new() { ClientSize = new System.Drawing.Size(128, 128) };
    public GraphicsDevice Device { get; }
    public RenderTarget2D Target { get; }

    public GraphicsTestContext()
    {
        Device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, new PresentationParameters
        {
            DeviceWindowHandle = _window.Handle,
            BackBufferWidth = 128,
            BackBufferHeight = 128,
            DepthStencilFormat = DepthFormat.None,
            IsFullScreen = false,
            PresentationInterval = PresentInterval.Immediate
        });
        Target = new RenderTarget2D(Device, 128, 128, false, SurfaceFormat.Color, DepthFormat.None);
        Device.SetRenderTarget(Target);
    }

    public Microsoft.Xna.Framework.Color[] ReadPixels()
    {
        Device.SetRenderTarget(null);
        var pixels = new Microsoft.Xna.Framework.Color[128 * 128];
        Target.GetData(pixels);
        Device.SetRenderTarget(Target);
        return pixels;
    }

    public void Dispose()
    {
        Device.SetRenderTarget(null);
        Target.Dispose();
        Device.Dispose();
        _window.Dispose();
    }
}
