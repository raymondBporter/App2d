using App2d.Core.Rendering;
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
            using var target = RenderCapture2D.CreateTarget(device, 1380, 840, DepthFormat.Depth24, 0);
            for (var frame = 0; frame < 4; frame++)
            {
                using (device.UseRenderTarget(target))
                {
                    host.RenderStudy(device, 1380, 840);
                }
            }
            target.SavePng(Path.Combine(outputDirectory, name + ".png"));
        }
    }
}
