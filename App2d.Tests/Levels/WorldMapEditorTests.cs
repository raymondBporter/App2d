using App2d.Editor;
using App2d.Levels;
using App2d.Core.Rendering;
using App2d.Core.Input;
using App2d.Core.Tiles;
using Microsoft.Data.Sqlite;
using System.Numerics;

namespace App2d.Tests.Levels;

public sealed class WorldMapEditorTests
{
    [Fact]
    public void SwitchingCommitsToOldFileAndUndoRemainsLocal()
    {
        var root = Path.Combine(Path.GetTempPath(), "app2d-map-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            WorldMapAsset2D Create(string id, int identity, float x)
            {
                var path = Path.Combine(root, id + ".db");
                using (var db = LevelDatabase2D.Open(path)) db.Save(new(16, 16, 32, 4, new(x, 0)), 0);
                return WorldFile2D.LoadMap(path, id, id, "zone", identity);
            }
            var left = Create("left", 0, 0);
            var right = Create("right", 1, 512);
            var camera = new Camera2D { Position = new(128, 128) };
            using var editor = new TileEditor2D(left.TileMap, () => LevelDatabase2D.Open(left.DatabasePath), camera);
            editor.ConfigureMaps([left, right]);
            var input = new InputState();
            input.Mouse.SetClientPosition(new(400, 300));
            input.SetKey(Keys.F1, true);
            editor.Update(input);
            input.EndFrame();
            input.SetKey(Keys.F1, false);
            input.SetMouseButton(MouseButtons.Left, true);
            editor.Update(input);
            Assert.True(left.TileMap.IsSolid(4, 4));
            editor.CycleMap(1); // Still holding the mouse: commit the old map before changing files.
            Assert.Equal(1, editor.SelectedMapIndex);
            Assert.Equal(right.TileMap.WorldBounds, editor.MapBounds);
            using (var db = LevelDatabase2D.OpenRead(left.DatabasePath)) Assert.True(db.Load().IsSolid(4, 4));
            input.SetMouseButton(MouseButtons.Left, false);
            input.CancelButtons();
            input.EndFrame();
            input.SetMouseButton(MouseButtons.Left, true);
            editor.Update(input);
            Assert.True(right.TileMap.IsSolid(8, 8));
            editor.CycleMap(-1);
            input.SetMouseButton(MouseButtons.Left, false);
            input.CancelButtons();
            input.EndFrame();
            input.SetKey(Keys.ControlKey, true);
            input.SetKey(Keys.Z, true);
            editor.Update(input);
            Assert.False(left.TileMap.IsSolid(4, 4));
            Assert.True(right.TileMap.IsSolid(8, 8));
            using (var db = LevelDatabase2D.OpenRead(left.DatabasePath)) Assert.False(db.Load().IsSolid(4, 4));
            using (var db = LevelDatabase2D.OpenRead(right.DatabasePath)) Assert.True(db.Load().IsSolid(8, 8));
        }
        finally
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.db"))
                foreach (var mode in new[] { SqliteOpenMode.ReadOnly, SqliteOpenMode.ReadWriteCreate })
                {
                    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode }.ToString());
                    SqliteConnection.ClearPool(connection);
                }
            Directory.Delete(root, true);
        }
    }
}
