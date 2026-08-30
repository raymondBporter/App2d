# Physical Restructure, Composite Shapes, and Rotational Contacts

**Date:** 2026-08-29
**Status:** Approved in chat (restructure into project folders; rotational contacts + friction; full scope)

## Context

The library projects compile linked sources from the `App2d` folder, which made VS
Solution Explorer moves silently evaporate (wildcard links regenerate on reload) and
left project folders on disk empty. The user wants source to physically live in its
owning project. Separately: dedupe projection math, add `Area`, add a non-convex
`CompositeShape2D`, and make a composite prop that tumbles when hit with the sword.

## Part 1 — Physical restructure (pure `git mv`, no content changes)

Code moves into its owning project folder; all `<Compile>` link machinery and
`EnableDefaultCompileItems=false` are deleted so every project uses default compile
items. Namespaces stay `App2d.Engine.*` (alignment is a later, separate pass).

| From (under `App2d\`) | To |
|---|---|
| `ArgGuard.cs`, `StateGuard.cs`, `Engine\FrameTime.cs`, `Engine\SpatialObject2D.cs`, `Engine\Animation\`, `Engine\Mathematics\`, `Engine\Geometry\` | `App2d.Core\` (folders at project root) |
| `Engine\Collision\*` | `App2d.Collision\` |
| `Engine\Tiles\*` | `App2d.Tiles\` |
| `Engine\Physics\*` | `App2d.Physics\` |
| `Engine\Rendering\*`, `Engine\Camera2D.cs`, `Engine\Scene2D.cs`, `Engine\WorldObject2D.cs` | `App2d.Rendering\` |
| `Engine\Audio\*` | `App2d.Audio\` |
| `Gameplay\*` except `PlayerInputMapper2D.cs`, `XboxControllerInput2D.cs` | `App2d.Gameplay\` |
| `Engine\Game2D.cs`, `Engine\GameHost.cs`, `Engine\InputState.cs`, `Engine\Diagnostics\` | `App2d\` root (composition root keeps hosting + diagnostics + the two input files) |

`App2d.csproj` drops its entire `Compile Remove/Include` block. The `Assets` item
group and project references are unchanged. No `Engine` folder exists anywhere after
the move. README folder references update to the new layout.

## Part 2 — Math dedupe and Area

- **`Interval1D`** (`App2d.Core\Geometry`): `readonly record struct Interval1D(float Min, float Max)`
  with `static ProjectPolygon(ReadOnlySpan<Vector2>, Vector2 axis)` and
  `static ProjectCapsule(Vector2 start, Vector2 end, float radius, Vector2 axis)`.
  `ShapeCollision2D` SAT paths (`ProjectPolygon`/`ProjectCapsule`/`TryUpdateMtv`) use it
  instead of loose min/max floats.
- **Lerp:** the two hand-rolled blends in `SideScrollerCamera2D` (lines ~140/146) become
  `float.Lerp`. No custom lerp helper — the codebase already uses the BCL ones.
- **`Area`** on `IShape2D`: circle `πr²`; rectangle `w·h`; capsule `2r·length + πr²`;
  convex polygon `|SignedAreaTwice|/2`; half-space `float.PositiveInfinity`; composite
  sums parts (overlapping parts double-count — documented). Supports future
  `density × area` mass; exact second moments are out of scope.
- **Half-space convexity:** mathematically convex but not compact — no finite support
  point except along `-Normal` — so it stays `IShape2D`-only by design.

## Part 3 — CompositeShape2D

`App2d.Core\Geometry\CompositeShape2D : IShape2D` holding `IConvexShape2D[]` parts
positioned in composite-local space (parts carry their own local geometry — no
per-part transforms). `LocalBounds` = union; `ContainsPoint` = any part; `Area` = sum.

**Not** `IConvexShape2D`: a union support map would make SAT collide against the convex
hull, filling notches of non-convex composites. Instead `ShapeCollision2D.Dispatch`
gains a first-priority `CompositeShape2D` row: loop parts, dispatch each part against
the other shape (both dispatch orders, reusing existing rows), keep the deepest
contact. Composite-vs-composite terminates through the reverse dispatch (part vs other
composite → other composite loops its parts against that part). One deepest contact
per pair per iteration is sufficient because `PhysicsWorld2D` re-collects contacts
every position iteration. Parts are limited to circle/capsule/rectangle (the
implemented dispatch rows).

`RayIntersection2D.TryIntersectLocal` gains a composite case: nearest hit across
parts. `Renderer2D` gains composite cases in `Draw`, `DrawShapeOutline`, and the
private `DrawShape` (loop parts, recurse), so a composite renders as a normal
`WorldObject2D` and the debug collision overlay works.

## Part 4 — Rotational contacts + friction

`PhysicsBody2D` gains `FreezeRotation` (**default true** — every existing body keeps
today's behavior exactly) and `Friction` (**default 0** — no behavior change until a
body opts in). `ImpulseVelocitySolver2D` becomes the standard 2D contact impulse with
rotational terms: lever arms from `contact.Geometry.Point` to each body's
`Transform.Position` (center-of-mass approximation, documented), effective mass
`invM1 + invM2 + invI1·(r1×n)² + invI2·(r2×n)²`, relative velocity including `ω×r`,
angular impulse `invI·(r×J)`. Friction: tangential impulse clamped by
`μ·|normal impulse|` with `μ = sqrt(f1·f2)`. A body's rotational terms use inverse
inertia 0 when frozen or non-dynamic. `MassWeightedPositionSolver2D` stays linear.

## Part 5 — TumbleProp2D

`App2d.Gameplay\TumbleProp2D : IEnemyActor2D, IEnemyCombatant2D`: a dumbbell composite
(rectangle bar + two end circles) built as a single `WorldObject2D` that is both the
scene visual (`SolidColorShader`) and the physics collider. Dynamic body:
`FreezeRotation=false`, `Friction≈0.4`, modest restitution, hand-tuned
`MomentOfInertia`. Enemy-layer collider, world-layer mask, huge `Health2D` (never
dies), `TryRegisterHit` dedup like `PatrolEnemy2D`. `TakeDamage` converts the
knockback vector into a linear velocity kick plus an angular kick signed by hit
direction — the sword's existing `CombatSystem2D.ResolveAttack` path needs zero
changes. Spawned near the player spawn by `SideScrollerEncounterSpawner2D` on a flat
ground run; registered with `EnemySystem2D` for chunk streaming.

## Testing

xUnit as before. Restructure is verified by full build + existing 30 tests (no content
changes). New tests: `Interval1D` projections; `Area` values; composite notch does NOT
collide (proves non-hull), composite deepest-contact and ray nearest-part; solver
angular response (off-center contact spins an unfrozen body, frozen body unchanged)
and friction clamp. Manual: run the game, whack the prop, watch it tumble and settle.

## Out of scope

Namespace alignment to project names; exact second moments / density-driven inertia;
contact manifolds (multi-point); polygon-vs-polygon dispatch rows; friction on the
player/enemies (they stay at 0).
