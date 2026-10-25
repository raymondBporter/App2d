using App2d.Core.Assets;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterSeeds.Diagnostics;

/// <summary>Every face expression the baked face part binds, on the authored Person's head instance, through the game's drawing path, labelled.</summary>
internal static class FaceRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, string directory)
    {
        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        if (catalog.Errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, catalog.Errors));
        }

        var person = catalog.Resolve(PersonTemplate.Id);
        var drawing = new PuppetDrawing();
        using var renderer = new PointCharacterRenderer(device);
        using var labels = new Renderer2D(new Camera2D(), device);
        RenderCapture2D.SavePng(device, 1280, 1450, Path.Combine(directory, "faces-expressions.png"), new Color4b(230, 234, 227), () =>
        {
            labels.BeginFrame(1280, 1450, default);
            for (var i = 0; i < ProceduralFace.Names.Count; i++)
            {
                var pose = PoseEvaluator.Rest(person, new(ProceduralFace.Names[i]) { Clock = 1 });
                drawing.Build(person, pose);
                var head = pose.World("head");
                device.Viewport = new Viewport(i % 4 * 320, i / 4 * 290, 320, 225);
                var projection = PointCharacterRenderer.Projection(320, 225, new(160, 112), 260);
                renderer.DrawActor(drawing, projection, Matrix.CreateTranslation(-head.X, -head.Y, 0));
            }
            device.Viewport = new Viewport(0, 0, 1280, 1450);
            for (var i = 0; i < ProceduralFace.Names.Count; i++)
            {
                labels.DrawScreenLabel(ProceduralFace.Names[i].ToUpperInvariant(), new(i % 4 * 320 + 65, i / 4 * 290 + 232));
            }

            labels.EndFrame();
        });
    }
}
