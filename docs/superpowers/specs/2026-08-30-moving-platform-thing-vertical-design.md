# Moving Platform Thing Vertical

## Purpose

Prove the authored-thing model with one complete, deliberately small vertical: remove the
four hard-coded side-scroller moving platforms from `SideScrollerLevel2D`, then make moving
platform definitions and instances authorable in the level editor and persistent in the
level database.

The slice must prove these ideas without building a general database ECS:

- a code-owned thing type defines a composition;
- an authored definition ties reusable pieces together;
- placed things point at that definition and add instance pieces;
- each piece owns typed SQL, validation, migration, and editor presentation;
- the runtime receives resolved initialization data, not database abstractions.

## Terminology

- **Thing type**: code-owned composition and construction contract. The first key is
  `moving-platform`.
- **Thing definition**: a reusable level-local palette entry of one thing type.
- **Thing**: a placed instance that points to a definition.
- **Piece**: typed authored data owned by one subsystem. Presence of its row attaches the
  piece to a definition or thing.

The word *piece* is used in the authoring model to avoid implying that the runtime must
become an ECS. A piece may simply supply initialization values to an existing subsystem.

## Current content disposition

`SideScrollerLevel2D.CreateMovingPlatforms` currently creates four teal, three-tile-wide,
half-tile-high, one-way kinematic platforms. The vertical deletes this construction code.
It does not migrate or reproduce those four instances.

Their current values are retained here only as a visual reference if the author wants to
recreate something similar by hand:

| Platform | Start tile X | Height above terrain | Travel | Speed |
|---|---:|---:|---:|---:|
| opening horizontal | 12 | 3 tiles | `(224, 0)` | 57.6 |
| first vertical | 48 | 2 tiles | `(0, 128)` | 43.2 |
| return horizontal | 88 | 5 tiles | `(-192, 0)` | 67.2 |
| second vertical | 132 | 2.5 tiles | `(0, 160)` | 51.2 |

After the schema upgrade the authored level intentionally contains zero moving platforms.
Every subsequently placed platform stores a concrete world-space position; runtime loading
never performs terrain-relative placement.

## Relational model

### Stable spine

```sql
CREATE TABLE thing_definitions(
    definition_id INTEGER PRIMARY KEY,
    type_key TEXT NOT NULL,
    name TEXT NOT NULL
);

CREATE TABLE things(
    thing_id INTEGER PRIMARY KEY,
    definition_id INTEGER NOT NULL
        REFERENCES thing_definitions(definition_id) ON DELETE RESTRICT,
    name TEXT,
    enabled INTEGER NOT NULL DEFAULT 1 CHECK(enabled IN (0, 1))
);
```

`type_key` is interpreted by the code-owned registry. It is not user-extensible script or
data. Changing a definition's type is not an ordinary editor operation; conversion creates
or selects another definition with the required pieces.

### Definition pieces

```sql
CREATE TABLE rectangle_shapes(
    definition_id INTEGER PRIMARY KEY
        REFERENCES thing_definitions(definition_id) ON DELETE CASCADE,
    width REAL NOT NULL CHECK(width > 0),
    height REAL NOT NULL CHECK(height > 0)
);

CREATE TABLE solid_color_art(
    definition_id INTEGER PRIMARY KEY
        REFERENCES thing_definitions(definition_id) ON DELETE CASCADE,
    color_argb INTEGER NOT NULL
);
```

The editor's **New moving platform definition** action proposes these defaults, which the
author may change before saving:

```text
definition_id: allocated by SQLite
type_key: moving-platform
name: Teal lift
rectangle shape: 96 x 16
solid color art: 0xFF25D2BE
```

The moving-platform type itself continues to decide that this shape becomes a kinematic,
one-way physics body. Collision layers and masks come from the runtime environment and are
not authored level data.

### Instance pieces

```sql
CREATE TABLE transforms(
    thing_id INTEGER PRIMARY KEY
        REFERENCES things(thing_id) ON DELETE CASCADE,
    x REAL NOT NULL,
    y REAL NOT NULL,
    rotation REAL NOT NULL DEFAULT 0
);

CREATE TABLE ping_pong_motors(
    thing_id INTEGER PRIMARY KEY
        REFERENCES things(thing_id) ON DELETE CASCADE,
    travel_x REAL NOT NULL,
    travel_y REAL NOT NULL,
    speed REAL NOT NULL CHECK(speed > 0),
    CHECK(travel_x <> 0 OR travel_y <> 0)
);
```

Distance along the path, current direction, and frame velocity are runtime state. They are
not stored in the authored level.

A placed platform points to the selected definition and has exactly one transform and one
ping-pong motor row.

## Type registration

The registry is explicit code, not reflection over table names. Conceptually the first
registration says:

```text
moving-platform
  definition pieces: rectangle shape, solid-color art
  instance pieces: transform, ping-pong motor
  construction: MovingPlatform2D factory
  definition UI: name, width, height, color
  instance UI: name, enabled, position, travel, speed
  viewport UI: start/end path line and endpoint handles
```

The descriptor validates composition before construction:

- every moving-platform definition has one rectangle-shape row and one solid-color-art row;
- every enabled instance has one transform and one ping-pong-motor row;
- dimensions and speed are positive;
- travel is non-zero;
- definitions and things may not contain unrecognized pieces unless the type explicitly
  marks them optional.

SQLite foreign keys prevent orphaned rows. The registry owns cross-table composition rules
that SQLite cannot express without brittle triggers.

## Loading and runtime boundary

The storage side resolves the logical wide row with typed joins:

```sql
SELECT
    t.thing_id,
    t.name,
    t.enabled,
    d.definition_id,
    d.name AS definition_name,
    s.width,
    s.height,
    a.color_argb,
    x.x,
    x.y,
    x.rotation,
    m.travel_x,
    m.travel_y,
    m.speed
FROM things t
JOIN thing_definitions d USING(definition_id)
JOIN rectangle_shapes s USING(definition_id)
JOIN solid_color_art a USING(definition_id)
JOIN transforms x USING(thing_id)
JOIN ping_pong_motors m USING(thing_id)
WHERE d.type_key = 'moving-platform';
```

The query produces a storage-neutral moving-platform initialization record. `App2d`, which
already composes `App2d.Levels` and `App2d.Gameplay`, translates that record into the public
gameplay construction input. `App2d.Gameplay` does not reference SQLite or
`App2d.Levels`.

The vertical does not introduce a new shared model project. A second thing type can provide
evidence that shared contracts justify one later.

## Editor workflow

Creation, placement, inspection, and deletion are part of this vertical because the old
hard-coded instances are deliberately discarded.

### UI boundary

The editor does not grow a general UI toolkit on top of SkiaSharp. Skia remains responsible
for the game viewport and game-shaped interaction: palette previews, selection outlines,
placement ghosts, paths, and draggable handles. The existing Skia sidebar may contain only
simple, fixed click targets such as mode, palette, Place, Properties, and Delete.

Structured editing uses the WinForms `PropertyGrid` already available to the host application.
The Things mode shows a native property panel for the selected definition or instance. The
grid edits a temporary, type-specific property object; Apply validates that object and saves
all affected component rows in one transaction, while Cancel discards it. A property setter
does not write directly to SQLite or mutate the running thing.

The first property objects expose only ordinary scalar properties supported well by the stock
grid. Vectors are flattened into fields such as position X, position Y, travel X, and travel Y.
Standard metadata such as category, display name, description, and ordering is fine. Custom
Skia text fields, focus handling, widget trees, layout engines, generic database-driven forms,
custom property editors, and vector type converters are explicitly out of scope. If a future
field truly needs a richer editor, it can use a small typed WinForms dialog without changing
this boundary.

The editor adds a **Things** section beside the existing tile workflow. Its first and only
registered type is **Moving platform**.

Definition palette workflow:

1. Choose **New moving platform definition**.
2. Edit the proposed name, rectangle width and height, and solid color in the property grid.
3. Apply it into the level-local definition palette.
4. Select an existing definition to place or edit it.

A referenced definition cannot be deleted. The editor may offer duplicate as a convenience,
but definition inheritance and per-instance definition overrides remain out of scope.

Placement workflow:

1. Select a moving-platform definition.
2. Choose **Place**.
3. Click in the world to create a thing at the cursor, snapped to the tile grid by default.
4. The new instance receives a horizontal three-tile travel vector and a positive default
   speed; both are immediately editable.
5. Placement, transform, and motor rows commit in one transaction.

Selection and deletion:

- Clicking a platform or either motor endpoint selects its thing.
- Delete removes the thing after confirmation; transform and motor rows cascade.
- Deleting a selected thing is one undoable editor operation.
- Selection is editor state and is not persisted.

Definition form:

- name;
- rectangle width and height;
- solid color.

Instance properties:

- optional name;
- enabled;
- position;
- travel vector;
- speed.

The viewport draws a line from start to end with two handles. Moving the start handle edits
the transform while retaining the end in world space; moving the end handle edits the travel
vector. Both use the same instance-piece save path as the form. Rotation is retained in the
common transform schema but hidden for moving platforms until runtime support exists.

Each piece owns its fields and validation. The moving-platform type decides which piece
properties appear and their order by constructing the typed property object. There is no
database-driven generic form language in this vertical.

## Migration and removal of generated content

The database format advances once to add the stable spine and the four component tables.
The migration creates empty tables and leaves every existing tile chunk unchanged. It does
not create a platform definition or instance.

The same change removes `CreateMovingPlatforms` and its four hard-coded calls from
`SideScrollerLevel2D`. Loading a level with no moving-platform things therefore creates no
moving platforms. There is no fallback generator and no automatic content import.

New full level bakes create the thing schema but begin with an empty thing layer. Normal
gameplay remains read-only and never performs schema or content migration.

Changing the motor later is intentionally ordinary SQL work. Its migration rebuilds only
`ping_pong_motors` (and any motor-owned child tables), converts the data that still has
meaning, updates its typed loader and editor fields, and leaves definitions, transforms,
art, and unrelated thing types untouched.

## Tests

- Schema migration creates all thing tables without changing existing tile chunks.
- An empty upgraded thing layer loads as zero moving platforms.
- A definition and manually placed instance round-trip through SQLite.
- Missing required definition or instance pieces fail with the relevant IDs and type key.
- Foreign keys reject orphaned pieces and restrict deleting a referenced definition.
- Deleting a thing cascades its transform and motor rows.
- Creating a definition and placing a thing commits all required rows atomically.
- Runtime construction produces a `MovingPlatform2D` with the manually authored values.
- Deleting a thing is undoable and cascades its transform and motor rows.
- A referenced definition cannot be deleted; an unreferenced definition cascades its pieces.
- A copied authored database loads read-only without changing its bytes.

## Explicitly deferred

- generic definition-to-instance overrides;
- a generic component or property table;
- reflection-derived schemas or forms;
- player spawn, goal, enemies, props, encounters, and cross-thing links;
- shared paths or paths with more than two points;
- saved runtime state;
- a separate shared level-model assembly;
- per-component migration registries.

These are added only when another concrete type demonstrates the need.
