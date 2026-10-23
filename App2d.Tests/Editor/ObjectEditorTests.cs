using App2d.Core.Input;
using App2d.Core.Objects;
using App2d.Core.Rendering;
using App2d.Editor;
using App2d.Levels;
using App2d.Things;
using Microsoft.Data.Sqlite;
using System.Numerics;

namespace App2d.Tests.Editor;

public sealed class ObjectEditorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-object-editor-" + Guid.NewGuid().ToString("N"));

    public ObjectEditorTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "objects"));
        File.Copy(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json"), Path.Combine(_root, "objects", "lift.json"));
        using var database = LevelDatabase2D.Open(Path.Combine(_root, "map.db"));
        database.Save(new(16, 16, 32, 4, new(0, 0)), 0);
    }

    [Fact]
    public void ObjectDefinitionPlacesDragsUndoesAndNotifiesTheRuntime()
    {
        var map = WorldFile2D.LoadMap(Path.Combine(_root, "map.db"), "map", "Map", "zone", 3, worldRoot: _root);
        var camera = new Camera2D { Position = new(128, 128) };
        using var editor = new TileEditor2D(map.TileMap, () => LevelDatabase2D.Open(map.DatabasePath), camera);
        editor.ConfigureMaps([map], _root);
        var notified = new List<IReadOnlyList<ObjectPlacement2D>>();
        editor.ObjectsChanged += objects => notified.Add(objects);
        var input = new InputState();
        input.Mouse.SetClientPosition(new(400, 300));
        input.SetKey(Keys.F1, true);
        editor.Update(input);
        input.EndFrame();
        input.SetKey(Keys.F1, false);
        editor.SelectMode(LevelEditorMode2D.Things);
        Assert.Equal(["objects/lift.json"], editor.ObjectAssets.Paths);
        // Entering the editor already reloads things and notifies the runtime; count from there.
        var baseline = notified.Count;

        editor.ApplyPositionDefinition(null, ThingTypeRegistry2D.Object.TypeKey, new() { Name = "Lift" }, "objects/lift.json");
        var definition = Assert.Single(editor.ThingDefinitions);
        Assert.Equal("objects/lift.json", definition.Asset);
        Assert.Equal(baseline + 1, notified.Count);

        editor.BeginThingPlacement();
        input.SetMouseButton(MouseButtons.Left, true);
        editor.Update(input);
        input.SetMouseButton(MouseButtons.Left, false);
        input.EndFrame();
        var placed = Assert.Single(editor.ObjectPlacements);
        var placedAt = editor.GridGeometry.SnapToNearestCorner(camera.DeviceToWorld(new(400, 300)));
        Assert.Equal(map.WorldThingId(1).ToString(), placed.Id);
        Assert.Equal(placedAt, placed.Position);
        Assert.Same(editor.ObjectAssets.Load("objects/lift.json"), placed.Asset);
        Assert.Equal(baseline + 2, notified.Count);

        // Press on the lift, drag it, release: the move is persisted and undoable.
        input.SetMouseButton(MouseButtons.Left, true);
        editor.Update(input);
        input.EndFrame();
        input.Mouse.SetClientPosition(new(432, 300));
        editor.Update(input);
        input.EndFrame();
        input.SetMouseButton(MouseButtons.Left, false);
        editor.Update(input);
        input.EndFrame();
        var movedTo = editor.GridGeometry.SnapToNearestCorner(camera.DeviceToWorld(new(432, 300)));
        Assert.NotEqual(placedAt, movedTo);
        Assert.Equal(movedTo, Assert.Single(editor.ObjectPlacements).Position);
        Assert.Equal(baseline + 3, notified.Count);
        using (var database = LevelDatabase2D.OpenRead(map.DatabasePath))
        {
            var stored = Assert.Single(database.LoadPositionThings());
            Assert.Equal(movedTo, new Vector2(stored.X, stored.Y));
        }

        editor.UndoThingEdit();
        Assert.Equal(placedAt, Assert.Single(editor.ObjectPlacements).Position);
        editor.UndoThingEdit();
        Assert.Empty(editor.ObjectPlacements);
        Assert.Equal(baseline + 5, notified.Count);
        Assert.Empty(notified[^1]);
    }

    [Fact]
    public void ADisabledObjectThingStaysInTheEditorButLeavesTheRuntime()
    {
        using var editor = CreateEditor(out _, out var input, out var notified);
        editor.ApplyPositionDefinition(null, ThingTypeRegistry2D.Object.TypeKey, new() { Name = "Lift" }, "objects/lift.json");
        editor.BeginThingPlacement();
        input.SetMouseButton(MouseButtons.Left, true);
        editor.Update(input);
        input.SetMouseButton(MouseButtons.Left, false);
        input.EndFrame();
        var placed = Assert.Single(editor.PositionThings);
        Assert.Single(editor.ObjectPlacements);

        editor.ApplyPositionThing(placed.ThingId, new() { Name = "parked", Enabled = false, PositionX = placed.X, PositionY = placed.Y });
        Assert.False(Assert.Single(editor.PositionThings).Enabled);
        Assert.Empty(editor.ObjectPlacements);
        Assert.Empty(notified[^1]);

        editor.ApplyPositionThing(placed.ThingId, new() { Name = "parked", Enabled = true, PositionX = placed.X, PositionY = placed.Y });
        Assert.Single(editor.ObjectPlacements);
        Assert.Single(notified[^1]);
    }

    [Fact]
    public void ADefinitionWhoseAssetDoesNotLoadIsRefusedBeforeAnythingIsWritten()
    {
        File.WriteAllText(Path.Combine(_root, "objects", "broken.json"), "{ \"format\": \"app2d-object\", \"id\": \"broken\" }");
        using var editor = CreateEditor(out var map, out _, out _);
        var error = Assert.Throws<InvalidOperationException>(() =>
            editor.ApplyPositionDefinition(null, ThingTypeRegistry2D.Object.TypeKey, new() { Name = "Broken" }, "objects/broken.json"));
        Assert.Contains("objects/broken.json", error.Message);
        Assert.Empty(editor.ThingDefinitions);
        using var database = LevelDatabase2D.OpenRead(map.DatabasePath);
        Assert.Empty(database.LoadThingDefinitions());
    }

    [Fact]
    public void UndoingANonObjectEditLeavesTheRuntimeAlone()
    {
        using var editor = CreateEditor(out _, out var input, out var notified);
        editor.ApplyPositionDefinition(null, ThingTypeRegistry2D.SavePoint.TypeKey, new() { Name = "Checkpoint" });
        editor.BeginThingPlacement();
        input.SetMouseButton(MouseButtons.Left, true);
        editor.Update(input);
        input.SetMouseButton(MouseButtons.Left, false);
        input.EndFrame();
        Assert.Single(editor.PositionThings);
        var baseline = notified.Count;

        editor.UndoThingEdit();
        Assert.Empty(editor.PositionThings);
        Assert.Equal(baseline, notified.Count);
    }

    /// <summary>An editor on the fixture map, already in Things mode with its database open, and the runtime notifications it raises.</summary>
    private TileEditor2D CreateEditor(out WorldMapAsset2D map, out InputState input, out List<IReadOnlyList<ObjectPlacement2D>> notified)
    {
        map = WorldFile2D.LoadMap(Path.Combine(_root, "map.db"), "map", "Map", "zone", 3, worldRoot: _root);
        var loaded = map;
        var editor = new TileEditor2D(map.TileMap, () => LevelDatabase2D.Open(loaded.DatabasePath), new Camera2D { Position = new(128, 128) });
        editor.ConfigureMaps([map], _root);
        var changes = new List<IReadOnlyList<ObjectPlacement2D>>();
        editor.ObjectsChanged += objects => changes.Add(objects);
        notified = changes;
        input = new InputState();
        input.Mouse.SetClientPosition(new(400, 300));
        input.SetKey(Keys.F1, true);
        editor.Update(input);
        input.EndFrame();
        input.SetKey(Keys.F1, false);
        editor.SelectMode(LevelEditorMode2D.Things);
        return editor;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }
}
