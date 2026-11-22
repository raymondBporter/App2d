# Shape constructors and builders

Built-in shapes, serializable curves, intervals and rectangle bounds expose raw constructors for JSON.
Validate the resulting values after the document has been read.

`APP2D001` warns when application code calls one of these constructors.

```csharp
// Before: APP2D001
var circle = new Circle2D(12f, new Vector2f(4, 8));
Circle2D other = new(6f);

// After applying "Use Circle2D.Builder.Create"
var circle = Circle2D.Builder.Create(12f, new Vector2f(4, 8));
Circle2D other = Circle2D.Builder.Create(6f);

var range = Interval<float>.Builder.Create(1f, 5f);
var floorBounds = Rect2f.Builder.CreateUnbounded(new(float.NegativeInfinity),
    new(float.PositiveInfinity, 0f));
```

Build the solution, then use Visual Studio's Quick Actions (`Ctrl+.`) on the warning.
The quick fix also supports Fix All. If an already open editor has cached the old analyzer,
reload the solution after building.

The fix adds `using App2d.Core.Geometry.Shapes;` when needed and uses the short type name.
It reuses existing imports (including global usings) and aliases. When a short name or
an added import would conflict with another type, it retains the necessary qualification.

The rule covers explicit and target-typed object creation, qualified names, and aliases.
It excludes generated files and the constructed shape's own implementation (including its
nested builder). A consumer class named `Builder` is still checked. The fix keeps the
argument list, including named arguments, comments, and evaluation order, and offers a change
only when Roslyn can bind it to a public `Builder.Create` returning the same shape type.
Initializers and parameterless construction can produce warnings without a fix; choosing valid
dimensions requires the caller. `default(Shape)` and `with` expressions are outside this rule.

The projects have three jobs:

- `App2d.Analyzers`: the **analyzer** inspects Roslyn's object-creation operations, resolves
  the actual type, and reports `APP2D001`. It does not edit source.
- `App2d.Analyzers.CodeFixes`: the **code fix** builds replacement syntax and registers the
  editor's Quick Action. Workspaces dependencies live here so the compiler analyzer stays small.
- `App2d.Analyzers.Tests`: tests compile source against the real shape API, run the analyzer,
  apply fixes, and compile the results. They also exercise Fix All and false-positive cases.

`Directory.Build.props` attaches both tool assemblies to application/library projects as
analyzer references. They are not runtime dependencies. The analyzer and fix target
`netstandard2.0` for IDE compatibility; tests use the application's Windows target framework.
No VSIX or separate Visual Studio extension installation is required for this solution.

Run the tests from the repository root:

```powershell
dotnet test App2d.Analyzers.Tests/App2d.Analyzers.Tests.csproj -m:1
```

Change severity in `.editorconfig` if needed:

```ini
[*.cs]
dotnet_diagnostic.APP2D001.severity = suggestion
```

`Builder.Create` on `Interval<T>`, `Rect2f` and `Rect2<TVector, TReal>` requires finite, ordered endpoints.
`Builder.CreateUnbounded` permits negative infinity at the lower end and positive infinity at the upper end of each axis.
Both paths reject NaN and reversed endpoints. After loading JSON, use `Validate()` for finite bounds or
`Validate(allowUnbounded: true)` for fields whose schema permits open-ended bounds.

For a longer introduction, see Microsoft's
[first analyzer and code fix tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix).
