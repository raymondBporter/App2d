# Authored Objects as the Only Moving-Platform Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the moving-platform vertical and make the generic authored object (shape, optional collision shape, material, physics, surface, path) the only way a platform exists, with placements the map editor can create, drag, undo and live-reload from the level database.

**Architecture:** `ObjectAsset2D` (Core) gains an optional `collision` shape and an optional `geometry`. An object placement is a position thing whose `thing_definitions` row has type `object` and an `object_references` row naming an asset file relative to the world root. The level database migrates to format version 3, converting the cavern's seven platform rows into seven named asset files. The runtime, contracts, presentation, editor, registry and world loader drop every `MovingPlatform*` type; `PingPongMotion2D` goes with them.

**Tech Stack:** C# 13 / .NET 10, MonoGame, Microsoft.Data.Sqlite, System.Text.Json, xUnit. Single-line method signatures. Namespaces stay where they are.

**Spec:** `docs/superpowers/specs/2026-10-22-authored-objects-only-platforms-design.md`

## Global Constraints

- Core (`App2d.Core`) must not reference the game project; thing types and database code stay in `App2d`.
- Level database format version becomes exactly 3 (`LevelDatabase2D.CurrentFormatVersion = 3`); read-only opens keep refusing older files with "open it for editing once".
- Object asset paths are relative to the world root (the folder holding `world.json`) and resolve through `FilePaths.ResolveUnderRoot`, which rejects `..`.
- The thing type key is `object`, display name `Object`, editor color `new XnaColor(37, 210, 190)`.
- Migrated asset values: rectangle from (−w/2, −h/2) to (w/2, h/2); `material.fill` from the ARGB color as `#rrggbb`, no outline; physics `Kinematic`, `restitution` 0, `transfersContactMotion` true; `surface.oneWayNormal` (0, 1); motion curve `line` from (0, 0) to (travel_x, travel_y), `periodSeconds` = 2 × |travel| / speed, `repeat` true, `progress` [`ping-pong`, `smooth`].
- Migrated names: a group with exactly one named thing uses that thing's name; otherwise the definition name with `-2`, `-3` suffixes. Slug: lower-case, runs of non-alphanumerics become one `-`, trimmed. Identical existing file: reuse; different existing file: throw `InvalidDataException` naming the file and leave the database untouched.
- Test baseline in a worktree: five known "Tileset manifest was not found" failures plus `TrialsCourse2DTests.GameUsesConfiguredWidthForTheActualCollidableTerrain`; "suite passes" below means no other failures.
- Commit after every task on the current branch; never push.

## Review Focus

1. An asset with `collision` but no `physics`, or with `material` but no `geometry`, must fail at load with a clear `InvalidDataException`, never reach `AuthoredObject2D` (Task 1 tests `InvalidAssetDefinitionsAreRejected` rows).
2. An `object` definition whose asset file is missing or outside the world root must fail map loading with a message naming the asset path, not a null reference later (Task 3 test `ObjectDefinitionWithMissingOrEscapingAssetFailsToLoad`).
3. A migration name collision with a different existing file must throw and leave the database at version 2 with its platform tables intact (Task 5 test `MigrationRefusesToOverwriteADifferentExistingAsset`).
4. Two platform things with identical travel and speed must share one asset and one definition after migration (Task 5 test `IdenticalPlatformsShareOneMigratedAssetAndDefinition`).
5. A world manifest that still carries an `objects` array after the data migration must fail to load with a message pointing at `--migrate-level`, not silently drop the objects (Task 6 test `ManifestObjectsArrayIsRejectedAfterMigration`).

---

### Task 1: Render shape and collision shape on the object asset

**Files:**
- Modify: `App2d.Core/Objects/ObjectAsset2D.cs`
- Modify: `App2d/Gameplay/World/AuthoredObject2D.cs`
- Modify: `App2d/Contracts/World/WorldState2D.cs` (the `ObjectDefinition2D` record)
- Modify: `App2d/Presentation/World/Presentation/WorldPresentation2D.cs` (`ApplyContent`)
- Test: `App2d.Tests/Gameplay/World/AuthoredObject2DTests.cs`

**Interfaces:**
- Produces: `ObjectAsset2D.Geometry : IShape2D?` (was non-null), `ObjectAsset2D.Collision : IShape2D?` (new, init), `ObjectDefinition2D(EntityId2D Id, string PlacementId, IShape2D? Geometry, RenderMaterialDefinition2D? Material, string TextureRoot)`.
- Every later task builds assets with these two properties.

- [ ] **Step 1: Write the failing tests**

Add to `AuthoredObject2DTests` (the sample is `levels/cavern/objects/lift.json`):

```csharp
[Fact]
public void CollisionShapeOverridesTheColliderAndGeometryStillDraws()
{
    var asset = ObjectAsset2D.Load(SamplePath) with { Collision = Circle2D.Builder.Create(4) };
    var physics = new PhysicsWorld2D();
    using var instance = new AuthoredObject2D(EntityId2D.Create(), new("lift", asset, Vector2.Zero), physics, 1, uint.MaxValue);
    Assert.IsType<Circle2D>(instance.WorldObject.Shape);
    Assert.IsType<Rectangle2D>(instance.CaptureDefinition().Geometry);
}

[Fact]
public void CollisionOnlyObjectHasABodyAndNoVisual()
{
    var asset = ObjectAsset2D.Load(SamplePath) with { Geometry = null, Material = null, Collision = Circle2D.Builder.Create(4) };
    var physics = new PhysicsWorld2D();
    using var instance = new AuthoredObject2D(EntityId2D.Create(), new("sensor", asset, Vector2.Zero), physics, 1, uint.MaxValue);
    Assert.NotNull(instance.Body);
    using var textures = new TextureCache2D(TestAssetPath.Root);
    var scene = new Scene2D();
    using var view = new WorldPresentation2D(scene, textures);
    view.ApplyState(new LevelContent2D(1, [], [], [], null) { Objects = [instance.CaptureDefinition()] }, WorldState2D.Empty with { Objects = [instance.CaptureState()] });
    Assert.Empty(scene);
}

[Fact]
public void CollisionShapeRoundTripsThroughJson()
{
    var asset = ObjectAsset2D.Load(SamplePath) with { Collision = Circle2D.Builder.Create(4) };
    var restored = ObjectAsset2D.FromJson(asset.ToJson());
    Assert.IsType<Circle2D>(restored.Collision);
    Assert.IsType<Rectangle2D>(restored.Geometry);
}
```

Extend the existing `[Theory] InvalidAssetDefinitionsAreRejected(string problem, Type exceptionType)` with three `InlineData` rows, each applied to the lift JSON through the `JsonNode` edit the theory already does: `"no-shape"` removes `geometry` → `InvalidDataException`; `"material-without-geometry"` removes `geometry` and adds `"collision"` equal to the old geometry → `InvalidDataException`; `"collision-without-physics"` removes `physics`, `surface` and `motion` and adds `collision` → `InvalidDataException`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~AuthoredObject2DTests" -v q`
Expected: build error on `Collision` and `Geometry = null`.

- [ ] **Step 3: Implement the asset change in `ObjectAsset2D.cs`**

`public IShape2D? Geometry { get; init; }`, `public IShape2D? Collision { get; init; }`. `Validate()` in this order: at least one of the two ("An object needs geometry or a collision shape."); `Material` requires `Geometry` ("A material needs geometry to draw on."); `Collision` requires `Physics` ("A collision shape needs physics."); each present shape is validated and must have finite `ShapeBounds2D` bounds; the existing physics, motion and surface rules stay.

- [ ] **Step 4: Implement the runtime and presentation change**

`AuthoredObject2D`: the collider shape is `asset.Collision ?? asset.Geometry!`; both the kinematic branch and the body branch use it; `CaptureDefinition()` passes `Placement.Asset.Geometry` (now nullable). `ObjectDefinition2D.Geometry` becomes `IShape2D?`. `WorldPresentation2D.ApplyContent`: skip the item when `item.Geometry is null` as it already skips when `Material is null`, and construct the `WorldObject2D` from the non-null geometry.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~AuthoredObject2DTests|FullyQualifiedName~WorldPresentationTests" -v q`
Expected: all pass except the known tileset failures.

- [ ] **Step 6: Commit**

```bash
git add App2d.Core/Objects/ObjectAsset2D.cs App2d/Gameplay/World/AuthoredObject2D.cs App2d/Contracts/World/WorldState2D.cs App2d/Presentation/World/Presentation/WorldPresentation2D.cs App2d.Tests/Gameplay/World/AuthoredObject2DTests.cs
git commit -m "Objects: optional collision shape overrides the collider; geometry becomes draw-only and optional"
```

---

### Task 2: Object definitions in the level database (format version 3)

**Files:**
- Modify: `App2d/Levels/LevelDatabase2D.cs` (`CurrentFormatVersion`, `Open`, `Migrate`)
- Modify: `App2d/Levels/LevelDatabase2D.Things.cs` (schema, definition and thing queries)
- Modify: `App2d/Levels/MovingPlatformRecords2D.cs` (`ThingDefinitionRecord2D`, `PositionThingRecord2D`)
- Create: `App2d.Tests/Levels/ObjectThingDatabase2DTests.cs`

**Interfaces:**
- Produces: `LevelDatabase2D.ObjectTypeKey = "object"` (public const); `LevelDatabase2D.Open(string path, string? worldRoot = null)`; `ThingDefinitionRecord2D(long DefinitionId, string TypeKey, string Name, string? Asset = null)`; `PositionThingRecord2D(long ThingId, long DefinitionId, string TypeKey, string DefinitionName, string? Name, bool Enabled, float X, float Y, float Rotation, string? Asset = null)`; `CreateThingDefinition(string typeKey, string name, string? asset = null)`; `UpdateThingDefinition(ThingDefinitionRecord2D record)` also writes `Asset`.
- Table: `object_references(definition_id INTEGER PRIMARY KEY REFERENCES thing_definitions(definition_id) ON DELETE CASCADE, asset TEXT NOT NULL)`.
- Rule: type `object` requires a non-blank asset; any other type must have none (`ArgumentException`).

- [ ] **Step 1: Write the failing tests**

`ObjectThingDatabase2DTests` with the same temp-directory fixture and `Dispose` as `MovingPlatformThingDatabase2DTests`:

```csharp
[Fact]
public void ObjectDefinitionRoundTripsItsAssetPathAndPlacedThingsCarryIt()
{
    using var database = LevelDatabase2D.Open(NewPath());
    var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Bridge ferry", "objects/bridge-ferry.json");
    Assert.Equal("objects/bridge-ferry.json", Assert.Single(database.LoadThingDefinitions()).Asset);
    var thing = database.CreatePositionThing(new(definition.DefinitionId, null, true, 10f, 20f));
    Assert.Equal("objects/bridge-ferry.json", Assert.Single(database.LoadPositionThings()).Asset);
    Assert.Equal(thing.ThingId, Assert.Single(database.LoadPositionThings()).ThingId);
    var renamed = database.UpdateThingDefinition(definition with { Name = "Ferry", Asset = "objects/ferry.json" });
    Assert.Equal("objects/ferry.json", Assert.Single(database.LoadThingDefinitions()).Asset);
}

[Fact]
public void ObjectDefinitionNeedsAnAssetAndOtherTypesMustNotHaveOne()
{
    using var database = LevelDatabase2D.Open(NewPath());
    Assert.Throws<ArgumentException>(() => database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift"));
    Assert.Throws<ArgumentException>(() => database.CreateThingDefinition("save-point", "Save", "objects/lift.json"));
}

[Fact]
public void DeletingAnObjectDefinitionCascadesItsReferenceAndIsRefusedWhilePlaced()
{
    using var database = LevelDatabase2D.Open(NewPath());
    var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift", "objects/lift.json");
    var thing = database.CreatePositionThing(new(definition.DefinitionId, null, true, 0f, 0f));
    Assert.Throws<SqliteException>(() => database.DeleteThingDefinition(definition.DefinitionId));
    database.DeletePositionThing(thing.ThingId);
    database.DeleteThingDefinition(definition.DefinitionId);
    Assert.Empty(database.LoadThingDefinitions());
}

[Fact]
public void VersionTwoFileGainsObjectReferencesOnOpen()
{
    var path = NewPath();
    using (var database = LevelDatabase2D.Open(path)) { }
    using (var connection = new SqliteConnection($"Data Source={path}"))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE object_references; PRAGMA user_version = 2;";
        command.ExecuteNonQuery();
    }
    using var reopened = LevelDatabase2D.Open(path);
    Assert.Equal(3, reopened.FormatVersion);
    Assert.Empty(reopened.LoadThingDefinitions());
}
```

Also change the existing assertion in `MovingPlatformThingDatabase2DTests.FreshDatabaseStartsWithAnEmptyThingLayer` to keep comparing against `LevelDatabase2D.CurrentFormatVersion` (no edit needed if it already does).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ObjectThingDatabase2DTests" -v q`
Expected: build errors on `ObjectTypeKey`, the third `CreateThingDefinition` argument and `Asset`.

- [ ] **Step 3: Implement the records and the schema**

Add the optional `Asset` positional parameters to both records (defaults keep every existing call compiling). In `LevelDatabase2D.Things.cs`: `public const string ObjectTypeKey = "object";` and `private const string ObjectReferencesSql = "CREATE TABLE IF NOT EXISTS object_references(...)"` with the table above. In `Migrate`, after the `version == 1` step add `if (version == 2) { execute ObjectReferencesSql; version = 3; }` and set `CurrentFormatVersion = 3`. Add `string? worldRoot = null` to `Open` and store it in a private field `_worldRoot` (used by Task 5); default is the directory of the file.

- [ ] **Step 4: Implement the queries**

`LoadThingDefinitions`: `LEFT JOIN object_references USING(definition_id)`, read `asset` into `Asset`. `LoadPositionThings`: join the same table through the definition and read `Asset`. `CreateThingDefinition(typeKey, name, asset)`: validate the rule, insert the definition, then `INSERT INTO object_references` when `typeKey == ObjectTypeKey`, in one transaction. `UpdateThingDefinition`: validate the rule, update the name, then upsert or delete the reference row as the rule requires, in one transaction. `DeleteThingDefinition` is unchanged (cascade).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ThingDatabase2DTests" -v q`
Expected: PASS, including the existing platform database tests.

- [ ] **Step 6: Commit**

```bash
git add App2d/Levels App2d.Tests/Levels/ObjectThingDatabase2DTests.cs
git commit -m "Level database: object definitions reference an asset file; format version 3"
```

---

### Task 3: Objects load from map databases and reload at runtime

**Files:**
- Modify: `App2d/Things/ThingTypeRegistry2D.cs`
- Modify: `App2d/Levels/WorldFile2D.cs` (`WorldMapAsset2D`, `WorldAsset2D`, `LoadMap`, `Load`)
- Modify: `App2d/LevelBootstrap2D.cs` (`LoadManifest` sets `Root`)
- Modify: `App2d/Gameplay/World/SideScrollerLevel2D.cs`
- Modify: `App2d/SideScrollerGame.cs` (only if the `WorldAsset2D` constructor change touches it)
- Test: `App2d.Tests/Levels/JsonWorldObjectsTests.cs`, create `App2d.Tests/Presentation/World/ObjectLevelTests.cs`

**Interfaces:**
- Consumes: Task 2's `ThingDefinitionRecord2D.Asset`, `PositionThingRecord2D.Asset`, `LevelDatabase2D.ObjectTypeKey`.
- Produces: `ThingTypeRegistry2D.Object` descriptor (`TypeKey == LevelDatabase2D.ObjectTypeKey`); `ThingTypeDescriptor2D.IsObject` (true only for it) and `IsMovingPlatform` now `TypeKey == MovingPlatform.TypeKey`; `ThingTypeRegistry2D.ToPlacement(PositionThingRecord2D record, ObjectAsset2D asset, long worldThingId) : ObjectPlacement2D` with `Id = worldThingId.ToString(CultureInfo.InvariantCulture)` and `Position = (record.X, record.Y)`; `WorldMapAsset2D.Objects : IReadOnlyList<ObjectPlacement2D> { get; set; }`; `WorldAsset2D.Root : string` (required init) and `WorldAsset2D.Objects` computed as `[.. manifest objects, .. Maps.SelectMany(m => m.Objects)]`; `WorldFile2D.LoadMap(string path, string id, string name, string zoneId, int identity, IReadOnlyList<string>? legacyTilesets = null, string? worldRoot = null)`; `SideScrollerLevel2D.ReloadObjects(IReadOnlyList<ObjectPlacement2D> placements)`.

- [ ] **Step 1: Write the failing tests**

In `JsonWorldObjectsTests` (keep its fixture; it already writes `lift.json` into `_root` and a `map.db`):

```csharp
[Fact]
public void MapDatabaseObjectDefinitionsBecomePlacementsWithWorldThingIds()
{
    using (var database = LevelDatabase2D.Open(Path.Combine(_root, "map.db")))
    {
        var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift", "lift.json");
        database.CreatePositionThing(new(definition.DefinitionId, "first", true, 200f, 300f));
        database.CreatePositionThing(new(definition.DefinitionId, "second", true, 500f, 600f));
    }
    using var world = Load(_document with { Objects = [] });
    Assert.Equal(2, world.Objects.Count);
    Assert.Same(world.Objects[0].Asset, world.Objects[1].Asset);
    Assert.Equal(world.Maps[0].WorldThingId(1).ToString(), world.Objects[0].Id);
    Assert.Equal(new Vector2(500f, 600f), world.Objects[1].Position);
    Assert.Equal(_root, world.Root);
}

[Fact]
public void ObjectDefinitionWithMissingOrEscapingAssetFailsToLoad()
{
    foreach (var asset in new[] { "missing.json", "../lift.json" })
    {
        var path = Path.Combine(_root, asset.Replace("/", "-") + ".db");
        using (var database = LevelDatabase2D.Open(path))
        {
            database.Save(new EditableTileMap2D(32, 32, 32, 32, Vector2.Zero, ["dark-cave"]), 0);
            var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Bad", asset);
            database.CreatePositionThing(new(definition.DefinitionId, null, true, 0f, 0f));
        }
        var error = Assert.Throws<InvalidDataException>(() => WorldFile2D.LoadMap(path, "bad", "Bad", "test", 7, worldRoot: _root));
        Assert.Contains(asset, error.Message);
    }
}
```

In `ObjectLevelTests` (new file; use `TraversalMetricsLoader2D.Load(TestAssetPath.Root)` and the map shape from `AuthoredMovingPlatform2DTests`):

```csharp
[Fact]
public void LevelReloadsObjectsAndPresentationDropsTheirVisuals()
{
    var asset = ObjectAsset2D.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json"));
    using var level = new SideScrollerLevel2D(TraversalMetricsLoader2D.Load(TestAssetPath.Root), CreateMap(), objects: [new("one", asset, new(128f, -64f))]);
    var scene = new Scene2D();
    var collision = new CollisionSystem2D();
    var physics = new PhysicsWorld2D(collision);
    using var textures = new TextureCache2D(TestAssetPath.Root);
    level.CreateSimulation(collision, physics, new EntityIdAllocator2D(), 1, 2, 4);
    using var presentation = new WorldPresentation2D(scene, textures);
    presentation.Update(level.CaptureContent(), level.CaptureWorld(), 0f);
    var visual = Assert.Single(scene);
    var first = Assert.Single(level.Objects);
    var revision = level.CaptureContent().Revision;
    level.ReloadObjects([new("two", asset, new(0f, 0f))]);
    Assert.DoesNotContain(first.Body!, physics.Bodies);
    Assert.NotEqual(first.Id, Assert.Single(level.Objects).Id);
    Assert.NotEqual(revision, level.CaptureContent().Revision);
    presentation.Update(level.CaptureContent(), level.CaptureWorld(), 0f);
    Assert.DoesNotContain(visual, scene);
    level.ReloadObjects([]);
    Assert.Empty(level.Objects);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~JsonWorldObjectsTests|FullyQualifiedName~ObjectLevelTests" -v q`
Expected: build errors on `Root`, `ReloadObjects`, the `worldRoot` argument.

- [ ] **Step 3: Implement the registry**

Add the `Object` descriptor to `ThingTypeRegistry2D.All` (first, before `MovingPlatform`), `IsObject`, the narrowed `IsMovingPlatform`, and `ToPlacement`. `ToRuntime(PositionThingRecord2D)` keeps throwing for `WorldKind is null`; callers filter objects out first.

- [ ] **Step 4: Implement world loading**

`WorldFile2D.LoadMap`: `var root = worldRoot ?? Path.GetDirectoryName(Path.GetFullPath(path))!;` after loading position things, build `Objects` from the things whose `TypeKey == LevelDatabase2D.ObjectTypeKey`: resolve `FilePaths.ResolveUnderRoot(root, thing.Asset!)` (wrap any `ArgumentException`/`FileNotFoundException`/`DirectoryNotFoundException` in `InvalidDataException($"Object definition '{thing.DefinitionName}' asset '{thing.Asset}' ...")`), cache assets by resolved path, call `ToPlacement(thing, asset, result.WorldThingId(thing.ThingId))`. `Load` passes `root` through and sets `Root = root`. `WorldAsset2D`: `required string Root`, `Objects` becomes a computed union of the manifest list (kept in a private field until Task 6) and the maps' objects; `WorldMapAsset2D.Objects { get; set; }`. `LevelBootstrap2D.LoadManifest` sets `Root` on both branches (manifest directory, or the level directory in the fallback). Fix any other `new WorldAsset2D(` call the compiler reports.

- [ ] **Step 5: Implement `SideScrollerLevel2D.ReloadObjects`**

Make `_objectPlacements` a settable field; extract `CreateObjects()` from `CreateSimulation`; `ReloadObjects(placements)`: validate as the constructor does, dispose and clear `_objects`, assign, `CreateObjects()`, `_definitionRevision++`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~JsonWorldObjectsTests|FullyQualifiedName~ObjectLevelTests|FullyQualifiedName~WorldStateTests" -v q`
Expected: PASS (plus the known tileset failures).

- [ ] **Step 7: Commit**

```bash
git add App2d/Things App2d/Levels/WorldFile2D.cs App2d/LevelBootstrap2D.cs App2d/Gameplay/World/SideScrollerLevel2D.cs App2d/SideScrollerGame.cs App2d.Tests/Levels/JsonWorldObjectsTests.cs App2d.Tests/Presentation/World/ObjectLevelTests.cs
git commit -m "Objects load from map databases as position things and reload at runtime"
```

---

### Task 4: The map editor places, draws and live-reloads objects

**Files:**
- Create: `App2d/Editor/ObjectAssetCatalog2D.cs`
- Create: `App2d/Editor/ObjectPathPreview2D.cs`
- Modify: `App2d/Editor/TileEditor2D.cs`
- Modify: `App2d/Editor/ThingEditorInspector2D.cs`
- Modify: `App2d/Editor/TileEditorView2D.cs`
- Modify: `App2d/Editor/MovingPlatformProperties2D.cs` (`PositionThingInstanceProperties2D.Asset`)
- Modify: `App2d/SideScrollerGame.cs`
- Create: `App2d.Tests/Editor/ObjectAssetCatalog2DTests.cs`, `App2d.Tests/Editor/ObjectPathPreview2DTests.cs`

**Interfaces:**
- Consumes: Task 3's `WorldAsset2D.Root`, `WorldMapAsset2D.Objects` setter, `ThingTypeRegistry2D.Object`, `ToPlacement`, `SideScrollerLevel2D.ReloadObjects`.
- Produces: `internal sealed class ObjectAssetCatalog2D(string worldRoot)` with `IReadOnlyList<string> Paths` (relative paths under `objects/`, forward slashes, ordinal order), `ObjectAsset2D Load(string relativePath)` (cached, through `FilePaths.ResolveUnderRoot`), `void Refresh()`; `internal static class ObjectPathPreview2D` with `static Vector2[] Sample(ObjectAsset2D asset, Vector2 origin, int count = 32)` (empty when `asset.Motion` is null; otherwise `count + 1` points of `CreateMotion(origin).Position(t)` for `t` from 0 to `PeriodSeconds`); `TileEditor2D.ConfigureMaps(IReadOnlyList<WorldMapAsset2D> maps, string worldRoot)`; `TileEditor2D.ObjectAssets : ObjectAssetCatalog2D`; `TileEditor2D.ObjectPlacements : IReadOnlyList<ObjectPlacement2D>`; `event Action<IReadOnlyList<ObjectPlacement2D>>? ObjectsChanged`; `ApplyPositionDefinition(long? definitionId, string typeKey, PositionThingDefinitionProperties2D properties, string? asset = null)`; `PositionThingInstanceProperties2D.Asset : string?` (`[ReadOnly(true)]`).

- [ ] **Step 1: Write the failing tests**

`ObjectAssetCatalog2DTests` (temp directory with `objects/b.json` and `objects/a.json` copied from the cavern lift, plus `objects/notes.txt`):

```csharp
[Fact]
public void CatalogListsJsonAssetsInOrderAndCachesLoads()
{
    var catalog = new ObjectAssetCatalog2D(_root);
    Assert.Equal(["objects/a.json", "objects/b.json"], catalog.Paths);
    Assert.Same(catalog.Load("objects/a.json"), catalog.Load("objects/a.json"));
    File.Copy(Path.Combine(_root, "objects", "a.json"), Path.Combine(_root, "objects", "c.json"));
    catalog.Refresh();
    Assert.Equal(3, catalog.Paths.Count);
    Assert.Throws<ArgumentException>(() => catalog.Load("../a.json"));
}
```

`ObjectPathPreview2DTests`:

```csharp
[Fact]
public void PreviewFollowsTheMotionFromTheOriginAndIsEmptyWithoutMotion()
{
    var asset = ObjectAsset2D.Load(LiftPath);
    var points = ObjectPathPreview2D.Sample(asset, new(100f, 50f), 4);
    Assert.Equal(5, points.Length);
    Assert.Equal(new Vector2(100f, 50f), points[0]);
    Assert.Equal(asset.Motion!.CreateMotion(new(100f, 50f)).Position(asset.Motion.PeriodSeconds / 2), points[2]);
    Assert.Empty(ObjectPathPreview2D.Sample(asset with { Motion = null }, Vector2.Zero));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ObjectAssetCatalog2DTests|FullyQualifiedName~ObjectPathPreview2DTests" -v q`
Expected: build errors, types missing.

- [ ] **Step 3: Implement the two helpers and run the tests**

Run the same filter. Expected: PASS.

- [ ] **Step 4: Teach `TileEditor2D` objects**

`ConfigureMaps(maps, worldRoot)` creates `ObjectAssets`. `ReloadThings`: include position things whose descriptor `IsObject` (the filter becomes `WorldKind is not null || IsObject`); rebuild `ObjectPlacements` from those things through `ObjectAssets.Load(thing.Asset!)` and `ThingTypeRegistry2D.ToPlacement(thing, asset, _maps[SelectedMapIndex].WorldThingId(thing.ThingId))`; when `notifyRuntime`, raise `ObjectsChanged(ObjectPlacements)` after `ThingsChanged`. `ApplyPositionDefinition` accepts the asset and passes it to `CreateThingDefinition`/`UpdateThingDefinition`; it allows `IsObject` as well as `WorldKind is not null`. `ApplyPositionThing`, `DeleteSelectedThing`, `PlaceThing` and `UndoThingEdit` pass `notifyRuntime: true` when the affected thing is an object (position things of other types keep today's behavior). `TryGetPositionPlacementPreview` returns true for objects too. Selection hit-testing for objects uses the asset's `Collision ?? Geometry` bounds (`ShapeBounds2D.Calculate`) translated to the thing position, checked before the marker-radius test.

- [ ] **Step 5: Teach the inspector and the view**

`ThingEditorInspector2D`: a private `string? _assetPath`; when `_editingTypeKey == ThingTypeRegistry2D.Object.TypeKey`, draw `Ui.Combo("Asset", _assetPath ?? "Choose an asset", _editor.ObjectAssets.Paths)` above the property panel and default the definition name to the asset file name without extension when it is still the type's display name; `Apply` passes `_assetPath` for object definitions and refuses with `_error = "Choose an asset file."` when it is null; `ShowDefinitionCore` loads `_assetPath` from `definition.Asset`; `ShowPositionThingCore` fills `Asset` on `PositionThingInstanceProperties2D`. `TileEditorView2D.DrawThings`: for an object thing, draw the outline of `Collision ?? Geometry` translated to the thing position with `renderer.DrawShape`, then `renderer.DrawWorldPolyline(ObjectPathPreview2D.Sample(asset, position), pathColor, width)` when the asset has motion, and mark the first and last points when selected; the placement ghost for an object draws the same outline at the preview position.

- [ ] **Step 6: Wire the game**

`SideScrollerGame`: `_editor.ConfigureMaps(loadedLevel.Maps, loadedLevel.Root)`; subscribe `_editor.ObjectsChanged += objects => { loadedLevel.Maps[_editor.SelectedMapIndex].Objects = objects; _simulation.Level.ReloadObjects(loadedLevel.Objects); }`.

- [ ] **Step 7: Build, run the editor tests and the suite**

Run: `dotnet build App2d.slnx -v q` then `dotnet test App2d.Tests/App2d.Tests.csproj -v q`
Expected: build succeeds; suite passes (baseline failures only).

- [ ] **Step 8: Manual check in the game**

Run the game, press F1, Things mode: New definition → type Object → pick `objects/lift.json` → Apply → Place → click; the lift appears in the paused simulation with its path drawn; drag it; Undo; Delete thing. Note anything that does not work in the commit message.

- [ ] **Step 9: Commit**

```bash
git add App2d/Editor App2d/SideScrollerGame.cs App2d.Tests/Editor
git commit -m "Map editor: object definitions from asset files, placement, path preview and live reload"
```

---

### Task 5: Convert platform rows and delete the moving-platform vertical

**Files:**
- Modify: `App2d/Levels/LevelDatabase2D.cs` (`Migrate` version-2 step), `App2d/Levels/LevelDatabase2D.Things.cs` (schema without the three platform tables; remove platform CRUD)
- Create: `App2d/Levels/MovingPlatformMigration2D.cs` (the version-2 conversion, internal static)
- Rename: `App2d/Levels/MovingPlatformRecords2D.cs` → `App2d/Levels/ThingRecords2D.cs` (platform records removed)
- Rename: `App2d/Editor/MovingPlatformProperties2D.cs` → `App2d/Editor/ThingProperties2D.cs` (platform classes removed)
- Delete: `App2d/Gameplay/World/MovingPlatform2D.cs`, `App2d/Gameplay/World/MovingPlatformSpec2D.cs`, `App2d.Core/Physics/Motion/PingPongMotion2D.cs`
- Modify: `App2d/Contracts/World/WorldState2D.cs`, `App2d/Gameplay/World/SideScrollerLevel2D.cs`, `App2d/Gameplay/Simulation/SideScrollerSessionDefinition2D.cs`, `App2d/Gameplay/Simulation/SideScrollerSimulation2D.cs`, `App2d/Presentation/World/Presentation/WorldPresentation2D.cs`, `App2d/Things/ThingTypeRegistry2D.cs`, `App2d/Levels/WorldFile2D.cs`, `App2d/Editor/TileEditor2D.cs`, `App2d/Editor/ThingEditorInspector2D.cs`, `App2d/Editor/TileEditorView2D.cs`, `App2d/SideScrollerGame.cs`, `App2d.CharacterSeeds/Diagnostics/PlatformStudy2D.cs`, `App2d/Gameplay/World/KINEMATICS.md`
- Tests: create `App2d.Tests/Gameplay/World/ObjectPlatforms.cs` (helper) and `App2d.Tests/Gameplay/World/ObjectPlatformTests.cs`; rename `MovingPlatformThingDatabase2DTests.cs` → `ThingDatabase2DTests.cs`; delete `MovingPlatform2DTests.cs`, `AuthoredMovingPlatform2DTests.cs`; modify `WorldPresentationTests.cs`, `WorldStateTests.cs`, `SideScrollerSimulation2DTests.cs`, `ObservationRoundTripTests.cs`, `AuthoredEntityEnemyTests.cs`, `KinematicEntity2DTests.cs`

**Interfaces:**
- Consumes: Task 2's `_worldRoot` in `LevelDatabase2D`, Task 3's `ReloadObjects`, Task 4's `ObjectsChanged`.
- Produces: `LevelContent2D(long Revision, ImmutableArray<TerrainChunkState2D> Terrain, ImmutableArray<CheckpointPlacement2D> Checkpoints, Vector2? GoalPosition)`; `WorldState2D(ImmutableArray<CheckpointState2D> Checkpoints)`; `SideScrollerSessionDefinition2D(TraversalMetrics2D Traversal, IChunkedTileMap2D TileMap, IReadOnlyList<WorldThingSpec2D> WorldThings)`; `SideScrollerLevel2D(TraversalMetrics2D traversal, IChunkedTileMap2D tileMap, IReadOnlyList<WorldThingSpec2D>? worldThings = null, IEnumerable<WorldZone2D>? zones = null, IEnumerable<GiantTree2D>? giantTrees = null, IReadOnlyList<ObjectPlacement2D>? objects = null)`; test helper `ObjectPlatforms.Lift(Vector2 travel, float speed, float width = 80f, float height = 14f, string id = "lift") : ObjectAsset2D` and `ObjectPlatforms.Place(string id, Vector2 position, Vector2 travel, float speed, float width = 80f, float height = 14f) : ObjectPlacement2D`, building exactly the migrated values from Global Constraints.
- `MovingPlatformMigration2D.Convert(SqliteConnection connection, string worldRoot)` runs inside the open migration transaction; it is a no-op when `rectangle_shapes` does not exist.

- [ ] **Step 1: Write the failing migration tests**

In `ThingDatabase2DTests` (renamed file; the platform CRUD tests in it are deleted in Step 4). Build version-2 files with raw SQL the way `VersionOneMigrationPreservesTileChunksAndCreatesEmptyThingTables` does, using the old schema text copied into the test as a constant, one `Lift` definition (width 96, height 16, color `unchecked((int)0xFF25D2BEu)`) and things with transforms and motors:

```csharp
[Fact]
public void VersionTwoPlatformRowsBecomeNamedObjectAssetsAndObjectThings()
{
    var path = WriteVersionTwoPlatforms(("Bridge ferry", 10f, 20f, 288f, 0f, 105f), ("Summit lift", 30f, 40f, 0f, 256f, 110f));
    using var database = LevelDatabase2D.Open(path, worldRoot: _directory);
    Assert.Equal(3, database.FormatVersion);
    var ferry = ObjectAsset2D.Load(Path.Combine(_directory, "objects", "bridge-ferry.json"));
    Assert.Equal(new Vector2(-48f, -8f), ShapeBounds2D.Calculate(ferry.Geometry!).Min);
    Assert.Equal("#25d2be", ferry.Material!.Fill);
    Assert.Equal(BodyMotionType2D.Kinematic, ferry.Physics!.MotionType);
    Assert.Equal(0f, ferry.Physics.Restitution);
    Assert.True(ferry.Physics.TransfersContactMotion);
    Assert.Equal(Vector2.UnitY, ferry.Surface!.OneWayNormal);
    var line = Assert.IsType<LineSegmentCurve2D>(ferry.Motion!.Curve);
    Assert.Equal(new Vector2(288f, 0f), line.End);
    Assert.Equal(2 * 288 / 105d, ferry.Motion.PeriodSeconds, 6);
    Assert.Equal([ProgressMapKind.PingPong, ProgressMapKind.Smooth], ferry.Motion.Progress);
    Assert.True(File.Exists(Path.Combine(_directory, "objects", "summit-lift.json")));
    var definitions = database.LoadThingDefinitions();
    Assert.Equal(["Bridge ferry", "Summit lift"], definitions.Select(d => d.Name));
    Assert.All(definitions, d => Assert.Equal(LevelDatabase2D.ObjectTypeKey, d.TypeKey));
    Assert.Equal(["objects/bridge-ferry.json", "objects/summit-lift.json"], definitions.Select(d => d.Asset));
    var things = database.LoadPositionThings();
    Assert.Equal([(10f, 20f), (30f, 40f)], things.Select(t => (t.X, t.Y)));
    Assert.DoesNotContain("rectangle_shapes", TableNames(path));
    Assert.DoesNotContain("ping_pong_motors", TableNames(path));
}

[Fact]
public void IdenticalPlatformsShareOneMigratedAssetAndDefinition()
{
    var path = WriteVersionTwoPlatforms(("A", 0f, 0f, 100f, 0f, 50f), ("B", 5f, 5f, 100f, 0f, 50f), ("C", 9f, 9f, 0f, 100f, 50f));
    using var database = LevelDatabase2D.Open(path, worldRoot: _directory);
    // A and B share one group, so that group keeps the definition's name; C is alone and takes its own.
    Assert.Equal(["C", "Lift"], database.LoadThingDefinitions().Select(d => d.Name));
    Assert.Equal(["objects/c.json", "objects/lift.json"], database.LoadThingDefinitions().Select(d => d.Asset));
    Assert.Equal(2, Directory.GetFiles(Path.Combine(_directory, "objects")).Length);
    Assert.Equal(3, database.LoadPositionThings().Count);
    Assert.Equal(2, database.LoadPositionThings().Count(t => t.Asset == "objects/lift.json"));
}

[Fact]
public void MigrationRefusesToOverwriteADifferentExistingAsset()
{
    var path = WriteVersionTwoPlatforms(("Bridge ferry", 0f, 0f, 288f, 0f, 105f));
    Directory.CreateDirectory(Path.Combine(_directory, "objects"));
    File.WriteAllText(Path.Combine(_directory, "objects", "bridge-ferry.json"), "{ \"format\": \"app2d-object\" }");
    var error = Assert.Throws<InvalidDataException>(() => LevelDatabase2D.Open(path, worldRoot: _directory));
    Assert.Contains("bridge-ferry.json", error.Message);
    Assert.Contains("rectangle_shapes", TableNames(path));
    Assert.Equal(2, UserVersion(path));
}

[Fact]
public void FreshDatabaseHasNoPlatformTablesAndMigrationIsIdempotent()
{
    var path = NewPath();
    using (var database = LevelDatabase2D.Open(path)) Assert.Equal(3, database.FormatVersion);
    Assert.DoesNotContain("rectangle_shapes", TableNames(path));
    using var again = LevelDatabase2D.Open(path);
    Assert.Equal(3, again.FormatVersion);
}
```

`TableNames(path)` reads `SELECT name FROM sqlite_master WHERE type = 'table'`; `UserVersion(path)` reads `PRAGMA user_version`; `WriteVersionTwoPlatforms(params (string Name, float X, float Y, float TravelX, float TravelY, float Speed)[] things)` returns the new file path.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ThingDatabase2DTests" -v q`
Expected: the four new tests fail (no conversion, tables still present).

- [ ] **Step 3: Implement the conversion**

`MovingPlatformMigration2D.Convert(connection, worldRoot)`: read definitions of type `moving-platform` joined with `rectangle_shapes` and `solid_color_art`; for each, read its things joined with `ping_pong_motors` and `transforms`; group by (`travel_x`, `travel_y`, `speed`); name each group by the Global Constraints rule; build the `ObjectAsset2D` (`Id` = slug, `Name` = group name, geometry `Rectangle2D.Builder.Create(new(-w/2, -h/2), new(w/2, h/2))`, the fixed material, physics, surface and `CurveMotionDefinition2D { Curve = LineSegmentCurve2D.Builder.Create(Vector2.Zero, travel), PeriodSeconds = 2 * travel.Length() / speed, Progress = [PingPong, Smooth] }`); target `Path.Combine(worldRoot, "objects", slug + ".json")`: if it exists and its text equals `asset.ToJson()` reuse it, if it exists and differs throw `InvalidDataException($"Migration would overwrite '{file}' with a different object; move it aside and open the level again.")`, otherwise `asset.Save(file)`; first group updates the definition (`type_key = 'object'`, name) and inserts the reference, later groups insert new definitions and re-point their things; finally `DROP TABLE` the three piece tables. Writing files happens only after every group is resolved, so a collision throws before anything is written. In `Migrate`, the version-2 step becomes: create `object_references`, `Convert(connection, _worldRoot)`, `DROP TABLE IF EXISTS rectangle_shapes; ... solid_color_art; ... ping_pong_motors;`, version = 3; `ThingSchemaSql` no longer creates those three tables. Because `Migrate` runs inside one transaction, a throw leaves the file at version 2.

- [ ] **Step 4: Delete the database vertical and its tests**

Remove `LoadMovingPlatformDefinitions`, `LoadMovingPlatforms`, `CreateMovingPlatformDefinition`, `UpdateMovingPlatformDefinition`, `DeleteMovingPlatformDefinition`, `CreateMovingPlatform`, `UpdateMovingPlatform`, `DeleteMovingPlatform`, `RestoreMovingPlatform` and `MovingPlatformTypeKey`; rename the records file and drop the three platform records; delete the platform tests from the renamed test file (`DefinitionAndPlacedThingRoundTripAsTypedRows`, `DeletingAThingCascadesItsInstancePiecesAndItCanBeRestoredForUndo`, `ReferencedDefinitionCannotBeDeletedButUnreferencedPiecesCascade`, `MissingRequiredPieceNamesTheDefinitionAndType`, `InvalidPlacementDoesNotLeavePartialThingRows` if it uses platform rows) and the two platform assertions in `FreshDatabaseStartsWithAnEmptyThingLayer`; move the Task 2 tests from `ObjectThingDatabase2DTests.cs` into this file and delete that file.

- [ ] **Step 5: Run the database tests**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ThingDatabase2DTests" -v q`
Expected: PASS.

- [ ] **Step 6: Write the test helper and the ported platform tests**

`ObjectPlatforms` builds the asset with `Rectangle2D.Builder.CreateFromSize(width, height)`, `Material = new() { Fill = "#25d2be" }`, physics `Kinematic`/`Restitution = 0`/`TransfersContactMotion = true`, `Surface = new() { OneWayNormal = Vector2.UnitY }`, motion as above. `ObjectPlatformTests`: copy `MovingPlatform2DTests`, keep every test name, and replace `CreatePlatform(physics, travel, speed)` with `new AuthoredObject2D(EntityId2D.Create(), ObjectPlatforms.Place("lift", Vector2.Zero, travel, speed, 40f, 10f), physics, WorldLayer, uint.MaxValue)`; `platform.Start` becomes the placement position and `platform.End` the position plus travel; `platform.Body` is non-null for these assets. Switch `AuthoredEntityEnemyTests.Ride` to the same construction (`ObjectPlatforms.Place("ride", Vector2.Zero, Vector2.Normalize(velocity) * 200, velocity.Length(), 200f, 10f)`).

- [ ] **Step 7: Delete the runtime, contracts, presentation, editor and world-asset vertical**

Delete the three files listed above. `LevelContent2D` and `WorldState2D` lose their `MovingPlatforms` parameters and `Empty` adjusts; `SideScrollerSessionDefinition2D` and `SideScrollerLevel2D` lose their platform parameters and fields (`_movingPlatformSpecs`, `_movingPlatforms`, `MovingPlatforms`, `CreateMovingPlatformsFromSpecs`, `ReloadMovingPlatforms`, the dispose loop); `SideScrollerSimulation2D.Create` stops passing platforms; `WorldPresentation2D` loses `_platforms`, `PlatformView` and both platform loops; `ThingTypeRegistry2D` loses `MovingPlatform`, `IsMovingPlatform` and `ToRuntime(MovingPlatformThingRecord2D)`; `WorldMapAsset2D`/`WorldAsset2D` lose `MovingPlatforms` and `LoadMap` stops loading them; `TileEditor2D` loses `_movingPlatformDefinitions`, `_movingPlatformThings`, `_thingDragOriginal`, `MovingPlatformDefinitions`, `MovingPlatformThings`, `SelectedThing`, `ThingsChanged`, `ApplyDefinition`, `ApplyThing`, `TryGetPlacementPreview`, the platform branches in `PlaceThing`, `DeleteSelectedThing`, `BeginThingSelectionOrDrag`, `BeginThingDrag`, `UpdateThingDrag`, `CommitThingDrag` and `ReloadThings` (object things keep dragging through the existing position-thing drag path); `ThingEditorInspector2D` loses `ShowThing`, `ShowThingCore` and the two platform `case`s; `TileEditorView2D` loses the platform loop; `SideScrollerGame` loses the `ThingsChanged` subscription; `KINEMATICS.md` loses its platform paragraph. Fix every `new SideScrollerSessionDefinition2D(`, `new SideScrollerLevel2D(`, `new LevelContent2D(` and `new WorldState2D(` call the compiler reports (tests, smokes and studies) by dropping the platform argument. `PlatformStudy2D` builds each scenario's platform as an `ObjectPlacement2D` with an inline `ObjectAsset2D` carrying the same values the test helper uses, passed through `Objects`.

- [ ] **Step 8: Update the remaining tests**

`WorldPresentationTests` and `WorldStateTests`: replace `Platform()` with `ObjectPlatforms.Place("lift", new(-200f, 100f), new(96f, 0f), 48f)` passed as `objects:`, `level.MovingPlatforms` with `level.Objects`, `ReloadMovingPlatforms([... Size ...])` with `ReloadObjects([... width 100f, height 12f ...])`, and `World.MovingPlatforms[0].Position` with `World.Objects[0].Position`. `SideScrollerSimulation2DTests`: the platform array becomes an object placement in `Objects`, and the id/state comparisons use `Level.Objects` and `World.Objects`. `ObservationRoundTripTests`: drop the platform definition and state from the constructed content and world and their two assertions. `KinematicEntity2DTests.PingPongMotionReflectsAtEndpointsForArbitrarySampleTimes`: build the motion as `new CurveMotion2D(LineSegmentCurve2D.Builder.Create(Vector2.Zero, new(10f, 0f)), 2 * 10 / 4d, ProgressMaps.PingPong)` and keep its assertions.

- [ ] **Step 9: Build everything and run the suite**

Run: `dotnet build App2d.slnx -v q` then `dotnet test App2d.Tests/App2d.Tests.csproj -v q`
Expected: no `MovingPlatform` or `PingPongMotion2D` symbol remains (`grep -rn "MovingPlatform\|PingPongMotion2D" --include=*.cs . | grep -v /bin/ | grep -v /obj/` prints nothing); suite passes (baseline failures only).

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "Replace the moving-platform vertical with authored objects; format version 3 converts platform rows"
```

---

### Task 6: Migrate the cavern data, drop the manifest objects path, update docs

**Files:**
- Modify: `App2d/LevelBootstrap2D.cs` (`MigrateWorld`), `App2d/Program.cs` (`--migrate-level`)
- Modify: `App2d/Levels/WorldFile2D.cs` (remove `WorldDocument.Objects`, `ObjectPlacementDocument`, `LoadObjects`, the manifest part of `WorldAsset2D.Objects`)
- Modify: `Assets/Static/levels/cavern/world.json`, `Assets/Static/levels/cavern/level.db`, `hub.db`, `sandbox-a.db`, `sandbox-b.db`, `generated/map-2/seed-12345-98701aa8fea649b9bb5fda0b7a80ad11/level.db`; create `Assets/Static/levels/cavern/objects/*.json` (seven files)
- Modify: `README.md`, `docs/world-maps.md`
- Tests: rename `App2d.Tests/Levels/JsonWorldObjectsTests.cs` → `WorldObjectsTests.cs`; create `App2d.Tests/Levels/WorldMigrationTests.cs`

**Interfaces:**
- Produces: `LevelBootstrap2D.MigrateWorld(string manifestPath) : IReadOnlyList<string>` (returns every map file it opened); `--migrate-level` with no argument migrates the cavern world, `--migrate-level <world.json>` any world.

- [ ] **Step 1: Write the failing tests**

`WorldMigrationTests` (temp world: `world.json` with one zone and one map `map.db` at version 2 containing no platform rows, written with the raw-SQL helper from Task 5, plus `objects/lift.json` and a manifest `objects` entry `{"id":"arrival","asset":"objects/lift.json","position":{"x":10,"y":20}}`):

```csharp
[Fact]
public void MigrateWorldOpensEveryMapImportsManifestObjectsAndStripsThem()
{
    var migrated = LevelBootstrap2D.MigrateWorld(ManifestPath);
    Assert.Equal([Path.Combine(_root, "map.db")], migrated);
    using var database = LevelDatabase2D.OpenRead(Path.Combine(_root, "map.db"));
    Assert.Equal(3, database.FormatVersion);
    var thing = Assert.Single(database.LoadPositionThings());
    Assert.Equal(("arrival", 10f, 20f, "objects/lift.json"), (thing.Name, thing.X, thing.Y, thing.Asset));
    Assert.DoesNotContain("\"objects\"", File.ReadAllText(ManifestPath));
    Assert.Equal(migrated, LevelBootstrap2D.MigrateWorld(ManifestPath));
}

[Fact]
public void ManifestObjectsArrayIsRejectedAfterMigration()
{
    var error = Assert.Throws<InvalidDataException>(() => WorldFile2D.Load(ManifestPath));
    Assert.Contains("--migrate-level", error.Message);
}
```

In `WorldObjectsTests` (renamed): delete `ManifestReferencesSpawnIndependentInstancesWithoutThingRegistryEntries`, `GameBootstrapPreservesTheManifestObjectsWhenAddingRegions` and the manifest half of `DuplicatePlacementIdsAndAssetPathsOutsideTheWorldAreRejected`; the document fixture no longer sets `Objects`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~WorldMigrationTests|FullyQualifiedName~WorldObjectsTests" -v q`
Expected: build error on `MigrateWorld`; the rejection test fails because the array still loads.

- [ ] **Step 3: Implement `MigrateWorld` and the manifest change**

`MigrateWorld(manifestPath)`: root = manifest directory; read the manifest with a private `JsonDocument` pass; for each entry of an `objects` array, find the map whose tile-map world bounds contain the position (load bounds through `WorldFile2D.LoadMap` after migrating that map), open it for editing, create or reuse an `object` definition for the asset path (by `Asset` equality) and create a position thing named by the entry id; then rewrite the manifest without `objects`; then open every `*.db` under root recursively with `LevelDatabase2D.Open(path, worldRoot: root)` and dispose it; return the paths in ordinal order. `WorldFile2D`: remove the objects document types and loading; in `Load`, after deserialising, if the raw document has an `objects` property throw `InvalidDataException("World manifests no longer carry objects; run 'dotnet run --project App2d -- --migrate-level <world.json>' once.")` (check with a `JsonDocument` before the typed deserialise, since `UnmappedMemberHandling.Disallow` would otherwise report a generic error). `WorldAsset2D.Objects` becomes the maps' union only. `Program.cs`: `--migrate-level` → `LevelBootstrap2D.MigrateWorld(LevelBootstrap2D.WorldManifestPath)` and print each path; `--migrate-level <path>` → the same with that manifest.

- [ ] **Step 4: Run the tests to verify they pass**

Run the Step 2 filter. Expected: PASS.

- [ ] **Step 5: Run the data migration and commit the data**

Run: `dotnet run --project App2d -- --migrate-level`
Expected output lists the five map files. Then verify: `Assets/Static/levels/cavern/objects/` holds `lift.json` plus seven new files named `bridge-ferry.json`, `terrace-elevator.json`, `canyon-lift.json`, `west-windlift.json`, `central-windlift.json`, `east-windlift.json`, `summit-lift.json` (confirm the slugs against the names the migration prints); `world.json` has no `objects`; a second run prints the same list and `git status` shows no further change.

- [ ] **Step 6: Play and smoke**

Run the game: the arrival lift and the seven platforms move, carry the player and are one-way. Run `dotnet run --project App2d.CharacterSeeds -- --platform-study <scratch dir>` and confirm it writes its images.

- [ ] **Step 7: Update the docs**

`README.md`: the editor paragraph (the "Things button" paragraph) describes object definitions picked from `objects/*.json`, object placements reloading live, and drops "Moving platforms keep their richer ..."; the production-level sentence lists objects instead of moving platforms; add one line under the level tooling commands for `--migrate-level`. `docs/world-maps.md`: object positions and asset paths; one sentence on `--migrate-level` for old files.

- [ ] **Step 8: Run the suite and commit**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj -v q`
Expected: suite passes (baseline failures only).

```bash
git add -A
git commit -m "Migrate the cavern platforms to object assets; world manifests no longer carry objects"
```
