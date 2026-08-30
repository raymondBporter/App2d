# Persons, Actors, and Combat Roles Design

**Date:** 2026-08-30
**Status:** Approved and initial end-to-end slice implemented

The implemented slice includes the sealed person simulation, person-neutral commands,
shared locomotion and weapons, target-neutral faction-aware combat, animation-independent
gun release timing, opt-in contact damage, and the AI-driven rival proof. Immutable person
definition/capability profiles remain a future extraction; the initial version keeps the
existing traversal metrics and explicit constructor values to avoid inventing unused policy.

## Goal

Extract the player's reusable simulation into a first-class **person**: a game entity with
humanoid locomotion, actions, combat state, and presentation state that can be driven by a
human, an AI, a replay, or a test. The first mirror enemy proves the abstraction by using
the same person implementation and the same actions without copying player behavior.

The person is not the base class for enemies and is not the base class for everything in
the game. It is one useful gameplay aggregate. A person may be the player, an enemy, an
ally, or neutral. An enemy may instead be a boiler brute, turret, creature, hazard, or
something else that is not person-shaped.

## Central decision

Use a **strong concrete core with coarse-grained composition**, not an inheritance tree and
not a bag of microscopic components.

`Person2D` is a sealed coordinator. It owns the invariant update order and the rules that
make its parts coherent. It composes a body, locomotion, combat state, an action set, and a
semantic presentation state. Variation comes from immutable profiles, installed actions,
and external command producers. There is no `PlayerPerson2D : Person2D` or
`EnemyPerson2D : Person2D` override hierarchy.

This resolves the apparent conflict between a convenient reusable core and flexible
composition:

- The core owns **coordination and invariants**: death suppresses control, hit reactions
  interrupt permitted actions, action locks affect locomotion, physics runs exactly once,
  and presentation state is derived after simulation.
- Components own **coherent domains**: locomotion, combat, and actions. Jump, wall grip,
  gravity, and dash stay together until a real second locomotion model proves that splitting
  them produces a better boundary.
- Profiles own **numeric and capability variation**.
- Controllers own **decisions**, outside the person.
- Views own **art and animation playback**, outside the person.
- Small capability interfaces exist only where another system needs to address unlike
  objects uniformly.

## The model has independent axes

The game should not encode these independent questions in one class hierarchy.

| Axis | Examples | Represented by |
| --- | --- | --- |
| Physical/gameplay form | person, brute, turret, prop, platform, hazard | concrete aggregate or bespoke actor |
| Control | human, AI, replay, scripted sequence, none | external command producer |
| Allegiance | player, hostile, ally, neutral | combat faction/disposition |
| Capabilities | damageable, attacking, streamable, encounter member | narrow interfaces/components |
| Presentation | player skin, rival skin, brute sprites, debug shape | external view/binding |
| Lifecycle policy | checkpoint respawn, despawn on death, persistent corpse | owning game/encounter adapter |

Consequences:

- `Person2D` is never synonymous with player.
- `Enemy` is a relationship or encounter role, not a physical type.
- Being damageable does not imply being hostile.
- Being hostile does not imply contact damage.
- Being registered in an encounter does not imply using person locomotion.
- A tumbling prop can remain a combatant without pretending to be either a person or an
  enemy.

## Person2D boundary

`Person2D` is simulation-only. It knows physics, collision, combat, actions, and semantic
state. It does not know keyboard/controller input, cameras, HUD, textures, sprite clips,
sound files, checkpoints, enemy counts, chunk activation rules, or rewards.

Conceptually:

```csharp
public sealed class Person2D
{
    public PhysicsBody2D Body { get; }
    public PersonLocomotion2D Locomotion { get; }
    public PersonCombat2D Combat { get; }
    public PersonActionSet2D Actions { get; }
    public PersonState2D State { get; }

    public void BeginFrame(float deltaSeconds);
    public void ApplyCommand(PersonCommand2D command, float deltaSeconds);
    public void UpdateAfterPhysics(float deltaSeconds);
    public void SetSimulationEnabled(bool enabled);
}
```

The exact surface may become smaller during implementation, but these ownership boundaries
are load-bearing. `Person2D` coordinates its parts; consumers do not update the parts in an
arbitrary order.

### Owned modules

**Body** owns the spatial object and physics body. Its collider geometry and physical
collision flags come from simulation data, not from a sprite.

**PersonLocomotion2D** is the current `CharacterMotor2D` evolved into a person-independent
module. It owns running, acceleration, ground support, coyote time, jump buffering, air
jumps, one-way drop-through, wall grip/jump, gravity shaping, and dash movement. These
features have tightly coupled physics ordering and remain one module initially.

**PersonCombat2D** owns health, damage immunity, hit sequence deduplication, hit reaction,
death state, and any damage-driven knockback that is intrinsic to a person. It exposes the
general combatant capability used by the combat resolver.

**PersonActionSet2D** owns installed discrete actions and their arbitration. Initially it
contains the equipped weapon/loadout behavior. An action owns its startup, active, and
recovery timing. Gameplay timing never waits for an animation clip.

**PersonState2D** is the read-only semantic result of simulation: facing, grounded/falling,
wall grip, dash, current action cue and phase, hit reaction, death, landing strength, and
presentation flash state. A view consumes it; it contains no textures or renderer objects.

### What is deliberately not a component

Do not immediately turn jump, dash, facing, coyote time, weapon switching, every cooldown,
and every animation choice into independent interfaces. Their ordering and shared state
would move into an implicit event network, making a new person easy to assemble but hard to
reason about.

The initial composition seams are locomotion, combat, action set, and view. A seam splits
further only when two real implementations need to vary independently.

## Commands and control

The person accepts commands; it does not own a controller interface. This avoids making
simulation depend on host input types and avoids forcing human and AI controllers to share
an artificial context object.

```csharp
public readonly record struct PersonCommand2D(
    PersonMovementIntent2D Movement,
    bool UsePrimaryAction,
    bool SwitchEquipment,
    Vector2? AimTarget);
```

The existing traversal-debug toggle is a game command and leaves this value. Camera control,
pause, editor mode, and HUD commands likewise never enter `Person2D`.

Different drivers produce the same value:

```text
InputState + Camera -> PlayerInputMapper2D -> PersonCommand2D --+
                                                                +-> Person2D
Target + World      -> RivalBrain2D       -> PersonCommand2D --+
Test script         -> Recorded commands  -> PersonCommand2D --+
```

There are two distinct ways for a person not to use an ability:

1. **Controller omission:** the rival is physically able to dash but its current brain
   chooses not to request one. This is tactics and difficulty tuning.
2. **Capability configuration:** a civilian has zero dash charges or no wall grip. Even a
   faulty controller cannot invoke the ability. This is the truth of the entity.

Both are necessary. Controller omission alone makes every person secretly omnipotent;
capability flags alone force behavior policy into entity definitions.

## Profiles and construction

Immutable profiles describe simulation facts. Runtime state never lives in a shared
profile.

```csharp
public sealed record PersonDefinition2D(
    PersonBodyProfile2D Body,
    PersonLocomotionProfile2D Locomotion,
    PersonCapabilityProfile2D Capabilities,
    PersonCombatProfile2D Combat);
```

Likely profile responsibilities:

- `PersonBodyProfile2D`: collider shape/offset, mass, physical layer and mask.
- `PersonLocomotionProfile2D`: speeds, accelerations, gravity, grace windows, probes.
- `PersonCapabilityProfile2D`: maximum jumps, dash charges, wall grip, drop-through.
- `PersonCombatProfile2D`: maximum health, hit immunity, stun and knockback response,
  faction, hurtbox query category.
- Loadout/action definitions are supplied separately because they construct runtime objects
  such as pooled projectiles and scene-visible hitboxes.

Definitions compose or copy data; they do not inherit from one another. A rival definition
may begin with the player's values and replace health/cooldowns explicitly, but there is no
mutable "enemy overrides player" chain at runtime.

The current `TraversalMetrics2D` mixes simulation geometry with visual size and foot-anchor
data. Extraction splits this over time: body and locomotion values belong to the person
definition; canvas size and visual offset belong to the skin/view binding. The migration
must preserve today's measured values exactly before changing their source.

## Actions and presentation state

Actions, not animation clips, are authoritative for gameplay timing.

An action exposes semantic state such as:

```csharp
public readonly record struct PersonActionState2D(
    PersonActionCue2D Cue,
    PersonActionPhase2D Phase,
    float ElapsedSeconds,
    float DurationSeconds,
    int Sequence);
```

`PersonActionPhase2D` is startup, active, or recovery. `PersonActionCue2D` is an opaque
semantic cue such as sword attack or firearm shot; the view maps it to whatever art that
skin provides. The action controls release and damage windows. The view may change playback
speed to match the action duration, but the action never asks whether the view reached a
particular frame.

This reverses the current gun dependency, where projectile release waits on
`PlayerPresentation2D.ShotAnimationElapsedSeconds`. After extraction, a firearm profile owns
the release time and the presentation follows it. A headless person therefore behaves
identically in tests and in the rendered game.

Presentation consumes a snapshot and owns all actual art:

```csharp
public sealed class PersonSpriteView2D
{
    public void Update(PersonState2D state, float deltaSeconds);
}
```

The player and rival can share every simulation object while using different skins,
animation mappings, tinting, offsets, and effects. Sound similarly reacts to semantic
events (`Jumped`, `Landed`, `ActionStarted`, `Damaged`, `Died`) instead of being called by
the person or weapon.

## Combat, collision, and hostility

Collision categories and combat allegiance solve different problems and remain distinct.

- **Physical layer/mask** answers what participates in physics contacts. A person's body
  normally collides with the world, not with other character bodies.
- **Hurtbox query category** lets attack broad-phase queries cheaply find possible targets.
- **Faction/disposition** answers whether the source is permitted to damage the candidate.
- **Contact attack** is an installed attack behavior, not an automatic property of every
  hostile combatant.

The combat system becomes target-neutral:

```csharp
public interface ICombatant2D
{
    CombatFaction2D Faction { get; }
    bool IsAlive { get; }
    bool TryRegisterHit(object source, int sequence);
    bool TryTakeDamage(DamageEvent2D damage);
}
```

`PersonCombat2D` implements this interface. A brute or prop may implement it differently.
The interface does not mention person, player, or enemy.

An attack supplies its source faction and query mask. The resolver uses collision for the
candidate set, then faction/disposition for permission. This supports player-versus-enemy,
enemy-versus-player, friendly fire rules, neutral damageable props, and future allies
without duplicating weapon implementations.

The current `PlayerCharacter2D.ResolveEnemyTouches` assumes that every live enemy combatant
deals touch damage. That behavior is removed. Shieldback or another body-damage enemy owns a
contact-attack component; a rival person simply does not install one.

Enemy death counting and enemy sounds also leave the generic combat resolver. Damage emits
events. An encounter tracker decides whether a death advances an encounter, while audio
chooses a cue based on the damaged actor's presentation/audio binding.

## No universal gameplay base class

Do not introduce `GameEntity2D -> Enemy2D -> PersonEnemy2D` or an equivalent root hierarchy.
Existing engine objects already provide spatial, physics, and rendering primitives. Gameplay
systems address capabilities through the smallest interface they actually require.

The likely capability boundaries are:

- `ICombatant2D`: can be targeted and damaged.
- `IAttackSource2D`: exposes active attacks or resolves attack behavior.
- A streamable/update lifecycle boundary used by chunk activation.
- An encounter-member boundary used by gates, counts, and rewards.

These are orthogonal. They may be implemented by an aggregate or by one of its owned
components. They should not be bundled into an `IEnemyActor2D` replacement merely because
the current enemy registry happens to need all of them at once.

Example compositions:

| Game object | Form | Control | Combatant | Attack source | Encounter member |
| --- | --- | --- | --- | --- | --- |
| Player | `Person2D` | input mapper | person combat | person actions | no |
| Mirror rival | `Person2D` | rival brain | person combat | person actions | yes |
| Friendly NPC | `Person2D` | follow/script brain | optional | optional | no |
| Boiler brute | bespoke brute actor | brute brain | brute combat | hammer/contact | yes |
| Turret | static bespoke actor | targeting policy | optional | projectile action | maybe |
| Tumble prop | rigid prop | none | prop combat | no | no |
| Spikes | hazard | none | no | environmental attack | no |

## Ownership and lifecycle

`Person2D` owns simulation state but not the policy for what happens around its life.

- The host owns the player person, feeds input commands, attaches HUD/camera, and resets it
  at a checkpoint.
- A rival adapter owns a rival person, feeds AI commands, participates in streaming, and
  registers it with an encounter tracker.
- A scripted scene can freeze a person by withholding commands or disabling simulation.
- Death makes the person dead; its owner decides whether to respawn, despawn, leave a corpse,
  drop an item, or unlock a gate.

The fixed-step order stays explicit and shared:

1. Begin the frame for all active simulation actors.
2. Produce commands from input, AI, replay, or script.
3. Apply all commands and update pre-physics state.
4. Step the physics world once.
5. Update all actors after physics.
6. Resolve attacks and contacts.
7. Derive semantic presentation state and update views/audio.

No person or enemy adapter steps the physics world itself.

## Mirror rival as the proof

The rival is deliberately not the architectural center. It is the first demanding client.

Its implementation should consist primarily of:

- A normal `Person2D` built from a rival definition.
- The same sword and firearm action implementations used by the player.
- `RivalBrain2D`, producing `PersonCommand2D` at a deliberately limited decision cadence.
- A rival skin/view binding.
- A thin world/encounter adapter for streaming and defeat tracking.

The brain may neglect wall grip, air jump, weapon switching, or dash in its first version.
Those capabilities remain available if the definition grants them. Adding a second rival
with a different brain or loadout should require no new person subclass.

The proof fails if the rival needs a copied motor, player-only weapon fork, enemy-only
damage path, or animation-driven attack timing.

## Migration strategy

This is a staged extraction that preserves the current player's feel before adding the
rival.

### Stage 1: Commands and semantic state

- Rename player-specific movement/command values to person-neutral equivalents.
- Remove developer and camera commands from the person command.
- Introduce the semantic person state consumed by presentation.
- Keep `SideScrollerGame` scheduling exactly as it is.

### Stage 2: Extract the person core

- Move body, facing, health/damage, locomotion, and update coordination out of
  `PlayerCharacter2D` into `Person2D` and its owned modules.
- Make the existing player a thin owner/reference rather than a subclass.
- Preserve traversal metrics, collision geometry, health, invulnerability, and all movement
  timings with characterization tests.

### Stage 3: Generalize actions and combat

- Replace player-only weapon ownership with person/action ownership.
- Make action timing authoritative and presentation observational.
- Replace `IEnemyCombatant2D` with target-neutral combat capability.
- Separate attack target queries, factions, contact attacks, death events, and encounter
  counting.
- Adapt existing bespoke enemies and the prop without converting them into persons.

### Stage 4: Add the rival proof

- Build a rival definition and skin.
- Add the rival brain and thin encounter adapter.
- Place the first instance in a bounded test encounter.
- Tune behavior only after the shared mechanics are proven.

### Stage 5: Generalize only from evidence

- Add a second person usage (NPC, alternate rival, or replay-controlled ghost).
- Split further component seams only where that second usage demonstrates real independent
  variation.
- Do not convert Boiler Brute or Shieldback merely to increase reuse statistics.

## Testing and acceptance

The architecture is accepted when:

- Feeding identical commands and definitions to two headless persons produces identical
  motion and action state.
- A person can run complete locomotion and combat tests without a scene, textures, audio,
  input devices, camera, or HUD.
- Controller omission and capability denial are independently testable.
- The player retains current movement, attack, damage, death, and respawn behavior.
- Player and rival use the same sword/firearm implementations against opposite factions.
- Friendly or same-faction targets are filtered after collision candidate lookup.
- A rival causes no touch damage unless a contact attack is explicitly installed.
- Changing a skin or animation duration cannot change projectile release or damage timing.
- The prop remains hittable without becoming a person or an encounter enemy.
- The rival contains no copied player locomotion, arsenal, or damage code.

## Non-goals

- A general-purpose ECS.
- A universal base class for every world object.
- Converting all existing enemies into persons.
- Making every locomotion verb independently hot-swappable.
- Data-driven mod loading or runtime component mutation.
- Navigation across the complete procedural level in the first rival proof.
- Final rival combat tuning or finished rival art.
