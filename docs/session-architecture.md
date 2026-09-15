# Local session and client boundary

The intended networking model is an authoritative session with shared simulation
code that clients can eventually run predictively. This change establishes the
first local boundary while preserving the existing single-player campaign.

## Project boundaries

The compiler now enforces the simulation/presentation split. Each project owns
its files physically; existing `App2d.Gameplay.*` namespaces are retained to keep
the extraction focused on assembly dependencies.

| Project | Responsibility | Internal dependencies |
| --- | --- | --- |
| `App2d.Game.Contracts` (`net10.0`) | Commands, state observations, events, shared timing and traversal configuration | Core, Tiles |
| `App2d.Gameplay` (`net10.0`) | Session, actors, combat, world simulation, local rollback | Contracts, Core, Collision, Physics, Tiles |
| `App2d.Game.Presentation` (Windows) | Views, camera, HUD, sound selection, local client endpoint | Contracts, Core, Tiles, Rendering, Audio |
| `App2d.Levels` (`net10.0`) | Authored level storage and traversal configuration loading | Contracts, Core, Tiles |
| `App2d` (Windows executable) | Composition, input devices, scheduling, editor, save-file I/O | Simulation, presentation, and their supporting projects |

The future server can reference Gameplay without acquiring rendering, audio,
Windows, or SQLite dependencies. A predictive client can also reference Gameplay;
the presentation assembly only consumes the shared contracts. Rollback checkpoints
and actor/action interfaces stay in Gameplay rather than in the observation contracts.

`TraversalMetrics2D.FromGeometry` constructs shared configuration from values.
`App2d.Levels.TraversalMetricsLoader2D.Load` owns manifest validation and file I/O.
The host supplies a private copy of the authored ground-height profile to its
camera callback, so the camera no longer reads the live simulation level.
`BoilerBruteTiming2D` shares attack timing without a presentation reference to the actor.

`Directory.Build.targets` checks resolved internal assembly references, including
transitive ones. Adding a forbidden reference fails the build even if no code uses
it yet. It also prevents the plain `net10.0` gameplay tests from acquiring the host
or presentation. This checks assembly dependencies; it does not police every BCL
API or prevent a host from passing an inappropriate callback.

Simulation tests remain in `App2d.Gameplay.Tests`. Graphics/audio and client endpoint
tests live in `App2d.Game.Presentation.Tests`, whose integration tests may reference
simulation and content loaders. Save-store tests live with host tests in `App2d.Tests`.

```powershell
dotnet build App2d.Gameplay/App2d.Gameplay.csproj
dotnet test App2d.Gameplay.Tests/App2d.Gameplay.Tests.csproj
dotnet build App2d.slnx
dotnet test App2d.slnx
```

This extraction changes file ownership and dependency direction. Identity allocation,
network transport, multi-player policy, prediction scheduling, and transferable
corrections remain separate passes.

## Running path

```text
SideScrollerClient2D captures device input
    -> PlayerInput2D(entity ID, tick, sequence, PersonCommand2D)
SideScrollerSession2D advances one 1/120-second tick
    -> SessionFrame2D(player state, enemy states, world state, acknowledged input, events)
SideScrollerClient2D accepts the frame and consumes its events once
GameHost advances presentation once per display update, independently of received frames
```

`SideScrollerGame` constructs the objects and schedules these calls. It no longer
decides when a player dies, respawns, activates a checkpoint, or reaches the goal.
It remains responsible for the offline editor, diagnostics, and persistence I/O.

The session receives the existing physics world, player, actions, and a world
adapter. It owns their update order and its own tick counter. Input is validated
before any state changes: wrong entity IDs, wrong ticks, repeated sequences, and
invalid movement values are rejected. Pausing does not advance the clock or
consume an input sequence. Each frame takes exactly one fixed simulation step;
the host continues to own the render cadence and fixed-step accumulator.

## Attaching and advancing a client

`SideScrollerSession2D.CaptureSnapshot()` captures a complete observation at a tick
boundary: tick, acknowledged input sequence, player, enemies, and world. It contains
no historical events. Construct `SessionClient2D` from this `SessionSnapshot2D` to
attach at an arbitrary tick. This observation is distinct from a rollback checkpoint.

The endpoint tracks sent input ticks/sequences separately from its received tick.
It can create several inputs before any responses arrive. Acknowledgements update
received state without moving the input clock backwards. Frame delivery still must
be contiguous and ordered; gaps and backwards acknowledgements are rejected before
mutation, and repeated/older frames return false. A future transport must supply this
delivery guarantee or introduce an explicit snapshot/event recovery policy.

Views expose `ApplyState` and `Advance`: acceptance copies observations and consumes
occurrences with zero elapsed time; the host advances visual time once per display
update. Animation, flash/trail decay, camera follow, and feedback continue between
messages. This does not predict motion: positions remain the latest observed values.
Editor pause continues to freeze gameplay presentation.

Alive/dead pose and equipment come from state. `PersonActionState2D` carries kind,
elapsed time, and gameplay duration so a newly attached player or rival view can
seek into an ongoing action without replaying an attack event. Gun observations
retain time since the shot after gameplay recovery; presentation keeps its authored
recoil duration. Beginning a new charge or deselecting clears that shot observation.
Transient sounds, hit reactions, and camera shake still use occurrences. Local phase
advancement is cosmetic; subsequent observations correct it.

## State, events, and effects

`PersonState2D` and `PlayerState2D` are value observations. They contain no actor,
physics body, texture, sound voice, or other mutable subsystem object. Session
frames own immutable event collections; a subsequent update cannot overwrite
an earlier frame. `PersonPresentation2D` now accepts this value state.

Events describe occurrences: landing impact speed, damage, death, attacks,
equipment changes, checkpoint activation, and respawn. The client chooses sounds, transient reactions, camera shake, and HUD feedback;
persistent poses and action phases can be reconstructed from observations. Checkpoint events request persistence
through the host only after the simulation step; reading state does not write a
save file. Damage has already changed authoritative health before its event is
presented.

`SessionClient2D` is the value-only local endpoint. It rejects another player's
frames, ignores repeated/older frames, and rejects gaps in this synchronous
transport. Consequently the presentation consumes each delivered frame's events
once. This is not a UDP delivery protocol and does not yet match predicted events
to authoritative confirmations. Event sequence numbers identify occurrences in
this session; matching them across rollback needs additional policy.

The B-key shield pose remains presentation-only, matching current behavior. It
does not imply a server-side blocking mechanic.

## Weapons and combat

`PersonArsenal2D`, sword actions, gun charging/firing, projectile motion, and damage
resolution have no scene, texture, shader, animation player, or sound dependency.
The host supplies a muzzle offset derived from authored geometry once at creation.
Charge duration and post-shot recovery remain gameplay timers; flash, cancellation
fade, and trail durations belong to `WeaponPresentation2D`.

Each player observation includes a `WeaponState2D` with charge state and immutable
active projectile values. Every launch gets a new entity ID, including when a
simulation slot is reused. IDs remain process-local. Each gun reserves an independent ID range and captures
its creation sequence, so replay reproduces projectile IDs without rewinding the
process allocator or affecting another session. Actor and melee-source identities
remain fixed for the lifetime of the owning session.

Charge start/cancel, gun firing, projectile impact, and sword impact are gameplay
facts. `WeaponOccurred2D` stamps them with the owning entity and session tick/sequence.
Facts retain their occurrence position. A projectile that spawns and hits within
the same tick still reports its impact even though the active-projectile snapshot
is empty. A barrel obstruction reports an impact with `EntityId2D.None`, because
no projectile was created. Expiration/removal is reflected in the active set.

`WeaponPresentation2D` consumes only these value observations and facts. It owns
sprites, charge animation/audio, HUD assets, muzzle flash, and a separate trail
pool. It can attach to an ongoing charge from state, and suspension stops the
voice. Old trails can fade while a different projectile uses a simulation slot;
render cadence and visibility never decide whether the simulation can fire.
The local endpoint filters duplicate frames before effects receive their events.

`CombatSystem2D` reports accepted damage through `CombatDamage2D` (target ID,
faction, position, killed flag). The session forwards it as a stamped occurrence;
the client maps it to enemy hurt/death sounds. Player damage presentation still
uses the player's own damage/death events to avoid duplicate effects.

## Enemy actors

Shieldback, green dinosaur, boiler brute, rival, and tumble prop simulation now
construct only physics/gameplay objects. `EnemySystem2D.CaptureStates()` creates
immutable `EnemyState2D` values, including identity, actor kind, pose, activation,
and the action state needed by the client. No actor owns a sprite or sound voice.

The brute has an explicit 0.8-second attack with a damage window from 0.5 to 0.6
seconds. These timings preserve the original eight-frame attack at 10 fps, but
are now gameplay constants. The client fits the authored animation to the
observed attack time; animation frame count and presentation cadence cannot
change when the hammer hits. Physics synchronization updates the hammer hitbox
without touching presentation. Streaming disables simulation and freezes the
attack clock, preserving its existing resume behavior.

Actors queue facts such as hammer start/strike and rival attack/damage/death.
The session drains them after the tick and stamps each with its source entity,
tick, and session event sequence. Per-actor event order is preserved; ordering
across different actors is delivery order, not a sub-tick timestamp. Capturing
state never consumes facts. These are local delivery semantics, not a reliable
network event journal.

`EnemyPresentation2D` creates and owns client views by entity ID. It updates
sprites, rival animation and markers, and tumble-prop rotation from observations.
Disabled actors hide their views; actors missing from a complete snapshot have
their scene objects disposed and removed. Hammer sounds use the occurrence
position, while damage sounds still use the combat facts. Editor streaming sends
a fresh observation with zero elapsed time so visibility follows the editor's
active area while simulation is paused.

The deferred sound bridge has been removed: all current gameplay audio is now
selected and played on the client from state or facts.

## Platforms, checkpoints, and terrain

`SideScrollerLevel2D.CreateSimulation` constructs the production world without a
scene, texture cache, shaders, or audio. Platforms own spatial physics objects;
checkpoint objects own entry/activation state. `SideScrollerChunkStreamer2D`
activates and removes only terrain colliders. Level disposal unregisters its
combatants, removes its physics bodies, and detaches its map-edit listener.

`WorldState2D` contains platform poses with runtime IDs and authored thing IDs,
checkpoint activation/base positions, goal position, and immutable active terrain
chunks. Platform color is plain ARGB metadata, so the simulation no longer needs
a graphics library's color type. Replacing a platform gives it a new runtime ID.
The persisted checkpoint ID remains the authored thing ID.

`TerrainChunkState2D.Capture` copies tile kinds, tileset indices, collision rectangles,
and a one-cell halo from a map. Its public value constructor accepts metadata,
immutable packed cells, tileset IDs, and collision rectangles, so a receiver can
construct terrain without possessing a live map. It validates halo length and
tileset references. The client can calculate surfaces, corners, connected spikes,
and ladder caps across chunk boundaries without consulting the editable map.
Each load/rebuild gets a new revision. Snapshots and their immutable collection
are reused while the active terrain is unchanged; successive ticks do not copy
all the tiles. An editor edit rebuilds affected active chunks, including neighbors
whose borders changed. Old frames retain their old tile values after edits,
unloads, and disposal.

`WorldPresentation2D` consumes those observations. It owns terrain visuals,
platform rectangles, checkpoint animation/glow, and the goal flag. It rebuilds
only chunks with new revisions and removes visuals missing from a complete
snapshot. The offline editor refreshes world and enemy observations with zero
elapsed simulation time after its dirty-chunk flush. Presentation neither advances
platforms nor triggers checkpoints, and rendering an old snapshot cannot mutate
physics or save state.

These are in-process observations, not a bandwidth-optimized wire format. Static
level distribution, terrain revision delivery, and network interest management
remain future work. The gameplay assembly targets plain `net10.0`; client views
and their Windows/rendering/audio dependencies live in `App2d.Game.Presentation`.

## Remaining scope

The session currently controls one campaign player. Entity-addressed messages
provide a boundary for adding player membership later; enemy targeting, streaming,
checkpoints, and cameras still need explicit multi-player policies. No player
limit or multiplayer performance claim follows from this extraction.

## Simulation capture and restore

`CaptureState()` and the frame messages remain client observations.
`SideScrollerSession2D.CaptureCheckpoint()` instead returns an immutable,
opaque `SessionCheckpoint2D` containing the current production simulation's
mutable state at a completed tick boundary:

- Tick, acknowledged input sequence, event sequence, queued facts, pause state,
  respawn location/health, restart countdown, and goal/checkpoint progression.
- Player and rival movement intent, jump buffers/coyote time, wall/ladder relatch,
  jump sustain, dash availability/cooldown, footsteps, invulnerability, and health.
- Equipment selection, charge/recovery/release state, every projectile pool slot
  (including inactive slots), lifetime, creation sequence, buffered melee swings,
  hitbox poses, attack numbers/directions, downward bounce state, and hit history.
- All authored enemy kinds, AI and stun/attack timers, hammer connection state,
  activation, queued occurrences, health/hit history, and the defeated-enemy count.
- Platform path distance/direction, checkpoint entry/activation history, loaded
  terrain chunks/revisions, and their collider identities.
- Physics body poses, previous poses, velocities, forces/torques, motion/material/
  collision settings, one-way exclusions, previous contacts, body/collider order,
  collider allocation sequence, gravity, and solver iteration/substep settings.

Snapshots own immutable value collections. They hold no live actor, physics body,
scene, texture, sound voice, or mutable map reference. Terrain data is shared only
through immutable chunk snapshots. Restoring recreates streamed terrain bodies,
then restores physics references using collider IDs and reinstates solver/query
ordering. Removed bodies immediately leave contact and one-way-exclusion lists.
Scratch query buffers and spatial indexes are rebuilt rather than captured.

```csharp
var checkpoint = session.CaptureCheckpoint();
// Record inputs while advancing normally.

session.RestoreCheckpoint(checkpoint);
foreach (var input in recordedInputsAfterCheckpoint)
{
    var replayedFrame = session.Advance(input);
    // The caller decides which state/facts to present after replay.
}
```

Restore assigns state directly: it does not call damage, reset, checkpoint entry,
physics stepping, or other gameplay operations that emit occurrences. Subsequent
replayed ticks generate their normal facts with restored tick/sequence stamps.
This does not itself suppress sounds, save writes, or other effects if a caller
chooses to deliver those replayed facts to presentation again. The existing local
client still rejects old frames; no live reconciliation loop is wired into it.

`SessionReplayBuffer2D` provides bounded input/checkpoint history (240 ticks by
default). Advance through the buffer, then call `ReplayFrom(tick)` to restore that
retained boundary and repeat its subsequent inputs with their original sequence
numbers. It returns replayed frames and replaces the corresponding checkpoints.
Expired history is rejected before restore. Outside session advances, restores,
or pause changes invalidate the buffer; recreate it after resuming or changing
session state outside the recorded input path. Direct capture/restore also works
while paused. The host still owns the render accumulator.

## Capture/restore boundaries

This is local rollback for an **existing session with fixed level/actor
configuration**. It is not a serialized save game, network message, or a way to
construct a second session. Ownership and membership checks reject foreign
checkpoints before mutation. Editable-map changes (including unloaded chunks),
platform replacement, actor membership changes, and disposal invalidate older
checkpoints. Flush pending editor changes before starting fresh history.
Normal terrain streaming, actor death/respawn, and projectile creation/removal
are supported and do not invalidate history.

The session must receive its combat system and own the captured combatants.
World/action implementations must explicitly support rollback; the interface
defaults throw instead of silently omitting state. Physics capture supports the
production built-in integration/filter/solver pipeline, without constraints or
standalone non-physics colliders. Custom solvers, mutable constraints, or new
subsystems need explicit checkpoint support before being used in this path.
Static shapes, traversal metrics, actor definitions, and other configuration must
remain fixed during retained history.

The remaining prediction work includes constructing matching server/client
instances with shared identities/configuration, a transferable correction format,
authoritative input acknowledgements, prediction/reconciliation scheduling, remote
interpolation, and presentation/effect confirmation policies. Replaying the same
inputs produces matching IDs/stamps; changed inputs may change occurrence order,
so sequence stamps alone do not match predicted effects across divergent branches.

Exact replay tests on this runtime establish local repeatability, not
cross-platform floating-point determinism. Capture currently favors explicit,
complete state over allocation or bandwidth optimization.

## Verification

The session tests use actual character movement and physics without graphics,
audio playback, a window, or save-file access. They cover independent replay of
recorded commands, invalid-input rejection before stepping, immutable observations,
event consumption, fixed ticking, pause/resume, checkpoints, death/respawn identity,
landing impact, and one-time goal events. Enemy tests cover damage windows,
one hit per hammer attack, stun interruption, streaming pause, rival facts, and
immutable state/event delivery through the real session.

Existing movement, weapon, ladder, balance, and presentation tests continue to
exercise the behavior exposed through the new `PersonState2D` boundary.

Weapon session tests also use the real arsenal without loading textures. They
cover immutable projectile observations across slot reuse, immediate spawn/impact
facts, damage target identities, and charge interruption. Presentation tests cover
state-driven charge voice lifetime and independent trail fading; the rendering
smoke exercises mirrored gun poses, automatic fire, flight, wall impact, and cleanup.

Enemy presentation tests verify that hammer poses follow authoritative time,
streamed-out actors hide, prop visuals do not share live physics objects, and
removed rivals leave neither sprites nor markers in the scene. Rendering smoke
also draws all enemy client views and checks complete visibility/removal cleanup.

World tests exercise the production level, arsenal, and every authored enemy type
inside a session without graphics. They verify platform carrying/reversal,
checkpoint entry, immutable terrain/halo revisions, cross-chunk ladder updates,
platform replacement identity, view isolation, and level/view cleanup. The normal
rendering smoke exercises the real terrain, checkpoints, goal, and platform views.

Rollback tests capture during interacting gameplay and compare every subsequent
checkpoint value and frame/event stream against the original run. They cover
charge/fire/expiration/pool reuse, independent session identity allocation, buffered
melee and hit history, all enemy kinds, lethal hits, platform reversal/carrying and
drop-through, ladder/wall/jump/dash state, downward bounces, accumulated forces and
spin, streamed collider ordering, checkpoint entry, death/respawn/goal progression,
pause, invalidated/foreign state, and bounded history expiration.


Interface tests attach at nonzero ticks, issue inputs ahead of responses, reconstruct
alive/dead and attack poses without events, and advance effects between messages.
A test-only JSON codec round-trips complete snapshots/frames, terrain halos and
collisions, projectiles, and every current event variant. Its entity-ID converter
and polymorphism registry are test infrastructure; no production serializer,
transport, loss recovery, interpolation, or prediction loop is selected here.
