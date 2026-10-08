# Consolidate Projects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Collapse 15 projects into 5, delete the rollback machinery, and make the engine own the game host, without changing game behaviour or any namespace other than `GraphicsSurface2D`'s.

**Architecture:** Pure `git mv` of source trees into folders named after the old projects, csproj and slnx edits per step, and targeted deletion of the capture/restore code. Each of five steps ends with a green `dotnet build App2d.slnx` and `dotnet test App2d.slnx` and one commit on `main`. The tests merge first so no soon-to-be-deleted `net10.0` project ever has to reference a Windows-targeted one.

**Tech Stack:** .NET 10 SDK, MonoGame WindowsDX 3.8.4.1, xunit 2.9.3, Git Bash on Windows.

**Spec:** `docs/superpowers/specs/2026-10-02-consolidate-projects-design.md`

## Global Constraints

- Namespaces stay as they are, with one exception: `GraphicsSurface2D` moves to `App2d.Rendering`.
- Target frameworks: `App2d.Core`, `App2d`, `App2d.Tests`, `App2d.Noodle`, `App2d.CharacterStudio`, and `tools/ClothLab` all end on `net10.0-windows10.0.19041.0`.
- Package versions copied verbatim: MonoGame.Framework.WindowsDX 3.8.4.1, NAudio 3.0.1, NVorbis 0.10.4, Microsoft.Data.Sqlite 10.0.11, ImGui.NET 1.91.6.1, Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 2.8.2.
- Nothing in `SessionFrame2D`, `SessionSnapshot2D`, `SessionClient2D`, `PlayerInput2D`, input validation, event stamping, or the `*State2D` records changes. That is sub-project B.
- Noodle, ClothLab, CharacterStudio, and the diagnostics smokes are not deleted.
- Commits go on `main` with the trailer `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. Nothing is pushed.
- Baseline before Task 1: 1,282 tests pass (Gameplay.Tests 280, App2d.Tests 867, Presentation.Tests 135). After Task 1: 1,263 (261, 867, 135). After Task 2 onward: 1,263 in one assembly.
- Every `git mv` of a directory is preceded by `rm -rf` of that directory's `bin` and `obj`. A nested `obj` folder would otherwise be globbed into the absorbing project and produce duplicate `AssemblyInfo` attributes.
- Visual Studio must be closed, or its reload prompts accepted, while Tasks 2 through 4 run.

## Review Focus

- A Release build of the game exe: it has Release-only asset copy targets that Debug never runs. Pinned in Task 5 step 7.
- The game composing and rendering end to end after the host moved into Core: `--render-smoke` builds a `SideScrollerGame` inside `GameHost` headlessly. Pinned in Task 5 step 6.
- Noodle starting with the engine's `GraphicsSurface2D` and `DepthFormat.None`: a wrong depth format would show as a device creation failure on the first paint. Pinned in Task 4 step 11 as a build plus a manual launch note.
- ClothLab still building after Core goes Windows-only. Pinned in Task 4 step 11.
- Rename detection: `git log --follow` on a moved file must show its old history. Pinned in Task 4 step 12.

---

### Task 1: Delete the rollback machinery

**Files:**
- Delete: the 25 `*.Simulation.cs` files, `App2d.Gameplay/Simulation/SessionCheckpoint2D.cs`, `App2d.Gameplay/Simulation/SessionReplayBuffer2D.cs`, `App2d.Gameplay/Simulation/SimulationState2D.cs`, `App2d.Gameplay/Simulation/WorldSimulationState2D.cs`, `Directory.Build.targets`, `App2d.Gameplay.Tests/Simulation/SimulationRollbackTests.cs`
- Modify: `App2d.Gameplay/Simulation/SideScrollerSession2D.cs`, `App2d.Gameplay/Simulation/ISideScrollerSessionWorld2D.cs`, `App2d.Gameplay/Simulation/ISessionPlayerActions2D.cs`, `App2d.Gameplay/Enemies/IEnemyActor2D.cs`, `App2d.Gameplay/World/SideScrollerSessionWorld2D.cs`, `App2d.Gameplay/Enemies/AuthoredEntityEnemy2D.cs`, `App2d.Gameplay/Combat/CombatSystem2D.cs`, `App2d.Gameplay/Combat/Health2D.cs`, `App2d.Core/Characters/Authored/EntityAnimator.cs`, `App2d.Core/Characters/Authored/EntityReaction.cs`
- Modify tests: `App2d.Tests/Physics/OneWayPlatform2DTests.cs`, `App2d.Gameplay.Tests/Enemies/AuthoredEntityEnemyTests.cs`, `App2d.Gameplay.Tests/Enemies/BabyTriceratopsTests.cs`, `App2d.Gameplay.Tests/Enemies/RockThrowerTests.cs`, `App2d.Gameplay.Tests/Enemies/ShieldDefenderTests.cs`, `App2d.Gameplay.Tests/Persons/PlayerSpellTests.cs`, `App2d.Gameplay.Tests/Simulation/WeaponSessionTests.cs`, `App2d.Gameplay.Tests/World/VegetationTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: `SideScrollerSession2D` with no `CaptureCheckpoint`, `RestoreCheckpoint`, `ValidateCheckpoint`, or `TimelineRevision`; `ISideScrollerSessionWorld2D` and `ISessionPlayerActions2D` without capture members. Later tasks move these files but do not edit them.

- [ ] **Step 1: Delete the rollback files**

```bash
cd /c/Users/eonei/source/App2d
git rm $(find . -name "*.Simulation.cs" -not -path "*/obj/*" -not -path "*/bin/*")
git rm App2d.Gameplay/Simulation/SessionCheckpoint2D.cs App2d.Gameplay/Simulation/SessionReplayBuffer2D.cs App2d.Gameplay/Simulation/SimulationState2D.cs App2d.Gameplay/Simulation/WorldSimulationState2D.cs
git rm Directory.Build.targets
git rm App2d.Gameplay.Tests/Simulation/SimulationRollbackTests.cs
```

Expected: 31 files removed from the index (25 partials, 4 simulation types, the targets file, the test file).

- [ ] **Step 2: Edit `SideScrollerSession2D.cs`**

Four edits in `App2d.Gameplay/Simulation/SideScrollerSession2D.cs`.

Add the reentrancy field after `private long _eventSequence;`:

```csharp
    private long _eventSequence;
    private bool _advancing;
```

Replace the body of `CaptureSnapshot`:

```csharp
    public SessionSnapshot2D CaptureSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(_advancing, "Snapshots require a completed simulation tick.");
        return new(Tick, CapturePlayers(), CaptureContent(), CaptureWorld(), CaptureEnemies());
    }
```

Delete the two `TimelineRevision++;` lines, one in `SetPaused` after `if (paused == IsPaused) return;` and one in `Advance` after `_advancing = true;`.

In the nested `Participant` class delete `CaptureSimulation` and `RestoreSimulation` (the block from `public ParticipantState CaptureSimulation()` through the closing brace of `RestoreSimulation`), and delete the `internal sealed record ParticipantState(...)` declaration at the bottom of the file. Also change the class summary comment from "The same type runs on a server and inside a predicting client." to "It is the one place the local game advances gameplay."

- [ ] **Step 3: Edit the two session interfaces and the enemy actor interface**

`App2d.Gameplay/Simulation/ISideScrollerSessionWorld2D.cs`: delete these four members.

```csharp
    ImmutableArray<int> TerrainColliderIds => [];
    WorldSimulationState2D CaptureSimulation() => throw new NotSupportedException("This world does not support rollback.");
    void ValidateSimulation(WorldSimulationState2D state) => throw new NotSupportedException("This world does not support rollback.");
    void RestoreSimulation(WorldSimulationState2D state) => throw new NotSupportedException("This world does not support rollback.");
```

`App2d.Gameplay/Simulation/ISessionPlayerActions2D.cs`: delete these two members.

```csharp
    SimulationState2D CaptureSimulation() => throw new NotSupportedException("This participant does not support rollback.");
    void RestoreSimulation(SimulationState2D state) => throw new NotSupportedException("This participant does not support rollback.");
```

`App2d.Gameplay/Enemies/IEnemyActor2D.cs`: delete the same two members and the line `using App2d.Gameplay.Simulation;`.

- [ ] **Step 4: Edit the adapters and systems that forwarded rollback**

`App2d.Gameplay/World/SideScrollerSessionWorld2D.cs`: delete lines 17 to 20.

```csharp
    public ImmutableArray<int> TerrainColliderIds => level.TerrainColliderIds;
    public WorldSimulationState2D CaptureSimulation() => level.CaptureSimulation();
    public void ValidateSimulation(WorldSimulationState2D state) => level.ValidateSimulation(state);
    public void RestoreSimulation(WorldSimulationState2D state) => level.RestoreSimulation(state);
```

`App2d.Gameplay/Enemies/AuthoredEntityEnemy2D.cs`: delete from `private sealed record Snapshot(` through the closing brace of `RestoreSimulation` (the last three members of the class). Then delete `using App2d.Gameplay.Simulation;` at the top; if the build in step 8 reports a missing type in this file, put the using back.

`App2d.Gameplay/Combat/CombatSystem2D.cs`: delete `internal void RestoreSimulation(int defeatedEnemies) => DefeatedEnemies = defeatedEnemies;`.

`App2d.Gameplay/Combat/Health2D.cs`: delete the `internal void RestoreSimulation(int current)` method (four lines plus braces).

- [ ] **Step 5: Edit the authored character runtime**

`App2d.Core/Characters/Authored/EntityAnimator.cs`:

- Delete the `public sealed record AnimatorState(...)` declaration near line 14 (it spans two lines).
- Delete `public AnimatorState Capture() => ...;` and the `public void Restore(AnimatorState state, Vector2 position, string? expression = null)` method from `EntityAnimator`.
- Delete `public ImmutableDictionary<string, Vector3> Capture() => _anchors.ToImmutableDictionary();` and the `public void Restore(IReadOnlyDictionary<string, Vector3> anchors, int facing)` method from `ContactHold`.
- Delete the `Capture` and `Restore` one-liners from `HitLedger`.

`App2d.Core/Characters/Authored/EntityReaction.cs`: delete the two lines

```csharp
    public float Capture() => Stagger;
    public void Restore(float stagger) => Stagger = stagger;
```

- [ ] **Step 6: Trim the mixed tests**

Each edit below names the method, the lines to delete, and any rename. Nothing else in these files changes.

`App2d.Tests/Physics/OneWayPlatform2DTests.cs`, method `OneWayNormalCanFaceDiagonallyOnARotatedSurface`: delete the last four statements of the method.

```csharp
        var checkpoint = world.CaptureSimulation();
        surface.OneWaySurfaceNormal = null;
        world.RestoreSimulation(checkpoint);
        Assert.InRange(Vector2.Distance(normal, surface.OneWaySurfaceNormal!.Value), 0f, 0.00001f);
```

`App2d.Gameplay.Tests/Enemies/AuthoredEntityEnemyTests.cs`:

- Class summary: change "spawning, the shared final pose, combat and rollback." to "spawning, the shared final pose and combat."
- `ContactRecoilFollowsTheHitWithoutTurningAndRestoresExactly` → rename to `ContactRecoilFollowsTheHitWithoutTurning`. Delete from `var snapshot = enemy.CaptureSimulation();` through `Assert.Equal(next, enemy.Pose.Local.Points.Values.ToArray());` (eight lines).
- `TheGuardThrustsAtThePlayerAndCombatReplaysExactly` → rename to `TheGuardThrustsAtThePlayer`. Delete `var checkpoint = game.Session.CaptureCheckpoint();`. Replace
  ```csharp
        var first = Run(); var damaged = game.Player.Health.Current;
        game.Session.RestoreCheckpoint(checkpoint); var second = Run();
        Assert.Equal(first, second);
  ```
  with
  ```csharp
        var first = Run(); var damaged = game.Player.Health.Current;
  ```
- `RivalPlacementsSpawnTheGunnerAndItsBoltsReplayExactly` → rename to `RivalPlacementsSpawnTheGunnerAndItsBolts`. Delete `var checkpoint = game.Session.CaptureCheckpoint();`. Replace
  ```csharp
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint); var second = Run();
        Assert.Equal(first, second);
  ```
  with
  ```csharp
        var first = Run();
  ```
- `CavemanCommitsHisFacingAndAHittingSwordCancelsTheSlamAcrossRollback` → rename to `CavemanCommitsHisFacingAndAHittingSwordCancelsTheSlam`. Delete `var before = enemy.CaptureSimulation();` and `var after = enemy.CaptureSimulation();`. Insert `Assert.Equal(6, enemy.Health.Current);` directly after the `Assert.Empty(enemy.GetActiveAttackHitboxes());` that follows `Assert.Equal("hit", enemy.CaptureState().ActionId);`. Delete the last six statements:
  ```csharp
        enemy.RestoreSimulation(before);
        Assert.Equal("attack", enemy.CaptureState().ActionId);
        Assert.Equal(9, enemy.Health.Current);
        enemy.RestoreSimulation(after);
        Assert.Equal("hit", enemy.CaptureState().ActionId);
        Assert.Equal(6, enemy.Health.Current);
  ```
- `CavemanPlacementPursuesOnTerrainAndReplaysExactly` → rename to `CavemanPlacementPursuesOnTerrain`. Delete `var checkpoint = game.Session.CaptureCheckpoint();`. Replace
  ```csharp
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint);
        Assert.Equal(first, Run());
  ```
  with
  ```csharp
        Run();
  ```

`App2d.Gameplay.Tests/Enemies/BabyTriceratopsTests.cs`:

- `LedgesAndWallsStopTheChargeAndDisableItsHitRegion`: replace the last three statements
  ```csharp
        var snapshot = enemy.CaptureSimulation();
        Tick(enemy, physics, player.Position, 10); enemy.RestoreSimulation(snapshot);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
  ```
  with
  ```csharp
        Tick(enemy, physics, player.Position, 10);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
  ```
- `PlacementAndChargeReplayExactlyInTheRealTerrainSimulation` → rename to `PlacementAndChargeRunInTheRealTerrainSimulation`. Delete `var checkpoint = game.Session.CaptureCheckpoint();`. Replace
  ```csharp
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint);
        Assert.Equal(first, Run());
  ```
  with
  ```csharp
        Run();
  ```

`App2d.Gameplay.Tests/Enemies/RockThrowerTests.cs`:

- `RockReleasesAtTheMarkerAndTargetsTheCommittedPosition`: delete the last four statements.
  ```csharp
        var snapshot = enemy.CaptureSimulation();
        enemy.TakeDamage(1, Vector2.Zero);
        enemy.RestoreSimulation(snapshot);
        Assert.Equal(rock, Assert.Single(enemy.CaptureState().Bolts));
  ```
- `ClosePlayerGetsOneShortRetreatThenAPauseThenAThrow`: delete `var checkpoint = enemy.CaptureSimulation();` and the last five statements.
  ```csharp
        var state = enemy.CaptureState();
        enemy.RestoreSimulation(checkpoint);
        Tick(enemy, player.Position, 125);
        Assert.Equal(state.ActionSeconds, enemy.CaptureState().ActionSeconds);
        Assert.Equal(state.IsAttacking, enemy.CaptureState().IsAttacking);
  ```
- `SoloAndPairedEncountersSpawnAndReplayTheirProjectiles` → rename to `SoloAndPairedEncountersSpawnTheirProjectiles`. Delete `var checkpoint = game.Session.CaptureCheckpoint();`. Replace
  ```csharp
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint);
        Assert.Equal(first, Run());
  ```
  with
  ```csharp
        Run();
  ```

`App2d.Gameplay.Tests/Enemies/ShieldDefenderTests.cs`:

- `FrontContactBlocksOutsideTheBodyAndDeduplicatesWithoutDamageFeedback`: delete `var snapshot = enemy.CaptureSimulation();` and the last two statements.
  ```csharp
        enemy.RestoreSimulation(snapshot);
        Assert.True(Strike()); Assert.Equal(8, enemy.Health.Current);
  ```
- `PlacementAndBashReplayExactly` (line 136) → rename to `PlacementAndBashRunInTheRealTerrainSimulation`. Delete `var checkpoint = game.Session.CaptureCheckpoint();`. Replace
  ```csharp
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint); Assert.Equal(first, Run());
  ```
  with
  ```csharp
        Run();
  ```

`App2d.Gameplay.Tests/Persons/PlayerSpellTests.cs`:

- `FractionalRechargeSurvivesRollbackAndPauseAndResetsOnRespawn` → rename to `FractionalRechargeSurvivesPauseAndResetsOnRespawn` with this body:
  ```csharp
        using var f = new Fixture(new() { StartingEnergy = 0, EnergyPerSecond = 7 });
        f.Steps(50);
        f.Session.SetPaused(true);
        Assert.Equal(2, f.Spells.Energy);
        f.Session.SetPaused(false);
        f.Steps(200);
        f.Player.Reset(Vector2.Zero);
        f.Steps(17);
        Assert.Equal(0, f.Spells.Energy);
        f.Step(default);
        Assert.Equal(1, f.Spells.Energy);
  ```
- Delete the whole method `MidHealRollbackRestoresProgressHealthEnergyAndCompletionEvents` including its `[Fact]` attribute (lines 182 to 199).
- In the nested `EmptyWorld` class delete the `State` record and the three members:
  ```csharp
        private sealed record State : WorldSimulationState2D
        {
            public override System.Collections.Immutable.ImmutableArray<int> TerrainColliderIds => [];
        }
        public WorldSimulationState2D CaptureSimulation() => new State();
        public void ValidateSimulation(WorldSimulationState2D state) => Assert.IsType<State>(state);
        public void RestoreSimulation(WorldSimulationState2D state) => ValidateSimulation(state);
  ```

`App2d.Gameplay.Tests/Simulation/WeaponSessionTests.cs`:

- `SnapshotPreservesShotPhaseAfterRecoveryAndAcrossRollback` → rename to `SnapshotPreservesShotPhaseAfterRecovery`. Delete the last seven statements.
  ```csharp
        var checkpoint = game.Session.CaptureCheckpoint();
        var next = game.Step();
        game.Session.RestoreCheckpoint(checkpoint);
        var replayed = game.Step();
        Assert.Equal(next.Players[0].Person.Action, replayed.Players[0].Person.Action);
        Assert.Equal(snapshot.Tick + 1, replayed.Tick);
        Assert.Empty(replayed.Events.OfType<AttackStarted2D>());
  ```
- In the nested `EmptyWorld` class delete the `EmptyState` record and the three members, exactly as in `PlayerSpellTests` but with the name `EmptyState`.

`App2d.Gameplay.Tests/World/VegetationTests.cs`, method `CutsAreIdempotentForgottenOnUnloadAndRestoreWithTheSession` → rename to `CutsAreIdempotentAndForgottenOnUnload`. Delete `var before = session.CaptureCheckpoint();`, `var after = session.CaptureCheckpoint();`, and the last four statements.

```csharp
        session.RestoreCheckpoint(before);
        Assert.Empty(session.CaptureSnapshot().World.CutGrass);
        session.RestoreCheckpoint(after);
        Assert.Contains(cell, session.CaptureSnapshot().World.CutGrass);
```

- [ ] **Step 7: Confirm nothing still names the deleted APIs**

```bash
cd /c/Users/eonei/source/App2d
grep -rnE "CaptureCheckpoint|RestoreCheckpoint|ValidateCheckpoint|CaptureSimulation|RestoreSimulation|ValidateSimulation|SessionReplayBuffer2D|TimelineRevision|RequireCheckpointBoundary|SimulationState2D|WorldSimulationState2D|PhysicsWorldState2D|CollisionOrderState2D|AnimatorState|TerrainColliderIds" --include="*.cs" . | grep -v "/obj/" | grep -v "/bin/"
```

Expected: no output.

- [ ] **Step 8: Build and test**

```bash
cd /c/Users/eonei/source/App2d && dotnet build App2d.slnx --nologo -v q 2>&1 | grep -E "error|Build succeeded|Warn" | head -20
```

Expected: `Build succeeded.` and no `error` lines. Unused-using warnings are acceptable.

```bash
cd /c/Users/eonei/source/App2d && dotnet test App2d.slnx --nologo -v q 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected three lines: Gameplay.Tests `Total:   261`, App2d.Tests `Total:   867`, Presentation.Tests `Total:   135`, each with `Failed:     0`.

- [ ] **Step 9: Commit**

```bash
cd /c/Users/eonei/source/App2d && git add -A && git commit -q -F - <<'EOF'
Remove rollback checkpoints, replay and the project boundary check

Networking was dropped, so the local rollback machinery it was built for goes:
all *.Simulation.cs capture/restore partials, SessionCheckpoint2D,
SessionReplayBuffer2D, the simulation-state base records, physics and collision
order capture, and Directory.Build.targets. Rollback-only tests are deleted and
mixed tests keep their behaviour assertions.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
EOF
git log --oneline -1
```

---

### Task 2: Merge the two test projects into App2d.Tests

**Files:**
- Move: `App2d.Gameplay.Tests/**` → `App2d.Tests/Gameplay/**`, `App2d.Presentation.Tests/**` → `App2d.Tests/Presentation/**`
- Delete: `App2d.Gameplay.Tests/App2d.Gameplay.Tests.csproj`, `App2d.Presentation.Tests/App2d.Presentation.Tests.csproj`, `App2d.Presentation.Tests/TestAssetPath.cs`
- Modify: `App2d.Tests/App2d.Tests.csproj`, `App2d.Tests/GlobalUsings.cs`, `App2d.Tests/TestAssetPath.cs`, `App2d.Gameplay/AssemblyInfo.cs`, `App2d.Presentation/AssemblyInfo.cs`, `App2d.Audio/Properties/AssemblyInfo.cs`, `App2d.slnx`

**Interfaces:**
- Consumes: Task 1's green tree.
- Produces: one test project at `App2d.Tests` that Tasks 3 and 4 re-point. `TestAssetPath` lives in namespace `App2d.Tests` and is visible everywhere through a global using.

- [ ] **Step 1: Move the files**

```bash
cd /c/Users/eonei/source/App2d
git rm App2d.Gameplay.Tests/App2d.Gameplay.Tests.csproj App2d.Presentation.Tests/App2d.Presentation.Tests.csproj
git mv App2d.Gameplay.Tests/TestAssetPath.cs App2d.Tests/TestAssetPath.cs
git rm App2d.Presentation.Tests/TestAssetPath.cs
rm -rf App2d.Gameplay.Tests/bin App2d.Gameplay.Tests/obj App2d.Presentation.Tests/bin App2d.Presentation.Tests/obj
git mv App2d.Gameplay.Tests App2d.Tests/Gameplay
git mv App2d.Presentation.Tests App2d.Tests/Presentation
ls App2d.Gameplay.Tests App2d.Presentation.Tests 2>&1 | head -2
```

Expected: the final `ls` reports both directories as not found.

- [ ] **Step 2: Make the shared asset path helper visible everywhere**

In `App2d.Tests/TestAssetPath.cs` change `namespace App2d.Gameplay.Tests;` to `namespace App2d.Tests;`.

Replace `App2d.Tests/GlobalUsings.cs` with:

```csharp
global using App2d.Tests;
global using Xunit;
```

Remove the now-redundant per-file xunit usings from the moved tests:

```bash
cd /c/Users/eonei/source/App2d && find App2d.Tests/Gameplay App2d.Tests/Presentation -name '*.cs' -exec sed -i '/^using Xunit;$/d' {} + && grep -rl "^using Xunit;" App2d.Tests | wc -l
```

Expected: `0`.

- [ ] **Step 3: Point the test project at every assembly the moved tests use**

Replace `App2d.Tests/App2d.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\App2d\App2d.csproj" />
    <ProjectReference Include="..\App2d.Core\App2d.Core.csproj" />
    <ProjectReference Include="..\App2d.Tiles\App2d.Tiles.csproj" />
    <ProjectReference Include="..\App2d.Levels\App2d.Levels.csproj" />
    <ProjectReference Include="..\App2d.Rendering\App2d.Rendering.csproj" />
    <ProjectReference Include="..\App2d.Audio\App2d.Audio.csproj" />
    <ProjectReference Include="..\App2d.Contracts\App2d.Contracts.csproj" />
    <ProjectReference Include="..\App2d.Gameplay\App2d.Gameplay.csproj" />
    <ProjectReference Include="..\App2d.Presentation\App2d.Presentation.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Grant the merged test assembly the internals the moved tests used**

Replace `App2d.Gameplay/AssemblyInfo.cs` with:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("App2d.Tests")]
```

Replace `App2d.Presentation/AssemblyInfo.cs` with the same two lines. Replace `App2d.Audio/Properties/AssemblyInfo.cs` with the same two lines.

- [ ] **Step 5: Drop the two test projects from the solution**

In `App2d.slnx` delete these two lines:

```xml
  <Project Path="App2d.Presentation.Tests/App2d.Presentation.Tests.csproj" />
  <Project Path="App2d.Gameplay.Tests/App2d.Gameplay.Tests.csproj" />
```

- [ ] **Step 6: Build and test**

```bash
cd /c/Users/eonei/source/App2d && dotnet build App2d.slnx --nologo -v q 2>&1 | grep -E "error|Build succeeded" | head -20
```

Expected: `Build succeeded.` If the build reports CS0104 ambiguity or CS0246 missing type inside `App2d.Tests/Gameplay` or `App2d.Tests/Presentation`, the cause is a name that both `App2d.Tests` and the file's own namespace define; fix it by fully qualifying that one reference, and note it in the commit message.

```bash
cd /c/Users/eonei/source/App2d && dotnet test App2d.slnx --nologo -v q 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: one line, `Passed!  - Failed:     0, Passed:  1263, Skipped:     0, Total:  1263`.

- [ ] **Step 7: Commit**

```bash
cd /c/Users/eonei/source/App2d && git add -A && git commit -q -F - <<'EOF'
Merge the gameplay and presentation test projects into App2d.Tests

The two extra test projects only existed to keep gameplay tests off Windows for
a server that is no longer planned. Files move under App2d.Tests/Gameplay and
App2d.Tests/Presentation with their namespaces unchanged; one TestAssetPath
helper serves all of them through a global using.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
EOF
git log --oneline -1
```

---

### Task 3: Merge Contracts, Gameplay, Presentation and Levels into the game project

**Files:**
- Move: `App2d.Gameplay/Entities/AuthoredArena.cs` → `App2d.Core/Characters/Authored/AuthoredArena.cs`; `App2d.Contracts/**` → `App2d/Contracts/**`; `App2d.Gameplay/**` → `App2d/Gameplay/**`; `App2d.Presentation/**` → `App2d/Presentation/**`; `App2d.Levels/**` → `App2d/Levels/**`
- Delete: the four absorbed `.csproj` files, `App2d.Gameplay/AssemblyInfo.cs`, `App2d.Presentation/AssemblyInfo.cs`
- Modify: `App2d/App2d.csproj`, `App2d.Tests/App2d.Tests.csproj`, `App2d.CharacterStudio/App2d.CharacterStudio.csproj`, `App2d.slnx`, `App2d.CharacterStudio/ArenaDrawing.cs`, `App2d.CharacterStudio/Editor/ArenaTest.cs`, `App2d.CharacterStudio/Proof.EntityProof.cs`, `App2d.Tests/Gameplay/Entities/AuthoredArenaTests.cs`

**Interfaces:**
- Consumes: Task 2's single test project.
- Produces: `App2d.Core.Characters.Authored.AuthoredArena` (same members as before, new namespace). The game exe now contains namespaces `App2d.Contracts.*`, `App2d.Gameplay.*`, `App2d.Presentation.*`, `App2d.Levels`.

- [ ] **Step 1: Move AuthoredArena into Core**

```bash
cd /c/Users/eonei/source/App2d
git mv App2d.Gameplay/Entities/AuthoredArena.cs App2d.Core/Characters/Authored/AuthoredArena.cs
rmdir App2d.Gameplay/Entities
sed -i 's/^namespace App2d\.Gameplay\.Entities;$/namespace App2d.Core.Characters.Authored;/; /^using App2d\.Core\.Characters\.Authored;$/d' App2d.Core/Characters/Authored/AuthoredArena.cs
sed -i '/^using App2d\.Gameplay\.Entities;$/d' App2d.CharacterStudio/ArenaDrawing.cs App2d.CharacterStudio/Editor/ArenaTest.cs App2d.Tests/Gameplay/Entities/AuthoredArenaTests.cs
sed -i 's/^using App2d\.Gameplay\.Entities;$/using App2d.Core.Characters.Authored;/' App2d.CharacterStudio/Proof.EntityProof.cs
grep -rn "Gameplay.Entities" --include="*.cs" . | grep -v "/obj/"
```

Expected: the final grep prints nothing. (`ArenaDrawing.cs`, `ArenaTest.cs`, and `AuthoredArenaTests.cs` already import `App2d.Core.Characters.Authored`; `Proof.EntityProof.cs` did not.)

- [ ] **Step 2: Move the four projects' sources**

```bash
cd /c/Users/eonei/source/App2d
git rm App2d.Contracts/App2d.Contracts.csproj App2d.Gameplay/App2d.Gameplay.csproj App2d.Presentation/App2d.Presentation.csproj App2d.Levels/App2d.Levels.csproj
git rm App2d.Gameplay/AssemblyInfo.cs App2d.Presentation/AssemblyInfo.cs
rm -rf App2d.Contracts/bin App2d.Contracts/obj App2d.Gameplay/bin App2d.Gameplay/obj App2d.Presentation/bin App2d.Presentation/obj App2d.Levels/bin App2d.Levels/obj
git mv App2d.Contracts App2d/Contracts
git mv App2d.Presentation App2d/Presentation
git mv App2d.Levels App2d/Levels
# App2d/Gameplay already exists (Player/, World/), so merge child by child.
for child in App2d.Gameplay/*; do
  name=$(basename "$child")
  if [ -d "App2d/Gameplay/$name" ]; then git mv "$child"/* "App2d/Gameplay/$name/"; rmdir "$child";
  else git mv "$child" "App2d/Gameplay/$name"; fi
done
rmdir App2d.Gameplay
ls App2d.Contracts App2d.Gameplay App2d.Presentation App2d.Levels 2>&1 | grep -c "No such file"
```

Expected: `4`. `App2d/Gameplay/World/` now holds both the old host file `ViewportTerrainSource2D.cs` and the moved simulation world files plus `KINEMATICS.md`.

- [ ] **Step 3: Rewrite the game project references**

In `App2d/App2d.csproj` replace the first `<ItemGroup>` (the seven project references) with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\App2d.Core\App2d.Core.csproj" />
    <ProjectReference Include="..\App2d.Tiles\App2d.Tiles.csproj" />
    <ProjectReference Include="..\App2d.Rendering\App2d.Rendering.csproj" />
    <ProjectReference Include="..\App2d.Audio\App2d.Audio.csproj" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.11" />
  </ItemGroup>
```

Everything else in the file (asset item groups and the three targets) stays as it is.

- [ ] **Step 4: Rewrite the test and studio references**

In `App2d.Tests/App2d.Tests.csproj` replace the project-reference `<ItemGroup>` with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\App2d\App2d.csproj" />
    <ProjectReference Include="..\App2d.Core\App2d.Core.csproj" />
    <ProjectReference Include="..\App2d.Tiles\App2d.Tiles.csproj" />
    <ProjectReference Include="..\App2d.Rendering\App2d.Rendering.csproj" />
    <ProjectReference Include="..\App2d.Audio\App2d.Audio.csproj" />
  </ItemGroup>
```

In `App2d.CharacterStudio/App2d.CharacterStudio.csproj` delete the line `<ProjectReference Include="../App2d.Gameplay/App2d.Gameplay.csproj" />`.

- [ ] **Step 5: Drop the four projects from the solution**

In `App2d.slnx` delete these four lines:

```xml
  <Project Path="App2d.Levels/App2d.Levels.csproj" />
  <Project Path="App2d.Gameplay/App2d.Gameplay.csproj" />
  <Project Path="App2d.Presentation/App2d.Presentation.csproj" />
  <Project Path="App2d.Contracts/App2d.Contracts.csproj" />
```

- [ ] **Step 6: Build and test**

```bash
cd /c/Users/eonei/source/App2d && dotnet build App2d.slnx --nologo -v q 2>&1 | grep -E "error|Build succeeded" | head -20
```

Expected: `Build succeeded.` The moved code now compiles with the exe's implicit `System.Drawing` and `System.Windows.Forms` usings. The spec checked for `Color`, `Point`, `Rectangle`, and `Size` ambiguities and found none; if a CS0104 appears anyway, add a `using X = Full.Name.X;` alias at the top of that one file.

```bash
cd /c/Users/eonei/source/App2d && dotnet test App2d.slnx --nologo -v q 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: `Passed!  - Failed:     0, Passed:  1263, Skipped:     0, Total:  1263`.

- [ ] **Step 7: Commit**

```bash
cd /c/Users/eonei/source/App2d && git add -A && git commit -q -F - <<'EOF'
Fold Contracts, Gameplay, Presentation and Levels into the game project

The compile-time wall between simulation and presentation was built for a
server that is no longer planned. The four libraries become folders of the
App2d exe with their namespaces unchanged. AuthoredArena moves into Core so
Character Studio no longer needs a gameplay reference.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
EOF
git log --oneline -1
```

---

### Task 4: Merge Rendering, Audio and Tiles into Core and move the host into the engine

**Files:**
- Move: `App2d.Rendering/**` → `App2d.Core/Rendering/**`; `App2d.Audio/**` → `App2d.Core/Audio/**`; `App2d.Tiles/**` → `App2d.Core/Tiles/**`; `App2d.Audio/Properties/AssemblyInfo.cs` → `App2d.Core/AssemblyInfo.cs`; `App2d/GameHost.cs`, `App2d/Game2D.cs`, `App2d/InputState.cs`, `App2d/AssetPaths.cs`, `App2d/Diagnostics/DeveloperConsole.cs`, `App2d/Diagnostics/DeveloperConsoleView.cs` → `App2d.Core/Hosting/`; `App2d/Input/` → `App2d.Core/Hosting/Input/`; `App2d/GraphicsSurface2D.cs` → `App2d.Core/Rendering/GraphicsSurface2D.cs`
- Delete: the three absorbed `.csproj` files, `App2d.Noodle/GraphicsSurface2D.cs`
- Modify: `App2d.Core/App2d.Core.csproj`, `App2d.Core/AssemblyInfo.cs`, `App2d.Core/Rendering/GraphicsSurface2D.cs`, `App2d/App2d.csproj`, `App2d.Tests/App2d.Tests.csproj`, `App2d.CharacterStudio/App2d.CharacterStudio.csproj`, `App2d.Noodle/App2d.Noodle.csproj`, `tools/ClothLab/ClothLab.csproj`, `App2d.Noodle/RigidCharacterHost.cs`, `App2d.Noodle/NoodleEditorHost.cs`, `App2d.Noodle/BoneEditorHost.cs`, `App2d.slnx`

**Interfaces:**
- Consumes: Task 3's game project.
- Produces: `public sealed class App2d.Rendering.GraphicsSurface2D : Control` with `public DepthFormat DepthStencilFormat { get; init; }` (default `DepthFormat.Depth24`), the events `RenderFrame` and `DeviceDisposing` unchanged. `App2d.GameHost`, `App2d.Game2D`, `App2d.InputState`, `App2d.AssetPaths`, `App2d.Diagnostics.DeveloperConsole` now live in `App2d.Core.dll`.

- [ ] **Step 1: Move the three libraries**

```bash
cd /c/Users/eonei/source/App2d
git rm App2d.Rendering/App2d.Rendering.csproj App2d.Audio/App2d.Audio.csproj App2d.Tiles/App2d.Tiles.csproj
git mv App2d.Audio/Properties/AssemblyInfo.cs App2d.Core/AssemblyInfo.cs
rmdir App2d.Audio/Properties
rm -rf App2d.Rendering/bin App2d.Rendering/obj App2d.Audio/bin App2d.Audio/obj App2d.Tiles/bin App2d.Tiles/obj
git mv App2d.Rendering App2d.Core/Rendering
git mv App2d.Audio App2d.Core/Audio
git mv App2d.Tiles App2d.Core/Tiles
ls App2d.Rendering App2d.Audio App2d.Tiles 2>&1 | grep -c "No such file"
```

Expected: `3`.

- [ ] **Step 2: Move the host plumbing**

```bash
cd /c/Users/eonei/source/App2d
mkdir -p App2d.Core/Hosting
git mv App2d/GameHost.cs App2d/Game2D.cs App2d/InputState.cs App2d/AssetPaths.cs App2d.Core/Hosting/
git mv App2d/Diagnostics/DeveloperConsole.cs App2d/Diagnostics/DeveloperConsoleView.cs App2d.Core/Hosting/
git mv App2d/Input App2d.Core/Hosting/Input
git mv App2d/GraphicsSurface2D.cs App2d.Core/Rendering/GraphicsSurface2D.cs
git rm App2d.Noodle/GraphicsSurface2D.cs
```

- [ ] **Step 3: Make the surface shared engine API**

In `App2d.Core/Rendering/GraphicsSurface2D.cs`:

- Change `namespace App2d;` to `namespace App2d.Rendering;`.
- Change `internal sealed class GraphicsSurface2D : Control` to `public sealed class GraphicsSurface2D : Control`.
- Change the summary to `/// <summary>A Direct3D-backed MonoGame surface hosted in a WinForms control. The game host and the tool windows share it.</summary>`.
- After `public event Action? DeviceDisposing;` add:
  ```csharp
    /// <summary>Depth buffer format for the swap chain. The game needs a depth buffer; flat tool views can pass <see cref="DepthFormat.None"/>.</summary>
    public DepthFormat DepthStencilFormat { get; init; } = DepthFormat.Depth24;
  ```
- In `CreateParameters` change `DepthStencilFormat = DepthFormat.Depth24,` to `DepthStencilFormat = DepthStencilFormat,`.

In each of `App2d.Noodle/RigidCharacterHost.cs`, `App2d.Noodle/NoodleEditorHost.cs`, `App2d.Noodle/BoneEditorHost.cs` change

```csharp
    private readonly GraphicsSurface2D _surface = new() { Dock = DockStyle.Fill, TabStop = true };
```

to

```csharp
    private readonly GraphicsSurface2D _surface = new() { Dock = DockStyle.Fill, TabStop = true, DepthStencilFormat = DepthFormat.None };
```

All three files already import `App2d.Rendering` and `Microsoft.Xna.Framework.Graphics`.

- [ ] **Step 4: Grant the exe and the tests the internals they already use**

Replace `App2d.Core/AssemblyInfo.cs` with:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("App2d")]
[assembly: InternalsVisibleTo("App2d.Tests")]
```

This covers `InputState`'s internal setters used by `SideScrollerGame` and the input tests, `AssetPaths` used by the game and the smokes, and `Game2D.OverlayControl` which `SideScrollerGame` overrides.

- [ ] **Step 5: Rewrite the engine project file**

Replace `App2d.Core/App2d.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisModePerformance>All</AnalysisModePerformance>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.WindowsDX" Version="3.8.4.1" />
    <PackageReference Include="NAudio" Version="3.0.1" />
    <PackageReference Include="NVorbis" Version="0.10.4" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Re-point every consumer at Core alone**

`App2d/App2d.csproj`: replace the first `<ItemGroup>` with

```xml
  <ItemGroup>
    <ProjectReference Include="..\App2d.Core\App2d.Core.csproj" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.11" />
  </ItemGroup>
```

`App2d.Tests/App2d.Tests.csproj`: replace the project-reference `<ItemGroup>` with

```xml
  <ItemGroup>
    <ProjectReference Include="..\App2d\App2d.csproj" />
    <ProjectReference Include="..\App2d.Core\App2d.Core.csproj" />
  </ItemGroup>
```

`App2d.CharacterStudio/App2d.CharacterStudio.csproj`: change `<ProjectReference Include="../App2d.Rendering/App2d.Rendering.csproj" />` to `<ProjectReference Include="../App2d.Core/App2d.Core.csproj" />`.

`App2d.Noodle/App2d.Noodle.csproj`: delete the line `<ProjectReference Include="..\App2d.Rendering\App2d.Rendering.csproj" />`.

`tools/ClothLab/ClothLab.csproj`: change `<TargetFramework>net10.0</TargetFramework>` to `<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>`.

- [ ] **Step 7: Drop the three projects from the solution**

Replace `App2d.slnx` with:

```xml
<Solution>
  <Project Path="App2d.Core/App2d.Core.csproj" />
  <Project Path="App2d/App2d.csproj" />
  <Project Path="App2d.Tests/App2d.Tests.csproj" />
  <Folder Name="/Solution Items/">
    <File Path=".editorconfig" />
  </Folder>
  <Project Path="App2d.Noodle/App2d.Noodle.csproj" />
  <Project Path="App2d.CharacterStudio/App2d.CharacterStudio.csproj" />
</Solution>
```

- [ ] **Step 8: Build**

```bash
cd /c/Users/eonei/source/App2d && dotnet build App2d.slnx --nologo -v q 2>&1 | grep -E "error|Build succeeded" | head -30
```

Expected: `Build succeeded.` Core now compiles with implicit `System.Drawing` and `System.Windows.Forms` usings. The spec checked Core, Audio, and Tiles for type names and unqualified uses that collide with those namespaces and found none. If a CS0104 appears anyway, add a `using X = Full.Name.X;` alias at the top of that one file and note it in the commit message.

- [ ] **Step 9: Test**

```bash
cd /c/Users/eonei/source/App2d && dotnet test App2d.slnx --nologo -v q 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: `Passed!  - Failed:     0, Passed:  1263, Skipped:     0, Total:  1263`.

- [ ] **Step 10: Confirm the tree has exactly five project directories plus ClothLab**

```bash
cd /c/Users/eonei/source/App2d && find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" | sort
```

Expected exactly:

```
./App2d.CharacterStudio/App2d.CharacterStudio.csproj
./App2d.Core/App2d.Core.csproj
./App2d.Noodle/App2d.Noodle.csproj
./App2d.Tests/App2d.Tests.csproj
./App2d/App2d.csproj
./tools/ClothLab/ClothLab.csproj
```

- [ ] **Step 11: Build the tools that the solution does not cover**

```bash
cd /c/Users/eonei/source/App2d && dotnet build tools/ClothLab/ClothLab.csproj --nologo -v q 2>&1 | grep -E "error|Build succeeded"
```

Expected: `Build succeeded.` Noodle and CharacterStudio are in the solution and built in step 8. Launching Noodle (`dotnet run --project App2d.Noodle`) should open its window and draw; a `DepthFormat.None` device failure would appear on first paint. This is a manual check for the owner, not a gate.

- [ ] **Step 12: Confirm git followed the renames**

```bash
cd /c/Users/eonei/source/App2d && git log --oneline --follow -- App2d.Core/Rendering/Renderer2D.cs | wc -l && git log --oneline --follow -- App2d/Gameplay/Persons/Person2D.cs | wc -l
```

Expected: both counts greater than 1. A count of 1 means the move was recorded as delete plus add and the `git mv` ordering needs checking before committing.

- [ ] **Step 13: Commit**

```bash
cd /c/Users/eonei/source/App2d && git add -A && git commit -q -F - <<'EOF'
Fold Rendering, Audio and Tiles into Core and move the game host into the engine

App2d.Core is now the whole engine: math, geometry, collision, physics, tiles,
rendering, audio, and the WinForms game host (GameHost, Game2D, InputState, the
developer console, GraphicsSurface2D). Namespaces are unchanged except that
GraphicsSurface2D is public engine API under App2d.Rendering, so Noodle drops its
copy. Core, ClothLab and every consumer target Windows.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
EOF
git log --oneline -1
```

---

### Task 5: Update the docs and comments, then prove the game still runs

**Files:**
- Modify: `README.md`, `docs/session-architecture.md`, `App2d/SideScrollerGame.cs`, `App2d/Gameplay/Simulation/SideScrollerSessionDefinition2D.cs`, `App2d/Gameplay/Simulation/SideScrollerSimulation2D.cs`, `App2d.Core/EntityIdAllocator2D.cs`

**Interfaces:**
- Consumes: the final tree from Task 4.
- Produces: nothing code-facing.

- [ ] **Step 1: Rewrite the README's project description**

Replace README lines 3 to 20 (from "A deliberately small MonoGame/XNA 2D engine skeleton" through "See [the project boundaries](...)") with:

```markdown
A small MonoGame/XNA 2D engine and the side-scroller built on it.

The solution has five projects. `App2d.Core` is the engine: mathematics, geometry,
collision, physics, tiles, rendering, audio, authored characters, and the WinForms game
host. `App2d` is the game executable: contracts, gameplay simulation, presentation,
level storage, the editor, and diagnostics. `App2d.Tests` holds every test.
`App2d.CharacterStudio` is the character editor and `App2d.Noodle` holds the rig
experiments; both build on `App2d.Core`. `tools/ClothLab` is a small cape study.
Everything targets `net10.0-windows10.0.19041.0`.

Within the game project, `Contracts` owns commands, immutable observations/events, and
shared configuration; `Gameplay` owns the simulation; `Presentation` owns views, camera,
HUD, and sound selection; `Levels` owns SQLite level storage. Those are folders and
namespaces, not assemblies. Networking was considered and dropped on 2026-10-01; the
remaining frame/observation layer is described in
[the session architecture](docs/session-architecture.md).
```

- [ ] **Step 2: Fix the README's remaining project mentions**

Apply these exact substitutions in `README.md`:

- Line 32: `` `App2d.Rendering` contains the renderer and shader abstractions. `` → `` `App2d.Core/Rendering` contains the renderer, shader abstractions, and the `GraphicsSurface2D` WinForms host surface. ``
- Line 34: `` `App2d.Tiles` presents the tile maps and mesher. `` → `` `App2d.Core/Tiles` presents the tile maps and mesher. ``
- Line 37: `` - `App2d.Levels` stores authored levels as SQLite files. `` → `` - `App2d/Levels` stores authored levels as SQLite files. ``
- Line 42: `` dependency is limited to `App2d.Contracts`. `` → `` dependency is limited to the `App2d.Contracts` namespace. ``
- Lines 88 to 89: replace "the host, a future server, and a\npredicting client all construct through it." with "the host constructs through it."
- Lines 99 to 100: replace "Simulation and presentation live in separate\nassemblies." with "Simulation and presentation are separate folders of the game project."
- Lines 103 to 107: delete the sentences "Local simulation\ncapture/restore and bounded input replay are available.\nNetworking and client prediction/reconciliation are not yet wired up. See\n[the session architecture](docs/session-architecture.md) for the boundaries and\nnext capture/restore work." and put in their place "See [the session architecture](docs/session-architecture.md) for the frame and observation flow."
- Line 124 to 125: `` `App2d.Tiles/TileMap2D` `` → `` `App2d.Core/Tiles/TileMap2D` `` and `` `App2d.Tiles/EditableTileMap2D` `` → `` `App2d.Core/Tiles/EditableTileMap2D` ``
- Line 147: "Snapshot\nattachment and rollback preserve this bounded state" → "Snapshot\nattachment preserves this bounded state"
- Line 150: `` `App2d.Rendering/Vegetation` `` → `` `App2d.Core/Rendering/Vegetation` ``
- Line 162: `` lives in `App2d.Tiles` `` → `` lives in `App2d.Core/Tiles` ``
- Line 416: `` (App2d.Gameplay/World/KINEMATICS.md) `` → `` (App2d/Gameplay/World/KINEMATICS.md) ``
- Line 472: `` `App2d.Gameplay/Persons/PersonLocomotion2D` `` → `` `App2d/Gameplay/Persons/PersonLocomotion2D` ``

Then confirm:

```bash
cd /c/Users/eonei/source/App2d && grep -nE "App2d\.(Rendering|Tiles|Levels|Gameplay|Presentation|Audio)/|Directory\.Build\.targets|future server|predicting client|capture/restore|rollback" README.md
```

Expected: no output.

- [ ] **Step 3: Trim the session architecture doc**

In `docs/session-architecture.md`:

- Replace lines 1 to 6 (the title and intro paragraph) with:
  ```markdown
  # Local session and client boundary

  > Networking was considered and dropped on 2026-10-01. The compile-time project boundary,
  > rollback checkpoints, and replay buffer described in earlier versions of this document
  > were removed with it. Contracts, Gameplay, and Presentation are now folders inside the
  > `App2d` game project. The frame and observation flow below still describes the code.

  The session advances the simulation at a fixed rate and hands immutable frames to the
  presentation. This document describes that flow.
  ```
- Delete the "## Project boundaries" section (from that header up to, not including, "## Network-readiness pass").
- Rename "## Network-readiness pass" to "## Session design decisions" and change its first sentence "These decisions were made so that adding a transport later is a drop-in rather than a rewrite:" to "These decisions shape how the session is built and advanced:".
- Delete the sections "## Simulation capture and restore" and "## Capture/restore boundaries" (from the first header up to, not including, "## Verification").
- In "## Verification" delete the paragraph that begins "Rollback tests capture during interacting gameplay" and ends "pause, invalidated/foreign state, and bounded history expiration."

Then confirm:

```bash
cd /c/Users/eonei/source/App2d && grep -nE "Directory\.Build\.targets|CaptureCheckpoint|RestoreCheckpoint|SessionReplayBuffer|Rollback tests|## Project boundaries|## Simulation capture|## Capture/restore" docs/session-architecture.md
```

Expected: no output.

- [ ] **Step 4: Reword the five server comments**

- `App2d/SideScrollerGame.cs`: `// The same recipe a server or predicting client will use; the host only adds I/O and presentation.` → `// The one construction recipe for the simulation; the host only adds I/O and presentation.`
- `App2d/Gameplay/Simulation/SideScrollerSessionDefinition2D.cs`: replace the summary
  ```csharp
  /// Everything needed to construct a session. A server and a predicting client that share
  /// a definition and construct from it in the same order get identical actors, identities,
  /// and physics; nothing here is a live simulation object.
  ```
  with
  ```csharp
  /// Everything needed to construct a session. Two sessions built from the same definition
  /// in the same order get identical actors, identities, and physics; nothing here is a live
  /// simulation object.
  ```
- `App2d/Gameplay/Simulation/SideScrollerSimulation2D.cs`: replace
  ```csharp
  /// The one construction recipe for a side-scroller session and everything it owns. The
  /// local host, a future server, and a predicting client all build through here, so they
  /// cannot drift apart in wiring, layers, physics settings, or identity allocation.
  ```
  with
  ```csharp
  /// The one construction recipe for a side-scroller session and everything it owns. The
  /// game, the tests, and the diagnostics all build through here, so they cannot drift
  /// apart in wiring, layers, physics settings, or identity allocation.
  ```
- `App2d.Core/EntityIdAllocator2D.cs`: replace
  ```csharp
  /// Deterministic, session-owned identity allocation. Two sessions built from the same
  /// definition in the same construction order allocate the same IDs, so a server and a
  /// predicting client agree on identities without exchanging them.
  ```
  with
  ```csharp
  /// Deterministic, session-owned identity allocation. Two sessions built from the same
  /// definition in the same construction order allocate the same IDs, which keeps tests
  /// and diagnostics reproducible.
  ```
- `App2d/Gameplay/Simulation/SideScrollerSession2D.cs` was reworded in Task 1.

```bash
cd /c/Users/eonei/source/App2d && grep -rnE "future server|predicting client" --include="*.cs" . | grep -v "/obj/"
```

Expected: no output.

- [ ] **Step 5: Build and test one last time**

```bash
cd /c/Users/eonei/source/App2d && dotnet build App2d.slnx --nologo -v q 2>&1 | grep -E "error|Build succeeded" && dotnet test App2d.slnx --nologo -v q 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: `Build succeeded.` then `Passed!  - Failed:     0, Passed:  1263, Skipped:     0, Total:  1263`.

- [ ] **Step 6: Prove the game composes and renders through the engine host**

```bash
cd /c/Users/eonei/source/App2d && SMOKE="$TEMP/app2d-smoke-$(date +%s)" && dotnet run --project App2d -c Debug -- --render-smoke "$SMOKE" 2>&1 | tail -5; ls "$SMOKE" | head
```

Expected: the command exits without an exception and the directory lists PNG files.

- [ ] **Step 7: Prove the Release configuration still builds**

```bash
cd /c/Users/eonei/source/App2d && dotnet build App2d.slnx -c Release --nologo -v q 2>&1 | grep -E "error|Build succeeded"
```

Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
cd /c/Users/eonei/source/App2d && git add -A && git commit -q -F - <<'EOF'
Describe the five-project layout and drop the networking plan from the docs

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
EOF
git log --oneline -6 && git status --short | wc -l
```

Expected: six commits listed (spec and plan, then one per task) and `0` changed files.
