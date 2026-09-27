# Combat contact



Sword contact uses the confirmed `CombatDamage2D.Contact` fact: attack source and

sequence, target, overlap position, incoming direction and kill outcome. Authored

targets select a point inside the attack/hurt-region overlap before applying their

reaction. This is a representative overlap point, not an exact blade intersection.



`CombatContactPresentation2D` draws five short, outlined ink strokes from that point.

An ordinary hit lasts 115 ms; a kill is 1.3 times larger and lasts 160 ms. The same

fact supplies the sword contact sound; existing enemy hurt/death voices remain.

The effects use the presentation clock and never change player movement, attack

timing or the simulation clock. Terrain/spike bounce sounds retain their weapon event.



Humanoid hit clips recoil immediately, reach their extreme at 1/60 s, hold briefly

and recover within the existing 300 ms stagger. Death shares the initial recoil

before folding to the ground. Horizontal reaction offsets, torso rotation and prop

orientation reverse when struck from behind, without turning the actor. That role

direction is captured with the animator for rollback. The simulation evaluates the

reaction on the damage tick, so drawing and hurt regions still use the same pose.

Dead actors retain the existing static physics behavior; the collapse is animation.



An invulnerable target may still cause a downward bounce. It emits no damage fact,

so it produces no new damage burst or sword-damage sound. A missed swing also has no

contact effect. Each damaged target in a multi-target swing gets its own fact.



## Review



Run `dotnet run --project App2d -- --contact-study output/contact-study`, then open

`output/contact-study/index.html`. The page contains synchronized real game renders

at 60 fps for miss, hit, kill, run-through and immediate reversal. It supports pause,

scrubbing, quarter speed and an impact-burst toggle. The preview is silent.

`contact-report.txt` records confirmed impacts and movement checks.



The first art pass targets the shared humanoid reactions. Creature-specific clips,

camera shake and physical corpse movement remain separate refinements.



## Reaction timing comparison

Run `dotnet run --project App2d -- --hitstop-study output/contact-study/timing`
and open that directory's `index.html`. Four synchronized variants compare Normal,
old Strong+ (enemy only), Contact Hold (both actors, 70 ms) and Exaggerated (both
actors, 140 ms). The new curves hold immediately, ease back up over 25 ms and repay
the delay at 2.5x or 3.5x peak speed. They rejoin live animation by 181 or 261 ms.
The old Strong+ curve starts at normal speed and only touches zero momentarily.

Player animation frames are sampled from a short history so slowdown can cross the
attack/recovery boundary without popping the sword back. Prop markers use the sampled
clip. Root positions and facing stay live; locomotion keeps live legs with delayed
upper-body sword motion. Enemy reactions are sampled with their current world roots.
Simulation, hitboxes, sound events and effect lifetimes retain their original clocks.

Actions cover ordinary hits, kills, running through and reversing immediately. Frames
preload before playback. Full recovery plays by default; a short contact-only loop is
optional. Contact Hold is now the runtime setting for confirmed sword damage, including
kills. Normal, old Strong+ and Exaggerated remain diagnostic comparisons. Rumble and
camera shake are excluded so the animation timing can be judged on its own.

Runtime tuning lives in `App2d.Game.Presentation/World/Presentation/CombatHitstop2D.cs`:
`HoldSeconds = .070f`, `ReleaseSeconds = .025f`, and `RecoveryPeak = 2.5f`.
There are no editor controls or configuration assets. The client feeds accepted damage
facts to the attacking player's view and the contacted enemy's view. New attacks and
player reactions cancel the previous hold; enemy role changes, removal and disabling
clear their hold. Respawn, suspension and editor refresh clear presentation state.
The same samplers drive the diagnostic comparison and runtime rendering.

Sword continuity fixes: an earlier landing no longer interrupts the attack-to-recovery
transition. Recovery has its own clock and layers its upper body and sword orientation
over walking, running and airborne poses, so movement does not instantly put the sword
on the back. Prop attachment follows that overlay's markers. The authored return keeps
the swing's unwrapped angle, avoiding an extra revolution before the hand reaches the
back hilt. Regression checks cover these transitions and hand/back alignment.

The runtime sword now uses the swing study's approved broad, chisel-tipped cartoon blade.
PlayerMoves.SwordProp generates that art, and hero.json's three sword attack bounds are regenerated from the longer blade.
