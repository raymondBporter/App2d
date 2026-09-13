using Microsoft.Xna.Framework.Graphics;

namespace App2d;

/// <summary>A Direct3D-backed MonoGame surface inside the existing WinForms editor host.</summary>
internal sealed class GraphicsSurface2D : Control
{
    private GraphicsDevice? _device;
    public event Action<GraphicsDevice, int, int>? RenderFrame;
    public event Action? DeviceDisposing;

    public GraphicsSurface2D()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0 || !Visible ||
            FindForm()?.WindowState == FormWindowState.Minimized) return;
        if (_device?.GraphicsDeviceStatus == GraphicsDeviceStatus.Lost)
            ReleaseDevice();
        if (_device is null)
            _device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, CreateParameters());
        else if (_device.PresentationParameters.BackBufferWidth != ClientSize.Width ||
            _device.PresentationParameters.BackBufferHeight != ClientSize.Height ||
            _device.GraphicsDeviceStatus == GraphicsDeviceStatus.NotReset)
            _device.Reset(CreateParameters());

        RenderFrame?.Invoke(_device, ClientSize.Width, ClientSize.Height);
        _device.Present();
    }

    private PresentationParameters CreateParameters() =>
        new()
        {
            DeviceWindowHandle = Handle,
            BackBufferWidth = ClientSize.Width,
            BackBufferHeight = ClientSize.Height,
            BackBufferFormat = SurfaceFormat.Color,
            DepthStencilFormat = DepthFormat.None,
            IsFullScreen = false,
            PresentationInterval = PresentInterval.One,
            MultiSampleCount = 4
        };

    protected override void OnHandleDestroyed(EventArgs e)
    {
        ReleaseDevice();
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ReleaseDevice();
        base.Dispose(disposing);
    }

    private void ReleaseDevice()
    {
        if (_device is null) return;
        DeviceDisposing?.Invoke();
        _device.Dispose();
        _device = null;
    }
}
