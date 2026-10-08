# Zones and maps

A world groups independently saved tile maps into named zones. Maps share world coordinates: put their edges next to one another to build a continuous world. Editing selects one map; gameplay sees their combined terrain and objects through the existing chunk streamer.

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
