using App2d.Core;
using App2d.Core.Input;
using App2d.Editor;
using App2d.Levels;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Diagnostics;

/// <summary>Exercises the real map views against a disposable level copy, without writing authored content.</summary>
internal static class MapEditorUiSmoke2D
{
    public static void Run(GraphicsDevice device, SideScrollerGame game, string outputDirectory)
    {
        var scratchPath = Path.Combine(outputDirectory, "editor-smoke-level.db");
        File.Copy(LevelBootstrap2D.CavernLevelPath, scratchPath, overwrite: true);
        using var database = LevelDatabase2D.OpenRead(scratchPath);
        var map = database.Load(LevelBootstrap2D.TerrainTilesetIds);
        using var editor = new TileEditor2D(map, () => LevelDatabase2D.Open(scratchPath), game.Camera);
        using var surface = new Control();
        using var ui = new MapEditorUi2D(editor, game.Textures);
        ui.Attach(surface);
        var input = new InputState();
        editor.RequestToggle();
        editor.Update(input);
        using var renderer = new Renderer2D(game.Camera, device);
        var width = device.PresentationParameters.BackBufferWidth;
        var height = device.PresentationParameters.BackBufferHeight;
        using var target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24);
        Draw("map-tiles");
        editor.SelectMode(LevelEditorMode2D.Things);
        if (editor.ThingDefinitions.Count > 0)
        {
            editor.SelectDefinition(editor.ThingDefinitions[0].DefinitionId);
        }

        editor.InspectorView.RefreshFromEditor();
        Draw("map-things");
        editor.InspectorView.BeginNewDefinition();
        Draw("map-new-platform");
        Console.WriteLine("Map palette and thing inspector rendered using a scratch level database.");

        void Draw(string name)
        {
            for (var frame = 0; frame < 4; frame++)
            {
                var time = new FrameTime(1f / 60, frame / 60d, frame);
                ui.BeginFrame(device, time);
                input.SetToolCapture(ui.CaptureKeyboard, ui.CaptureMouse);
                editor.Update(input);
                input.EndFrame();
                device.SetRenderTarget(target);
                renderer.BeginFrame(width, height, time);
                game.RenderFrame(renderer, time);
                TileEditorView2D.DrawWorldDebug(renderer, editor);
                renderer.EndFrame();
                ui.Render();
                device.SetRenderTarget(null);
            }
            using var stream = File.Create(Path.Combine(outputDirectory, name + ".png"));
            target.SaveAsPng(stream, width, height);
        }
    }
}
