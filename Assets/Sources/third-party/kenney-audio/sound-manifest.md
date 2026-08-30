# Sound manifest

| Gameplay event | File stem(s) | Intended use |
|---|---|---|
| Player step | `player-footstep-01` through `04` | Random concrete-footstep variant while grounded and moving |
| Player jump begins | `player-jump` | Mechanical upward impulse |
| Normal landing | `player-land-soft` | Low or medium downward landing speed |
| Hard landing | `player-land-hard` | High downward landing speed |
| Sword attack starts | `sword-swing-01`, `02` | Alternate swing variants |
| Sword damages enemy | `sword-hit-01` through `03` | Random metallic hit variant |
| Fireball launches | `fireball-launch` | Actual projectile release, not button press |
| Fireball hits world/enemy | `fireball-impact` | Projectile deactivation caused by a hit |
| Player takes damage | `player-hurt` | Successful damage after invulnerability check |
| Enemy takes nonlethal damage | `enemy-hurt-01`, `02` | Random variant |
| Enemy is defeated | `enemy-death` | Lethal damage transition |
| Boiler Brute attack starts | `hammer-windup` | Start of hammer animation |
| Boiler Brute impact frame | `hammer-impact` | Impact frame, even if the player is missed |
| Player resets | `player-respawn` | Pit fall or defeat reset |
| Goal first reached | `goal-reached` | Play once on the false-to-true goal transition |

Every stem exists as a WAV input in `Assets/Static/audio/sfx/` and is copied to
`Assets/Runtime/audio/sfx/` by the asset pipeline.
