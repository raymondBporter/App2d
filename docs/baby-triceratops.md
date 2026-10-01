# Baby triceratops

Playable charger. The meadow dinosaur placement (thing 1029, X=1616, Y=-237)
now uses baby triceratops. Add more with **Baby triceratops** in the Things palette;
open `baby-triceratops` in Character Studio.

## Player decision and behavior

Clear the charge lane during the foot scrape and head-lowering tell, then punish
the braking/recovery window. Jumping over the rush is supported; moving away or
crossing behind during preparation also exploits its committed facing. Normal
damage interrupts preparation, rush and recovery. No automatic contact damage.

Approach within 180 world units, plant, scrape, lower the head, rush straight,
brake, recover. Facing commits when the attack starts. The rush marker is at
1.00 seconds and the brake marker at 1.85. Rush speed is 200 world units/second;
braking reduces it linearly to zero over 0.30 seconds. The remaining recovery
holds until the action ends at 2.70, followed by a 0.70-second cooldown. Ordinary
approach speed is 48 world units/second. Attack vertical range is 32 units.

The horn region follows the head-art socket and damages once for 3 during the
rush-to-brake window. Health is 8, mass is 1.5. Terrain probes account for charge
speed and the wide body. A wall or ledge stops movement and disables the attack
region for the rest of the action; the animation still completes its timed
recovery. No navigation, jumping, wall-stun bonus, armor or projectile deflection.

## Editable assets

- Entity: `Assets/Characters/authored/entities/baby-triceratops.json`.
- Rig/art: `models/baby-triceratops-rig.json`, a 70% copy of the existing
  triceratops model with smaller horns and slightly larger eyes.
- Defining action: `animations/baby-triceratops-charge.json`. Named rush/brake
  events drive movement and hit markers; controller start/end values are fallbacks
  for clips without those events. Keep braking inside the visible recovery.
- Eleven `baby-triceratops-*` clips cover shared quadruped roles plus charge,
  hit and death. The charge uses mathematical foot cycles and planted anticipation
  and recovery intervals. Death settles into a low folded pose.

`python tools/ArtPipeline/build_baby_triceratops.py` explicitly overwrites this
entity, rig and eleven clips. Ordinary builds do not regenerate them. Use
Character Studio for later edits. No separate concept sheet was generated;
the existing editable triceratops artwork is the visual starting point.

Placeholder audio: `charge-scrape` and `charge-stop` → PlayerLandHard,
`charge-rush` → HammerWindup, player impact → shared hit cue. Enemy hurt/death
use the existing combat sound path. No additional downloaded audio.

## Review

Follow [the enemy workflow](enemy-workflow.md). The wardrobe review now emits
`baby-triceratops.png` and `baby-triceratops-frames`; the GIF packer creates a
moving attack preview at 24 fps. Pose samples show idle, scrape, lowered head,
rush, brake, recovery, hit and death, with mirrored game-size versions.

Focused tests cover both facings, stationary preparation, committed rush,
braking, single-hit timing, jump escape, normal-hit/death interruption, wall and
ledge stops, placement and exact rollback replay. Human tuning remains open.
