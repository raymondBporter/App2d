# Interpolation JSON

`Interpolator<TValue, TReal>` and `ProgressMap<TReal>` have core JSON converters. Factories retain construction
recipes alongside their executable functions. Loading builds the function once; evaluation does not inspect JSON,
resolve channel names, or rebuild a recipe. The converters also work with ordinary `JsonSerializer` options through
the types' attributes. `JsonProfiles.Mathematics` additionally supplies strict members and numeric handling.

```csharp
var rule = Interpolation.Default<Vector3f, float>().WithProgress(Easingf.Smooth)
    .With(Vector3f.Channels.X, Easingf.Power(2));
var json = JsonSerializer.Serialize(rule, JsonProfiles.Mathematics);
var restored = JsonSerializer.Deserialize<Interpolator<Vector3f, float>>(json, JsonProfiles.Mathematics);
```

The target generic type determines the value and numeric precision. JSON contains operation names and parameters,
not CLR type names or serialized delegates. Built-in bindings cover scalars, 2D/3D/4D vectors, polar values, and
`RigidTransform2D`, including whole values, scalar components, grouped RGB, translation, and rotation.

## Rules and ordered operations

Simple rules can be strings: `"linear"`, `"hold"`, `"shortest-angle"`, or `"default"`. Default uses the value type's
registered interpolation rule; linear and shortest-angle require scalar values. A simple rule with progress is:

```json
{ "kind": "default", "progress": "smooth" }
```

Channel and progress composition uses an ordered array. This matches
`Default.WithProgress(Smooth).With(X, Linear)`:

```json
{
  "kind": "default",
  "operations": [
    { "kind": "progress", "map": "smooth" },
    { "kind": "channel", "channel": "x", "interpolator": "linear" }
  ]
}
```

The override reads the original endpoints and original input time. Reversing those operations applies the progress
map to the completed channel composition instead. Later channel overrides win on overlaps. A recipe uses either
the `progress` shorthand or `operations`, so their ordering is always explicit.

Vector channel names are `v`, `x`, `y`, `z`, and `w` within the value's dimension. Four-dimensional vectors also
accept `r`, `g`, `b`, and `a` aliases and the grouped `rgb` channel. Aliases write their canonical Cartesian name.
Rigid transforms have `v`, `translation`, and `rotation`; polar values have `v`, `radius`, and `angle`.

Bezier rules contain `kind: "bezier"`, `x1`, `y1`, `x2`, `y2`, and optional `absolute`. Absolute controls can produce
excursions between equal endpoint values. Relative Bezier factories canonically write linear interpolation with
a Bezier progress map. Cartesian polar rules use `kind: "polar"` and optional scalar `radius` and `angle` rules;
their defaults are linear radius and shortest angle.

Progress recipes include the named easing/travel functions, power degree, smoothstep order, constants, affine and
range mappings, direction, sequential composition, and Bezier control values. Range endpoints and float/double
parameters are retained. Loading reconstructs the original derivative, inverse availability, and input-clamp span
through the same factories used by code.

## Application bindings

Custom value types and channels can participate without adding character dependencies to the core converter.
Create an `InterpolatorJsonConverterFactory`, configure `For<TValue, TReal>()`, then insert it before the default
factory in a clone of the mathematics profile:

```csharp
var factory = new InterpolatorJsonConverterFactory();
factory.For<MyValue, float>().Default(myDefaultRule).Channel("amount", myAmountChannel);
var options = new JsonSerializerOptions(JsonProfiles.Mathematics);
options.Converters.Insert(0, factory);
options.MakeReadOnly();
```

`Named(name, rule)` registers a fixed code-defined rule; `Coordinates(name, to, from)` registers a coordinate
conversion. Reuse the registered callbacks when composing `InCoordinates`. A `ProgressMapJsonConverterFactory`
can similarly register a fixed custom map. Such names depend on the application's bindings, including any captured
state supplied by code. Configure bindings before the first serialization operation.

Unregistered functions, channels, and coordinate conversions fail when saved rather than producing an incomplete
recipe. Runtime use of those functions remains available. JSON never attempts to infer a lambda's implementation.
