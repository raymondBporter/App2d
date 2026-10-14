using Microsoft.Xna.Framework.Graphics;

namespace App2d.Noodle;

internal static class EditorUiSmoke
{
    public static void Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var window = new Form { ClientSize = new Size(1380, 840) };
        using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters
            {
                DeviceWindowHandle = window.Handle,
                BackBufferWidth = 1380,
                BackBufferHeight = 840,
                DepthStencilFormat = DepthFormat.Depth24,
                PresentationInterval = PresentInterval.Immediate
            });
        using var bones = new BoneEditorHost();
        using var prototype = new NoodleEditorHost();
        using var rigid = new RigidCharacterHost();
        Render(bones, "noodle-bones");
        bones.ShowShapeStudy();
        Render(bones, "noodle-shape");
        Render(prototype, "noodle-ik");
        Render(rigid, "noodle-rigid-play");
        rigid.ShowAuthoringStudy();
        Render(rigid, "noodle-rigid-pose");
        Console.WriteLine("All Noodle editor views rendered with the shared UI host; no documents were saved.");

        void Render(NoodleToolHost host, string name)
        {
            using var target = new RenderTarget2D(device, 1380, 840, false, SurfaceFormat.Color, DepthFormat.Depth24);
            for (var frame = 0; frame < 4; frame++)
            {
                device.SetRenderTarget(target);
                host.RenderStudy(device, 1380, 840);
                device.SetRenderTarget(null);
            }
            using var stream = File.Create(Path.Combine(outputDirectory, name + ".png"));
            target.SaveAsPng(stream, target.Width, target.Height);
        }
    }
}
