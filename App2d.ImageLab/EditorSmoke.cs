using App2d.Core.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace App2d.ImageLab;

internal static class EditorSmoke
{
    public static void Run(string outputDirectory, string? sourceImage = null)
    {
        Directory.CreateDirectory(outputDirectory);
        var sourcePath = sourceImage ?? Path.Combine(outputDirectory, "sample.png");
        if (sourceImage is null)
        {
            CreateSample(sourcePath);
        }
        Capture(1380, 880, "palette-preview", window =>
        {
            window.LoadImage(sourcePath);
            window.SelectTransform(new(TransformKind.Palette, Colors: 6));
        }, waitForPreview: true);
        Capture(1100, 760, "contour-stack", window =>
        {
            window.LoadImage(sourcePath);
        }, waitForPreview: false, buildStack: true);
        Capture(1100, 760, "empty", _ => { }, waitForPreview: false);
        Capture(1380, 880, "canny-preview", window =>
        {
            window.LoadImage(sourcePath);
            window.SelectTransform(new(TransformKind.Canny, WeakEdge: .006f, StrongEdge: .025f, Smoothing: 2));
        }, waitForPreview: true);
        Capture(1100, 760, "brush-overlay", window =>
        {
            window.LoadImage(sourcePath);
            window.SelectTransform(new(TransformKind.Canny, WeakEdge: .006f, StrongEdge: .025f, Smoothing: 2));
        }, waitForPreview: true, brush: true);
        Capture(1380, 880, "largest-four-hover", window =>
        {
            window.LoadImage(sourcePath);
            window.SelectTransform(new(TransformKind.Canny, WeakEdge: .006f, StrongEdge: .025f, Smoothing: 2));
        }, waitForPreview: true, study: (window, wait, draw) =>
        {
            window.ApplyPreview();
            window.SelectTransform(new(TransformKind.LargestRegion, LargestRegions: 4));
            wait();
            window.ApplyPreview();
            wait();
            Hover(window, draw);
        });
        Capture(1100, 760, "one-pixel-line", window => window.PixelRepairStudy(), waitForPreview: false,
            study: (window, _, draw) =>
            {
                window.LineReleaseStudy(draw);
                Hover(window, draw);
            });
        Capture(1100, 760, "pixel-palette", window =>
        {
            window.LoadImage(sourcePath);
            window.SelectTransform(new(TransformKind.Palette, Colors: 6));
        }, waitForPreview: true, study: (window, _, _) =>
        {
            window.ApplyPreview();
            window.SetCanvasTool(CanvasTool.Paint);
            window.SetCanvasTool(CanvasTool.Eyedropper);
            window.PickInk(0, 0);
        });
        Capture(1100, 760, "arrow-navigation", window => window.LoadImage(sourcePath), waitForPreview: false,
            study: (window, _, draw) => window.ArrowPanStudy(draw));
        Console.WriteLine($"Image Lab rendered at two sizes. Screenshots and raster outputs: {Path.GetFullPath(outputDirectory)}");

        void Hover(ImageLabWindow window, Action draw)
        {
            var pixel = window.RegionInspectionStudy();
            draw();
            draw();
            window.PointAtPixelForStudy(pixel);
        }

        void Capture(int width, int height, string name, Action<ImageLabWindow> configure, bool waitForPreview, bool buildStack = false, bool brush = false,
            Action<ImageLabWindow, Action, Action>? study = null)
        {
            using var owner = new Form { ClientSize = new(width, height) };
            using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
                new PresentationParameters
                {
                    DeviceWindowHandle = owner.Handle,
                    BackBufferWidth = width,
                    BackBufferHeight = height,
                    DepthStencilFormat = DepthFormat.None,
                    PresentationInterval = PresentInterval.Immediate
                });
            using var target = RenderCapture2D.CreateTarget(device, width, height, DepthFormat.None, 0);
            using var window = new ImageLabWindow();
            configure(window);
            if (buildStack)
            {
                foreach (var transform in new RasterTransform[]
                {
                    new(TransformKind.SingleChannel, ChannelSource.Alpha),
                    new(TransformKind.Threshold, Threshold: .5f),
                    new(TransformKind.Contours)
                })
                {
                    window.SelectTransform(transform);
                    WaitForPreview();
                    window.ApplyPreview();
                }
                WaitForPreview();
            }
            else if (waitForPreview)
            {
                WaitForPreview();
            }
            if (brush)
            {
                window.ApplyPreview();
                window.SetCanvasTool(CanvasTool.Erase);
                window.BrushStudy();
            }
            study?.Invoke(window, WaitForPreview, Draw);
            for (var i = 0; i < 4; i++)
            {
                Draw();
            }
            target.SavePng(Path.Combine(outputDirectory, name + ".png"));

            void Draw()
            {
                using (device.UseRenderTarget(target))
                {
                    window.RenderStudy(device);
                }
            }
            void WaitForPreview()
            {
                var clock = Stopwatch.StartNew();
                while (!window.PreviewReady)
                {
                    Draw();
                    if (clock.Elapsed.TotalSeconds > 15)
                    {
                        throw new TimeoutException("Image Lab preview did not become applicable.");
                    }
                    Thread.Sleep(20);
                }
            }
        }
    }

    private static void CreateSample(string path)
    {
        using var bitmap = new Bitmap(360, 420, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var outline = new Pen(Color.FromArgb(30, 37, 53), 8);
        using var body = new SolidBrush(Color.FromArgb(63, 173, 174));
        using var face = new SolidBrush(Color.FromArgb(247, 185, 107));
        using var dark = new SolidBrush(Color.FromArgb(30, 37, 53));
        using var blush = new SolidBrush(Color.FromArgb(226, 112, 102));
        graphics.FillRectangle(body, 105, 200, 150, 140);
        graphics.DrawRectangle(outline, 105, 200, 150, 140);
        graphics.FillEllipse(face, 79, 42, 202, 190);
        graphics.DrawEllipse(outline, 79, 42, 202, 190);
        graphics.FillEllipse(dark, 125, 103, 18, 26);
        graphics.FillEllipse(dark, 217, 103, 18, 26);
        graphics.FillEllipse(blush, 108, 142, 36, 18);
        graphics.FillEllipse(blush, 216, 142, 36, 18);
        graphics.DrawArc(outline, 145, 130, 70, 50, 15, 150);
        graphics.DrawLine(outline, 105, 224, 55, 290);
        graphics.DrawLine(outline, 255, 224, 305, 290);
        graphics.DrawLine(outline, 140, 342, 123, 392);
        graphics.DrawLine(outline, 220, 342, 237, 392);
        // A real transparent hole and a disconnected island exercise contour and region behavior.
        graphics.CompositingMode = CompositingMode.SourceCopy;
        using var transparent = new SolidBrush(Color.Transparent);
        graphics.FillEllipse(transparent, 158, 254, 44, 44);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.FillEllipse(blush, 298, 60, 16, 16);
        bitmap.Save(path, ImageFormat.Png);
    }
}
