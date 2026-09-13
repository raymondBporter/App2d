# Airborne downward sword attack

`sword-downward.blend` contains the editable `sword-down-attack` action: six
poses, 24 fps, 0.25 seconds, non-looping. The character faces right. The first
pose is already fully stabbed downward, with no wind-up. The remaining poses
hold, retract the blade, and return to an airborne guard. The character's
normal jump/fall movement continues throughout.

The original RGS Grease Pencil sword, character drawings, and fourth drawn sweep
shape are reused. The narrow slash flash appears on frames 1–2. Its leading edge is fitted
to the transformed blade and it is compressed across its trailing direction to
keep the sword legible. The sweep remains controlled by the original
`slashEffect` bone and its `slashFX` custom property. `poses.json` labels each
frame. Existing balance assets are in the parent directory and are unaffected.

Edit the `sword-down-attack` action in this file and save it, then run from the
repository root:

```powershell
& .\tools\ArtPipeline\render_sword_attack.ps1 -BuildRuntime
```

Without `-BuildRuntime`, the command only refreshes the cached PNGs. The normal
runtime asset build imports this recipe along with the parent balance recipe.
The cached source renders use the same original camera and 512×512 canvas;
the importer applies the shared 1.35× scale and foot-anchor normalization.

`create_downward_sword.py` creates the initial poses and should not be used for
ordinary re-renders. It refuses to replace an existing source without
`--replace`. `render_sword_attack.ps1` renders the saved edits without overwriting
the source file.

`python tools/ArtPipeline/preview_downward_sword.py` generates normal-speed and
quarter-speed GIFs, a contact sheet, and canvas checks under
`Assets/Library/characters/player-sword/downward-attack`. The GIFs add a 0.6-second
review pause after each playback; that pause is not part of the game clip.

Gameplay selects this clip with Down + primary attack while airborne, away from
walls and ladders and outside a dash. Gameplay uses the authored 0.25-second
duration (24 fps), applying sword damage below the player immediately during frames 1–2.
Connecting with an enemy or spikes bounces the player upward once per attack,
preserving sideways movement. A successful bounce takes priority over contact
damage on that frame. Taking damage or switching equipment cancels the attack.

The earlier swinging version is archived for comparison under
`Assets/Library/characters/player-sword/downward-attack/swing-version`.

Source artwork license and provenance: RGS Dev CC0, documented under
`Assets/Sources/third-party/rgs-stick-figure` and on the
[original asset page](https://rgsdev.itch.io/animated-stick-figure-character-2d-free-cc0).
