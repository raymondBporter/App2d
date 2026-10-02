# Local session and client boundary

> Networking was considered and dropped on 2026-10-01. The compile-time project boundary,
> rollback checkpoints, and replay buffer described in earlier versions of this document
> were removed with it. Contracts, Gameplay, and Presentation are now folders inside the
> `App2d` game project. The frame and observation flow below still describes the code.

The session advances the simulation at a fixed rate and hands immutable frames to the
presentation. This document describes that flow.

## Session design decisions

These decisions shape how the session is built and advanced:

- **One construction recipe.** `SideScrollerSessionDefinition2D` is a value (traversal
  metrics, tile map, authored specs, health, saved progress) and
  `SideScrollerSimulation2D.Create` is the only code that wires collision, physics,
  level, player, arsenal, combat, and session from it. Layers live in
  `SideScrollerLayers2D`. The host only adds I/O and presentation on top.
- **Deterministic identities.** Every actor, platform, melee source, and projectile
  range takes its ID from the session's `EntityIdAllocator2D`. Identical definitions
  constructed in the same order produce identical IDs on a server and a predicting
  client, so identities never need to be negotiated. `EntityId2D.Create()` remains
  for tests and diagnostics only.
- **Held-state commands.** `PersonCommand2D` carries axes and held buttons only.
  `Person2D` derives presses and releases from the previous command it saw (part of
  its rollback state), so a lost packet cannot drop a jump and a repeated command
  cannot re-trigger one. A tap that begins and ends inside one tick reaches the
  simulation as a one-tick hold.
- **Missing input policy.** `Advance` takes any number of inputs for one tick. A
  player without an input repeats its last command and its acknowledged sequence
  does not move.
- **Rejections, not exceptions, on the transport path.** `TryValidateInput` and
  `SessionClient2D.TryApply` report `InputRejection2D` / `FrameRejection2D` without
  changing state. `Advance` and `Apply` still throw for local programming errors.
- **Per-player shape.** Frames and snapshots carry `Players` with each player's own
  acknowledged input sequence; events are already stamped with their entity.
  Enemy targeting and terrain streaming follow the first (living) participant as an
  explicit policy until multiple participants are actually added.
- **Static content split from dynamic state.** `LevelContent2D` (active terrain,
  platform definitions, checkpoint placements, goal) changes only when streaming or
  authoring changes and carries a revision; `WorldState2D` is the small per-tick
  part (platform poses, checkpoint activation).
- **Equipment is an enum** (`EquipmentKind2D`), and physics contact order is kept
  in explicit lists rather than relying on dictionary enumeration.

Not done in this pass: gameplay timers are still accumulated floats rather than
tick counts. Local replay is exact; cross-machine float determinism remains a
known caveat.

## Running path

```text
SideScrollerClient2D captures device input
    -> PlayerInput2D(entity ID, tick, sequence, held-state PersonCommand2D)
SideScrollerSession2D advances one 1/120-second tick
    -> SessionFrame2D(per-player states with acknowledged input, enemy states,
                      level content, world state, events)
SideScrollerClient2D accepts the frame and consumes its events once
GameHost advances presentation once per display update, independently of received frames
```

`SideScrollerGame` builds a `SideScrollerSimulation2D` from a definition and
schedules these calls. It no longer decides when a player dies, respawns, activates
a checkpoint, or reaches the goal. It remains responsible for the offline editor,
diagnostics, and persistence I/O.

The session owns update order and its own tick counter. Inputs are validated
before any state changes: unknown players, wrong ticks, stale sequences, invalid
movement values, and duplicate players in one batch are rejected. Pausing does not
advance the clock or consume an input sequence. Each frame takes exactly one fixed
simulation step; the host continues to own the render cadence and fixed-step
accumulator.

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
simulation slot is reused. Each gun reserves an ID range from the session allocator
and captures its creation sequence, so replay reproduces projectile IDs without
affecting another session. Actor and melee-source identities remain fixed for the
lifetime of the owning session.

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

`LevelContent2D` contains platform definitions (runtime ID, authored thing ID, size,
ARGB color), checkpoint placements, goal position, and immutable active terrain
chunks; it carries a revision and is shared between ticks until streaming or
authoring changes it. `WorldState2D` contains only platform poses and checkpoint
activation. Replacing a platform gives it a new runtime ID. The persisted
checkpoint ID remains the authored thing ID.

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
and their Windows/rendering/audio dependencies live in `App2d.Presentation`.

## Remaining scope

The session currently controls one campaign player. Entity-addressed messages
provide a boundary for adding player membership later; enemy targeting, streaming,
checkpoints, and cameras still need explicit multi-player policies. No player
limit or multiplayer performance claim follows from this extraction.

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


Interface tests attach at nonzero ticks, issue inputs ahead of responses, reconstruct
alive/dead and attack poses without events, and advance effects between messages.
A test-only JSON codec round-trips complete snapshots/frames, terrain halos and
collisions, projectiles, and every current event variant. Its entity-ID converter
and polymorphism registry are test infrastructure; no production serializer,
transport, loss recovery, interpolation, or prediction loop is selected here.
