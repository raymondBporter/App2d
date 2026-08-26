# Sound manifest

| Gameplay event | File stem(s) | Intended use |
|---|---|---|
| Player step | `player_footstep_01` through `04` | Random concrete-footstep variant while grounded and moving |
| Player jump begins | `player_jump` | Mechanical upward impulse |
| Normal landing | `player_land_soft` | Low or medium downward landing speed |
| Hard landing | `player_land_hard` | High downward landing speed |
| Weapon changed | `weapon_switch` | One cue per successful weapon-cycle step |
| Sword attack starts | `sword_swing_01`, `02` | Alternate swing variants |
| Sword damages enemy | `sword_hit_01` through `03` | Random metallic hit variant |
| Grapple launched | `grapple_fire` | Start of extension |
| Grapple attaches | `grapple_latch` | Successful ceiling latch only |
| Grapple starts returning | `grapple_retract` | Miss, manual cancel, or post-hit retraction |
| Grapple released | `grapple_release` | Player releases an active latch |
| Ball and chain thrown | `ball_throw` | Start of throw |
| Ball lands | `ball_land` | First grounded transition after a throw |
| Ball is yanked | `ball_yank` | Start of return trip |
| Fireball launches | `fireball_launch` | Actual projectile release, not button press |
| Fireball hits world/enemy | `fireball_impact` | Projectile deactivation caused by a hit |
| Player takes damage | `player_hurt` | Successful damage after invulnerability check |
| Enemy takes nonlethal damage | `enemy_hurt_01`, `02` | Random variant |
| Enemy is defeated | `enemy_death` | Lethal damage transition |
| Boiler Brute attack starts | `hammer_windup` | Start of hammer animation |
| Boiler Brute impact frame | `hammer_impact` | Impact frame, even if the player is missed |
| Player resets | `player_respawn` | Pit fall or defeat reset |
| Goal first reached | `goal_reached` | Play once on the false-to-true goal transition |

Every stem exists as a WAV file in `Sfx/`.
