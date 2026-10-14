# Pterosaur

Status: placeholder first pass, ready for user playtesting.

Integrated first pass using the existing `pterosaur` model and its six flight studies. Two pterosaurs are placed in cavern: Meadow edge at x1904, y-102 before the bridge, and Terrace clearing at x6672, y58 farther along. Place more with **Pterosaur** in the Things palette; open `flying-pterosaur` in Character Studio.

## Behavior and counterplay

The flyer approaches a hover point 128 world units above the player and 96 to one side, within a 560-unit awareness radius. Gravity is disabled while alive. It folds its wings and plays a placeholder screech, samples the player once at 0.40 seconds, then keeps its dive velocity and facing committed. A late dodge clears the lane.

| Phase | Action time | Behavior |
| --- | --- | --- |
| Fold/tell | 0–0.48 s | Stationary; target sampled at 0.40 s |
| Dive | 0.48–1.13 s | Body/beak damage active, one hit per attack |
| Brake | 1.13–1.45 s | Slows horizontal travel and descends toward recovery height; no damage |
| Low pass | 1.45–2.45 s | Slow vulnerable pass before any climb; grounded sword reach |
| Reset | 2.45–3.65 s | Climbs toward the previous hover point |

Health 4, damage 2, cooldown 0.8 seconds. Normal hits interrupt the action. Recovery height comes from terrain below the sampled player, so jumping during the tell does not leave the recovery floating overhead. Broad wings remain visual: only the authored compact body/head hurt layout takes damage. Terrain sweeps use the compact movement box; an obstructed dive stops and loses its hit window. Obstruction can prevent reaching the intended low altitude, and the flyer never crosses terrain to enforce it.

## Editable assets

- Existing `models/pterosaur.json` and `pterosaur-hover/fold/dive/brake/low-recovery/reset` clips are reused unchanged.
- `entities/flying-pterosaur.json` owns flight settings, health, movement box, hit geometry and cues. The distinct entity ID avoids conflicting with the model ID.
- `animations/pterosaur-swoop.json` joins the existing flight studies with named phase markers. `pterosaur-hit` and `pterosaur-death` provide reactions.
- Flight movement is implemented in `AuthoredFlightController2D`; it retains sampled direction, altitude and hover origin throughout each action.

Paths are under `Assets/Characters/authored`. `python tools/ArtPipeline/build_pterosaur.py` explicitly overwrites only the entity and gameplay clips, deriving the action from the current studies. Ordinary builds never regenerate assets. After editing source studies, rerun deliberately or edit the composed action directly.

## Review and verification

Wardrobe smoke emits `pterosaur-frames` using the actual physics/controller path, with a gray standing player collider as a target reference. The GIF packer creates `pterosaur.gif`. Existing `--smoke-pterosaur` reviews all six pose studies and both facings.

Tests cover both facings, commit-time sampling, late dodge, one hit, mandatory low vulnerability, hovering and animation under gravity, interruption/death, terrain obstruction, placement and deterministic dive/recovery across fresh simulations. Sound cues reuse existing hammer windup, sword swing and landing placeholders. Human playtesting is still needed for timing, encounter balance and sound selection.
