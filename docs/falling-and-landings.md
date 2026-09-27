# Falling and landing tuning

`TraversalMetrics2D` owns the fall curve and landing severity. Normal gravity applies
until 80% of the 1,100 px/s terminal speed. Beyond that, acceleration scales with the
square of the remaining fraction of the 220 px/s drag band. The transition keeps
acceleration continuous and makes the last few percent take seconds. Ordinary jumps
never reach this band. Horizontal acceleration, dash, wall grip, and ladder control
retain their existing behavior.

The curve is integrated analytically, including ticks that cross into the drag band.
The motor passes the resulting effective vertical acceleration to physics before
collision handling. Jump prediction uses the same curve. A downward impulse above
terminal speed decays toward terminal with a 0.5-second exponential time constant;
it is not immediately clamped.

Hard landing sound, facial reaction, crouch, and camera feedback begin at 95% of
terminal speed (1,045 px/s). The extra camera emphasis grows from 1 to 1.75 between
95% and 99%; its reciprocal-speed curve follows time spent in the fall tail, then
saturates. No airtime gate is needed, so a sudden downward throw can land hard.
Ordinary landing sounds still play, but ordinary jumps do not trigger the deep crouch.

From rest, under full gravity, reaching 90% takes about 0.58 s, 95% about 0.81 s,
and 99% about 2.66 s. Jump apex assistance can slightly increase total descent time.

## Drop tower

The durable cavern map includes a ladder just left of **Trailhead**, at world X = -432
(tile column 2). From the starting spawn, walk left about six tiles and hold Up.
Exit right onto any board and walk off its right edge. The higher boards extend
farther right, leaving a clear drop past all lower boards to the original flat ground.
Use jump to leave the ladder if needed. The boards are one-way platforms.

| Board height | Intended comparison |
| --- | --- |
| 4 tiles / 128 px | Ordinary jump-sized, soft landing |
| 12 tiles / 384 px | Fast fall, still below hard landing |
| 24 tiles / 768 px | First clearly hard landing |
| 48 tiles / 1,536 px | Sustained fall, stronger impact |
| 80 tiles / 2,560 px | Long fall, near maximum emphasis |

`python tools/LevelLab/add_drop_tower.py` reproduces the map edit and is idempotent.
It changes three terrain chunks, preserves world things and the spawn, and refuses
to overwrite nonempty terrain. Reopen the level after changing the file externally.

Automated coverage exercises the motor at these five heights, normal gravity and
air steering, approach timing, tick-size independence, a fast short-distance throw,
and the authored animation's hard-landing threshold. The board spacing and subjective
camera/sound strength should also be judged in a play session.
