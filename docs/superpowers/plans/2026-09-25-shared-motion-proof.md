# Shared Motion Proof (Character Editor Replacement, Phase 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove that one authored walk and one authored run, stored once as channel clips against a base `person` model, play correctly on the standard, tall/thin and short/broad Person builds. This is the schema checkpoint before any new editor UI.

**Architecture:** New graphics-free authored asset types in `App2d.Core/Characters/Authored/`: `CharacterModel` (base structure), `ModelVariant` (explicit overrides, one level), `MotionClip` (channels with declared scale sources), `ResolvedModel` (base plus variant, compiled once) and `PoseEvaluator` (hierarchy → IK → contacts). A one-way converter turns the prototype `PuppetTemplates.StepStudy/RunStudy` motions into channel clips. Tests prove exact reproduction at reference proportions. A studio smoke mode renders the three builds side by side, plus a game-scale band, for human review.

**Tech Stack:** C# / .NET 10, System.Text.Json via the existing `AuthoredJson` options, xUnit 2.9, MonoGame through the existing `PointCharacterRenderer` and `CharacterMesh`. No new packages.

**Spec:** `docs/character-editor-replacement.md`: "Proportions and reusable motion", "Travel and contacts", "Files and editing semantics" and "Implementation sequence → 1. Prove shared motion before replacing the UI".

## Global Constraints

- Continue using C#, MonoGame and ImGui. Reuse the renderer and math. Add no packages.
- Keep existing project boundaries. The pose evaluator has no graphics: it lives in `App2d.Core`, which has no MonoGame reference.
- Files are readable versioned JSON under `Assets/Characters/authored/{models,variants,animations}`, one asset per file. IDs do not depend on filenames or display labels. The catalog is built by scanning these directories, with no index file.
- Structure has a compatibility revision (`structureRevision`) separate from the file-format `version`.
- A variant references one base. It overrides proportions, shape dimensions and colors. It never adds, removes or reparents controls, and never changes IK topology. There is one level of inheritance.
- Missing references are errors, not silently discarded data. Selecting a variant never edits the base.
- Coordinates are orthographic XY, Y up, with Z as explicit depth (positive Z away from the viewer). Rotation is in radians. Z is never scaled by a measure.
- Offset rule: `resolved offset = variant rest offset + authored delta * variant measure / reference measure`.
- IK limbs key an end target in a declared frame. The middle joint is a solved result, never a keyed channel.
- Contacts use the locomotion frame and the travel scale. Runtime intervals are half-open `[start, finish)`, except that a non-repeating sample held at the clip's end includes a contact whose finish equals the duration.
- Do not normalize coordinates by overall height, and do not scale a planted world target each frame.
- The project is in early development: no compatibility shims for the prototype formats (memory: dev stage tolerates breakage).
- Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **A variant override naming a control or part the base lacks** (for example after a base rename). It must fail to resolve, with an error naming the variant and the field (`rest.<id>` / `parts.<id>`), rather than dropping the override silently. Pinned in Task 2.
2. **A clip authored against an older structure revision.** It must be rejected, and the message must state both revisions. Pinned in Task 3.
3. **A build whose limb cannot reach its keyed or contact target.** Lengths must be preserved, the shortfall reported as a residual, and no exception thrown. Pinned in Task 6.
4. **Sampling exactly at a loop seam, at a held endpoint, or on a contact boundary.** The half-open rule must apply deterministically. Pinned in Task 4.
5. **Hand-edited JSON with a misspelled field or a null collection.** The result must be an error, never a partial load, and the catalog must attribute it to its file. Pinned in Tasks 1, 3 and 7.

## Environment notes (read first)

- Work in the worktree `C:\Users\rober\source\repos\App2d\.claude\worktrees\character-editor-replacement-68cb73`. The prototype baseline is commit `02f11545`, and the MAX_PATH fix for the studio output is the commit after it.
- `App2d.Tests` builds the game project, which requires the gitignored `Assets/Runtime/`. It is already copied into this worktree. If it is missing, copy it from the main checkout or run `python tools/ArtPipeline/build_runtime_assets.py`.
- Run tests with: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"`. The full suite has 243 passing tests at baseline.
- All new Core types stay in namespace `App2d.Core.Characters`. `AuthoredJson.OmitDefaults` only trims types in exactly that namespace.

## File Structure

| File | Responsibility |
| --- | --- |
| `App2d.Core/Characters/PuppetDefinition.cs` (modify) | Extract `PuppetPoint.Check` and `PuppetPart.Validate` for reuse. The prototype behaviour is unchanged. |
| `App2d.Core/Characters/Authored/AuthoredAsset.cs` | ID rule, JSON parse helper, atomic write. |
| `App2d.Core/Characters/Authored/CharacterModel.cs` | Base model schema and structural validation, including shared geometry checks. |
| `App2d.Core/Characters/Authored/ModelVariant.cs` | Variant schema (rest and part overrides). |
| `App2d.Core/Characters/Authored/ResolvedModel.cs` | Base plus variant, then compiled order, children, measures and lengths. |
| `App2d.Core/Characters/Authored/MotionClip.cs` | Clip schema, its own validation and compatibility validation against a `ResolvedModel`. |
| `App2d.Core/Characters/Authored/PoseEvaluator.cs` | Sampling, hierarchy, IK, contacts, travel and loops, producing an `EvaluatedPose`. |
| `App2d.Core/Characters/Authored/PuppetMotionConverter.cs` | One-way conversion from prototype absolute keys to channels. |
| `App2d.Core/Characters/Authored/PersonTemplate.cs` | Person base model built from the walk study rest pose. Writes the study assets. |
| `App2d.Core/Characters/Authored/PersonBuild.cs` | Explicit Person build values, written as variant overrides. |
| `App2d.Core/Characters/Authored/AuthoredCatalog.cs` | Scans the authored directories, collects per-file errors, resolves IDs. |
| `App2d.Rendering/Characters/PuppetDrawing.cs` (modify) | Generic `Build` over parts plus a world lookup, with an overload for `ResolvedModel`/`EvaluatedPose`. |
| `App2d.CharacterStudio/StudioGame.MotionProof.cs` | `--smoke-motion` side-by-side and game-scale renders, plus a report. |
| `App2d.CharacterStudio/Program.cs`, `StudioGame.cs` (modify) | The `--convert-studies` and `--smoke-motion` arguments. |
| `Assets/Characters/authored/**` | Generated `person`, `tall-thin`, `short-broad`, `person-walk`, `person-run`. |
| `App2d.Tests/Authored/*.cs` | Tests for every task above. |

---

### Task 1: Authored asset helpers and the base model schema

**Files:**
- Modify: `App2d.Core/Characters/PuppetDefinition.cs` (the `PuppetPoint` struct, `PuppetPart` record, and the point and part checks inside `Validate`)
- Create: `App2d.Core/Characters/Authored/AuthoredAsset.cs`
- Create: `App2d.Core/Characters/Authored/CharacterModel.cs`
- Create: `App2d.Tests/Authored/TestModels.cs`
- Test: `App2d.Tests/Authored/CharacterModelTests.cs`

**Interfaces:**
- Consumes: `Limit`, `Limit.Color`, `EntityVocabulary.Require`, `FaceExpressions.Contains`, `AuthoredJson.Options` (existing).
- Produces:
  - `PuppetPoint.Check(string field)`, and `PuppetPart.Validate(Func<string, bool> isControl)`.
  - `AuthoredAsset.RequireId(string? id, string field)`, `AuthoredAsset.Parse<T>(string json, string kind)`, `AuthoredAsset.Write(string path, string json)`.
  - `ModelControl { Id, Parent?, Rest: PuppetPoint, Scale = "unit" }`, `ModelChain { Id, Root, Joint, End, Bend = 1, Frame = "locomotion", Scale = "unit" }`, `ModelMeasure { Id, Path: List<string> }`.
  - `CharacterModel { Format, Version, Id, Name, StructureRevision = 1, Ink, LineWidth, Controls, Chains, Measures, Parts: List<PuppetPart> }` with `Validate()`, `ToJson()`, `static FromJson(string)`, `static Load(string path)`, `Save(string path)`, `const Locomotion = "locomotion"`, `const Unit = "unit"`.
  - `static CharacterModel.CheckGeometry(CharacterModel model, IReadOnlyDictionary<string, Vector3> rest, string owner)`.
  - Test helpers: `TestModels.Creature()`, `TestModels.Near(Vector3, Vector3, float, string)`, `TestModels.AuthoredRoot`.

- [ ] **Step 1: Extract the point and part checks in `PuppetDefinition.cs`**

Add to `PuppetPoint`, after `Lerp`:

```csharp
    public void Check(string field)
    {
        new Limit(-10000, 10000).Check(X, field + ".x"); new Limit(-10000, 10000).Check(Y, field + ".y"); new Limit(-32, 32).Check(Z, field + ".z");
    }
```

Add to `PuppetPart`, after `FaceX`:

```csharp
    /// <summary>Checks this part against the controls it may attach to. Shared by the prototype puppet and authored models.</summary>
    public void Validate(Func<string, bool> isControl)
    {
        if (!isControl(A)) throw new InvalidDataException("Unknown control: " + A);
        if (B is not null && !isControl(B)) throw new InvalidDataException("Unknown control: " + B);
        EntityVocabulary.Require(Kind, ["stroke", "ellipse", "box"], "part.kind");
        if (Kind == "stroke" && (B is null || A == B)) throw new InvalidDataException("A stroke needs two different controls.");
        new Limit(.001f, 100).Check(Width, "part.width"); new Limit(.001f, 100).Check(Height, "part.height");
        new Limit(-100, 100).Check(OffsetX, "part.offsetX"); new Limit(-100, 100).Check(OffsetY, "part.offsetY");
        new Limit(-16, 16).Check(Depth, "part.depth"); new Limit(0, 1).Check(Roundness, "part.roundness"); Limit.Color(Fill, "part.fill");
        if (Face != "none" && !FaceExpressions.Contains(Face)) throw new InvalidDataException("Unknown part expression: " + Face);
        new Limit(-1, 1).Check(FaceX, "part.faceX");
    }
```

In `PuppetDefinition.Validate`, replace the body of the local `Point` function with `point.Check(field);`. Replace the part loop body after the ID-uniqueness `Require` with `part!.Validate(controls.ContainsKey);`, which deletes the old `Reference(part!.A)`, kind, stroke, `Number(...)`, color, face and faceX lines. Keep the `Number` local function, because motions still use it.

- [ ] **Step 2: Run the existing puppet tests to confirm the refactor keeps behaviour**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~PuppetAuthoringTests"`
Expected: PASS (same count as before).

- [ ] **Step 3: Write the test helpers**

Create `App2d.Tests/Authored/TestModels.cs`:

```csharp
using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

internal static class TestModels
{
    /// <summary>A body with a two-bone arm keyed in the shoulder's frame and a two-bone leg keyed in the locomotion frame. Rest IK reproduces rest exactly.</summary>
    public static CharacterModel Creature() => new()
    {
        Id = "creature", Name = "Creature",
        Controls =
        [
            new() { Id = "body", Rest = new(0, 1), Scale = "leg" },
            new() { Id = "shoulder", Parent = "body", Rest = new(0, 1.5f) },
            new() { Id = "elbow", Parent = "shoulder", Rest = new(.4f, 1.3f) },
            new() { Id = "hand", Parent = "elbow", Rest = new(.8f, 1.5f) },
            new() { Id = "hip", Parent = "body", Rest = new(0, 1) },
            new() { Id = "knee", Parent = "hip", Rest = new(.1f, .5f) },
            new() { Id = "foot", Parent = "knee", Rest = new(0, 0) },
        ],
        Chains =
        [
            new() { Id = "arm", Root = "shoulder", Joint = "elbow", End = "hand", Bend = -1, Frame = "shoulder", Scale = "arm" },
            new() { Id = "leg", Root = "hip", Joint = "knee", End = "foot", Bend = 1, Scale = "leg" },
        ],
        Measures = [new() { Id = "leg", Path = ["hip", "knee", "foot"] }, new() { Id = "arm", Path = ["shoulder", "elbow", "hand"] }],
        Parts = [new() { Id = "torso", Kind = "box", A = "hip", B = "shoulder" }, new() { Id = "upper-arm", Kind = "stroke", A = "shoulder", B = "elbow" }],
    };

    public static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-4f, string what = "") =>
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"{what}: expected {expected}, found {actual}");

    public static string AuthoredRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "Assets", "Characters");
                if (File.Exists(Path.Combine(path, "catalog.json"))) return Path.Combine(path, "authored");
            }
            throw new DirectoryNotFoundException("Character assets not found.");
        }
    }
}
```

- [ ] **Step 4: Write the failing model tests**

Create `App2d.Tests/Authored/CharacterModelTests.cs`:

```csharp
using System.Text.Json;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class CharacterModelTests
{
    [Fact]
    public void JsonRoundTripIsExactAndRejectsMisspelledOrNullFields()
    {
        var model = TestModels.Creature(); model.Validate();
        var json = model.ToJson();
        Assert.Equal(json, CharacterModel.FromJson(json).ToJson());
        Assert.Contains("\"frame\": \"shoulder\"", json);
        Assert.DoesNotContain("\"scale\": \"unit\"", json);
        Assert.Throws<JsonException>(() => CharacterModel.FromJson(json.Replace("\"chains\":", "\"chanes\":")));
    }

    [Fact]
    public void NullCollectionIsRejected()
    {
        var error = Assert.Throws<InvalidDataException>(() => CharacterModel.FromJson("{\"id\":\"x\",\"name\":\"X\",\"controls\":null}"));
        Assert.Contains("null", error.Message);
    }

    public static TheoryData<string, string> Rejections => new()
    {
        { "cycle", "cycle" }, { "unknown-parent", "unknown parent" }, { "joint-not-child", "connected" },
        { "shared-solve", "share" }, { "frame-solved", "frame" }, { "unknown-scale", "scale" },
        { "reserved-id", "reserved" }, { "bad-id", "lowercase" }, { "short-bone", "length" }, { "part-control", "Unknown control" },
    };

    [Theory, MemberData(nameof(Rejections))]
    public void StructuralErrorsAreRejectedWithAUsefulMessage(string edit, string fragment)
    {
        var model = TestModels.Creature();
        ModelControl Control(string id) => model.Controls.Single(c => c.Id == id);
        switch (edit)
        {
            case "cycle": Control("body").Parent = "foot"; break;
            case "unknown-parent": Control("hip").Parent = "tail"; break;
            case "joint-not-child": Control("knee").Parent = "body"; break;
            case "shared-solve": model.Chains.Add(new() { Id = "leg2", Root = "hip", Joint = "knee", End = "foot" }); break;
            case "frame-solved": model.Chains[1].Frame = "hand"; break;
            case "unknown-scale": Control("body").Scale = "tail"; break;
            case "reserved-id": Control("body").Id = "locomotion"; break;
            case "bad-id": model.Measures[0].Id = "Leg Length"; break;
            case "short-bone": Control("knee").Rest = new(0, 1.001f); break;
            case "part-control": model.Parts[0].A = "tail"; break;
        }
        var error = Assert.Throws<InvalidDataException>(model.Validate);
        Assert.Contains(fragment, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveAndLoadAreAtomicAndEquivalent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"model-{Guid.NewGuid():N}.json");
        try
        {
            var model = TestModels.Creature(); model.Save(path);
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Equal(model.ToJson(), CharacterModel.Load(path).ToJson());
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"`
Expected: build FAIL with `The type or namespace name 'CharacterModel' could not be found`.

- [ ] **Step 6: Implement `AuthoredAsset`**

Create `App2d.Core/Characters/Authored/AuthoredAsset.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;

namespace App2d.Core.Characters;

/// <summary>Shared rules for authored character files: stable lowercase IDs, strict parsing and atomic per-file writes.</summary>
public static partial class AuthoredAsset
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    public static void RequireId(string? id, string field)
    {
        if (id is null || !IdPattern().IsMatch(id))
            throw new InvalidDataException($"{field} must be a lowercase id of letters, digits and hyphens; found '{id}'.");
    }

    public static T Parse<T>(string json, string kind) where T : class =>
        JsonSerializer.Deserialize<T>(json, AuthoredJson.Options) ?? throw new InvalidDataException($"Empty {kind} file.");

    public static void Write(string path, string json)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json); File.Move(temporary, path, true);
    }
}
```

- [ ] **Step 7: Implement `CharacterModel`**

Create `App2d.Core/Characters/Authored/CharacterModel.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>Rest is in model space; the parent-local rest offset is derived. Scale names the measure this control's animated translation scales with.</summary>
public sealed record ModelControl
{
    public string Id { get; set; } = "";
    public string? Parent { get; set; }
    public PuppetPoint Rest { get; set; }
    public string Scale { get; set; } = CharacterModel.Unit;
}

/// <summary>A two-bone IK chain. Its end target is keyed in Frame: the locomotion frame, or a control that no chain moves.</summary>
public sealed record ModelChain
{
    public string Id { get; set; } = "";
    public string Root { get; set; } = "";
    public string Joint { get; set; } = "";
    public string End { get; set; } = "";
    public int Bend { get; set; } = 1;
    public string Frame { get; set; } = CharacterModel.Locomotion;
    public string Scale { get; set; } = CharacterModel.Unit;
}

/// <summary>A named length: the sum of rest XY distances along a path of controls, such as leg reach.</summary>
public sealed record ModelMeasure
{
    public string Id { get; set; } = "";
    public List<string> Path { get; set; } = [];
}

/// <summary>A reusable character structure. Controls without a parent hang from the locomotion frame.</summary>
public sealed class CharacterModel
{
    public const string FormatId = "app2d-model", Locomotion = "locomotion", Unit = "unit";
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Changes when controls, parents, chains or frames change; proportions and appearance never change it.</summary>
    public int StructureRevision { get; set; } = 1;
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .045f;
    public List<ModelControl> Controls { get; set; } = [];
    public List<ModelChain> Chains { get; set; } = [];
    public List<ModelMeasure> Measures { get; set; } = [];
    public List<PuppetPart> Parts { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static CharacterModel FromJson(string json) { var model = AuthoredAsset.Parse<CharacterModel>(json, "model"); model.Validate(); return model; }
    public static CharacterModel Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    public void Validate()
    {
        var owner = $"Model '{Id}'";
        Require(Format == FormatId && Version == 1, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "model id");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(StructureRevision >= 1, $"{owner}: structureRevision must be at least 1.");
        Require(Controls is not null && Chains is not null && Measures is not null && Parts is not null, $"{owner}: collections cannot be null.");
        Require(Controls.Count <= 256 && Chains.Count <= 64 && Measures.Count <= 64 && Parts.Count <= 512, $"{owner}: capacity exceeded.");
        Limit.Color(Ink, "ink"); new Limit(.001f, 1).Check(LineWidth, "lineWidth");

        var controls = new Dictionary<string, ModelControl>(StringComparer.Ordinal);
        foreach (var control in Controls)
        {
            Require(control is not null, $"{owner}: null control.");
            Require(control.Id != Locomotion && control.Id != Unit, $"{owner}: '{control.Id}' is a reserved name.");
            AuthoredAsset.RequireId(control.Id, $"{owner} control id");
            Require(controls.TryAdd(control.Id, control), $"{owner}: duplicate control '{control.Id}'.");
            control.Rest.Check($"{owner} control '{control.Id}' rest");
        }
        foreach (var control in Controls)
            Require(control.Parent is null || control.Parent != control.Id && controls.ContainsKey(control.Parent), $"{owner}: control '{control.Id}' has unknown parent '{control.Parent}'.");
        foreach (var control in Controls)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var current = control; current.Parent is not null; current = controls[current.Parent])
                Require(seen.Add(current.Id), $"{owner}: parent cycle at '{control.Id}'.");
        }

        var scales = new HashSet<string>(StringComparer.Ordinal) { Unit };
        foreach (var measure in Measures)
        {
            Require(measure is not null && measure.Path is not null, $"{owner}: incomplete measure.");
            AuthoredAsset.RequireId(measure.Id, $"{owner} measure id");
            Require(measure.Id != Unit && scales.Add(measure.Id), $"{owner}: duplicate or reserved measure '{measure.Id}'.");
            Require(measure.Path.Count >= 2 && measure.Path.All(controls.ContainsKey), $"{owner}: measure '{measure.Id}' needs a path of at least two known controls.");
        }
        foreach (var control in Controls) Require(scales.Contains(control.Scale), $"{owner}: control '{control.Id}' uses unknown scale '{control.Scale}'.");

        var chainIds = new HashSet<string>(StringComparer.Ordinal); var solved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chain in Chains)
        {
            Require(chain is not null, $"{owner}: null chain.");
            AuthoredAsset.RequireId(chain.Id, $"{owner} chain id");
            Require(chainIds.Add(chain.Id), $"{owner}: duplicate chain '{chain.Id}'.");
            Require(chain.Bend is -1 or 1, $"{owner}: chain '{chain.Id}' bend must be -1 or 1.");
            Require(controls.ContainsKey(chain.Root) && controls.ContainsKey(chain.Joint) && controls.ContainsKey(chain.End), $"{owner}: chain '{chain.Id}' references an unknown control.");
            Require(controls[chain.Joint].Parent == chain.Root && controls[chain.End].Parent == chain.Joint, $"{owner}: chain '{chain.Id}' needs two connected bones (root → joint → end).");
            Require(solved.Add(chain.Joint) && solved.Add(chain.End), $"{owner}: chains cannot share solved controls ('{chain.Id}').");
            Require(scales.Contains(chain.Scale), $"{owner}: chain '{chain.Id}' uses unknown scale '{chain.Scale}'.");
            Require(chain.Frame == Locomotion || controls.ContainsKey(chain.Frame), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' is not a control or '{Locomotion}'.");
        }
        IEnumerable<string> SelfAndAncestors(string id) { for (string? current = id; current is not null; current = controls[current].Parent) yield return current; }
        foreach (var chain in Chains)
        {
            Require(!SelfAndAncestors(chain.Root).Any(solved.Contains), $"{owner}: chain '{chain.Id}' is nested inside another chain; nested IK is not supported.");
            Require(chain.Frame == Locomotion || !SelfAndAncestors(chain.Frame).Any(solved.Contains), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' must not be moved by IK.");
        }

        var parts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            Require(part is not null, $"{owner}: null part.");
            AuthoredAsset.RequireId(part.Id, $"{owner} part id");
            Require(parts.Add(part.Id), $"{owner}: duplicate part '{part.Id}'.");
            part.Validate(controls.ContainsKey);
        }
        CheckGeometry(this, Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ, StringComparer.Ordinal), owner);
    }

    /// <summary>Checks lengths that depend on rest geometry, so a variant's overrides are held to the same rules as its base.</summary>
    public static void CheckGeometry(CharacterModel model, IReadOnlyDictionary<string, Vector3> rest, string owner)
    {
        float Length(string a, string b) => Vector2.Distance(new(rest[a].X, rest[a].Y), new(rest[b].X, rest[b].Y));
        foreach (var chain in model.Chains)
            Require(Length(chain.Root, chain.Joint) >= .01f && Length(chain.Joint, chain.End) >= .01f, $"{owner}: chain '{chain.Id}' needs segment lengths of at least 0.01.");
        foreach (var measure in model.Measures)
            Require(measure.Path.Zip(measure.Path.Skip(1)).Sum(p => Length(p.First, p.Second)) >= .001f, $"{owner}: measure '{measure.Id}' has no length.");
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored|FullyQualifiedName~PuppetAuthoringTests"`
Expected: PASS. If `reserved-id` fails because an ID check runs first, reorder so the reserved-name `Require` precedes `RequireId` (as written above).

- [ ] **Step 9: Commit**

```bash
git add App2d.Core/Characters/PuppetDefinition.cs App2d.Core/Characters/Authored App2d.Tests/Authored
git commit -m "Add authored base model schema with structural validation

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Variants and the resolved model

**Files:**
- Create: `App2d.Core/Characters/Authored/ModelVariant.cs`
- Create: `App2d.Core/Characters/Authored/ResolvedModel.cs`
- Test: `App2d.Tests/Authored/ResolvedModelTests.cs`

**Interfaces:**
- Consumes: `CharacterModel`, `CharacterModel.CheckGeometry`, `AuthoredAsset.*`, `PuppetPoint.Check`.
- Produces:
  - `PartOverride { float? Width, Height, OffsetX, OffsetY; string? Fill }`.
  - `ModelVariant { Format, Version, Id, Name, Base, Dictionary<string, PuppetPoint> Rest, Dictionary<string, PartOverride> Parts }` with `Validate()`, `ToJson()`, `static FromJson(string)`, `Save(string)`, `const FormatId = "app2d-variant"`.
  - `ResolvedModel` with `Base: CharacterModel`, `Variant: ModelVariant?`, `Id: string`, `Rest: IReadOnlyDictionary<string, Vector3>`, `Parts: IReadOnlyList<PuppetPart>`, `Measures: IReadOnlyDictionary<string, float>`, `Order: IReadOnlyList<ModelControl>` (parents first), `Controls: IReadOnlyDictionary<string, ModelControl>`, `Chains: IReadOnlyDictionary<string, ModelChain>`, `Children: IReadOnlyDictionary<string, IReadOnlyList<string>>`, `float Measure(string scale)`, `float Length(string from, string to)` (rest XY distance), `static ResolvedModel From(CharacterModel model, ModelVariant? variant = null)`.

- [ ] **Step 1: Write the failing tests**

Create `App2d.Tests/Authored/ResolvedModelTests.cs`:

```csharp
using System.Numerics;
using System.Text.Json;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class ResolvedModelTests
{
    private static ModelVariant LongArm() => new()
    {
        Id = "long-arm", Name = "Long arm", Base = "creature",
        Rest = { ["elbow"] = new(.6f, 1.2f), ["hand"] = new(1.2f, 1.5f) },
        Parts = { ["torso"] = new() { Width = .9f, Fill = "#aa3300" } },
    };

    [Fact]
    public void VariantOverridesApplyWithoutEditingTheBase()
    {
        var model = TestModels.Creature();
        var resolved = ResolvedModel.From(model, LongArm());
        Assert.Equal("long-arm", resolved.Id);
        TestModels.Near(new(1.2f, 1.5f, 0), resolved.Rest["hand"]);
        Assert.Equal(new PuppetPoint(.8f, 1.5f), model.Controls.Single(c => c.Id == "hand").Rest);
        var torso = resolved.Parts.Single(p => p.Id == "torso");
        Assert.Equal(.9f, torso.Width); Assert.Equal("#aa3300", torso.Fill);
        Assert.Equal(.4f, model.Parts.Single(p => p.Id == "torso").Width);
        Assert.Equal(1.5f * 2 * MathF.Sqrt(.2f), resolved.Measures["arm"], 4);
        Assert.Equal(2 * MathF.Sqrt(.26f), resolved.Measures["leg"], 4);
    }

    [Fact]
    public void UnoverriddenBaseValuesPropagate()
    {
        var model = TestModels.Creature(); var variant = LongArm();
        model.Controls.Single(c => c.Id == "knee").Rest = new(.15f, .5f);
        TestModels.Near(new(.15f, .5f, 0), ResolvedModel.From(model, variant).Rest["knee"]);
    }

    [Fact]
    public void OrderPlacesParentsFirstAndChildrenAreIndexed()
    {
        var model = TestModels.Creature(); model.Controls.Reverse();
        var resolved = ResolvedModel.From(model);
        var index = resolved.Order.Select((c, i) => (c.Id, i)).ToDictionary(p => p.Id, p => p.i);
        foreach (var control in model.Controls.Where(c => c.Parent is not null)) Assert.True(index[control.Parent!] < index[control.Id]);
        Assert.Equal(new[] { "hip", "shoulder" }, resolved.Children["body"].Order());
        Assert.Empty(resolved.Children["foot"]);
    }

    public static TheoryData<string, string> Rejections => new()
    {
        { "wrong-base", "references base 'hound'" }, { "unknown-control", "rest.tail" },
        { "unknown-part", "parts.wing" }, { "zero-bone", "long-arm" }, { "bad-fill", "fill" },
    };

    [Theory, MemberData(nameof(Rejections))]
    public void BrokenVariantsFailToResolveWithTheVariantAndFieldNamed(string edit, string fragment)
    {
        var variant = LongArm();
        switch (edit)
        {
            case "wrong-base": variant.Base = "hound"; break;
            case "unknown-control": variant.Rest["tail"] = new(1, 1); break;
            case "unknown-part": variant.Parts["wing"] = new() { Width = 1 }; break;
            case "zero-bone": variant.Rest["elbow"] = new(0, 1.5f); break;
            case "bad-fill": variant.Parts["torso"].Fill = "red"; break;
        }
        var error = Assert.Throws<InvalidDataException>(() => ResolvedModel.From(TestModels.Creature(), variant));
        Assert.Contains(fragment, error.Message);
    }

    [Fact]
    public void VariantJsonRoundTripsAndRejectsMisspelledFields()
    {
        var json = LongArm().ToJson();
        Assert.Equal(json, ModelVariant.FromJson(json).ToJson());
        Assert.Contains("\"base\": \"creature\"", json);
        Assert.Throws<JsonException>(() => ModelVariant.FromJson(json.Replace("\"parts\":", "\"prats\":")));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ResolvedModelTests"`
Expected: build FAIL, `ModelVariant` not found.

- [ ] **Step 3: Implement `ModelVariant`**

Create `App2d.Core/Characters/Authored/ModelVariant.cs`:

```csharp
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>A part value a variant replaces. Null keeps the base value, which then follows base edits.</summary>
public sealed record PartOverride
{
    public float? Width { get; set; }
    public float? Height { get; set; }
    public float? OffsetX { get; set; }
    public float? OffsetY { get; set; }
    public string? Fill { get; set; }
}

/// <summary>Explicit overrides of one base model. Never structural: no controls, parents or chains.</summary>
public sealed class ModelVariant
{
    public const string FormatId = "app2d-variant";
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Base { get; set; } = "";
    /// <summary>Model-space rest positions replacing the base's, by control ID.</summary>
    public Dictionary<string, PuppetPoint> Rest { get; set; } = [];
    public Dictionary<string, PartOverride> Parts { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static ModelVariant FromJson(string json) { var variant = AuthoredAsset.Parse<ModelVariant>(json, "variant"); variant.Validate(); return variant; }
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    public void Validate()
    {
        var owner = $"Variant '{Id}'";
        if (Format != FormatId || Version != 1) throw new InvalidDataException($"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "variant id"); AuthoredAsset.RequireId(Base, $"{owner} base");
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException($"{owner}: a name is required.");
        if (Rest is null || Parts is null) throw new InvalidDataException($"{owner}: collections cannot be null.");
        foreach (var (id, point) in Rest) point.Check($"{owner} rest.{id}");
        foreach (var (id, part) in Parts)
        {
            if (part is null) throw new InvalidDataException($"{owner} parts.{id}: null override.");
            if (part.Width is { } width) new Limit(.001f, 100).Check(width, $"{owner} parts.{id}.width");
            if (part.Height is { } height) new Limit(.001f, 100).Check(height, $"{owner} parts.{id}.height");
            if (part.OffsetX is { } x) new Limit(-100, 100).Check(x, $"{owner} parts.{id}.offsetX");
            if (part.OffsetY is { } y) new Limit(-100, 100).Check(y, $"{owner} parts.{id}.offsetY");
            if (part.Fill is not null) Limit.Color(part.Fill, $"{owner} parts.{id}.fill");
        }
    }
}
```

- [ ] **Step 4: Implement `ResolvedModel`**

Create `App2d.Core/Characters/Authored/ResolvedModel.cs`:

```csharp
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>A base model with one variant's overrides applied, compiled once and shared by every actor that uses it.</summary>
public sealed class ResolvedModel
{
    private ResolvedModel(CharacterModel model, ModelVariant? variant, Dictionary<string, Vector3> rest, List<PuppetPart> parts)
    {
        Base = model; Variant = variant; Rest = rest; Parts = parts;
        Controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Chains = model.Chains.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Children = model.Controls.ToDictionary(c => c.Id, c => (IReadOnlyList<string>)model.Controls.Where(child => child.Parent == c.Id).Select(child => child.Id).ToList(), StringComparer.Ordinal);
        Measures = model.Measures.ToDictionary(m => m.Id, m => m.Path.Zip(m.Path.Skip(1)).Sum(p => Length(p.First, p.Second)), StringComparer.Ordinal);
        var order = new List<ModelControl>(); var placed = new HashSet<string>(StringComparer.Ordinal);
        void Place(ModelControl control)
        {
            if (!placed.Add(control.Id)) return;
            if (control.Parent is not null) Place(Controls[control.Parent]);
            order.Add(control);
        }
        foreach (var control in model.Controls) Place(control);
        Order = order;
    }

    public CharacterModel Base { get; }
    public ModelVariant? Variant { get; }
    public string Id => Variant?.Id ?? Base.Id;
    public IReadOnlyDictionary<string, Vector3> Rest { get; }
    public IReadOnlyList<PuppetPart> Parts { get; }
    public IReadOnlyDictionary<string, float> Measures { get; }
    /// <summary>Controls ordered so every parent precedes its children.</summary>
    public IReadOnlyList<ModelControl> Order { get; }
    public IReadOnlyDictionary<string, ModelControl> Controls { get; }
    public IReadOnlyDictionary<string, ModelChain> Chains { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Children { get; }

    public float Measure(string scale) => scale == CharacterModel.Unit ? 1 : Measures[scale];
    public float Length(string from, string to) => Vector2.Distance(new(Rest[from].X, Rest[from].Y), new(Rest[to].X, Rest[to].Y));

    public static ResolvedModel From(CharacterModel model, ModelVariant? variant = null)
    {
        model.Validate(); variant?.Validate();
        var rest = model.Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ, StringComparer.Ordinal);
        var parts = model.Parts.Select(p => p with { }).ToList();
        if (variant is not null)
        {
            var owner = $"Variant '{variant.Id}'";
            if (variant.Base != model.Id) throw new InvalidDataException($"{owner} references base '{variant.Base}', not '{model.Id}'.");
            foreach (var (id, point) in variant.Rest)
            {
                if (!rest.ContainsKey(id)) throw new InvalidDataException($"{owner} rest.{id}: the base has no such control.");
                rest[id] = point.XYZ;
            }
            foreach (var (id, change) in variant.Parts)
            {
                var index = parts.FindIndex(p => p.Id == id);
                if (index < 0) throw new InvalidDataException($"{owner} parts.{id}: the base has no such part.");
                var part = parts[index];
                parts[index] = part with
                {
                    Width = change.Width ?? part.Width, Height = change.Height ?? part.Height,
                    OffsetX = change.OffsetX ?? part.OffsetX, OffsetY = change.OffsetY ?? part.OffsetY, Fill = change.Fill ?? part.Fill,
                };
            }
            CharacterModel.CheckGeometry(model, rest, owner);
        }
        return new(model, variant, rest, parts);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add App2d.Core/Characters/Authored App2d.Tests/Authored
git commit -m "Add model variants with explicit overrides and a resolved model

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Motion clip schema and compatibility validation

**Files:**
- Create: `App2d.Core/Characters/Authored/MotionClip.cs`
- Test: `App2d.Tests/Authored/MotionClipTests.cs`

**Interfaces:**
- Consumes: `ResolvedModel` (`Base`, `Controls`, `Chains`, `Measures`), `AuthoredAsset.*`, `EntityVocabulary.Require`, `Limit`.
- Produces:
  - `ClipKey { float Time, X, Y, Z, Angle }`.
  - `ClipTrack { string Kind = "translate", string Target, string? Scale, List<ClipKey> Keys }`.
  - `ClipTravel { string Scale = "unit", List<ClipKey> Keys }`.
  - `ClipContact { string Chain, float Start, float Finish = .5f, PuppetPoint Target }`.
  - `MotionClip { Format, Version, Id, Name, Model, StructureRevision = 1, Duration = 1, Loop, Dictionary<string, float> Reference, ClipTravel Travel, List<ClipTrack> Tracks, List<ClipContact> Contacts }` with `Validate()`, `Validate(ResolvedModel)`, `ToJson()`, `static FromJson(string)`, `Save(string)`.
  - Kind constants: `MotionClip.TranslateKind = "translate"`, `RotateKind = "rotate"`, `TargetKind = "target"`, `MotionClip.TrackKinds`.
  - `TestModels.Clip(ResolvedModel model)`: an empty looping 1-second clip carrying the model's measures as its reference.

- [ ] **Step 1: Add the clip helper to `TestModels`**

Append inside `TestModels`:

```csharp
    /// <summary>An empty one-second looping clip authored against this model's own measures, so every ratio is 1.</summary>
    public static MotionClip Clip(ResolvedModel model) => new()
    {
        Id = "test-clip", Name = "Test", Model = model.Base.Id, Loop = true,
        Reference = model.Measures.ToDictionary(p => p.Key, p => p.Value),
    };
```

- [ ] **Step 2: Write the failing tests**

Create `App2d.Tests/Authored/MotionClipTests.cs`:

```csharp
using System.Text.Json;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class MotionClipTests
{
    private static readonly ResolvedModel Model = ResolvedModel.From(TestModels.Creature());

    private static MotionClip Valid()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel = new() { Scale = "leg", Keys = [new() { Time = 0 }, new() { Time = 1, X = .4f }] };
        clip.Tracks.Add(new() { Kind = MotionClip.TranslateKind, Target = "body", Keys = [new() { Time = 0 }, new() { Time = .5f, Y = -.1f }] });
        clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "shoulder", Keys = [new() { Time = 0, Angle = .2f }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "arm", Keys = [new() { Time = 0, X = .1f }] });
        clip.Contacts.Add(new() { Chain = "leg", Start = 0, Finish = .5f });
        clip.Contacts.Add(new() { Chain = "leg", Start = .5f, Finish = 1, Target = new(.2f, 0) });
        return clip;
    }

    [Fact]
    public void AValidClipPassesBothChecksAndRoundTrips()
    {
        var clip = Valid(); clip.Validate(Model);
        var json = clip.ToJson();
        Assert.Equal(json, MotionClip.FromJson(json).ToJson());
        Assert.Throws<JsonException>(() => MotionClip.FromJson(json.Replace("\"contacts\":", "\"contcats\":")));
    }

    public static TheoryData<string, string> Rejections => new()
    {
        { "revision", "revision 2" }, { "model", "for model 'hound'" }, { "solved-control", "solved by IK" },
        { "unknown-chain", "unknown chain" }, { "duplicate-track", "duplicate" }, { "key-order", "strictly increasing" },
        { "key-beyond", "time" }, { "rotate-shape", "angle only" }, { "translate-shape", "x, y and z only" },
        { "contact-frame", "locomotion frame" }, { "contact-scale", "must match the travel scale" },
        { "missing-reference", "no reference measurement" }, { "unknown-measure", "no measure" }, { "contact-overlap", "overlap" },
        { "null-tracks", "null" },
    };

    [Theory, MemberData(nameof(Rejections))]
    public void InvalidOrIncompatibleClipsAreRejected(string edit, string fragment)
    {
        var clip = Valid();
        switch (edit)
        {
            case "revision": clip.StructureRevision = 2; break;
            case "model": clip.Model = "hound"; break;
            case "solved-control": clip.Tracks.Add(new() { Target = "knee", Keys = [new() { X = .1f }] }); break;
            case "unknown-chain": clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "tail", Keys = [new()] }); break;
            case "duplicate-track": clip.Tracks.Add(new() { Target = "body", Keys = [new()] }); break;
            case "key-order": clip.Tracks[0].Keys.Add(new() { Time = .25f }); break;
            case "key-beyond": clip.Tracks[0].Keys.Add(new() { Time = 1.5f }); break;
            case "rotate-shape": clip.Tracks[1].Keys[0].X = 1; break;
            case "translate-shape": clip.Tracks[0].Keys[0].Angle = 1; break;
            case "contact-frame": clip.Contacts.Add(new() { Chain = "arm", Start = 0, Finish = .2f }); break;
            case "contact-scale": clip.Travel.Scale = "arm"; break;
            case "missing-reference": clip.Reference.Remove("arm"); break;
            case "unknown-measure": clip.Tracks[0].Scale = "tail"; clip.Reference["tail"] = 1; break;
            case "contact-overlap": clip.Contacts[1].Start = .4f; break;
            case "null-tracks": clip.Tracks = null!; break;
        }
        var error = Assert.Throws<InvalidDataException>(() => clip.Validate(Model));
        Assert.Contains(fragment, error.Message);
    }

    [Fact]
    public void RevisionMismatchStatesBothRevisions()
    {
        var clip = Valid(); clip.StructureRevision = 2;
        var message = Assert.Throws<InvalidDataException>(() => clip.Validate(Model)).Message;
        Assert.Contains("revision 2", message); Assert.Contains("revision 1", message);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~MotionClipTests"`
Expected: build FAIL, `MotionClip` not found.

- [ ] **Step 4: Implement `MotionClip`**

Create `App2d.Core/Characters/Authored/MotionClip.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>A keyed value at a time. Translate, target and travel keys use X/Y/Z; rotate keys use Angle (radians).</summary>
public sealed record ClipKey
{
    public float Time { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Angle { get; set; }
}

/// <summary>
/// translate: a control's parent-local offset delta. rotate: a control's XY rotation, inherited by its children.
/// target: an IK chain's end-target delta in the chain's frame. Deltas are from rest, in reference units.
/// </summary>
public sealed record ClipTrack
{
    public string Kind { get; set; } = MotionClip.TranslateKind;
    public string Target { get; set; } = "";
    /// <summary>Overrides the control's or chain's default measure. Null uses the model default.</summary>
    public string? Scale { get; set; }
    public List<ClipKey> Keys { get; set; } = [];
}

/// <summary>The locomotion frame's travel in authored preview. The controller owns travel in gameplay.</summary>
public sealed record ClipTravel
{
    public string Scale { get; set; } = CharacterModel.Unit;
    public List<ClipKey> Keys { get; set; } = [];
}

/// <summary>A chain end held over [Start, Finish) at a target in the locomotion frame at the cycle's start. Target is a delta from the end's rest position, in the chain's scale.</summary>
public sealed record ClipContact
{
    public string Chain { get; set; } = "";
    public float Start { get; set; }
    public float Finish { get; set; } = .5f;
    public PuppetPoint Target { get; set; }
}

/// <summary>A named animation compatible with one base model's structure revision. Owns no colors, proportions or gameplay.</summary>
public sealed class MotionClip
{
    public const string FormatId = "app2d-clip", TranslateKind = "translate", RotateKind = "rotate", TargetKind = "target";
    public static readonly IReadOnlyList<string> TrackKinds = [TranslateKind, RotateKind, TargetKind];
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public int StructureRevision { get; set; } = 1;
    public float Duration { get; set; } = 1;
    public bool Loop { get; set; }
    /// <summary>Measure lengths the clip was authored against, so a base edit never silently reinterprets authored units.</summary>
    public Dictionary<string, float> Reference { get; set; } = [];
    public ClipTravel Travel { get; set; } = new();
    public List<ClipTrack> Tracks { get; set; } = [];
    public List<ClipContact> Contacts { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static MotionClip FromJson(string json) { var clip = AuthoredAsset.Parse<MotionClip>(json, "clip"); clip.Validate(); return clip; }
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    public void Validate()
    {
        var owner = $"Clip '{Id}'";
        Require(Format == FormatId && Version == 1, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "clip id"); AuthoredAsset.RequireId(Model, $"{owner} model");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(StructureRevision >= 1, $"{owner}: structureRevision must be at least 1.");
        new Limit(.05f, 60).Check(Duration, $"{owner} duration");
        Require(Reference is not null && Travel is not null && Travel.Keys is not null && Tracks is not null && Contacts is not null, $"{owner}: collections cannot be null.");
        foreach (var (measure, length) in Reference) { AuthoredAsset.RequireId(measure, $"{owner} reference"); new Limit(.001f, 1000).Check(length, $"{owner} reference.{measure}"); }
        CheckKeys(Travel.Keys, $"{owner} travel", k => k.Z == 0 && k.Angle == 0, "travel keys use x and y only");
        var seen = new HashSet<(string, string)>();
        foreach (var track in Tracks)
        {
            Require(track is not null && track.Keys is not null, $"{owner}: incomplete track.");
            EntityVocabulary.Require(track.Kind, TrackKinds, $"{owner} track kind");
            Require(seen.Add((track.Kind, track.Target)), $"{owner}: duplicate {track.Kind} track for '{track.Target}'.");
            var rotate = track.Kind == RotateKind;
            CheckKeys(track.Keys, $"{owner} {track.Kind} track '{track.Target}'",
                rotate ? k => k.X == 0 && k.Y == 0 && k.Z == 0 : k => k.Angle == 0, rotate ? "rotate keys use angle only" : "keys use x, y and z only");
        }
        foreach (var contact in Contacts)
        {
            Require(contact is not null, $"{owner}: null contact.");
            var field = $"{owner} contact on '{contact.Chain}'";
            new Limit(0, Duration).Check(contact.Start, field + " start"); new Limit(0, Duration).Check(contact.Finish, field + " finish");
            Require(contact.Finish > contact.Start, $"{field}: finish must follow start.");
            contact.Target.Check(field + " target");
        }
        foreach (var group in Contacts.GroupBy(c => c.Chain))
        {
            ClipContact? previous = null;
            foreach (var contact in group.OrderBy(c => c.Start))
            {
                Require(previous is null || previous.Finish <= contact.Start, $"{owner}: contacts on '{contact.Chain}' must not overlap.");
                previous = contact;
            }
        }
    }

    private void CheckKeys(List<ClipKey> keys, string field, Func<ClipKey, bool> shape, string shapeMessage)
    {
        Require(keys.Count <= 4096, $"{field}: too many keys.");
        var previous = -1f;
        foreach (var key in keys)
        {
            Require(key is not null, $"{field}: null key.");
            new Limit(0, Duration).Check(key.Time, field + " key time");
            Require(key.Time > previous, $"{field}: key times must be strictly increasing."); previous = key.Time;
            foreach (var value in new[] { key.X, key.Y, key.Z, key.Angle }) new Limit(-1000, 1000).Check(value, field + " key value");
            Require(shape(key), $"{field}: {shapeMessage}.");
        }
    }

    /// <summary>Checks this clip against the model it will play on. Matching labels alone never establish compatibility.</summary>
    public void Validate(ResolvedModel model)
    {
        Validate();
        var owner = $"Clip '{Id}'"; var basis = model.Base;
        Require(Model == basis.Id, $"{owner} is for model '{Model}', not '{basis.Id}'.");
        Require(StructureRevision == basis.StructureRevision, $"{owner} was authored against structure revision {StructureRevision} of '{Model}'; the model is at revision {basis.StructureRevision}.");
        var solved = basis.Chains.SelectMany(c => new[] { c.Joint, c.End }).ToHashSet(StringComparer.Ordinal);
        void Scale(string scale, string field)
        {
            if (scale == CharacterModel.Unit) return;
            Require(model.Measures.ContainsKey(scale), $"{field}: the model has no measure '{scale}'.");
            Require(Reference.ContainsKey(scale), $"{field}: no reference measurement for '{scale}'.");
        }
        Scale(Travel.Scale, $"{owner} travel");
        foreach (var track in Tracks)
        {
            var field = $"{owner} {track.Kind} track '{track.Target}'";
            if (track.Kind == TargetKind)
            {
                Require(model.Chains.TryGetValue(track.Target, out var chain), $"{field}: unknown chain.");
                Scale(track.Scale ?? chain.Scale, field);
            }
            else
            {
                Require(model.Controls.TryGetValue(track.Target, out var control), $"{field}: unknown control.");
                Require(!solved.Contains(track.Target), $"{field}: this control is solved by IK; key its chain's target instead.");
                Scale(track.Scale ?? control.Scale, field);
            }
        }
        foreach (var contact in Contacts)
        {
            var field = $"{owner} contact on '{contact.Chain}'";
            Require(model.Chains.TryGetValue(contact.Chain, out var chain), $"{field}: unknown chain.");
            Require(chain.Frame == CharacterModel.Locomotion, $"{field}: contacts need a chain keyed in the locomotion frame.");
            Require(chain.Scale == Travel.Scale, $"{field}: the chain's scale '{chain.Scale}' must match the travel scale '{Travel.Scale}'.");
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"`
Expected: PASS. For `key-beyond`, the message is `... key time must be between 0 and 1; found 1.5.`, which contains `time`.

- [ ] **Step 6: Commit**

```bash
git add App2d.Core/Characters/Authored/MotionClip.cs App2d.Tests/Authored
git commit -m "Add motion clip schema with scale sources and compatibility checks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Pose evaluator

**Files:**
- Create: `App2d.Core/Characters/Authored/PoseEvaluator.cs`
- Test: `App2d.Tests/Authored/PoseEvaluatorTests.cs`

**Interfaces:**
- Consumes: `ResolvedModel` (`Order`, `Rest`, `Controls`, `Chains`, `Children`, `Base.Chains`, `Measure`, `Length`), `MotionClip` and its record types, `TwoBoneIk2D.Solve` (existing, `App2d.Core.Kinematics`).
- Produces:
  - `ChainResult(string Chain, float Residual, bool Reached)`, `ContactResult(string Chain, Vector3 Target, float Residual)`.
  - `EvaluatedPose { Dictionary<string, Vector3> Points (world), Vector3 Locomotion, List<ChainResult> Chains, List<ContactResult> Contacts, Vector3 World(string id) }`.
  - `static EvaluatedPose PoseEvaluator.Sample(ResolvedModel model, MotionClip clip, double seconds, bool repeat = false)`. Precondition: `clip.Validate(model)` has passed; the evaluator does not revalidate per frame.

- [ ] **Step 1: Write the failing tests**

Create `App2d.Tests/Authored/PoseEvaluatorTests.cs`:

```csharp
using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class PoseEvaluatorTests
{
    private static readonly ResolvedModel Model = ResolvedModel.From(TestModels.Creature());

    private static EvaluatedPose Sample(MotionClip clip, double seconds, bool repeat = false, ResolvedModel? model = null)
    {
        clip.Validate(model ?? Model);
        return PoseEvaluator.Sample(model ?? Model, clip, seconds, repeat);
    }

    [Fact]
    public void AnEmptyClipReproducesRest()
    {
        var pose = Sample(TestModels.Clip(Model), .3);
        foreach (var (id, rest) in Model.Rest) TestModels.Near(rest, pose.World(id), 1e-4f, id);
        Assert.All(pose.Chains, c => { Assert.True(c.Reached); Assert.InRange(c.Residual, 0, 1e-4f); });
    }

    [Fact]
    public void TranslationMovesChildrenAndFrameTargetsButNotLocomotionTargets()
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new() { Target = "body", Keys = [new() { Y = -.2f }] });
        var pose = Sample(clip, 0);
        TestModels.Near(new(0, .8f, 0), pose.World("body"));
        TestModels.Near(new(0, 1.3f, 0), pose.World("shoulder"));
        TestModels.Near(new(.8f, 1.3f, 0), pose.World("hand"), 1e-4f, "arm target follows the shoulder frame");
        TestModels.Near(Vector3.Zero, pose.World("foot"), 1e-4f, "leg target stays in the locomotion frame");
    }

    [Fact]
    public void RotationIsInheritedByChildOffsetsAndFrames()
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "body", Keys = [new() { Angle = MathF.PI / 2 }] });
        var pose = Sample(clip, 0);
        TestModels.Near(new(-.5f, 1, 0), pose.World("shoulder"));
        TestModels.Near(new(-.5f, 1.8f, 0), pose.World("hand"));
        TestModels.Near(new(0, 1, 0), pose.World("hip"));
    }

    [Fact]
    public void DeltasScaleByVariantOverReferenceMeasureButDepthDoesNot()
    {
        var variant = new ModelVariant { Id = "long-arm", Name = "Long arm", Base = "creature", Rest = { ["elbow"] = new(.6f, 1.2f), ["hand"] = new(1.2f, 1.5f) } };
        var longArm = ResolvedModel.From(TestModels.Creature(), variant);
        var clip = TestModels.Clip(Model);   // reference measures are the base's
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "arm", Keys = [new() { Y = .1f, Z = .3f }] });
        var pose = Sample(clip, 0, model: longArm);
        TestModels.Near(new(1.2f, 1.65f, .3f), pose.World("hand"));
        Assert.Equal(longArm.Length("shoulder", "elbow"), Vector2.Distance(XY(pose.World("shoulder")), XY(pose.World("elbow"))), 4);
    }

    [Fact]
    public void TravelLoopsAccumulateAndOneShotsClamp()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel = new() { Scale = "leg", Keys = [new() { Time = 0 }, new() { Time = 1, X = .4f }] };
        Assert.Equal(.9f, Sample(clip, 2.25, repeat: true).Locomotion.X, 4);
        Assert.Equal(.4f, Sample(clip, 2.25).Locomotion.X, 4);
        clip.Loop = false;
        Assert.Equal(.4f, Sample(clip, 2.25, repeat: true).Locomotion.X, 4);
    }

    [Fact]
    public void ContactsHoldAWorldTargetOverAHalfOpenInterval()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel = new() { Scale = "leg", Keys = [new() { Time = 0 }, new() { Time = 1, X = .4f }] };
        clip.Contacts.Add(new() { Chain = "leg", Start = 0, Finish = .5f });
        var planted = Sample(clip, .25);
        TestModels.Near(Vector3.Zero, planted.World("foot"), 1e-4f, "planted while travelling");
        Assert.Single(planted.Contacts);
        var released = Sample(clip, .5);
        Assert.Empty(released.Contacts);
        TestModels.Near(new(.2f, 0, 0), released.World("foot"), 1e-4f, "released at the finish");
        TestModels.Near(new(.8f, 0, 0), Sample(clip, 2.25, repeat: true).World("foot"), 1e-4f, "planted at the third cycle's start");
    }

    [Fact]
    public void AHeldEndpointIncludesAContactThatFinishesThere()
    {
        var clip = TestModels.Clip(Model); clip.Loop = false; clip.Travel.Scale = "leg";
        clip.Contacts.Add(new() { Chain = "leg", Start = .5f, Finish = 1, Target = new(.1f, 0) });
        Assert.Single(Sample(clip, 1).Contacts);
        Assert.Single(Sample(clip, 5).Contacts);
        clip.Loop = true;
        Assert.Empty(Sample(clip, 1, repeat: true).Contacts);
    }

    [Fact]
    public void AnUnreachableTargetKeepsLengthsAndReportsTheShortfall()
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "leg", Keys = [new() { Y = -5 }] });
        var pose = Sample(clip, 0);
        var leg = pose.Chains.Single(c => c.Chain == "leg");
        Assert.False(leg.Reached); Assert.InRange(leg.Residual, 4.9f, 5.1f); // target is 6 below the hip; the leg reaches ~1.02
        Assert.Equal(Model.Length("hip", "knee"), Vector2.Distance(XY(pose.World("hip")), XY(pose.World("knee"))), 4);
        Assert.Equal(Model.Length("knee", "foot"), Vector2.Distance(XY(pose.World("knee")), XY(pose.World("foot"))), 4);
    }

    [Theory, InlineData(double.NaN), InlineData(-1d), InlineData(double.PositiveInfinity)]
    public void InvalidTimesAreRejected(double seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PoseEvaluator.Sample(Model, TestModels.Clip(Model), seconds));

    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~PoseEvaluatorTests"`
Expected: build FAIL, `PoseEvaluator` not found.

- [ ] **Step 3: Implement the evaluator**

Create `App2d.Core/Characters/Authored/PoseEvaluator.cs`:

```csharp
using System.Numerics;
using App2d.Core.Kinematics;

namespace App2d.Core.Characters;

public sealed record ChainResult(string Chain, float Residual, bool Reached);
public sealed record ContactResult(string Chain, Vector3 Target, float Residual);

/// <summary>The final pose: world positions for every control and the residuals that produced them. Drawing, sockets and collision all read this.</summary>
public sealed class EvaluatedPose
{
    public Dictionary<string, Vector3> Points { get; } = new(StringComparer.Ordinal);
    /// <summary>The locomotion frame's origin at this sample.</summary>
    public Vector3 Locomotion { get; set; }
    public List<ChainResult> Chains { get; } = [];
    public List<ContactResult> Contacts { get; } = [];
    public Vector3 World(string id) => Points[id];
}

/// <summary>Samples channels, builds the hierarchy, then solves IK and contacts. No graphics.</summary>
public static class PoseEvaluator
{
    /// <summary>The clip must already have passed <see cref="MotionClip.Validate(ResolvedModel)"/> for this model.</summary>
    public static EvaluatedPose Sample(ResolvedModel model, MotionClip clip, double seconds, bool repeat = false)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var cycles = repeat && clip.Loop ? Math.Floor(seconds / clip.Duration) : 0;
        var time = (float)(cycles > 0 ? seconds % clip.Duration : Math.Min(seconds, clip.Duration));
        float Ratio(string scale) => scale == CharacterModel.Unit ? 1 : model.Measure(scale) / clip.Reference[scale];

        var travelRatio = Ratio(clip.Travel.Scale);
        Vector2 Travel(float t) { var (value, _) = Interpolate(clip.Travel.Keys, t); return new Vector2(value.X, value.Y) * travelRatio; }
        var cycleTravel = (Travel(clip.Duration) - Travel(0)) * (float)cycles;
        var cycleOrigin = new Vector3(Travel(0) + cycleTravel, 0);
        var pose = new EvaluatedPose { Locomotion = new(Travel(time) + cycleTravel, 0) };

        var tracks = clip.Tracks.ToDictionary(t => (t.Kind, t.Target));
        Vector3 Delta(string kind, string target, string defaultScale)
        {
            if (!tracks.TryGetValue((kind, target), out var track)) return default;
            var (value, _) = Interpolate(track.Keys, time); var ratio = Ratio(track.Scale ?? defaultScale);
            return new(value.X * ratio, value.Y * ratio, value.Z);
        }
        float Angle(string target) => tracks.TryGetValue((MotionClip.RotateKind, target), out var track) ? Interpolate(track.Keys, time).Angle : 0;

        var angles = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var control in model.Order)
        {
            var parentPoint = control.Parent is null ? pose.Locomotion : pose.Points[control.Parent];
            var parentAngle = control.Parent is null ? 0 : angles[control.Parent];
            var parentRest = control.Parent is null ? Vector3.Zero : model.Rest[control.Parent];
            var offset = model.Rest[control.Id] - parentRest + Delta(MotionClip.TranslateKind, control.Id, control.Scale);
            pose.Points[control.Id] = parentPoint + RotateXY(offset, parentAngle);
            angles[control.Id] = parentAngle + Angle(control.Id);
        }
        foreach (var chain in model.Base.Chains)
        {
            var locomotion = chain.Frame == CharacterModel.Locomotion;
            var framePoint = locomotion ? pose.Locomotion : pose.Points[chain.Frame];
            var frameAngle = locomotion ? 0 : angles[chain.Frame];
            var frameRest = locomotion ? Vector3.Zero : model.Rest[chain.Frame];
            var target = framePoint + RotateXY(model.Rest[chain.End] - frameRest + Delta(MotionClip.TargetKind, chain.Id, chain.Scale), frameAngle);
            pose.Chains.Add(Solve(model, pose, chain, target));
        }
        foreach (var contact in clip.Contacts)
        {
            if (!(time >= contact.Start && (time < contact.Finish || time == clip.Duration && contact.Finish == clip.Duration))) continue;
            var chain = model.Chains[contact.Chain]; var ratio = Ratio(chain.Scale);
            var target = cycleOrigin + model.Rest[chain.End] + new Vector3(contact.Target.X * ratio, contact.Target.Y * ratio, contact.Target.Z);
            var result = Solve(model, pose, chain, target);
            pose.Chains[pose.Chains.FindIndex(c => c.Chain == chain.Id)] = result;
            pose.Contacts.Add(new(chain.Id, target, result.Residual));
        }
        return pose;
    }

    private static ChainResult Solve(ResolvedModel model, EvaluatedPose pose, ModelChain chain, Vector3 target)
    {
        var root = pose.Points[chain.Root];
        var solved = TwoBoneIk2D.Solve(new(root.X, root.Y), new(target.X, target.Y), model.Length(chain.Root, chain.Joint), model.Length(chain.Joint, chain.End), chain.Bend);
        var joint = new Vector3(solved.Joint, pose.Points[chain.Joint].Z);
        var end = new Vector3(solved.End, target.Z);
        MoveDescendants(model, pose, chain.Joint, joint - pose.Points[chain.Joint], chain.End);
        MoveDescendants(model, pose, chain.End, end - pose.Points[chain.End], null);
        pose.Points[chain.Joint] = joint; pose.Points[chain.End] = end;
        return new(chain.Id, Vector2.Distance(solved.End, new(target.X, target.Y)), solved.ReachesTarget);
    }

    private static void MoveDescendants(ResolvedModel model, EvaluatedPose pose, string id, Vector3 delta, string? except)
    {
        foreach (var child in model.Children[id])
        {
            if (child == except) continue;
            pose.Points[child] += delta; MoveDescendants(model, pose, child, delta, null);
        }
    }

    /// <summary>Linear interpolation, holding the first and last keys outside their range. No keys means a zero delta.</summary>
    private static (Vector3 Value, float Angle) Interpolate(List<ClipKey> keys, float time)
    {
        if (keys.Count == 0) return default;
        static (Vector3, float) Of(ClipKey k) => (new(k.X, k.Y, k.Z), k.Angle);
        if (time <= keys[0].Time) return Of(keys[0]);
        for (var i = 1; i < keys.Count; i++)
        {
            if (time > keys[i].Time) continue;
            var a = keys[i - 1]; var b = keys[i]; var u = (time - a.Time) / (b.Time - a.Time);
            return (Vector3.Lerp(new(a.X, a.Y, a.Z), new(b.X, b.Y, b.Z), u), a.Angle + (b.Angle - a.Angle) * u);
        }
        return Of(keys[^1]);
    }

    private static Vector3 RotateXY(Vector3 v, float angle)
    {
        if (angle == 0) return v;
        var (sin, cos) = MathF.SinCos(angle);
        return new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos, v.Z);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"`
Expected: PASS. In `ContactsHoldAWorldTargetOverAHalfOpenInterval`, the hip stays within reach: at t = .25 the hip is at (.1, 1) and the foot at (0, 0), a distance of 1.005, against a leg length of 1.0198.

- [ ] **Step 5: Commit**

```bash
git add App2d.Core/Characters/Authored/PoseEvaluator.cs App2d.Tests/Authored/PoseEvaluatorTests.cs
git commit -m "Add graphics-free pose evaluator for channel clips

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Person template and prototype conversion (reference reproduction)

**Files:**
- Create: `App2d.Core/Characters/Authored/PersonTemplate.cs`
- Create: `App2d.Core/Characters/Authored/PuppetMotionConverter.cs`
- Test: `App2d.Tests/Authored/PersonStudyConversionTests.cs`

**Interfaces:**
- Consumes: `PuppetTemplates.StepStudy()/RunStudy()`, `PuppetPose.Sample(definition, motion, seconds, repeat)`, `PuppetPose.World(id)` (prototype, unchanged), plus Tasks 1–4.
- Produces:
  - `PersonTemplate.Id = "person"` and `static CharacterModel PersonTemplate.Model()`. Chains are `left-arm`, `right-arm` (frame = that side's shoulder, scale `arm`) and `left-leg`, `right-leg` (frame `locomotion`, scale `leg`). Measures are `leg`, `arm` and `torso`.
  - `static MotionClip PuppetMotionConverter.Convert(PuppetDefinition puppet, PuppetMotion motion, CharacterModel model, string id, string name, string travelScale)`.
  - Test helper `TestModels.AssertMatchesPrototype(PuppetDefinition puppet, ResolvedModel model, MotionClip clip)`.

- [ ] **Step 1: Add the prototype comparison helper to `TestModels`**

Append inside `TestModels`:

```csharp
    /// <summary>Two full cycles at 240 samples each: every control's world position must match the prototype evaluator.</summary>
    public static void AssertMatchesPrototype(PuppetDefinition puppet, ResolvedModel model, MotionClip clip)
    {
        var motion = puppet.Motions[0];
        for (var i = 0; i <= 480; i++)
        {
            var seconds = i * motion.Duration / 240.0;
            var expected = PuppetPose.Sample(puppet, motion, seconds, repeat: true);
            var actual = PoseEvaluator.Sample(model, clip, seconds, repeat: true);
            foreach (var control in puppet.Controls) Near(expected.World(control.Id), actual.World(control.Id), 1e-4f, $"{control.Id} at {seconds:F4}s");
        }
    }
```

- [ ] **Step 2: Write the failing tests**

Create `App2d.Tests/Authored/PersonStudyConversionTests.cs`:

```csharp
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class PersonStudyConversionTests
{
    [Theory, InlineData("walk"), InlineData("run")]
    public void ConvertedStudiesReproduceThePrototypeAtReferenceProportions(string which)
    {
        var puppet = which == "walk" ? PuppetTemplates.StepStudy() : PuppetTemplates.RunStudy();
        var model = PersonTemplate.Model();
        var clip = PuppetMotionConverter.Convert(puppet, puppet.Motions[0], model, "person-" + which, which, "leg");
        var resolved = ResolvedModel.From(model);
        clip.Validate(resolved);
        TestModels.AssertMatchesPrototype(puppet, resolved, clip);
    }

    [Fact]
    public void ConversionKeysTargetsNotSolvedJointsAndDropsEmptyTracks()
    {
        var puppet = PuppetTemplates.StepStudy(); var model = PersonTemplate.Model();
        var clip = PuppetMotionConverter.Convert(puppet, puppet.Motions[0], model, "person-walk", "Walk", "leg");
        Assert.DoesNotContain(clip.Tracks, t => t.Target.EndsWith("-knee") || t.Target.EndsWith("-elbow") || t.Target.EndsWith("-foot") || t.Target.EndsWith("-hand"));
        Assert.Contains(clip.Tracks, t => t is { Kind: MotionClip.TargetKind, Target: "left-leg" });
        Assert.Contains(clip.Tracks, t => t is { Kind: MotionClip.TranslateKind, Target: "hips" });
        Assert.DoesNotContain(clip.Tracks, t => t.Keys.All(k => k.X == 0 && k.Y == 0 && k.Z == 0));
        Assert.Equal(new[] { "left-leg", "right-leg" }, clip.Contacts.Select(c => c.Chain).Order());
        Assert.Equal(.5f, clip.Travel.Keys[^1].X, 5);
    }

    [Fact]
    public void PersonModelDeclaresFramesAndMeasures()
    {
        var model = PersonTemplate.Model();
        Assert.Equal("left-shoulder", model.Chains.Single(c => c.Id == "left-arm").Frame);
        Assert.Equal(CharacterModel.Locomotion, model.Chains.Single(c => c.Id == "right-leg").Frame);
        Assert.Equal(new[] { "arm", "leg", "torso" }, model.Measures.Select(m => m.Id).Order());
        Assert.Equal("leg", model.Controls.Single(c => c.Id == "hips").Scale);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~PersonStudyConversionTests"`
Expected: build FAIL, `PersonTemplate` not found.

- [ ] **Step 4: Implement `PersonTemplate.Model`**

Create `App2d.Core/Characters/Authored/PersonTemplate.cs`:

```csharp
namespace App2d.Core.Characters;

/// <summary>The Person starting template. Built from the walk study's rest pose so converted studies reproduce at reference proportions. A template, not an engine category.</summary>
public static class PersonTemplate
{
    public const string Id = "person";

    public static CharacterModel Model()
    {
        var study = PuppetTemplates.StepStudy();
        var parents = study.Bones.ToDictionary(b => b.To, b => b.From, StringComparer.Ordinal);
        // Pelvis bob, stride and hip swing follow leg reach; the upper body follows torso length.
        static string ScaleOf(string id) => id switch
        {
            "hips" or "left-hip" or "right-hip" => "leg",
            "chest" or "head" or "left-shoulder" or "right-shoulder" => "torso",
            _ => CharacterModel.Unit,
        };
        var model = new CharacterModel
        {
            Id = Id, Name = "Person", Ink = study.Ink, LineWidth = study.LineWidth,
            Controls = [.. study.Controls.Select(c => new ModelControl { Id = c.Id, Parent = parents.GetValueOrDefault(c.Id), Rest = c.Rest, Scale = ScaleOf(c.Id) })],
            Chains = [.. study.Chains.Select(c =>
            {
                var arm = c.End.EndsWith("-hand", StringComparison.Ordinal);
                return new ModelChain
                {
                    Id = c.End.Replace(arm ? "-hand" : "-foot", arm ? "-arm" : "-leg"), Root = c.Root, Joint = c.Joint, End = c.End, Bend = c.Bend,
                    Frame = arm ? c.Root : CharacterModel.Locomotion, Scale = arm ? "arm" : "leg",
                };
            })],
            Measures =
            [
                new() { Id = "leg", Path = ["right-hip", "right-knee", "right-foot"] },
                new() { Id = "arm", Path = ["right-shoulder", "right-elbow", "right-hand"] },
                new() { Id = "torso", Path = ["hips", "chest"] },
            ],
            Parts = [.. study.Parts.Select(p => p with { })],
        };
        model.Validate(); return model;
    }
}
```

- [ ] **Step 5: Implement the converter**

Create `App2d.Core/Characters/Authored/PuppetMotionConverter.cs`:

```csharp
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// One-way, explicit conversion of a prototype motion (absolute per-key positions) into channels for a model built from the
/// same rest pose. Keys keep their times; joints solved by IK get no channel. The prototype stays a reference fixture.
/// </summary>
public static class PuppetMotionConverter
{
    public static MotionClip Convert(PuppetDefinition puppet, PuppetMotion motion, CharacterModel model, string id, string name, string travelScale)
    {
        var resolved = ResolvedModel.From(model);
        var solved = model.Chains.SelectMany(c => new[] { c.Joint, c.End }).ToHashSet(StringComparer.Ordinal);
        foreach (var control in model.Controls.Where(c => !solved.Contains(c.Id)))
            for (var parent = control.Parent; parent is not null; parent = resolved.Controls[parent].Parent)
                if (solved.Contains(parent)) throw new InvalidDataException($"Control '{control.Id}' hangs from IK-solved '{parent}'; conversion does not support that yet.");

        Vector3 Rest(string control) => resolved.Rest[control];
        var clip = new MotionClip
        {
            Id = id, Name = name, Model = model.Id, StructureRevision = model.StructureRevision, Duration = motion.Duration, Loop = motion.Loop,
            Reference = resolved.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), Travel = new() { Scale = travelScale },
        };
        var tracks = new Dictionary<(string, string), ClipTrack>();
        void Add(string kind, string target, float time, Vector3 delta)
        {
            if (!tracks.TryGetValue((kind, target), out var track)) tracks[(kind, target)] = track = new() { Kind = kind, Target = target };
            track.Keys.Add(new() { Time = time, X = delta.X, Y = delta.Y, Z = delta.Z });
        }
        foreach (var key in motion.Keys)
        {
            var position = key.Position.XYZ; var locomotion = new Vector3(position.X, 0, 0);
            Vector3 World(string control) => (key.Points.TryGetValue(control, out var p) ? p.XYZ : Rest(control)) + position;
            clip.Travel.Keys.Add(new() { Time = key.Time, X = position.X });
            foreach (var control in model.Controls.Where(c => !solved.Contains(c.Id)))
            {
                var parentWorld = control.Parent is null ? locomotion : World(control.Parent);
                var parentRest = control.Parent is null ? Vector3.Zero : Rest(control.Parent);
                Add(MotionClip.TranslateKind, control.Id, key.Time, World(control.Id) - parentWorld - (Rest(control.Id) - parentRest));
            }
            foreach (var chain in model.Chains)
            {
                var inLocomotion = chain.Frame == CharacterModel.Locomotion;
                var frameWorld = inLocomotion ? locomotion : World(chain.Frame);
                var frameRest = inLocomotion ? Vector3.Zero : Rest(chain.Frame);
                Add(MotionClip.TargetKind, chain.Id, key.Time, World(chain.End) - frameWorld - (Rest(chain.End) - frameRest));
            }
        }
        const float epsilon = 1e-7f;
        clip.Tracks = [.. tracks.Values.Where(t => t.Keys.Any(k => MathF.Abs(k.X) > epsilon || MathF.Abs(k.Y) > epsilon || MathF.Abs(k.Z) > epsilon))];
        var start = clip.Travel.Keys.Count > 0 ? clip.Travel.Keys[0].X : 0;
        foreach (var contact in motion.Contacts)
        {
            var chain = model.Chains.Single(c => c.End == contact.End);
            clip.Contacts.Add(new() { Chain = chain.Id, Start = contact.Start, Finish = contact.Finish, Target = PuppetPoint.From(contact.Target.XYZ - new Vector3(start, 0, 0) - Rest(chain.End)) });
        }
        clip.Validate(resolved); return clip;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"`
Expected: PASS. **If the reproduction test fails at a contact boundary** (the prototype uses closed intervals, the new evaluator half-open ones), print the failing time and both positions. The keys agree with the contact targets at the boundaries, so a mismatch there means a converter bug, not a tolerance issue. Do not widen the tolerance above `1e-4`. This is the doc's schema checkpoint: if the reproduction cannot pass, stop and report rather than changing the channel scheme on your own.

- [ ] **Step 7: Commit**

```bash
git add App2d.Core/Characters/Authored App2d.Tests/Authored
git commit -m "Convert walk and run studies to channel clips on a Person model

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Person build values and the three-build motion gate

**Files:**
- Create: `App2d.Core/Characters/Authored/PersonBuild.cs`
- Test: `App2d.Tests/Authored/PersonVariantMotionTests.cs`

**Interfaces:**
- Consumes: `PersonTemplate.Model()`, `PuppetMotionConverter.Convert`, `ResolvedModel`, `PoseEvaluator`, `ModelVariant`, `PartOverride`.
- Produces: `PersonBuild { float Legs, Torso, Arms, Width, Head (init, default 1) }`, `PersonBuild.TallThin`, `PersonBuild.ShortBroad`, `PersonBuild.Range` (a `Limit` of .5–2), and `ModelVariant Apply(CharacterModel person, string id, string name, string? bodyFill = null)`.

- [ ] **Step 1: Write the failing tests**

Create `App2d.Tests/Authored/PersonVariantMotionTests.cs`:

```csharp
using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

/// <summary>The phase-one gate: one shared walk and run, no copied keys, correct on three builds.</summary>
public sealed class PersonVariantMotionTests
{
    private static readonly CharacterModel Person = PersonTemplate.Model();

    private static MotionClip Clip(string which)
    {
        var puppet = which == "walk" ? PuppetTemplates.StepStudy() : PuppetTemplates.RunStudy();
        return PuppetMotionConverter.Convert(puppet, puppet.Motions[0], Person, "person-" + which, which, "leg");
    }
    private static PersonBuild BuildValues(string build) => build switch { "tall" => PersonBuild.TallThin, "short" => PersonBuild.ShortBroad, _ => new() };
    private static ResolvedModel Build(string build) =>
        build == "standard" ? ResolvedModel.From(Person) : ResolvedModel.From(Person, BuildValues(build).Apply(Person, build + "-build", build));
    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);
    private static IEnumerable<double> TwoCycles(MotionClip clip) => Enumerable.Range(0, 481).Select(i => i * clip.Duration / 240.0);

    public static TheoryData<string, string> Cases => new()
    {
        { "walk", "standard" }, { "walk", "tall" }, { "walk", "short" }, { "run", "standard" }, { "run", "tall" }, { "run", "short" },
    };

    [Theory, MemberData(nameof(Cases))]
    public void LimbsKeepTheirLengthsAndEveryTargetIsReached(string clipName, string build)
    {
        var model = Build(build); var clip = Clip(clipName); clip.Validate(model);
        foreach (var seconds in TwoCycles(clip))
        {
            var pose = PoseEvaluator.Sample(model, clip, seconds, repeat: true);
            foreach (var chain in model.Chains.Values)
            {
                Assert.InRange(MathF.Abs(model.Length(chain.Root, chain.Joint) - Vector2.Distance(XY(pose.World(chain.Root)), XY(pose.World(chain.Joint)))), 0, 1e-4f);
                Assert.InRange(MathF.Abs(model.Length(chain.Joint, chain.End) - Vector2.Distance(XY(pose.World(chain.Joint)), XY(pose.World(chain.End)))), 0, 1e-4f);
            }
            Assert.All(pose.Chains, c => Assert.True(c.Reached, $"{c.Chain} misses by {c.Residual} at {seconds:F3}s"));
            Assert.All(pose.Contacts, c => Assert.InRange(c.Residual, 0, 1e-4f));
        }
    }

    [Theory, InlineData("standard"), InlineData("tall"), InlineData("short")]
    public void LimbSegmentsScaleWithTheirBuildValue(string build)
    {
        var model = Build(build); var standard = Build("standard");
        Assert.Equal(standard.Length("left-hip", "left-knee") * BuildValues(build).Legs, model.Length("left-hip", "left-knee"), 4);
        Assert.Equal(standard.Length("right-shoulder", "right-elbow") * BuildValues(build).Arms, model.Length("right-shoulder", "right-elbow"), 4);
    }

    [Theory, MemberData(nameof(Cases))]
    public void PlantedFeetStayPutAndStrideScalesWithLegLength(string clipName, string build)
    {
        var model = Build(build); var clip = Clip(clipName); var standard = Build("standard");
        float Stride(ResolvedModel m) => PoseEvaluator.Sample(m, clip, clip.Duration, repeat: true).Locomotion.X - PoseEvaluator.Sample(m, clip, 0).Locomotion.X;
        Assert.InRange(MathF.Abs(Stride(model) - Stride(standard) * BuildValues(build).Legs), 0, 1e-4f);
        foreach (var contact in clip.Contacts)
        {
            var end = model.Chains[contact.Chain].End;
            var planted = PoseEvaluator.Sample(model, clip, clip.Duration + contact.Start, repeat: true).World(end);
            for (var i = 0; i < 20; i++)
            {
                var t = contact.Start + (contact.Finish - contact.Start) * i / 20f;
                TestModels.Near(planted, PoseEvaluator.Sample(model, clip, clip.Duration + t, repeat: true).World(end), 1e-4f, $"{end} at {t:F3}s");
            }
        }
    }

    [Theory, MemberData(nameof(Cases))]
    public void TheLoopSeamIsContinuous(string clipName, string build)
    {
        var model = Build(build); var clip = Clip(clipName);
        var before = PoseEvaluator.Sample(model, clip, clip.Duration - 1e-4, repeat: true);
        var after = PoseEvaluator.Sample(model, clip, clip.Duration, repeat: true);
        foreach (var id in model.Rest.Keys) TestModels.Near(before.World(id), after.World(id), .01f, id);
        TestModels.Near(before.Locomotion, after.Locomotion, .001f, "travel");
    }

    [Theory, InlineData("walk"), InlineData("run")]
    public void BuildsKeepTheReferenceJointAngles(string clipName)
    {
        var clip = Clip(clipName); var standard = Build("standard");
        static float Bend(EvaluatedPose pose, ModelChain chain)
        {
            var a = Vector2.Normalize(XY(pose.World(chain.Root) - pose.World(chain.Joint)));
            var b = Vector2.Normalize(XY(pose.World(chain.End) - pose.World(chain.Joint)));
            return MathF.Acos(Math.Clamp(Vector2.Dot(a, b), -1, 1)) * 180 / MathF.PI;
        }
        foreach (var build in new[] { "tall", "short" })
        {
            var model = Build(build);
            for (var i = 0; i < 48; i++)
            {
                var seconds = i * clip.Duration / 48.0;
                var a = PoseEvaluator.Sample(standard, clip, seconds); var b = PoseEvaluator.Sample(model, clip, seconds);
                foreach (var chain in standard.Chains.Values) Assert.InRange(MathF.Abs(Bend(a, chain) - Bend(b, chain)), 0, 1f);
            }
        }
    }

    [Fact]
    public void EditingTheSharedClipChangesEveryBuild()
    {
        var clip = Clip("walk"); var builds = new[] { "standard", "tall", "short" }.Select(Build).ToArray();
        var before = builds.Select(m => PoseEvaluator.Sample(m, clip, 0).World("hips").Y).ToArray();
        clip.Tracks.Single(t => t is { Kind: MotionClip.TranslateKind, Target: "hips" }).Keys[0].Y += .1f;
        var after = builds.Select(m => PoseEvaluator.Sample(m, clip, 0).World("hips").Y).ToArray();
        Assert.Equal(.1f, after[0] - before[0], 4);
        Assert.Equal(.1f * PersonBuild.TallThin.Legs, after[1] - before[1], 4);
        Assert.Equal(.1f * PersonBuild.ShortBroad.Legs, after[2] - before[2], 4);
    }

    [Fact]
    public void AnUnreachableBuildKeepsLimbLengthsAndReportsTheShortfall()
    {
        // Straighten the left knee: that leg can no longer reach mid-stance, while the leg measure (right leg) is unchanged.
        var hip = Person.Controls.Single(c => c.Id == "left-hip").Rest.XYZ; var foot = Person.Controls.Single(c => c.Id == "left-foot").Rest.XYZ;
        var variant = new ModelVariant { Id = "stiff-left", Name = "Stiff left", Base = "person", Rest = { ["left-knee"] = PuppetPoint.From(Vector3.Lerp(hip, foot, .5f)) } };
        var model = ResolvedModel.From(Person, variant); var clip = Clip("walk"); clip.Validate(model);
        var misses = 0;
        foreach (var seconds in TwoCycles(clip))
        {
            var pose = PoseEvaluator.Sample(model, clip, seconds, repeat: true);
            Assert.InRange(MathF.Abs(model.Length("left-hip", "left-knee") - Vector2.Distance(XY(pose.World("left-hip")), XY(pose.World("left-knee")))), 0, 1e-4f);
            if (pose.Chains.Single(c => c.Chain == "left-leg") is { Reached: false, Residual: > 1e-3f }) misses++;
        }
        Assert.True(misses > 0, "Expected the straightened leg to report reach errors.");
    }

    [Fact]
    public void BuildValuesAreRangeChecked() =>
        Assert.Contains("legs", Assert.Throws<InvalidDataException>(() => new PersonBuild { Legs = 3 }.Apply(Person, "giant", "Giant")).Message);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~PersonVariantMotionTests"`
Expected: build FAIL, `PersonBuild` not found.

- [ ] **Step 3: Implement `PersonBuild`**

Create `App2d.Core/Characters/Authored/PersonBuild.cs`:

```csharp
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// Person build values: multipliers on the template's proportions, applied by the explicit geometry below and written
/// as ordinary variant overrides. An authoring shortcut, not a parameter system or a live parent.
/// </summary>
public sealed record PersonBuild
{
    public static readonly Limit Range = new(.5f, 2);
    public float Legs { get; init; } = 1;
    public float Torso { get; init; } = 1;
    public float Arms { get; init; } = 1;
    public float Width { get; init; } = 1;
    public float Head { get; init; } = 1;

    public static PersonBuild TallThin { get; } = new() { Legs = 1.2f, Torso = 1.12f, Arms = 1.15f, Width = .8f, Head = .92f };
    public static PersonBuild ShortBroad { get; } = new() { Legs = .82f, Torso = .9f, Arms = .88f, Width = 1.3f, Head = 1.1f };

    public ModelVariant Apply(CharacterModel person, string id, string name, string? bodyFill = null)
    {
        Range.Check(Legs, "legs"); Range.Check(Torso, "torso"); Range.Check(Arms, "arms"); Range.Check(Width, "width"); Range.Check(Head, "head");
        if (person.Id != PersonTemplate.Id) throw new InvalidDataException($"Person build values apply to the '{PersonTemplate.Id}' template, not '{person.Id}'.");
        var rest = person.Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ, StringComparer.Ordinal);
        var built = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        // Feet stay on the ground; everything above them scales from it. Width spreads the hips and shoulders, and their depth.
        var ground = rest["left-foot"].Y;
        float Lift(float y) => ground + (y - ground) * Legs;
        Vector3 Across(Vector3 v) => new(v.X * Width, v.Y, v.Z * Width);
        built["hips"] = rest["hips"] with { Y = Lift(rest["hips"].Y) };
        built["chest"] = built["hips"] + (rest["chest"] - rest["hips"]) * Torso;
        built["head"] = built["chest"] + (rest["head"] - rest["chest"]) * Torso;
        foreach (var side in new[] { "left", "right" })
        {
            string Side(string control) => side + "-" + control;
            built[Side("foot")] = Across(rest[Side("foot")]);
            built[Side("hip")] = Across(rest[Side("hip")]) with { Y = Lift(rest[Side("hip")].Y) };
            built[Side("knee")] = built[Side("foot")] + (rest[Side("knee")] - rest[Side("foot")]) * Legs;
            var shoulder = rest[Side("shoulder")] - rest["chest"];
            built[Side("shoulder")] = built["chest"] + new Vector3(shoulder.X * Width, shoulder.Y * Torso, shoulder.Z * Width);
            built[Side("elbow")] = built[Side("shoulder")] + (rest[Side("elbow")] - rest[Side("shoulder")]) * Arms;
            built[Side("hand")] = built[Side("elbow")] + (rest[Side("hand")] - rest[Side("elbow")]) * Arms;
        }
        var variant = new ModelVariant { Id = id, Name = name, Base = person.Id };
        foreach (var (control, point) in built) if (point != rest[control]) variant.Rest[control] = PuppetPoint.From(point);
        var body = person.Parts.Single(p => p.Id == "body"); var head = person.Parts.Single(p => p.Id == "head");
        variant.Parts["body"] = new() { Width = body.Width * Width, Height = body.Height * Torso, OffsetY = body.OffsetY * Torso, Fill = bodyFill };
        variant.Parts["head"] = new() { Width = head.Width * Head, Height = head.Height * Head };
        variant.Validate(); return variant;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"`
Expected: PASS. **This is the schema checkpoint.** If `BuildsKeepTheReferenceJointAngles` or `LimbsKeepTheirLengthsAndEveryTargetIsReached` fails, the channel scheme does not preserve the performance on the builds. Stop, capture the failing chain, build and time, and report to your human partner before changing the scheme or the build geometry.

- [ ] **Step 5: Commit**

```bash
git add App2d.Core/Characters/Authored/PersonBuild.cs App2d.Tests/Authored/PersonVariantMotionTests.cs
git commit -m "Add Person build values and prove shared motion on three builds

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Authored catalog, conversion command and checked-in assets

**Files:**
- Create: `App2d.Core/Characters/Authored/AuthoredCatalog.cs`
- Modify: `App2d.Core/Characters/Authored/PersonTemplate.cs` (add `WriteStudies`)
- Modify: `App2d.CharacterStudio/Program.cs` (the `--convert-studies` argument)
- Create (generated): `Assets/Characters/authored/models/person.json`, `variants/tall-thin.json`, `variants/short-broad.json`, `animations/person-walk.json`, `animations/person-run.json`
- Test: `App2d.Tests/Authored/AuthoredCatalogTests.cs`

**Interfaces:**
- Consumes: every schema type, `PersonBuild`, `PuppetMotionConverter`.
- Produces:
  - `AuthoredCatalog.Load(string authoredRoot)`, with `Models`, `Variants` and `Animations` (`IReadOnlyDictionary<string, …>`), `Errors: List<string>` (each `"<file path>: <message>"`), and `ResolvedModel Resolve(string modelOrVariantId)` (cached).
  - `PersonTemplate.WriteStudies(string authoredRoot)`.

- [ ] **Step 1: Write the failing tests**

Create `App2d.Tests/Authored/AuthoredCatalogTests.cs`:

```csharp
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class AuthoredCatalogTests
{
    [Fact]
    public void CheckedInAssetsLoadCleanly()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.Empty(catalog.Errors);
        Assert.Contains("person", catalog.Models.Keys);
        Assert.Equal(new[] { "short-broad", "tall-thin" }, catalog.Variants.Keys.Order());
        Assert.Equal(new[] { "person-run", "person-walk" }, catalog.Animations.Keys.Order());
        Assert.Same(catalog.Resolve("tall-thin"), catalog.Resolve("tall-thin"));
    }

    [Theory, InlineData("person-walk"), InlineData("person-run")]
    public void CheckedInClipsReproduceThePrototype(string id)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var puppet = id == "person-walk" ? PuppetTemplates.StepStudy() : PuppetTemplates.RunStudy();
        TestModels.AssertMatchesPrototype(puppet, catalog.Resolve("person"), catalog.Animations[id]);
    }

    [Fact]
    public void BrokenFilesAreReportedAgainstTheirPathWithoutStoppingTheScan()
    {
        var root = Path.Combine(Path.GetTempPath(), $"authored-{Guid.NewGuid():N}");
        try
        {
            PersonTemplate.WriteStudies(root);
            var orphan = PersonBuild.TallThin.Apply(PersonTemplate.Model(), "orphan", "Orphan"); orphan.Base = "hound";
            File.WriteAllText(Path.Combine(root, "variants", "orphan.json"), orphan.ToJson());
            File.WriteAllText(Path.Combine(root, "variants", "copy.json"), File.ReadAllText(Path.Combine(root, "variants", "tall-thin.json")));
            File.WriteAllText(Path.Combine(root, "animations", "typo.json"), File.ReadAllText(Path.Combine(root, "animations", "person-walk.json")).Replace("\"tracks\":", "\"trakcs\":"));
            var catalog = AuthoredCatalog.Load(root);
            Assert.Contains(catalog.Errors, e => e.Contains("orphan.json") && e.Contains("hound"));
            Assert.Contains(catalog.Errors, e => e.Contains("tall-thin") && e.Contains("already used"));
            Assert.Contains(catalog.Errors, e => e.Contains("typo.json"));
            Assert.Contains("person-walk", catalog.Animations.Keys);
            Assert.Throws<InvalidDataException>(() => catalog.Resolve("orphan"));
        }
        finally { Directory.Delete(root, true); }
    }
}
```

`copy.json` sorts before `tall-thin.json`, so the scan loads `copy.json` first and reports the checked-in `tall-thin.json` as the duplicate. The assertion only needs one error containing both "tall-thin" and "already used", which holds either way.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~AuthoredCatalogTests"`
Expected: build FAIL, `AuthoredCatalog` not found.

- [ ] **Step 3: Implement `AuthoredCatalog`**

Create `App2d.Core/Characters/Authored/AuthoredCatalog.cs`:

```csharp
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>Every authored character asset under one root, found by scanning its folders. IDs come from file contents; errors are collected per file, not thrown.</summary>
public sealed class AuthoredCatalog
{
    private readonly Dictionary<string, CharacterModel> _models = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModelVariant> _variants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MotionClip> _animations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResolvedModel> _resolved = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, CharacterModel> Models => _models;
    public IReadOnlyDictionary<string, ModelVariant> Variants => _variants;
    public IReadOnlyDictionary<string, MotionClip> Animations => _animations;
    public List<string> Errors { get; } = [];

    public static AuthoredCatalog Load(string root)
    {
        var catalog = new AuthoredCatalog();
        catalog.Scan(root, "models", CharacterModel.FromJson, catalog._models, m => m.Id);
        catalog.Scan(root, "variants", ModelVariant.FromJson, catalog._variants, v => v.Id);
        catalog.Scan(root, "animations", MotionClip.FromJson, catalog._animations, a => a.Id);
        foreach (var id in catalog._variants.Keys) catalog.Check(id, () => catalog.Resolve(id));
        foreach (var clip in catalog._animations.Values)
            catalog.Check(clip.Id, () =>
            {
                if (!catalog._models.ContainsKey(clip.Model)) throw new InvalidDataException($"Clip '{clip.Id}' references missing model '{clip.Model}'.");
                clip.Validate(catalog.Resolve(clip.Model));
            });
        return catalog;
    }

    /// <summary>Resolves a base model or variant by ID. Compiled once and shared.</summary>
    public ResolvedModel Resolve(string id)
    {
        if (_resolved.TryGetValue(id, out var cached)) return cached;
        ResolvedModel resolved;
        if (_models.TryGetValue(id, out var model)) resolved = ResolvedModel.From(model);
        else if (!_variants.TryGetValue(id, out var variant)) throw new KeyNotFoundException($"No model or variant '{id}'.");
        else if (!_models.TryGetValue(variant.Base, out model)) throw new InvalidDataException($"Variant '{id}' references missing base model '{variant.Base}'.");
        else resolved = ResolvedModel.From(model, variant);
        return _resolved[id] = resolved;
    }

    private void Scan<T>(string root, string folder, Func<string, T> parse, Dictionary<string, T> into, Func<T, string> id)
    {
        var directory = Path.Combine(root, folder);
        if (!Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var asset = parse(File.ReadAllText(path)); var key = id(asset);
                if (_paths.TryGetValue(key, out var other)) { Errors.Add($"{path}: id '{key}' is already used by {other}."); continue; }
                _paths[key] = path; into[key] = asset;
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException) { Errors.Add($"{path}: {ex.Message}"); }
        }
    }

    private void Check(string id, Action action)
    {
        try { action(); }
        catch (InvalidDataException ex) { Errors.Add($"{_paths[id]}: {ex.Message}"); }
    }
}
```

- [ ] **Step 4: Add `WriteStudies` to `PersonTemplate`**

Append inside `PersonTemplate`:

```csharp
    /// <summary>Writes the Person model, its converted walk and run, and the two reviewed builds as authored assets.</summary>
    public static void WriteStudies(string authoredRoot)
    {
        var model = Model(); var walk = PuppetTemplates.StepStudy(); var run = PuppetTemplates.RunStudy();
        void Write(string folder, string id, string json)
        {
            var directory = Path.Combine(authoredRoot, folder); Directory.CreateDirectory(directory);
            AuthoredAsset.Write(Path.Combine(directory, id + ".json"), json);
        }
        Write("models", model.Id, model.ToJson());
        Write("animations", "person-walk", PuppetMotionConverter.Convert(walk, walk.Motions[0], model, "person-walk", "Walk", "leg").ToJson());
        Write("animations", "person-run", PuppetMotionConverter.Convert(run, run.Motions[0], model, "person-run", "Run", "leg").ToJson());
        Write("variants", "tall-thin", PersonBuild.TallThin.Apply(model, "tall-thin", "Tall and thin", "#d9e2ef").ToJson());
        Write("variants", "short-broad", PersonBuild.ShortBroad.Apply(model, "short-broad", "Short and broad", "#efdcc9").ToJson());
    }
```

- [ ] **Step 5: Add `--convert-studies` to `Program.cs`**

In `Program.Main`, as the first statement inside `try` (before `FindAssets`):

```csharp
            if (args is ["--convert-studies", var output]) { PersonTemplate.WriteStudies(Path.GetFullPath(output)); return 0; }
```

Add `using App2d.Core.Characters;` at the top of `Program.cs`. In the usage string, insert `--convert-studies authored-directory | ` after `[--workshop | `.

- [ ] **Step 6: Generate the checked-in assets**

Run:

```bash
dotnet build App2d.CharacterStudio -v q -nologo && dotnet run --project App2d.CharacterStudio --no-build -- --convert-studies Assets/Characters/authored && ls -R Assets/Characters/authored
```

Expected: five files, one each under `models/`, `variants/` (2) and `animations/` (2). Open `person-walk.json` and confirm it is readable: `format`, `id`, `model`, `reference`, `travel`, `tracks` (no `-knee`/`-elbow`/`-foot`/`-hand` targets) and `contacts`.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~App2d.Tests.Authored"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add App2d.Core/Characters/Authored App2d.CharacterStudio/Program.cs Assets/Characters/authored App2d.Tests/Authored
git commit -m "Add authored catalog and check in Person walk, run and two builds

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Side-by-side and game-scale preview (visual gate)

**Files:**
- Modify: `App2d.Rendering/Characters/PuppetDrawing.cs`
- Create: `App2d.CharacterStudio/StudioGame.MotionProof.cs`
- Modify: `App2d.CharacterStudio/StudioGame.cs` (constructor, `Draw`, `Dispose`)
- Modify: `App2d.CharacterStudio/Program.cs` (the `--smoke-motion` argument)
- Test: `App2d.Tests/Authored/ModelDrawingTests.cs`

**Interfaces:**
- Consumes: `AuthoredCatalog`, `PoseEvaluator`, `EvaluatedPose`, `ResolvedModel.Parts/Base.Ink/Base.LineWidth`, `PointCharacterRenderer.Projection(width, height, anchor, ppu)`, `_renderer.Draw(mesh, projection, transform, writeDepth:)`, `CharacterMesh.Line/Clear`.
- Produces: `PuppetDrawing.Build(string ink, float lineWidth, IEnumerable<PuppetPart> parts, Func<string, Vector3> world)`, `PuppetDrawing.Build(ResolvedModel model, EvaluatedPose pose)`, and the studio argument `--smoke-motion <dir>` writing `person-walk-NN.png`, `person-run-NN.png` and `motion-proof.txt`.

- [ ] **Step 1: Write the failing drawing test**

Create `App2d.Tests/Authored/ModelDrawingTests.cs`:

```csharp
using App2d.Core.Characters;
using App2d.Rendering.Characters;

namespace App2d.Tests.Authored;

public sealed class ModelDrawingTests
{
    [Fact]
    public void AVariantDrawsFromTheSameEvaluatedPoseAsItsBase()
    {
        var person = PersonTemplate.Model();
        var clip = PuppetMotionConverter.Convert(PuppetTemplates.StepStudy(), PuppetTemplates.StepStudy().Motions[0], person, "person-walk", "Walk", "leg");
        var standard = ResolvedModel.From(person);
        var tall = ResolvedModel.From(person, PersonBuild.TallThin.Apply(person, "tall-thin", "Tall"));
        var a = new PuppetDrawing(); a.Build(standard, PoseEvaluator.Sample(standard, clip, .3));
        var b = new PuppetDrawing(); b.Build(tall, PoseEvaluator.Sample(tall, clip, .3));
        Assert.True(a.Mesh.Count > 0);
        Assert.Equal(a.Mesh.Count, b.Mesh.Count);
        Assert.True(b.Mesh.Max.Y > a.Mesh.Max.Y + .2f, "The tall build should draw taller.");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ModelDrawingTests"`
Expected: build FAIL, no `Build(ResolvedModel, EvaluatedPose)` overload.

- [ ] **Step 3: Generalize `PuppetDrawing`**

In `App2d.Rendering/Characters/PuppetDrawing.cs`, replace the signature and first lines of `Build(PuppetDefinition definition, PuppetPose pose)` so the class reads:

```csharp
    public void Build(PuppetDefinition definition, PuppetPose pose) => Build(definition.Ink, definition.LineWidth, definition.Parts, pose.World);
    public void Build(ResolvedModel model, EvaluatedPose pose) => Build(model.Base.Ink, model.Base.LineWidth, model.Parts, pose.World);

    /// <summary>Plain primitives from parts and a world-position lookup. The only drawing path for both prototype and authored models.</summary>
    public void Build(string inkColor, float lineWidth, IEnumerable<PuppetPart> parts, Func<string, Vector3> world)
    {
        Mesh.Clear(); var ink = CharacterJson.Color(inkColor);
        foreach (var part in parts)
        {
            var a = world(part.A); var b = part.B is { } end ? world(end) : a + Vector3.UnitY;
```

The rest of the loop body is unchanged, except that `definition.LineWidth` becomes `lineWidth` in the two places it appears (`Mesh.Polygon(...)` and `FaceDrawing.Build(...)`).

- [ ] **Step 4: Run the drawing and puppet tests**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj --filter "FullyQualifiedName~ModelDrawingTests|FullyQualifiedName~PuppetAuthoringTests"`
Expected: PASS.

- [ ] **Step 5: Add the studio proof renderer**

Create `App2d.CharacterStudio/StudioGame.MotionProof.cs`:

```csharp
using App2d.Core.Characters;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

/// <summary>Phase-one visual gate: one shared walk and run on three Person builds, side by side at one world scale, over a game-scale band.</summary>
internal sealed partial class StudioGame
{
    private const int ProofFrames = 24, ProofWidth = 1800, ProofHeight = 900, ProofPanelHeight = 640;
    private const float ProofPpu = 200, GamePpu = 48; // Arena scale is min(width / 26, height / 8): about 49 at 1280x720.
    private static readonly string[] ProofSubjects = ["person", "tall-thin", "short-broad"];
    private static readonly string[] ProofClips = ["person-walk", "person-run"];
    private readonly bool _motionSmoke;
    private AuthoredCatalog? _proofCatalog;
    private RenderTarget2D? _proofTarget;
    private readonly PuppetDrawing[] _proofDrawings = [new(), new(), new()];
    private readonly CharacterMesh _proofGround = new(8192);

    private AuthoredCatalog ProofCatalog()
    {
        if (_proofCatalog is not null) return _proofCatalog;
        var catalog = AuthoredCatalog.Load(Path.Combine(_assetRoot, "authored"));
        if (catalog.Errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, catalog.Errors));
        return _proofCatalog = catalog;
    }

    private bool PrepareMotionProof()
    {
        if (_smokeIndex >= ProofClips.Length * ProofFrames) { WriteMotionProofReport(); return false; }
        var catalog = ProofCatalog();
        _proofTarget ??= new(GraphicsDevice, ProofWidth, ProofHeight, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var clipId = ProofClips[_smokeIndex / ProofFrames]; var frame = _smokeIndex % ProofFrames;
        var clip = catalog.Animations[clipId]; var seconds = frame * clip.Duration / ProofFrames;
        var panel = ProofWidth / ProofSubjects.Length; var band = ProofHeight - ProofPanelHeight;
        GraphicsDevice.SetRenderTarget(_proofTarget); GraphicsDevice.Clear(new Color(237, 238, 226));
        for (var i = 0; i < ProofSubjects.Length; i++)
        {
            var model = catalog.Resolve(ProofSubjects[i]);
            var pose = PoseEvaluator.Sample(model, clip, seconds);
            _proofDrawings[i].Build(model, pose);
            // Close-up: the view follows this subject's travel, so ground ticks scroll past a planted foot.
            GraphicsDevice.Viewport = new(i * panel, 0, panel, ProofPanelHeight);
            var close = PointCharacterRenderer.Projection(panel, ProofPanelHeight, new(panel * .5f - pose.Locomotion.X * ProofPpu, ProofPanelHeight * .92f), ProofPpu);
            BuildProofGround(pose, pose.Locomotion.X, ProofPpu);
            _renderer.Draw(_proofGround, close, Matrix.Identity, writeDepth: false);
            _renderer.Draw(_proofDrawings[i].Mesh, close, Matrix.Identity);
            // Game scale: a fixed camera at arena size, so travel and readability show at player size.
            GraphicsDevice.Viewport = new(0, ProofPanelHeight, ProofWidth, band);
            var game = PointCharacterRenderer.Projection(ProofWidth, band, new(ProofWidth * .12f + i * 11 * GamePpu, band * .85f), GamePpu);
            BuildProofGround(pose, 5, GamePpu);
            _renderer.Draw(_proofGround, game, Matrix.Identity, writeDepth: false);
            _renderer.Draw(_proofDrawings[i].Mesh, game, Matrix.Identity);
        }
        GraphicsDevice.SetRenderTarget(null);
        using var stream = File.Create(Path.Combine(_smokePath!, $"{clipId}-{frame:D2}.png"));
        _proofTarget.SaveAsPng(stream, ProofWidth, ProofHeight);
        return true;
    }

    private void BuildProofGround(EvaluatedPose pose, float centerX, float ppu)
    {
        _proofGround.Clear(); var ink = new Color(154, 169, 158); var pixel = 1 / ppu;
        _proofGround.Line(new(centerX - 20, 0, 7), new(centerX + 20, 0, 7), pixel, ink);
        var first = (int)MathF.Floor((centerX - 6) * 4);
        for (var x = first; x <= first + 48; x++) _proofGround.Line(new(x * .25f, 0, 7), new(x * .25f, -(x % 4 == 0 ? 9 : 4) * pixel, 7), pixel, ink);
        foreach (var contact in pose.Contacts)
        {
            var color = contact.Residual < .005f ? new Color(36, 140, 104) : new Color(240, 106, 50); var p = contact.Target with { Z = 6.9f };
            _proofGround.Line(p - new Vector3(8 * pixel, 0, 0), p + new Vector3(8 * pixel, 0, 0), 2 * pixel, color);
            _proofGround.Line(p - new Vector3(0, 8 * pixel, 0), p + new Vector3(0, 8 * pixel, 0), 2 * pixel, color);
        }
    }

    private void WriteMotionProofReport()
    {
        var catalog = ProofCatalog();
        var lines = new List<string> { "Panels, left to right: " + string.Join(", ", ProofSubjects) + $". Close-ups at {ProofPpu} px/unit; bottom band at game scale, {GamePpu} px/unit, fixed camera." };
        foreach (var clipId in ProofClips)
            foreach (var subject in ProofSubjects)
            {
                var model = catalog.Resolve(subject); var clip = catalog.Animations[clipId];
                var poses = Enumerable.Range(0, 481).Select(i => PoseEvaluator.Sample(model, clip, i * clip.Duration / 240.0, repeat: true)).ToArray();
                var stride = PoseEvaluator.Sample(model, clip, clip.Duration, repeat: true).Locomotion.X - poses[0].Locomotion.X;
                var contact = poses.SelectMany(p => p.Contacts).Select(c => c.Residual).DefaultIfEmpty().Max();
                var reach = poses.SelectMany(p => p.Chains).Max(c => c.Residual);
                lines.Add($"{clipId} on {subject}: stride {stride:F3}, max contact residual {contact:E1}, max reach residual {reach:E1}");
            }
        File.WriteAllLines(Path.Combine(_smokePath!, "motion-proof.txt"), lines);
    }
}
```

- [ ] **Step 6: Wire the mode into `StudioGame.cs` and `Program.cs`**

In `StudioGame.cs`:
- Change the constructor signature to `public StudioGame(string assetRoot, string? smokePath, bool wolfSmokeOnly = false, bool workshop = false, bool workshopSmoke = false, bool motionSmoke = false)`.
- Replace `_workshopActive = workshop || workshopSmoke || _libraries.Length == 0; _workshopSmoke = workshopSmoke;` with `_workshopActive = workshop || workshopSmoke || motionSmoke || _libraries.Length == 0; _workshopSmoke = workshopSmoke; _motionSmoke = motionSmoke;`.
- In `Draw`, replace `if (!(_workshopSmoke ? PrepareWorkshopSmoke() : PrepareSmokeFrame())) { Exit(); return; }` with `if (!(_motionSmoke ? PrepareMotionProof() : _workshopSmoke ? PrepareWorkshopSmoke() : PrepareSmokeFrame())) { Exit(); return; }`.
- In `Draw`, replace `if (_workshopSmoke) CaptureWorkshopSmoke(); else CaptureSmokeFrame();` with `if (_workshopSmoke) CaptureWorkshopSmoke(); else if (!_motionSmoke) CaptureSmokeFrame();`.
- In `Dispose`, add `_proofTarget?.Dispose();` after `_puppetTarget?.Dispose();`.

In `Program.cs`:
- Extend the argument check pattern `"--smoke" or "--smoke-wolf" or "--smoke-workshop"` to include `or "--smoke-motion"`, and add `--smoke-motion output-directory` to the usage string.
- Pass `motionSmoke: args is ["--smoke-motion", _]` as the last constructor argument.

- [ ] **Step 7: Build and run the proof**

Set `OUT` to an absolute output directory outside the repo, for example `<session scratchpad>/motion-proof`, then run:

```bash
dotnet build App2d.CharacterStudio -v q -nologo && dotnet run --project App2d.CharacterStudio --no-build -- --smoke-motion "$OUT" ; ls "$OUT" | wc -l ; cat "$OUT/motion-proof.txt"
```

Expected: exit 0, 49 files (48 PNGs plus the report). Walk strides are about 0.500 / 0.600 / 0.410 and run strides about 1.600 / 1.920 / 1.312. Contact residuals print about `0.0E+000`.

- [ ] **Step 8: Review the images (human gate)**

Open `person-walk-00.png`, `-06`, `-12`, `-18` and `person-run-00.png`, `-06`, `-12`, `-18`. Check:
- The planted foot's contact cross stays green and the foot sits on it.
- No knee pops or snaps, and the swing knee is visibly bent.
- The tall/thin build reads taller and thinner, and short/broad reads shorter and wider, while the performance is recognizably the same walk and run.
- In the bottom band at game scale, all three are distinguishable and the gait reads.

Send the eight images to your human partner. The doc requires review at intended game scale, so this gate is theirs to pass. Record their verdict in the Task 9 doc update.

- [ ] **Step 9: Run the whole suite and commit**

Run: `dotnet test App2d.Tests/App2d.Tests.csproj`
Expected: PASS, at least 243 plus the new tests.

```bash
git add App2d.Rendering/Characters/PuppetDrawing.cs App2d.CharacterStudio App2d.Tests/Authored/ModelDrawingTests.cs
git commit -m "Render shared walk and run on three builds for review

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Record the checkpoint in the design doc

**Files:**
- Modify: `docs/character-editor-replacement.md` (under `### 1. Prove shared motion before replacing the UI`)

- [ ] **Step 1: Add a progress note**

Append after the section's "This is the schema checkpoint." paragraph. Fill in the reviewer's verdict from Task 8, Step 8:

```markdown
**Progress (2026-09-25).** Implemented in `App2d.Core/Characters/Authored/`: `CharacterModel`, `ModelVariant`,
`ResolvedModel`, `MotionClip`, `PoseEvaluator`, `AuthoredCatalog`, plus `PersonTemplate` and `PersonBuild`. Assets
live in `Assets/Characters/authored/`; regenerate them from the prototype studies with
`App2d.CharacterStudio --convert-studies Assets/Characters/authored`. Render the comparison with
`App2d.CharacterStudio --smoke-motion <dir>`. `App2d.Tests.Authored` verifies exact reproduction against the prototype
(≤1e-4), preserved limb lengths and joint angles, planted contacts, stride scaled by leg length and continuous seams
on all three builds. Visual review: <verdict>. Not yet covered: face/appearance channels, rotation keys in authored
clips (supported and unit-tested, unused by the converted studies), and build values beyond the Person template.
```

- [ ] **Step 2: Commit**

```bash
git add docs/character-editor-replacement.md
git commit -m "Record shared-motion checkpoint results in the replacement design

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
