using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Noodle.Rigging;
using App2d.Rendering;
using System.Numerics;

namespace App2d.Noodle;

internal sealed class BoneEditorHost : NoodleToolHost
{
    private readonly RigDocument2D _document = RigDocument2D.CreateStarterPerson();
    private readonly BoneEditorPanel _panel;
    private bool _panning;

    public BoneEditorHost() : base("App2d Entity Editor — Bones", new Size(1380, 840),
        new Camera2D { Position = new Vector2(0, -55), Zoom = 1.05f }) => _panel = new(_document);

    protected override void DrawControls() => _panel.Draw(() => AfterFrame(ExportModel));
    internal void ShowShapeStudy() => _panel.Select(_document.Shapes.FirstOrDefault());
    protected override void DrawScene(Renderer2D renderer, FrameTime time)
    {
        renderer.Clear(new Color4b(17, 22, 33));
        renderer.DrawGrid(50, 5);
        RigDocumentRenderer2D.Render(renderer, _document, _panel.SelectedItem);
    }
    protected override void HandleSceneInput(bool mouseCaptured, bool keyboardCaptured)
    {
        if (ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S, false))
        {
            AfterFrame(ExportModel);
        }

        if (!keyboardCaptured)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                Close();
            }
        }
        if (mouseCaptured || !ImGui.IsMouseDown(ImGuiMouseButton.Middle))
        {
            _panning = false;
        }

        if (mouseCaptured)
        {
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Middle))
        {
            _panning = true;
        }

        if (_panning)
        {
            var delta = ImGui.GetIO().MouseDelta;
            Camera.Position -= new Vector2(delta.X, -delta.Y) / Camera.Zoom;
        }
        ZoomAtPointer();
    }
    private void ExportModel()
    {
        if (!_panel.Commit())
        {
            return;
        }

        using var dialog = new SaveFileDialog { Filter = "App2d model (*.model.json)|*.model.json|JSON (*.json)|*.json", FileName = "noodle-rig.model.json" };
        if (dialog.ShowDialog(Window) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var fileName = Path.GetFileName(dialog.FileName);
            var name = fileName.EndsWith(".model.json", StringComparison.OrdinalIgnoreCase) ? fileName[..^".model.json".Length] : Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Noodle rig";
            }

            CharacterModel model = RigAuthoredBridge2D.Export(_document, PuppetImport.Slug(name, "noodle-rig"), name);
            model.Save(dialog.FileName);
            MessageBox.Show(Window, $"Saved authored model to {dialog.FileName}", "Model exported");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            MessageBox.Show(Window, ex.Message, "Model export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
