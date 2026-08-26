# Provenance and license

Kenney source assets are Creative Commons Zero (CC0). Attribution is not required, but
the original pack license files are included. Procedural layers were created specifically
for this App2d placeholder pack and may be used, modified, and redistributed without
restriction.

Official source packs:

- [Impact Sounds](https://kenney.nl/assets/impact-sounds) — CC0
- [Interface Sounds](https://kenney.nl/assets/interface-sounds) — CC0
- [RPG Audio](https://kenney.nl/assets/rpg-audio) — CC0
- [Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds) — CC0

## Exact source mapping

| Output | Kenney source file(s) or recipe |
|---|---|
| `player_footstep_01` through `04` | Impact Sounds `footstep_concrete_000.ogg` through `003.ogg` |
| `player_jump` | Procedural upward pitch sweep, filtered air, and mechanical transient |
| `player_land_soft` | Impact Sounds `footstep_concrete_004.ogg` plus procedural low thud |
| `player_land_hard` | Impact Sounds `impactPunch_medium_001.ogg` plus procedural low thud |
| `weapon_switch` | Interface Sounds `switch_003.ogg` |
| `sword_swing_01`, `02` | RPG Audio `knifeSlice.ogg`, `knifeSlice2.ogg` |
| `sword_hit_01` through `03` | Impact Sounds `impactMetal_light_001.ogg`, `impactMetal_light_002.ogg`, `impactMetal_medium_000.ogg` |
| `grapple_fire` | Procedural cable sweep plus Sci-fi Sounds `laserSmall_003.ogg` |
| `grapple_latch` | RPG Audio `metalLatch.ogg` plus procedural low thud |
| `grapple_retract` | Procedural descending cable sweep and mechanical ticks |
| `grapple_release` | RPG Audio `metalClick.ogg` |
| `ball_throw` | Pitch-shifted RPG Audio `knifeSlice2.ogg` plus procedural cable sweep |
| `ball_land` | Impact Sounds `impactPlate_heavy_001.ogg` plus procedural low thud |
| `ball_yank` | Procedural cable sweep plus Interface Sounds `minimize_004.ogg` |
| `fireball_launch` | Speed-adjusted Sci-fi Sounds `laserLarge_001.ogg` |
| `fireball_impact` | Sci-fi Sounds `explosionCrunch_002.ogg` |
| `player_hurt` | Impact Sounds `impactPunch_medium_002.ogg` plus Interface Sounds `glitch_002.ogg` |
| `enemy_hurt_01`, `02` | Impact Sounds `impactPunch_medium_000.ogg`, `impactPunch_medium_003.ogg` |
| `enemy_death` | Sci-fi Sounds `impactMetal_004.ogg` plus procedural low thud |
| `hammer_windup` | Slowed RPG Audio `knifeSlice2.ogg` |
| `hammer_impact` | Impact Sounds `impactPlate_heavy_003.ogg` plus procedural low thud |
| `player_respawn` | Procedural down/up energy sweeps |
| `goal_reached` | Interface Sounds `confirmation_004.ogg` |

All outputs were trimmed, faded, downmixed to mono, resampled to 44.1 kHz, and level-matched
by role. WAV files use signed 16-bit PCM.

## Download verification

| Original archive | SHA-256 |
|---|---|
| `kenney_impact-sounds.zip` | `029D734AF1582474EDF3A694D1B0CEBC97C1C152F2F39FA34D4C2BAFC5DE77F8` |
| `kenney_interface-sounds.zip` | `F2193D072726D6758A5F7871B2DCC54DCCE0D5C35C6F0A62F92549B327C81232` |
| `kenney_rpg-audio.zip` | `6DBEAF8544DA958D8F2ADCB4A4A4B76C1ADE34A05F8AB9EDCCD327DA7375F38B` |
| `kenney_sci-fi-sounds.zip` | `119340F351A5098AD814F78719438C0DA355A9CE8A4C8A3AF6A8D48AA3D49E04` |
