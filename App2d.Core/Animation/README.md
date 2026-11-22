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

Character clips use `Track<T>`. The track's `Rule` supplies the default interpolator, and a key can supply
its own outgoing `Next` interpolator. Sampling holds the endpoints and returns exact key values.
For scalar Bezier interpolation, relative controls shape progress and absolute controls specify values.
See the [interpolation recipes](../Mathematics/Serialization/README.md) for the serialized control fields.

`KeyframeEditing` supplies `AtTime`, `Put`, `Move`, and `DeleteAtTime` on editable key lists.
Editing uses a shared time tolerance. Sampling selects exact key times.

`AnimationClip2D<TFrame>` is a discrete frame sequence. Uniform timing uses an implicit `UniformSequence<float>`
of end times and one shared frame duration; there are no retained timing arrays. Variable timing keeps cumulative
end times for lookup and the original durations for exact retrieval. `OrderedSearch.UpperBound` selects the next
frame at an exact end time, holding the final frame at or past the clip duration. `KeyframeSampling` uses the same
search helper's `LowerBound` with a time selector, retaining exact key values and endpoint holds.
