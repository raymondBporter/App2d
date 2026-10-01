# Club caveman

Playable first pass. The first meadow guard (thing 1006, X=848) in the cavern
level is now a club caveman. Additional placements use **Club caveman** in the
Things palette. Character Studio opens the `club-caveman` entity.
The terrace placement (thing 1008, X=5040) pairs him with a rock thrower at X=5200.
See [the shared enemy workflow](enemy-workflow.md) for generation, review and handoff.

The enemy runs toward the player at 100 world units/second, stops within 46
units, and commits its facing for the entire slam. A normal damaging hit cancels
the action and plays the shared hit reaction; death cancels it too. Pursuit
checks the next step for floor and walls. There is no body contact damage,
jumping, or pathfinding. Targets more than 560 horizontal units away are ignored.

The slam lifts over 0.55 seconds, holds overhead until 0.70, strikes at 0.78,
and has a damage window through 0.95. The low pose holds until 1.35, then returns
to standing by 1.80. An additional 0.35-second cooldown follows. Damage is 3;
health is 9. The club-tip hit region comes from the same pose the game renders.

## Editing

- Entity/controller, health, hit region and sounds:
  `Assets/Characters/authored/entities/club-caveman.json`.
- Slam keys, markers and facial expressions:
  `Assets/Characters/authored/animations/club-caveman-slam.json`.
- Build and spotted tunic:
  `Assets/Characters/authored/variants/club-caveman-build.json`.
- Equipment: `wooden-club`, `club-caveman-hair`, `club-caveman-beard`, and
  `club-caveman-hide-wrap` under authored props. The club includes its editable
  extrusion outline. The other equipment uses the existing wardrobe geometry.

Art is rigged vector/mesh content, based on the curated club-caveman reference
and existing person/caveman wardrobe. Animation uses existing solved human rig
curves with a longer planted recovery. Idle, running, hurt and death reuse the
shared person clips. Everything is editable in Character Studio.

Sound placeholders reuse the existing hammer windup and impact bank entries;
enemy hurt/death feedback uses existing combat presentation. No downloaded sound
or new licensing dependency. `club-windup` selects HammerWindup; `heavy` selects
HammerImpact. These mappings live in EnemyPresentation2D.

`python tools/ArtPipeline/build_club_caveman.py` explicitly regenerates the new
assets and **overwrites edits to them**. It does not run in ordinary builds.
Prefer editing the saved assets once tuning starts.

`dotnet run --project App2d.CharacterStudio -- --smoke-wardrobe artifacts/club-caveman`
also renders `club-caveman.png`: idle, run, lift, overhead hold, strike,
recovery, hit, death; each at large size and mirrored game size.

Gameplay tests cover placement/pursuit, hit interruption, facing commitment,
damage timing and single-hit accounting, escape, ledges/walls, death, and rollback.
The next tuning step is playing the opening encounter and adjusting these assets.
