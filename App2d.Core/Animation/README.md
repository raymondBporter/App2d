# Keyframe sampling

`KeyframeSampling.Sample` reads an ordered timeline at an absolute time. It finds the surrounding keys with binary
search, computes progress within that interval, and calls a value reader or segment evaluator. Callers can reuse
static callbacks; no keyframe interpolation interface or character-specific interpolation policy type is required.

```csharp
var value = KeyframeSampling.Sample(keys, seconds, emptyValue, getValue, sampleSegment);
```

Keys implement `ITimedKey` and are ordered by increasing `Time`; asset validation owns that requirement.
Times use the caller's units and are not restricted to [0, 1]. An empty timeline returns `emptyValue`.
A single key is held everywhere. Queries outside the range hold the first or last value. At an exact key time,
the key's own value is returned without segment interpolation, including for outgoing step segments.
`TrySegment` remains available for consumers needing the two keys and their local progress.
`Held(keys, time, holdBeforeFirst: false)` instead returns no key before the first time; attachment and draw-order
tracks use this to preserve setup state. A present key with a null value is distinct from having no key yet.

The timeline returns exact keys and endpoint holds before evaluating a strategy. A segment evaluator resolves the
saved curve descriptions into core interpolation strategies. `Interpolation.Hold<TValue, TReal>()` describes a held
value; step easing can also hold progress at zero until the next key. Motion and color retain independent
component curves, absolute handles, authored full rotation turns, and their different empty-track defaults.
Setup-before-first behavior remains with the owning track, and color clamping remains with color evaluation.

Legacy motion and color keys store `EasingCurve` and `EasingDirection` separately and resolve them through `Easingf.FromCurve`.
The enum converters keep existing `linear`, `smooth`, and `step` curve names; omitted direction defaults to `in`.
For example, an in-out sine uses `"ease": "sine", "easeDirection": "in-out"`. Travel belongs to the motion or
playback clock. Step with direction In holds the outgoing key; other step directions use the transformed function.
`KeyframeSampling` selects timeline segments; `Interpolator<TValue, TReal>` evaluates values within one.

`IInterpolatable<TSelf, TReal>` supplies a value type's default rule in Mathematics. An `Interpolator<TValue, TReal>`
can use that default, hold a value, take a custom function, change coordinates, or override any number of typed channels.
`WithProgress(map)` transforms time and `With(channel, child)` replaces a part using its own strategy. Motion binds
its saved X/Y/Z or angle curves to these channels; color binds R/G/B/A. There is no separate component sampling API.
`PoseEvaluator.Sample` names the timeline operation; the actual numeric work uses the shared strategies.

`AuthoredInterpolation.CreateOverride` turns saved Bezier arguments into `Interpolation.Bezier` strategies. Relative
controls shape progress; absolute controls specify actual values and can produce excursions between equal endpoints.
For compatibility, Step/In suppresses saved component overrides; the step progress map itself provides the hold.
Other ease directions retain the existing custom-curve precedence. Exact first and last key values are held by the timeline.

Existing `curve` / `curveY` / `curveZ` and color component curve objects deserialize into `BezierParameters`.
This record contains only four control arguments, the `Absolute` flag, and validation. Its JSON fields remain
`x1`, `y1`, `x2`, `y2`, and `absolute`, including the previous sparse defaults. There is no separate key-curve
evaluation, geometry conversion, slicing, or editor conversion API. Function construction lives in the shared
mathematics factories. Version 4 clips retain this representation.

Clip version 5 can store a core vector `interpolator`, scalar `angleInterpolator`, or color `interpolator` directly.
The [mathematics converters](../Mathematics/Serialization/README.md) read and write these recipes and build their
functions during loading. Sampling uses the loaded strategy directly; it does not translate its recipe every frame.
Keys with core recipes must use the defaults for legacy ease/curve fields, and rotation/vector roles stay explicit.
`player-idle.json` is the first migrated asset; its key values and sampled poses are compared with a preserved version 4 fixture.

Sampling belongs to Animation. Numeric interpolation and progress maps remain in Mathematics; geometric curves remain
in Geometry. Interpolation computes a value between endpoints; easing maps its progress. Integrating a velocity over
time is a separate operation. The sampler adds neither clock advancement nor animation events, loops, or serialized
track types. Existing version 4 JSON remains readable alongside the version 5 trial.

`KeyframeEditing` supplies `AtTime`, `Put`, `Move`, and `DeleteAtTime` on the existing key lists. Authoring uses a shared
time tolerance; sampling still selects exact key times. `Put` maintains time order when inserting, and `Move` delegates
copying the key's payload to its caller (draw-order keys also copy their mutable slot list). Easing and curve metadata
travel with the copied key. Callers own clip bounds and which lists an edit targets.
Editable keys implement `IEditableTimedKey`, adding a writable time to the read-only sampling contract. Studio uses one
enumeration of motion, travel, expression, attachment, color, and draw-order keys for key times and retiming; retiming
updates the existing objects. Markers, events, and contact intervals retain their separate editing semantics.
`SliceHeld` shares excerpt creation for expression, attachment, and draw-order timelines, including exact end keys.
Attachments and draw order retain setup state until their first key; expressions retain their first selection before
its key, with playback waiting for that key. A selection active when the excerpt starts restarts at excerpt time zero,
as before. Payload copying stays with the caller so draw-order slot lists remain independent.

`AnimationClip2D<TFrame>` is a discrete frame sequence. Uniform timing uses an implicit `UniformSequence<float>`
of end times and one shared frame duration; there are no retained timing arrays. Variable timing keeps cumulative
end times for lookup and the original durations for exact retrieval. `OrderedSearch.UpperBound` selects the next
frame at an exact end time, holding the final frame at or past the clip duration. `KeyframeSampling` uses the same
search helper's `LowerBound` with a time selector, retaining exact key values and endpoint holds.
