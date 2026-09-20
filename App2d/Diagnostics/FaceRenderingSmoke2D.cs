using App2d.Core.Characters;
using App2d.Rendering;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.Diagnostics;

internal static class FaceRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, string directory)
    {
        var library = PointLibrary.Load(Path.Combine(AssetPaths.Characters, "person", "library.json"));
        var geometry = new CharacterGeometry(library); var pose = new PersonPose(library);
        var clip = library.Clips["idle"]; var raw = new Vector3[library.PointNames.Count]; clip.Sample(0, raw);
        using var drawing = new PointCharacterRenderer(device);
        using var labels = new Renderer2D(new Camera2D(), device);
        using var target = new RenderTarget2D(device, 1280, 1160, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        foreach (var custom in new[] { false, true })
        {
            device.SetRenderTarget(target); labels.BeginFrame(1280, 1160, default); labels.Clear(new Color(230, 234, 227));
            for (var i = 0; i < FaceExpressions.Names.Count; i++)
            {
                var look = new CharacterAppearance { Face = FaceExpressions.Names[i], Weapons = false,
                    CustomHead = custom ? new HeadShape { Muzzle = .2f, Face = FaceExpressions.Names[i] } : null };
                pose.Transform(raw, look); var center = pose.Point(0);
                geometry.Build(clip, 0, look, new(false, false, false));
                device.Viewport = new Viewport(i % 4 * 320, i / 4 * 290, 320, 225);
                var projection = PointCharacterRenderer.Projection(320, 225, new(160, 108), 88 / pose.Radius(look));
                drawing.Draw(geometry.Body, projection, Matrix.CreateTranslation(-center.X, -center.Y, 0));
            }
            device.Viewport = new Viewport(0, 0, 1280, 1160);
            for (var i = 0; i < FaceExpressions.Names.Count; i++) labels.DrawScreenLabel(FaceExpressions.Names[i].ToUpperInvariant(), new(i % 4 * 320 + 65, i / 4 * 290 + 232));
            labels.EndFrame(); device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(directory, custom ? "faces-custom-head.png" : "faces-expressions.png"));
            target.SaveAsPng(stream, target.Width, target.Height);
        }
    }
}
