using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.CharacterStudio.Editor;

/// <summary>Renders the real editor on a hidden WinForms surface without constructing a MonoGame Game.</summary>
internal static class EditorSurfaceSmoke
{
    public static void Run(string authoredRoot, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var window = new Form { ClientSize = new Size(1600, 940) };
        using var surface = new Control { Dock = DockStyle.Fill };
        window.Controls.Add(surface);
        using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters
            {
                DeviceWindowHandle = surface.Handle,
                BackBufferWidth = 1600,
                BackBufferHeight = 940,
                DepthStencilFormat = DepthFormat.Depth24,
                PresentationInterval = PresentInterval.Immediate
            });
        // A second live host also exercises context selection while fonts and styles are initialized.
        using var otherSurface = new Control();
        using var otherGui = new ImGuiHost(device, new WinFormsImGuiPlatform(otherSurface));
        using var gui = new ImGuiHost(device, new WinFormsImGuiPlatform(surface));
        using var renderer = new PointCharacterRenderer(device);
        var assets = AuthoringWorkspace.Open(authoredRoot);
        var sources = SourceLibraries.Open(Path.GetDirectoryName(Path.GetFullPath(authoredRoot))!);
        var session = new EditorSession(assets, sources);
        using var shell = new EditorShell(session, new Viewport(device, gui, renderer), new ArenaTest(assets, device, gui, renderer), gui);
        session.Open("person-walk"); session.Pin("tall-thin"); session.Pin("short-broad"); session.Seek(.3f);
        if (session.ActiveDocument is null) throw new InvalidOperationException(session.Message);

        foreach (var (width, height, scale) in new[] { (1600, 940, 1f), (1200, 800, 1.25f) })
        {
            window.ClientSize = new Size(width, height);
            device.Reset(new PresentationParameters
            {
                DeviceWindowHandle = surface.Handle,
                BackBufferWidth = width,
                BackBufferHeight = height,
                DepthStencilFormat = DepthFormat.Depth24,
                PresentationInterval = PresentInterval.Immediate
            });
            gui.UserScale = scale;
            using var target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            for (var frame = 0; frame < 4; frame++)
            {
                gui.Begin(1f / 60);
                shell.Draw();
                device.SetRenderTarget(target);
                device.Clear(new Color(19, 23, 29)); gui.Render();
                device.SetRenderTarget(null);
            }
            using var file = File.Create(Path.Combine(outputDirectory, $"character-editor-surface-{width}x{height}.png"));
            target.SaveAsPng(file, width, height);
        }
        Console.WriteLine("The character editor rendered on a WinForms surface at two sizes and scales without MonoGame.Game.");
    }
}
