# Prehistoric enemy roster — brainstorm

Working design proposals. The [club caveman](club-caveman.md) now has a playable
first pass, as does the [rock thrower](rock-thrower.md); the other behaviors remain
proposals. Their repeatable production process is in [Enemy workflow](enemy-workflow.md).
The club caveman and rock
thrower reference sheets are the user's preferred visual anchors. All additional
enemies below are candidates, not approved designs.

## Reference shelf

| Enemy | Role | Design status | Reference |
| --- | --- | --- | --- |
| Club caveman | Pursuer | Playable authored first pass | [Sheet](../Assets/Sources/characters/prehistoric/club-caveman/reference.png) |
| Rock thrower | Ranged attacker | Playable authored first pass | [Sheet](../Assets/Sources/characters/prehistoric/rock-thrower/reference.png) |
| Shell-shield caveman | Defender | [Playable authored first pass](shield-defender.md) | Editable caveman rig and shell mesh |
| Baby triceratops | Charger | [Playable authored first pass](baby-triceratops.md) | Existing editable quadruped art |
| Tar spitter | Area controller | Proposed | Needs concept sheet |
| Pterosaur | Flying harasser | Proposed | Needs concept sheet |

The curated sheets live under `Assets/Sources/characters/prehistoric/<enemy-id>`.
Generation experiments remain in `output/imagegen`; promote a chosen version here
with its provenance. These are design references, not runtime sprites or authored
Character Studio entities. See the [source shelf](../Assets/Sources/characters/prehistoric/README.md).

## Six enemies, six decisions

| Role / enemy | Player decision | Behavior and readable tell | Opening / counterplay |
| --- | --- | --- | --- |
| Pursuer: club caveman | When do I turn and attack versus keep moving? | Runs into melee range, plants feet, lifts the heavy club, then slams. Stops tracking the player once the slam commits. | Step out during the lift, then attack while he pulls the club off the ground. Pursuit creates pressure; recovery creates permission to turn. |
| Defender: shell-shield caveman | How do I bait or get around its protection? | Carries an oversized scavenged shell in front. Shuffles toward the player, then visibly pulls the shell back before a short bash. | Bait the bash and punish its recovery, or get behind him where the room allows. Front protection should be visually localized, not unexplained whole-body immunity. |
| Ranged: rock thrower | When can I safely close distance? | Searches the bag, lifts a rock, aims, then throws an arc toward the player's sampled position. Does not retarget after release. | Advance during the bag search or after the rock passes. At close range, use a short nervous retreat with a pause so he cannot kite forever. |
| Charger: baby triceratops | Where do I stand to dodge and punish it? | Low triangular silhouette, broad frill and tiny horns. Scrapes a foot, lowers its head, then commits to a straight ground rush. | Clear its lane, then punish the braking skid. Turns slowly during recovery. A wall collision can add comic daze later, but is not required for the basic behavior. |
| Area controller: tar spitter | Which parts of the room are safe right now? | Squat creature with a swollen throat pouch. Inflates, spits a high blob, then leaves a temporary dark puddle at the visibly marked landing point. | Move to clean ground and attack between spits. Begin with one puddle at a time, short lifetime, and no damage before landing. Keep an escape route available. |
| Flying harasser: pterosaur | How do I handle a threat above me? | Large angular wings and small body. Hovers, folds its wings with a short screech, then dives through the player's sampled position. | Move out of the dive path, then hit it during a low recovery pass. It must enter ordinary attack reach regularly so aerial abilities are optional. |

## Why these shapes work together

Keep the cavemen's round nose-free faces, huge beards, skinny dark line limbs,
ochre skins, and restrained crayon texture. Distinguish enemies through equipment,
posture, and outline before adding detail or relying on color.

- Club caveman: tall beard-and-club shape; club raised overhead signals commitment.
- Rock thrower: stone overhead and bag at the hip; bag search signals downtime.
- Shell defender: broad shell interrupts the thin human outline; shield position shows protection.
- Triceratops: low horizontal wedge; lowered frill signals the charge lane.
- Tar spitter: squat pear shape; expanding throat signals the next spit.
- Pterosaur: wide wing triangle; folding wings signals the dive.

This mixes two familiar humans, one equipment variation, and three creatures to
make the prehistoric setting feel inhabited. Match the creatures to the simple
handmade drawing language rather than realistic dinosaur anatomy.

## Alternatives worth keeping

| Candidate | Role | Reason to choose it / tradeoff |
| --- | --- | --- |
| Raptor rider | Charger, later elite | Existing experimental sheets in `output/imagegen/raptor-rider-*`. A mounted passing club attack is expressive, but adds mount/rider animation work. Keep as a candidate; the user has only selected the two on-foot designs. |
| Bare raptor | Pursuer variant | Faster, shorter bites and less recovery. Good later pressure, but overlaps the club caveman's job. |
| Armored ankylosaur | Defender alternative | Excellent creature silhouette and exposed face/tail opportunities; protection needs a clear punish window that does not require getting behind it. |
| Prickly seed plant | Area-controller alternative | Rooted plant spits bouncing seed pods. Good for a jungle section and cheaper locomotion; can feel more like a room hazard than an active enemy. |
| Giant mosquito | Flying alternative | Thin limbs fit the art style and a swollen belly can telegraph a lunge. Avoid making it tiny or constantly outside melee reach. |
| Boulder roller caveman | Ranged elite | Pushes a large rolling rock down a lane. Strong terrain interaction, but overlaps charger pressure and needs careful slope behavior. |

## Teach, then combine

1. Club caveman on a flat stretch: teach windup and recovery.
2. Rock thrower with a clear approach: teach closing distance between throws.
3. Shell defender in a roomy clearing: teach baiting its bash before requiring flanks.
4. Triceratops on a long lane with dodge space: teach commitment and skid recovery.
5. Tar spitter in a broad room: teach temporary unsafe ground without blocking the only exit.
6. Pterosaur over forgiving ground: teach the dive and low punish window.

Then try pairs:

- **Club + rock thrower:** advance through ranged fire, then decide when to turn on the pursuer.
- **Defender + rock thrower:** draw the defender forward to open the approach to the thrower. Avoid an unavoidable projectile barrage behind permanent protection.
- **Triceratops + tar spitter:** choose a clean dodge location before the charge. Preserve enough clean ground to escape.
- **Club + pterosaur:** alternate ground and aerial attention; offset their attack tells so the first encounter is readable.

Begin with two types per encounter. Raise difficulty through placement and timing
before stacking extra types or increasing health.

## Next concept sheets

Suggested first pair: **shell-shield caveman** and **baby triceratops**. They add
protection and committed movement, which the existing two sheets do not cover.
Follow with tar spitter and pterosaur after testing room space and melee reach.

For each sheet request: large neutral design, side view, alert, locomotion,
anticipation, attack, missed attack/recovery, and hit reaction. Show the protected
side or hazard footprint where relevant. Include a player-height silhouette to
check attack reach. Keep all poses consistent and full bodies visible.

Before implementation, decide which player movement options are guaranteed.
Every basic enemy should have a counter using those options; jumps, dashes,
spells, and directional attacks should only be required if already established.
