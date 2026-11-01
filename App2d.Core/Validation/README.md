# Validation

`Ensure` is the shared throwing API for inputs and live values. `NumericValidation`
provides its numeric predicates without throwing. The methods have the same names
(predicates add `Is`) and the same order: scalars, vectors, matrices.

```csharp
Ensure.Finite(position);
Ensure.Positive(radius);
Ensure.NonNegative(seconds);
Ensure.NonZero(direction);
Ensure.LessThanOrEqual(alpha, 1f);
Ensure.GreaterThanOrEqual(iterations, 1);
Ensure.InRange(fraction, 0f, 1f, bounds: RangeBounds.Open);
Ensure.Invertible(localToWorld, out var inverse);

// Checks return the original value and capture its expression on failure.
public Vector2 TileSize { get; } = Ensure.Positive(tileSize);

// Nonthrowing consumers use the same predicates.
if (NumericValidation.IsPositive(radius)) { /* use the radius */ }
```

## Numeric vocabulary

| Check | Requirement |
| --- | --- |
| `Number` | Not NaN; infinity is allowed |
| `Finite` | Neither NaN nor infinity |
| `Positive`, `Negative` | Finite and `> 0`, `< 0` |
| `NonNegative`, `NonPositive` | Finite and `>= 0`, `<= 0` |
| `NonZero` | Finite and not zero |
| `Equal`, `NotEqual` | Finite scalar operands satisfying `==`, `!=` |
| `LessThan`, `LessThanOrEqual` | Finite operands satisfying `<`, `<=` |
| `GreaterThan`, `GreaterThanOrEqual` | Finite operands satisfying `>`, `>=` |
| `InRange` | Finite value between ordered bounds |
| `Invertible` | Finite matrix with a finite inverse |

Scalars use generic math without conversion to float or double. This supports
integers, unsigned integers, `Int128`, `Half`, `float`, `double`, `decimal`,
`BigInteger`, and other types implementing the required numeric interface.
Negative zero is zero. Comparisons are exact; tolerance formulas remain explicit.

`Vector2`, `Vector3`, and `Vector4` have component-wise sign, ordering, and range
checks. `NonZero` checks the whole vector, so `(1, 0)` is a valid direction.
The engine's float/double vector structs also support `Number`, `Finite`, and
`NonZero` without narrowing their coordinates. `Matrix3x2` and `Matrix4x4`
support `Number`, `Finite`, and `Invertible`. A finite singular matrix is valid
for `Finite`; use `Invertible` when an inverse is required.

## Bounds and infinity

`RangeBounds` selects `[min, max]`, `(min, max)`, `[min, max)`, or `(min, max]`.
Range bounds may be infinite, but the checked value must be finite. Only a closed
range may collapse to one value.

Intentional infinity is explicit on throwing checks:

```csharp
Ensure.NonNegative(maxDistance, allowInfinity: true);
Ensure.Negative(lowerBound, allowNegativeInfinity: true);
Ensure.LessThanOrEqual(minimum, maximum, allowInfinity: true);
```

The ordering predicates use ordinary comparisons, allowing infinity and rejecting
NaN. Throwing ordering checks require finite operands unless opted in. This keeps
unbounded rectangles and heightfields valid while rejecting accidental infinities
in ordinary values.

## Other existing checks

The existing null, string, collection, type, and custom-condition helpers are
available on `Ensure`. Their rules remain separate from the numeric vocabulary.
Use `ThrowIf` / `ThrowIfNot` for domain conditions that do not fit a named check.
Numeric checks throw `ArgumentOutOfRangeException`; custom conditions throw
`ArgumentException`. Caller expressions and rejected values are retained. Successful
numeric checks allocate nothing, and exception instances are created on failure.
