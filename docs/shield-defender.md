# Shell-shield defender

Playable first pass: the Ruin sentry at x7568 in cavern is now a shell-shield defender. Place more through the Things palette; open `shield-defender` in Character Studio.

## Behavior and counterplay

He approaches at 32 units/second, holds the shell forward, and commits to a bash within 44 units. Protection requires a strike from his front overlapping the drawn shell. Rear attacks and hits above or below the shell hurt him normally. Blocks stop projectiles and consume that swing without damage or stagger.

The shell pulls back at 0.20 seconds, opening protection for the remainder of the action. Bash damage is active from 0.55 to 0.70 seconds; recovery continues until 1.40 seconds. Bait the bash, dodge, then punish recovery. Facing stays committed during the action. Normal damage interrupts him. Health 8, bash damage 2, cooldown 0.5 seconds. Pursuit respects terrain and ledges.

## Editable assets

- `entities/shield-defender.json`: stats, guard coverage, controller, cues and damage window.
- `variants/shield-defender-build.json`: existing caveman rig proportions.
- `props/scavenged-shell.json`: editable green shell mesh; own hair, beard and hide wrap props.
- `animations/shield-defender-idle.json`, `shield-defender-walk.json`, `shield-defender-bash.json`: mathematically keyed poses with a planted bash stance. Hit/death reuse the person clips.

All paths above are under `Assets/Characters/authored`. `python tools/ArtPipeline/build_shield_defender.py` explicitly regenerates and overwrites this first pass; ordinary builds never regenerate it. Keep later hand edits in the assets or update the seed script deliberately.

## Review and limits

Wardrobe smoke exports `shield-defender-frames`; the GIF packer creates `shield-defender.gif`. Gameplay tests cover both facings, shield-only contact, rear/head vulnerability, projectile interception, swing deduplication, interruption, bash timing and deterministic replay.

Sound cues use existing free placeholder assets: hammer impact for block, hammer windup for pullback, sword swing for bash. This needs a human playtest for feel and sound choice. Guard collision uses the posed shell bounds, matching the existing authored collision approach; it is not per-triangle collision.
