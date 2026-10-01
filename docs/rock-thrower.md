# Rock thrower

Playable authored first pass. Place **Rock thrower** in the Things palette or open
`rock-thrower` in Character Studio. The bridge guard (thing 1007, X=3024) is the
solo thrower. The terrace has a club caveman at X=5040 and a thrower at X=5200.
The first meadow encounter remains the solo club caveman.

## Behavior

Approach into 280 world units, search the bag, lift the stone overhead, hold,
throw, recover. Facing and player position commit when the action begins. The
release occurs at 1.14 seconds, with a 1.85-second action and 0.7-second additional
cooldown. Normal damage interrupts any unreleased throw. Released rocks continue
after the thrower is hurt or dies.

Within 80 world units, retreat for 0.35 seconds, then pause for 0.65 seconds before
throwing. Only one retreat is allowed per attack cycle. Terrain checks can stop
the retreat, but never skip the pause. Pursuit/retreat stop at walls and ledges;
there is no jumping or pathfinding. Walk/retreat speed is 72 world units/second.
Attacks require less than 120 world units of vertical separation and an unobstructed
horizontal check at the target's height. Maximum horizontal pursuit is 560 units.

Rocks solve a ballistic arc to the committed position over 0.85 seconds, using
600 world units/s² downward acceleration. They sweep collision in small steps,
deal 2 damage once, and disappear on terrain impact or after 3 seconds. Their
simulation box is 14.4 by 14.4 world units; presentation uses a gray polygon.
Health is 6. There is no body contact damage.

## Editing

- `Assets/Characters/authored/entities/rock-thrower.json`: controller, health,
  projectile and cues.
- `animations/rock-thrower-throw.json`: search/lift/aim/release/recovery poses and
  markers. Shared person running/hit/death roles supply the other movements.
- `variants/rock-thrower-build.json`: bare torso and stick-figure proportions.
- Props: `throwing-rock`, `rock-thrower-bag`, `rock-thrower-hide-wrap`,
  `rock-thrower-hair`, `rock-thrower-beard`. The bag has a muted teal sling.

The stone appears in the hand during preparation and disappears at the release
marker. Its muzzle point is the release origin used by simulation. Depth/grip
placement keeps the overhead stone readable in front of the hair. The reference
is `Assets/Sources/characters/prehistoric/rock-thrower/reference.png`; art derives
from the existing person rig/wardrobe and editable extruded polygons.

Placeholder cues: `rock-search` → HammerWindup, `rock-throw` → SwordSwing,
`rock-impact` → HammerImpact. Player impact uses the shared hit cue; enemy hurt
and death use the existing combat sound path. No new downloaded audio.

`python tools/ArtPipeline/build_rock_thrower.py` overwrites this enemy's entity,
variant, attack clip and five props. It never runs automatically. Edit the saved
assets in Character Studio once tuning starts.

## Review and limits

Follow [the enemy workflow](enemy-workflow.md) to render pose sheets and attack
GIFs. Tests cover both facings, release timing, committed aim, interruption/death,
projectile collision/expiry, dodging, bounded retreat/pause, and solo/paired rollback.
The paired encounter still needs human tuning. Audio and secondary motion are
placeholders; the bag follows the body rigidly. No projectile deflection, bounces,
terrain navigation or thrown-rock loot is included.

Verification for this first pass: 248 gameplay tests, 5 focused enemy presentation
tests, and 47 authored catalog/animation/runtime tests passed. The playable game
and review builds passed, and native game/pose renders were inspected. Human
playtest and balance tuning remain open.
