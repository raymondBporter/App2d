using App2d.EditorUi;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Demos;

internal interface IDemo
{
    string Name { get; }
    void Draw();
    void Update(float seconds) { }
    void Deactivate() { }
}

internal sealed class DemoGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly IDemo[] _demos = [new ConicDemo(), new HexWheelDemo()];
    private ImGuiHost? _gui;
    private int _selected = 1;

    internal DemoGame()
    {
        _graphics = new(this)
        {
            PreferredBackBufferWidth = 1200,
            PreferredBackBufferHeight = 780,
            SynchronizeWithVerticalRetrace = true,
            GraphicsProfile = GraphicsProfile.HiDef,
        };
        Window.Title = "App2d Demos";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        IsFixedTimeStep = false;
    }

    protected override void LoadContent()
    {
        var surface = Control.FromHandle(Window.Handle);
        // Queue window input so a short click can survive an expensive wheel update.
        IImGuiPlatform input = surface is null ? new MonoGameImGuiPlatform(this) : new WinFormsImGuiPlatform(surface);
        _gui = new(GraphicsDevice, input);
        if (surface?.FindForm() is { } form)
        {
            form.MinimumSize = new(690, 530);
        }
    }

    protected override void UnloadContent()
    {
        _gui?.Dispose();
        _gui = null;
        base.UnloadContent();
    }

    protected override void Update(GameTime time)
    {
        if (IsActive)
        {
            _demos[_selected].Update((float)time.ElapsedGameTime.TotalSeconds);
        }
        base.Update(time);
    }

    protected override void Draw(GameTime time)
    {
        if (_gui is null)
            return;

        GraphicsDevice.Clear(new Color4b(17, 22, 30));
        _gui.Begin((float)time.ElapsedGameTime.TotalSeconds);
        Ui.Root("App2d Demos", DrawRoot);
        _gui.Render();
        base.Draw(time);
    }

    private void DrawRoot()
    {
        for (var i = 0; i < _demos.Length; i++)
        {
            if (ImGui.RadioButton(_demos[i].Name, i == _selected))
            {
                Select(i);
            }
            ImGui.SameLine();
        }
        if (ImGui.Button("Next demo"))
        {
            Select((_selected + 1) % _demos.Length);
        }
        ImGui.Separator();
        _demos[_selected].Draw();
    }

    private void Select(int index)
    {
        if (index == _selected)
            return;

        _demos[_selected].Deactivate();
        _selected = index;
    }
}
