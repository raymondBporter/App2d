using App2d.Beamwright;
using App2d.Core;
using App2d.Core.Input;
using System.Numerics;

namespace App2d.Tests.Beamwright;

public sealed class BenchSessionTests
{
    [Fact]
    public void PlacementMoveRotationAndUndoPreserveInventoryAndOccupancy()
    {
        var session = new BenchSession();
        Assert.False(session.TryPlace("mirror", 0, 3)); // Source.
        Assert.False(session.TryPlace("mirror", 4, 0)); // Photocell.
        Assert.False(session.TryPlace("mirror", 6, 2)); // Wall.
        Assert.False(session.TryPlace("mirror", -1, 0));
        Assert.True(session.TryPlace("mirror", 4, 3));
        Assert.True(session.Result.Win);
        Assert.Equal(0, session.Available("mirror"));
        Assert.False(session.TryPlace("mirror", 2, 3));
        session.Rotate(0, 15);
        Assert.False(session.Result.Win);
        session.Undo();
        Assert.True(session.Result.Win);
        Assert.False(session.TryMove(0, 0, 3));
        Assert.True(session.TryMove(0, 3, 3));
        Assert.False(session.Result.Win);
        session.Undo();
        Assert.True(session.Result.Win);
        session.Remove(0);
        Assert.Equal(1, session.Available("mirror"));
        Assert.False(session.Result.Win);
        session.Undo();
        Assert.True(session.Result.Win);
    }

    [Fact]
    public void SandboxIsUnlimitedAndHintsDoNotPlaceParts()
    {
        var session = new BenchSession();
        session.RevealHint();
        session.RevealHint();
        Assert.Equal(1, session.HintCount);
        Assert.Empty(session.Placed);
        session.LoadLevel(-1);
        Assert.Equal(0, session.HintCount);
        Assert.False(session.CanUndo);
        Assert.Equal(int.MaxValue, session.Available("prism"));
        Assert.True(session.TryPlace("prism", 1, 1));
        Assert.True(session.TryPlace("prism", 2, 1));
        Assert.False(session.CanOccupy(7, 6)); // Hot air.
    }

    [Fact]
    public void ProgressRoundTripsAndCorruptFilesDoNotPreventPlaying()
    {
        var path = Path.Combine(Path.GetTempPath(), $"app2d-beamwright-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "broken json");
            var first = new BenchSession(savePath: path);
            Assert.True(first.TryPlace("mirror", 4, 3));
            first.LoadLevel(7);
            var reloaded = new BenchSession(savePath: path);
            Assert.True(reloaded.Completed(0));
            Assert.Equal(1, reloaded.BestParts(0));
            Assert.Equal(7, reloaded.LevelIndex);
            Assert.False(reloaded.HasSaveError);
            Assert.Empty(reloaded.Placed);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void DesktopInputCanEquipPlaceRotateUndoAndReturnAPart()
    {
        using var game = new BeamwrightGame2D();
        game.Camera.SetViewport(1440, 900);
        var input = new InputState();
        var time = new FrameTime(1f / 120f, 0, 0);
        Click(new(80, 195)); // Equip mirror from rack.
        Click(new(986, 598)); // First Light: E5.
        Assert.True(game.Session.Result.Win);
        input.SetKey(Keys.E, true);
        game.Update(time, input);
        input.EndFrame();
        input.SetKey(Keys.E, false);
        Assert.False(game.Session.Result.Win);
        input.SetKey(Keys.ControlKey, true);
        input.SetKey(Keys.Z, true);
        game.Update(time, input);
        input.EndFrame();
        input.SetKey(Keys.ControlKey, false);
        input.SetKey(Keys.Z, false);
        Assert.True(game.Session.Result.Win);
        Click(new(986, 598), MouseButtons.Right);
        Assert.Empty(game.Session.Placed);
        Assert.Equal(1, game.Session.Available("mirror"));

        void Click(Vector2 position, MouseButtons button = MouseButtons.Left)
        {
            input.Mouse.SetClientPosition(position);
            input.SetMouseButton(button, true);
            game.Update(time, input);
            input.EndFrame();
            input.SetMouseButton(button, false);
            game.Update(time, input);
            input.EndFrame();
        }
    }

    [Fact]
    public void DesktopInputCanDragFromTheRackAndReturnThePartToIt()
    {
        using var game = new BeamwrightGame2D();
        game.Camera.SetViewport(1440, 900);
        var input = new InputState();
        var time = new FrameTime(1f / 120f, 0, 0);
        Drag(new(80, 195), new(986, 598));
        Assert.True(game.Session.Result.Win);
        Drag(new(986, 598), new(80, 195));
        Assert.Empty(game.Session.Placed);
        Assert.Equal(1, game.Session.Available("mirror"));

        void Drag(Vector2 from, Vector2 to)
        {
            input.Mouse.SetClientPosition(from);
            input.SetMouseButton(MouseButtons.Left, true);
            game.Update(time, input);
            input.EndFrame();
            input.Mouse.SetClientPosition(to);
            game.Update(time, input);
            input.EndFrame();
            input.SetMouseButton(MouseButtons.Left, false);
            game.Update(time, input);
            input.EndFrame();
        }
    }
}
