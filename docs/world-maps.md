# Zones and maps

A world groups independently saved tile maps into named zones. Maps share world coordinates: put their edges next to one another to build a continuous world. Editing selects one map; gameplay sees their combined terrain and objects through the existing chunk streamer.

## Level playground

The checked-in world contains **Central hub**, **Combat Grounds** (zone ID `sandbox-a`), **Sandbox B**, and the **Original playground**. The Original playground uses upstream's `level.db` and is the default arrival destination. Combat Grounds retains its assigned generated map. The hub and Sandbox B remain editable canvases. See [zone generation](level-generation.md) to tune and regenerate Combat Grounds.

During play, press **F2** to open the level picker. Click a map, or use **Up / Down** and **Enter**, to travel there. Press **F2** again to close without traveling. The game pauses while the picker is open. Choose **Central hub** from anywhere to return. Escape retains its existing quit-game behavior.

Travel uses the destination map's first enabled `player-spawn` thing, or the existing ground-column fallback if it has none. It restores health, clears attacks and velocity, and makes that arrival your temporary respawn point. It does not write a checkpoint save. `startMapId` selects the initial map; `startAtArrival: true` starts there without loading an old checkpoint. The shipped playground enables this setting. With it disabled, a valid saved checkpoint takes precedence.

Press **F1** after arriving to edit that map. **Page Up / Page Down** switches maps in the editor; F1 returns to the player's current location. To play another map after editing it, leave the editor and select it with F2. Terrain edits apply immediately; moving an arrival marker currently requires restarting, like other position-object edits.

To add a destination, create a database with the command below and register it in `world.json`; the picker lists every map automatically after a restart. Rename its `name` to change the picker label. Keep IDs and identities stable. No portal layout is required.

The starter files can be created in another directory containing `level.db` with `dotnet run --project App2d -- --create-playground <directory>`. This refuses to replace any existing hub, sandbox, or manifest file. Normal startup never regenerates these maps.

## Map files

The game reads `Assets/Static/levels/cavern/world.json` when present. Without it, the existing `level.db` and `zones.json` still load as before. No existing terrain is migrated automatically.

Create a blank map database with the game stopped (width/height are tiles; origins are world units):

```powershell
dotnet run --project App2d -- --create-map Assets/Static/levels/cavern/east.db 128 96 19968 -640
```

The command refuses to overwrite a file. Maps created this way use 32-unit tiles and 32-tile chunks, matching the current game. Then add a `world.json` next to the databases:

```json
{
  "version": 1,
  "zones": [
    {
      "id": "cavern-world",
      "name": "Cavern",
      "priority": -1,
      "maps": [
        { "id": "main", "name": "Main cavern", "identity": 0, "file": "level.db" },
        { "id": "east", "name": "East cavern", "identity": 1, "file": "east.db" }
      ]
    }
  ]
}
```

Restart the game after changing the manifest. Press **F1** to edit, then **Page Up / Page Down** to choose a map. Clicking the map name at the top of the tile palette also advances to the next map. The camera moves to that map, and the grid and painting bounds follow it. Tile and thing undo histories remain separate per map. Switching commits an unfinished stroke or platform drag before opening the next database. F1 resumes the same world session.

Each zone's rectangle encloses its maps. Existing `zones.json` regions remain available for music and biome selection; a manifest zone with the same ID replaces that region. Use a lower container priority when narrower existing regions should take precedence.

Map IDs and numeric identities must be unique across the world. Keep `identity` stable once saves exist: it namespaces thing/checkpoint IDs independently of manifest order. Identity zero preserves the original database's IDs. Map files must be unique relative paths inside the manifest directory. All maps must use the same tile and chunk sizes, align to one tile grid, and have non-overlapping rectangles; gaps are empty space. Maps can use different tileset catalogs, up to sixteen distinct tilesets across the world.

Map database origins and object positions are world-space coordinates. Existing position-object edits retain their current restart requirement. Terrain changes and moving-platform edits update immediately. Terrain data currently loads at startup; simulation and presentation continue streaming chunks around the player and camera. This does not yet unload map databases' in-memory tile data on distant travel.
