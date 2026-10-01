# Player spells: playable first pass

The player always has the sword. There is no weapon-selection command or loadout cycle.
The gun appears while casting and recovering from a shot, then the player returns to melee.

| Action | Xbox | Keyboard/mouse | Initial behavior |
| --- | --- | --- | --- |
| Melee | X | F / left click | Sword attack; hits do not restore energy |
| Spell shot | Hold Y | Hold Q / right click | Charge for 0.25 s, spend 30 energy, fire once, recover for 0.2 s |
| Heal | Hold B | Hold R | Stand on the ground for 1 s, spend 30 energy, restore 20% of maximum health |

Energy starts at 90 and resets to the starting amount on respawn. It restores 10 points
per second during active play, including combat and casting, up to the maximum of 90.
Recharge pauses with simulation and does not bank extra energy at full capacity.
Holding the shot button cannot repeat shots; release and press again. Holding
heal repeats completed cycles until health is full or energy runs out. A partial heal
at the health cap still costs one cast. Neither spell grants invulnerability.

Release, damage, dashing, climbing, editor entry or input suppression cancels an
unfinished shot without spending energy. Healing also cancels on movement, jumping,
attacking or losing ground support. Damage and pause require releasing heal before
another channel can begin. Movement remains responsive during a shot. Melee, charging,
healing and shot recovery cannot overlap. Damage does not affect energy recovery.

## Tuning and assets

Edit `Assets/Static/gameplay/player-spells.json` for capacity, starting energy,
recharge rate (`energyPerSecond`), costs, charge/recovery times, heal interval and healing fraction. The host
loads the runtime copy at launch; the session owns the resulting immutable settings.
Projectile damage/size/speed still come from the hero's authored shoot action.

To iterate only on spell tuning/audio without rebuilding every asset:

```powershell
Copy-Item Assets/Static/gameplay/player-spells.json Assets/Runtime/gameplay/player-spells.json
.venv/Scripts/python.exe tools/ArtPipeline/build_spell_audio.py --content-root Assets/Runtime
```

Restart the game after tuning. The full `build_runtime_assets.py` pipeline also copies
the settings, generates the cues and refreshes the runtime manifest for packaging.

The first-pass art is intentionally simple:

- `player-gun-charge.json`: free hand gathers beside the pistol; an upper-body overlay
  keeps locomotion running. Wall casting keeps the gripping hand on the wall.
- `player-heal-gather.json`: grounded crouch with hands drawn toward the chest.
- Both gathering clips use normalized progress, independent of their authored duration.
- The existing blue muzzle glow, flash, bolt and trail are reused. Healing adds inward
  rings and motes, an outward completion pulse and an interruption fade.
- Short synthesized gun charge, healing hum, completion chord and cancellation tone.

Regenerate only the two new body clips with Character Studio's
`--write-spells Assets/Characters/authored`. This does not rewrite existing moves or props.

## Verification and preview

`App2d --spell-study Assets/Work/spell-study` runs actual simulation and produces charge,
release, heal and completion screenshots in both facings, including the HUD. It checks
health/energy changes and sound dispatch. It is silent and does not open or save a level.

Gameplay tests cover configurable timing, input interruption, resource spending and
refill, repeated healing, full-health clamping and rollback of health, energy and
events. Presentation tests check temporary gun visibility and normalized animation
progress. Input tests verify Xbox Y/B and keyboard/controller suppression.
