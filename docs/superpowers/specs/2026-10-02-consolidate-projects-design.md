# Consolidate projects and remove rollback

Date: 2026-10-02

## Goal

Reduce the solution from 15 projects to 5 and delete the local rollback machinery that was
built for a networking plan that has since been dropped. Game behaviour does not change.
Namespaces do not change. Every test that is not a rollback test keeps passing.

This is sub-project A of two. Sub-project B (collapsing the session frame, snapshot, and
observation-record layer so presentation reads simulation objects directly) gets its own
spec after this lands.

## Non-goals

- No namespace renames. `App2d.Rendering`, `App2d.Gameplay`, and the rest stay as they are
  even though their files move into other projects.
- No change to `SessionFrame2D`, `SessionSnapshot2D`, `SessionClient2D`, `PlayerInput2D`,
  input validation, event stamping, or the `*State2D` observation records. Those are B.
- No deletion of Noodle, ClothLab, CharacterStudio, or the diagnostics smokes.
- No behaviour change in the running game or the editor.

## Baseline

`dotnet test App2d.slnx` on commit `3448b4eb` passes 1,282 tests with none failing:
Gameplay.Tests 280, App2d.Tests 867, Presentation.Tests 135.

## Target layout

| Project | Role | Absorbs | Target framework | Package refs |
| --- | --- | --- | --- | --- |
| `App2d.Core` | engine | `App2d.Rendering`, `App2d.Audio`, `App2d.Tiles` | `net10.0-windows10.0.19041.0`, `UseWindowsForms` | MonoGame.Framework.WindowsDX 3.8.4.1, NAudio 3.0.1, NVorbis 0.10.4 |
| `App2d` | game exe | `App2d.Contracts`, `App2d.Gameplay`, `App2d.Presentation`, `App2d.Levels` | unchanged | Microsoft.Data.Sqlite 10.0.11 |
| `App2d.Tests` | all tests | `App2d.Gameplay.Tests`, `App2d.Presentation.Tests` | unchanged | unchanged |
| `App2d.CharacterStudio` | editor exe | nothing | unchanged | ImGui.NET (unchanged) |
| `App2d.Noodle` | rig experiments | nothing | unchanged | none |

`tools/ClothLab` stays as is and keeps referencing `App2d.Core`.

Project references after the change:

- `App2d` references `App2d.Core`.
- `App2d.Tests` references `App2d` and `App2d.Core`.
- `App2d.CharacterStudio` references `App2d.Core`.
- `App2d.Noodle` references `App2d.Core`.

`Directory.Build.targets` is deleted. Nothing replaces it.

## File moves

Every move is a `git mv` of the whole project directory's `.cs` tree into a folder named
after the old project. `bin`, `obj`, and the old `.csproj` are not moved; the old
directories are deleted once empty.

| From | To |
| --- | --- |
| `App2d.Rendering/**/*.cs` | `App2d.Core/Rendering/**` |
| `App2d.Audio/**/*.cs` | `App2d.Core/Audio/**` |
| `App2d.Tiles/**/*.cs` | `App2d.Core/Tiles/**` |
| `App2d.Contracts/**/*.cs` | `App2d/Contracts/**` |
| `App2d.Gameplay/**/*.cs` | `App2d/Gameplay/**` (merges with the existing `App2d/Gameplay/Player` and `App2d/Gameplay/World` folders; no file-name clashes) |
| `App2d.Gameplay/World/KINEMATICS.md` | `App2d/Gameplay/World/KINEMATICS.md` |
| `App2d.Presentation/**/*.cs` | `App2d/Presentation/**` |
| `App2d.Levels/**/*.cs` | `App2d/Levels/**` |
| `App2d.Gameplay.Tests/**/*.cs` | `App2d.Tests/Gameplay/**` |
| `App2d.Presentation.Tests/**/*.cs` | `App2d.Tests/Presentation/**` |

One file changes namespace: `App2d.Gameplay/Entities/AuthoredArena.cs` moves to
`App2d.Core/Characters/Authored/AuthoredArena.cs` and its namespace becomes
`App2d.Core.Characters.Authored`. It depends only on Core already. CharacterStudio uses it,
and CharacterStudio must not reference the game exe. The `using App2d.Gameplay.Entities;`
lines in CharacterStudio (`ArenaDrawing.cs`, `Editor/ArenaTest.cs`, `Proof.EntityProof.cs`)
and in `AuthoredArenaTests.cs` change to `using App2d.Core.Characters.Authored;` or are
dropped where that using already exists. No other file imports that namespace. Its nested
`Actor` class and the `Arena*` record structs have no name clash in the destination namespace.

`tools/ClothLab/ClothLab.csproj` retargets to `net10.0-windows10.0.19041.0` because a plain
`net10.0` project cannot reference the Windows-targeted Core. It stays a console `Exe`.

The game project is a WinForms project, so its implicit usings include `System.Drawing` and
`System.Windows.Forms`. The moved Gameplay, Presentation, Contracts, and Levels code was
checked: no file uses `Color`, `Point`, `Rectangle`, or `Size` unqualified alongside an XNA
using, and no moved type shares a name with a WinForms or Drawing type. The same check holds
for Rendering, Audio, and Tiles moving into Core.

## The engine owns the host

Today there are three startup paths. The game boots through a WinForms host in the exe
(`GameHost`, `Game2D`, `GraphicsSurface2D`, `InputState`, `Input/*`, `DeveloperConsole`,
`DeveloperConsoleView`, `AssetPaths`). Noodle has three hand-rolled WinForms hosts and a
copy-pasted `GraphicsSurface2D`. CharacterStudio uses MonoGame's own `Game` class with an
ImGui layer. Those host files depend only on Core and Rendering, so they move into the engine:

| From | To | Namespace |
| --- | --- | --- |
| `App2d/GameHost.cs`, `App2d/Game2D.cs`, `App2d/InputState.cs`, `App2d/AssetPaths.cs` | `App2d.Core/Hosting/` | `App2d` (unchanged) |
| `App2d/Input/*.cs` | `App2d.Core/Hosting/Input/` | `App2d.Input` (unchanged) |
| `App2d/Diagnostics/DeveloperConsole.cs`, `DeveloperConsoleView.cs` | `App2d.Core/Hosting/` | `App2d.Diagnostics` (unchanged) |
| `App2d/GraphicsSurface2D.cs` | `App2d.Core/Rendering/GraphicsSurface2D.cs` | `App2d.Rendering` (changed; it is now shared engine API) |

`GraphicsSurface2D` becomes `public` and gains `public DepthFormat DepthStencilFormat { get; init; } = DepthFormat.Depth24;`
which `CreateParameters` uses. `App2d.Noodle/GraphicsSurface2D.cs` is deleted; the three Noodle
hosts construct the engine's surface with `DepthStencilFormat = DepthFormat.None`, which is
the only way their copy differed.

`InputState`, `AssetPaths`, and `Game2D.OverlayControl` have `internal` members the game exe
and the tests use. Core's assembly info declares `InternalsVisibleTo("App2d")` and
`InternalsVisibleTo("App2d.Tests")` so nothing else has to change. Making those members
public is a later cleanup.

Porting Noodle's three hosts and CharacterStudio's ImGui host onto `GameHost`/`Game2D` is
sub-project C. It is real behaviour work on tools the owner still uses, so it gets its own
spec after A lands.

## Deletions

### Rollback partials (all 25)

`App2d.Core/Collision/CollisionSystem2D.Simulation.cs`,
`App2d.Core/Physics/PhysicsBody2D.Simulation.cs`,
`App2d.Core/Physics/PhysicsWorld2D.Simulation.cs`, and every `*.Simulation.cs` under
`App2d.Gameplay` (22 files: enemies, person, locomotion, rival brain, every weapon and
action, projectile, arsenal, session, tumble prop, moving platform, save point, chunk
streamer, level).

### Rollback types and files

- `App2d.Gameplay/Simulation/SessionCheckpoint2D.cs`
- `App2d.Gameplay/Simulation/SessionReplayBuffer2D.cs`
- `App2d.Gameplay/Simulation/SimulationState2D.cs`
- `App2d.Gameplay/Simulation/WorldSimulationState2D.cs`
- `Directory.Build.targets`

### Rollback members removed from files that stay

- `SideScrollerSession2D`: the `_advancing` guard stays as a plain reentrancy field moved
  into the main file; `TimelineRevision`, `RequireCheckpointBoundary`, and
  `_checkpointOwner` go. `CaptureSnapshot()` loses its `RequireCheckpointBoundary()` call
  and keeps the disposed and reentrancy checks inline. `Participant.CaptureSimulation`,
  `Participant.RestoreSimulation`, and the `ParticipantState` record go.
- `ISideScrollerSessionWorld2D`: `TerrainColliderIds`, `CaptureSimulation`,
  `ValidateSimulation`, `RestoreSimulation` go.
- `ISessionPlayerActions2D`: `CaptureSimulation`, `RestoreSimulation` go.
- `IEnemyActor2D`: `CaptureSimulation`, `RestoreSimulation` go.
- `SideScrollerSessionWorld2D`: the three forwarding members go.
- `AuthoredEntityEnemy2D`: the `Snapshot` record, `CaptureSimulation`, `RestoreSimulation` go.
- `CombatSystem2D.RestoreSimulation` and `Health2D.RestoreSimulation` go.
- `Core/Characters/Authored/EntityAnimator.cs`: `EntityAnimator.Capture`/`Restore`,
  `AnimatorState`, `ContactHold.Capture`/`Restore`, `HitLedger.Capture`/`Restore`, and
  `EntityReaction.Capture`/`Restore` are rollback-only. They go unless the build shows
  another caller. `TerrainColliderIds` on `SideScrollerLevel2D` goes if nothing else reads it.
- `EntityIdAllocator2D` and `EntityIdSequence2D` stay. They allocate IDs and are not
  rollback-specific.

### Tests

- Delete `App2d.Gameplay.Tests/Simulation/SimulationRollbackTests.cs` (438 lines, 18 test
  cases) and `PlayerSpellTests.MidHealRollbackRestoresProgressHealthEnergyAndCompletionEvents`
  (1 case). Those are the only tests whose sole purpose is rollback.
- Sixteen other test methods mix a real behaviour check with a capture/restore replay at the
  end. They are trimmed, not deleted: the rollback lines go, the behaviour assertions stay,
  and names lose their "ReplaysExactly"/"AcrossRollback" suffixes. Files:
  `App2d.Tests/Physics/OneWayPlatform2DTests.cs` (1), `AuthoredEntityEnemyTests.cs` (5),
  `BabyTriceratopsTests.cs` (2), `RockThrowerTests.cs` (3), `ShieldDefenderTests.cs` (2),
  `PlayerSpellTests.cs` (1 method plus the test world's `State` record and
  `CaptureSimulation`/`ValidateSimulation`/`RestoreSimulation` members),
  `WeaponSessionTests.cs` (1 method plus the same test world members), `VegetationTests.cs` (1).
- Expected count after step 1: 1,282 minus 19, so 1,263 (Gameplay.Tests 261, App2d.Tests 867,
  Presentation.Tests 135).
- `SessionClient2DTests`, `EnemySessionClientTests`, `ObservationRoundTripTests`, and
  `SideScrollerSession2DTests` stay. They test the frame layer, which is B.

## Project file contents

### `App2d.Core/App2d.Core.csproj`

Same as today plus: `TargetFramework` `net10.0-windows10.0.19041.0`, `UseWindowsForms`
`true`, and the three package references above. No project references.

### `App2d/App2d.csproj`

Project references reduce to `..\App2d.Core\App2d.Core.csproj`. Add
`Microsoft.Data.Sqlite 10.0.11`. The asset item groups and targets stay as they are.

### `App2d.Tests/App2d.Tests.csproj`

Project references reduce to `..\App2d\App2d.csproj` and `..\App2d.Core\App2d.Core.csproj`.

### `App2d.CharacterStudio` and `App2d.Noodle`

Project references reduce to `App2d.Core`.

### `App2d.slnx`

Lists `App2d.Core`, `App2d`, `App2d.Tests`, `App2d.Noodle`, `App2d.CharacterStudio`, and
keeps the `.editorconfig` solution item.

## Assembly info and test plumbing

- `App2d/AssemblyInfo.cs` keeps `InternalsVisibleTo("App2d.Tests")`. The Gameplay and
  Presentation `AssemblyInfo.cs` files are deleted, not moved.
- `App2d.Audio/Properties/AssemblyInfo.cs` becomes `App2d.Core/AssemblyInfo.cs` with
  `InternalsVisibleTo("App2d.Tests")` and `InternalsVisibleTo("App2d")`.
- The two identical `TestAssetPath.cs` files become one at `App2d.Tests/TestAssetPath.cs`
  in namespace `App2d.Tests`. `App2d.Tests/GlobalUsings.cs` gains `global using App2d.Tests;`
  so the moved tests in the `App2d.Gameplay.Tests.*` and `App2d.Presentation.Tests.*`
  namespaces resolve it without per-file edits.
- Moved test files drop their `using Xunit;` line because `GlobalUsings.cs` already has it.
- Test namespaces stay as they are.

## Docs

- `README.md`: the opening project-layout paragraphs describe five projects; the
  Contracts/Gameplay/Presentation boundary paragraph, the `Directory.Build.targets`
  sentence, the "Local simulation capture/restore" sentences, and the "future server /
  predicting client" wording go. The command examples stay valid.
- `docs/session-architecture.md`: a note at the top says networking was dropped on
  2026-10-01 and the boundary is no longer enforced; the project-boundaries table, the
  "Simulation capture and restore", "Capture/restore boundaries", and the rollback
  paragraphs under "Verification" are removed. The frame and observation sections stay
  until B.
- Source comments that say "a server or predicting client" in `SideScrollerGame`,
  `SideScrollerSession2D`, `SideScrollerSessionDefinition2D`, `SideScrollerSimulation2D`,
  and `EntityIdAllocator2D` are reworded to describe the local game.

## Order of work and commits

Each step ends with `dotnet build App2d.slnx` and `dotnet test App2d.slnx` green, then one
commit on `main`. Nothing is pushed. Each merge step also removes the absorbed projects from
`App2d.slnx`, because the solution cannot list a deleted project file.

1. Delete rollback code and tests in the existing project structure, and delete
   `Directory.Build.targets`. Expected test count: 1,263.
2. Merge the two test projects into App2d.Tests. Temporarily add
   `InternalsVisibleTo("App2d.Tests")` to the Gameplay, Presentation, and Audio assembly
   info files so the moved tests keep their internals access until those projects merge.
3. Merge Contracts, Gameplay, Presentation, Levels into App2d. Move `AuthoredArena` to
   Core. Update CharacterStudio and the test project references.
4. Merge Rendering, Audio, Tiles into Core. Move the host plumbing from the exe into
   `App2d.Core/Hosting` and `GraphicsSurface2D` into `App2d.Core/Rendering`. Delete Noodle's
   surface copy. Retarget Core and ClothLab. Update every csproj that referenced the three.
5. Update README, the session-architecture doc, and source comments.

The tests merge before the game and engine merges so that no soon-to-be-deleted `net10.0`
project ever has to be retargeted to reference a Windows-targeted one. Steps 2 through 4 are
moves plus csproj edits. If a step's build fails for a reason the spec did not anticipate,
the fix goes in that step's commit and is noted in the commit message.

## Verification

- `dotnet build App2d.slnx` has no errors. Warning count is not worse than baseline.
- `dotnet test App2d.slnx` reports one test assembly with the expected count and no
  failures.
- `dotnet run --project App2d -- --render-smoke <dir>` still writes its PNGs and exits
  cleanly, proving the game composes and renders without a visible window.
- `dotnet build App2d.CharacterStudio`, `dotnet build App2d.Noodle`, and
  `dotnet build tools/ClothLab/ClothLab.csproj` succeed.
- `dotnet build App2d.slnx -c Release` succeeds, because the game project has Release-only
  asset targets that a Debug build never runs.
- `git status` is clean, and `git log` shows the five commits.
- Visual Studio must reload the solution after the moves. Keep it closed, or accept its
  reload prompts, while steps 2 through 4 run.
