# Player move set on the authored Person

**Status:** first full pass built, awaiting review. Review page: https://claude.ai/artifact/JjUTZhsbWLCCTEWDbh6UtT

## Goal

Replace the in-game player's animations (the 3D-sourced point library behind `PointPersonPresentation2D`) with a
complete move set drawn on the authored Person rig, in the style of the existing `person-walk` and `person-run`. This
is art: clip, prop and socket files. The game now plays them; see "In the game" below.

## Decisions

- **Rig and view.** The phase-3 Person model: right-facing, turned toward the viewer by one yaw shared by hips and
  shoulders. The near (left) limbs face the camera; the far right hand carries the sword and gun. Every clip is checked
  for a shared-turn offset from rest, so the old arms-turned-differently-from-legs fault cannot come back.
- **Method.** Each move is a short key-pose script (`App2d.CharacterStudio/PlayerMoves/PlayerMoves.cs`) that writes
  a plain clip file. `MoveBuilder` turns animator terms (foot and hand positions in actor space, torso lean, blade
  angle) into tracks, and solves hands against the shoulders the body keys actually produce.
- **Sheathed sword.** A scabbard on the `back` socket is always worn. The sword sits there too, except between a clip's
  `sword-draw` and `sword-sheathe` markers, when it rides the `sword-hand` socket. In profile the whole sheath hides
  behind the torso, hilt below the shoulder line, with only the tip showing. The latest of a clip's `view-back` /
  `view-profile` markers picks the socket: `back-view` wears it across the back. The draw is the wind-up of the
  slash; the sheathe clip twirls the blade back over the shoulder. `gun-hand` holds the pistol.
- **Layering.** Upper-body clips (the gun shot) key only chest, head, shoulders and arms, so they layer on any legs.
  The review page's Layered cards build aim+shot, walk+shot and run+shot from existing clips.
- **Reference.** RGS stick-figure pack (CC0) for climb, dash, wall slide, hit, death and air attack poses and timing;
  first principles elsewhere.
- **Timing.** Durations fit current gameplay: dash 0.16 s, sword 0.35 s (damage 0.10–0.27), downward attack 0.25 s
  (damage from frame one), jump leaves the ground on the press (no crouch).
- **Turning and back views.** No perspective at this size, so a turn is orthographic: each girdle's near-to-far axis
  spins in the ground plane (rest 60 degrees, profile 90, back 180), the limbs slide across and near and far swap depth.
  `player-climb-on` turns onto the ladder; `player-climb-off` is the same keys reversed; the climb is the back view.
  Face keys may be `none`, and target keys may set `bend` (held from that key on) so an elbow or knee flips side where
  the limb passes straight. Both are small engine additions.
- **Out of scope this round.** Hard landing, wall melee attack, unarmed punch and kick.

## Move list

| Group | Clips |
|---|---|
| Movement | `person-walk`, `person-run` (existing), `player-idle`, `player-jump`, `player-fall`, `player-land`, `player-dash` |
| Traversal | `player-climb-on`, `player-climb`, `player-climb-off`, `player-wall-grip`, `player-balance-forward`, `player-balance-backward` |
| Reactions | `player-hit`, `player-death`, `player-celebrate` |
| Sword | `player-sword-draw-slash`, `player-sword-slash`, `player-sword-sheathe`, `player-sword-down-attack` |
| Gun | `player-gun-aim`, `player-gun-shot` (upper body), `player-gun-wall-shot` |

Props: `sword`, `sheath`, `pistol`. Sockets added to `person`: `back`, `back-view`, `sword-hand`, `gun-hand` (mirrored in
`StarterContent.AddPersonExtras`).

## In the game

The in-game player is drawn by `AuthoredPersonPresentation2D` (`App2d.Game.Presentation/Persons/`), which replaced the
point-library `PointPersonPresentation2D`. Gameplay is unchanged: traversal, hit boxes and attack timing still come from
`Person2D` and `entities/player.json`.

- `PersonMoves` resolves the Person model, every clip in the move list and the three props, and fails naming anything missing.
- `PersonAnimationDirector` maps observed state to a clip: death and hit reactions, then attacks (controller timing mapped
  onto the clip), dash, celebrate, then locomotion. Sword swings draw first and follow up with the slash while the sword is
  out; standing still afterwards plays the sheathe. The gun shot and the aim are upper-body overlays on whatever the legs do
  (`PoseInput.Overlay`, masked by `PersonLoadout.UpperBody`); standing with the gun plays the full aim.
- Walk, run and climb advance by distance (climb: `ClimbRise` = 1.1 per cycle), capped at `MaxCadence` = 2.5 times the
  clip's own pace. Beyond the cap feet follow the clip instead of holding world anchors.
- `PersonLoadout` (Core) owns the prop rules above, shared with the review renders.
- The figure is scaled so its rest height matches the traversal collider; the blended gameplay face (`PersonFace2D`) is
  drawn on it, and back views still hide it.
- `App2d --render-smoke <dir>` writes `player-*.png`: eleven states in both facings through the real presentation.

Known gaps: the game's ground speed (430 px/s, about 13 model units/s at the drawn scale) is roughly six times the run's
authored pace (2.2 units/s), so at full speed the run is capped and the feet slide; a longer stride or a sprint clip is an
art decision. Punch and kick reuse the slash with no sword; wall melee reuses the slash; the climb-off turn plays at the
top of a ladder as well as the bottom.

## Review loop

`python tools/MoveReview/build.py <out>` regenerates the clips, renders every move at 30 fps through the game's drawing
path (`App2d.CharacterStudio --review-moves`), writes mechanical checks (contacts, reach, loop seams, shared turn),
and assembles `<out>/site` for publishing over the same artifact URL. Verdicts and notes left on the page are stored in
its `reviews` collection, one document per move, and read back to drive the next pass.

## Open questions for review

1. Turn onto the ladder: 0.3 s, girdles round in the first 0.15 s.
2. Wall grip faces away from the wall, which is how gameplay already turns the player.
3. Sheath size and placement; sword length (0.82 units).
4. Downward attack as a pogo stab rather than a downward slash.
