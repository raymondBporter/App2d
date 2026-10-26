using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;

namespace App2d.Tests.Authored;

/// <summary>P6b of the character-system migration: the explicit, verified, rerunnable conversion of legacy variant files to typed overrides.</summary>
public sealed class VariantConversionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "variant-conversion-" + Guid.NewGuid().ToString("N"));
    private string Output => Path.Combine(_root, "scratch");

    public VariantConversionTests()
    {
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_root, "authored", Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    private string Authored => Path.Combine(_root, "authored");
    private static readonly string[] Family = ["tall-thin", "short-broad", "cinder", "brute"];
    private static (string Id, float ScaleX, float ScaleY, float OffsetX, float OffsetY, bool Hidden, string Material) Appearance(PuppetPart p) =>
        (p.Id, p.ScaleX, p.ScaleY, p.OffsetX, p.OffsetY, p.Hidden, System.Text.Json.JsonSerializer.Serialize(p.RenderMaterial, AuthoredJson.Options));

    /// <summary>A version-4 file of the shipped shape, with rest pins, for the conversion to prove the pin path.</summary>
    private static string LegacyWithPins(string id) =>
        "{\"format\":\"app2d-variant\",\"version\":4,\"id\":\"" + id + "\",\"name\":\"Pinned\",\"base\":\"person\"," +
        "\"build\":{\"legs\":1.1,\"arms\":1.05}," +
        "\"rest\":{\"chest\":{\"x\":0.02,\"y\":1.3,\"z\":0.0},\"head\":{\"x\":0.05,\"y\":1.62,\"z\":0.0},\"right-hip\":{\"x\":0.08,\"y\":0.82,\"z\":0.12}}," +
        "\"parts\":{\"body\":{\"material\":{\"fill\":\"#abcdef\"},\"scaleY\":1.1},\"head\":{\"hidden\":false}}}";

    /// <summary>The version-4 file a shipped (converted) family variant came from: the same values in the legacy per-field dictionaries.</summary>
    private static string LegacyOf(ModelVariant converted)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(converted.ToJson())!.AsObject();
        root["version"] = 4;
        root.Remove("overrides");
        var parts = new System.Text.Json.Nodes.JsonObject();
        foreach (var change in converted.Overrides)
        {
            // The face instance's expression (P7c) was the head part's face word in the legacy form.
            if (change.Target.Attachment == PersonTemplate.FaceInstance && change.Target.Property == RigProperties.Expression)
            {
                var head = parts[Humanoid.Head] as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
                head["face"] = change.Selection;
                parts[Humanoid.Head] = head;
                continue;
            }

            Assert.NotNull(change.Target.Part);
            var part = parts[change.Target.Part!] as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
            part[change.Target.Property] = change.Material is { } material ? System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(material, AuthoredJson.Options))
                : change.Selection is { } selection ? selection : change.Flag is { } flag ? flag : change.Value!.Value;
            parts[change.Target.Part!] = part;
        }
        root["rest"] = new System.Text.Json.Nodes.JsonObject();
        root["parts"] = parts;
        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    [Fact]
    public void TheShippedPersonFamilyConvertsOntoTypedOverridesWithoutMovingAnythingAndRerunsUnchanged()
    {
        // The shipped family is already converted (P6b); rebuild its version-4 inputs and prove the conversion reproduces the shipped files.
        var shipped = Family.ToDictionary(id => id, id => File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "variants", id + ".json")));
        foreach (var (id, json) in shipped)
        {
            Assert.Null(LegacyVariantReader.LegacyVersion(json));
            File.WriteAllText(Path.Combine(Authored, "variants", id + ".json"), LegacyOf(ModelVariant.FromJson(json)));
        }
        // The rebuilt legacy files carry the face words the catalog refuses by name since P7c; the rest baseline is the shipped family's.
        var catalog = AuthoredCatalog.Load(Authored);
        Assert.Contains(catalog.Errors, e => e.Contains("convert the bundle"));
        var shippedCatalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var before = Family.ToDictionary(id => id, id => shippedCatalog.Resolve(id));
        var legacyFiles = Family.ToDictionary(id => id, id => File.ReadAllText(Path.Combine(Authored, "variants", id + ".json")));
        Assert.All(legacyFiles.Values, json => Assert.Equal(4, LegacyVariantReader.LegacyVersion(json)));
        var faceInstances = new Dictionary<string, string> { [Humanoid.Head] = PersonTemplate.FaceInstance };
        var results = VariantConversion.ConvertFamily(Authored, "person", Family, Output, faceInstances: faceInstances);
        Assert.All(results, r => Assert.True(r.Converted, $"{r.Id}: {r.Status} {string.Join("; ", r.Differences)}"));
        Assert.True(File.Exists(Path.Combine(Output, "variant-conversion-report.md")));
        var report = File.ReadAllText(Path.Combine(Output, "variant-conversion-report.md"));
        Assert.Contains("brute | converted", report);
        Assert.Contains("part 'body' material -> material", report);
        // The source tree is untouched; the outputs are version 6 and resolve identically through the real catalog.
        foreach (var (id, json) in legacyFiles)
        {
            Assert.Equal(json, File.ReadAllText(Path.Combine(Authored, "variants", id + ".json")));
            Assert.Equal(shipped[id], File.ReadAllText(Path.Combine(Output, "variants", id + ".json")));
            var converted = ModelVariant.FromJson(File.ReadAllText(Path.Combine(Output, "variants", id + ".json")));
            Assert.Equal(ModelVariant.CurrentVersion, converted.Version);
            Assert.Null(LegacyVariantReader.LegacyVersion(File.ReadAllText(Path.Combine(Output, "variants", id + ".json"))));
            var after = ResolvedModel.From(catalog.Models["person"], converted, catalog.Parts.GetValueOrDefault);
            foreach (var control in after.Order)
            {
                TestModels.Near(before[id].Rest[control.Id], after.Rest[control.Id], 1e-6f, id + "/" + control.Id);
            }

            Assert.Equal(before[id].Parts.Select(Appearance), after.Parts.Select(Appearance));
            Assert.Equal(before[id].ParameterValues, after.ParameterValues);
        }
        Assert.Equal(7, ModelVariant.FromJson(File.ReadAllText(Path.Combine(Output, "variants", "brute.json"))).Overrides.Count); // six materials and the angry face
        Assert.Single(ModelVariant.FromJson(File.ReadAllText(Path.Combine(Output, "variants", "tall-thin.json"))).Overrides);

        // Rerunning writes nothing new; an output edited afterwards is kept, not overwritten.
        var rerun = VariantConversion.ConvertFamily(Authored, "person", Family, Output, faceInstances: faceInstances);
        Assert.All(rerun, r => Assert.StartsWith("unchanged", r.Status));
        var edited = ModelVariant.FromJson(File.ReadAllText(Path.Combine(Output, "variants", "tall-thin.json")));
        edited.Set(PropertyOverride.Of(PropertyRef.OfControl(Humanoid.LeftElbow, RigProperties.Length), .5f));
        edited.Save(Path.Combine(Output, "variants", "tall-thin.json"));
        var kept = VariantConversion.ConvertFamily(Authored, "person", ["tall-thin"], Output).Single();
        Assert.StartsWith("kept", kept.Status);
        Assert.Equal(.5f, ModelVariant.FromJson(File.ReadAllText(Path.Combine(Output, "variants", "tall-thin.json"))).Override(PropertyRef.OfControl(Humanoid.LeftElbow, RigProperties.Length))?.Value);
        // A source already at the current version is reported, not converted.
        File.Copy(Path.Combine(Output, "variants", "brute.json"), Path.Combine(Authored, "variants", "brute.json"), overwrite: true);
        Assert.StartsWith("already at version", VariantConversion.ConvertFamily(Authored, "person", ["brute"], Output).Single().Status);
        Assert.StartsWith("missing", VariantConversion.ConvertFamily(Authored, "person", ["nobody"], Output).Single().Status);
    }

    [Fact]
    public void LegacyRestPinsBecomeSetupOverridesThatLandWhereThePinsSaidAndLeaveTheRestAlone()
    {
        File.WriteAllText(Path.Combine(Authored, "variants", "pinned.json"), LegacyWithPins("pinned"));
        var catalog = AuthoredCatalog.Load(Authored);
        Assert.Contains(catalog.Errors, e => e.Contains("VariantConversion"));
        var result = VariantConversion.ConvertFamily(Authored, "person", ["pinned"], Output).Single();
        Assert.True(result.Converted, result.Status + " " + string.Join("; ", result.Differences));
        Assert.Contains(result.Report, line => line.Contains("rest: 3 pin(s)"));
        var converted = ModelVariant.FromJson(File.ReadAllText(Path.Combine(Output, "variants", "pinned.json")));
        var person = catalog.Models["person"];
        var resolved = ResolvedModel.From(person, converted, catalog.Parts.GetValueOrDefault);
        TestModels.Near(new(.02f, 1.3f, 0), resolved.Rest[Humanoid.Chest], 1e-5f);
        TestModels.Near(new(.05f, 1.62f, 0), resolved.Rest[Humanoid.Head], 1e-5f);
        TestModels.Near(new(.08f, .82f, .12f), resolved.Rest[Humanoid.RightHip], 1e-5f);
        // Unpinned controls outside the pinned subtrees keep the parameter-built positions; the shoulders ride on the pinned chest.
        var unpinned = new ModelVariant { Id = "unpinned", Name = "Unpinned", Base = "person", Parameters = { ["legs"] = 1.1f, ["arms"] = 1.05f } };
        var built = ResolvedModel.From(person, unpinned, catalog.Parts.GetValueOrDefault);
        TestModels.Near(built.Rest[Humanoid.Hips], resolved.Rest[Humanoid.Hips], 1e-5f);
        TestModels.Near(built.Rest[Humanoid.LeftFoot], resolved.Rest[Humanoid.LeftFoot], 1e-5f);
        TestModels.Near(built.Rest[Humanoid.LeftShoulder] + (resolved.Rest[Humanoid.Chest] - built.Rest[Humanoid.Chest]), resolved.Rest[Humanoid.LeftShoulder], 1e-5f);
        Assert.False(resolved.Parts.Single(p => p.Id == Humanoid.Head).Hidden);
        Assert.Equal(1.1f, resolved.Parts.Single(p => p.Id == Humanoid.Body).ScaleY);
        Assert.True(converted.HasOverride(PropertyRef.OfControl(Humanoid.Chest, RigProperties.LocalY)));
        Assert.True(converted.HasOverride(PropertyRef.OfControl(Humanoid.RightHip, RigProperties.LocalLayer)));
        Assert.DoesNotContain("\"rest\"", File.ReadAllText(Path.Combine(Output, "variants", "pinned.json")));
    }

    [Fact]
    public void TheLastLegacyFilesOutsideTheFamilyConvertedWithTheFaceBundleAndTheReaderHasNoShippedCaller()
    {
        // P7c converted club-caveman-build, rock-thrower-build and shield-defender-build to version 6 with the Person face bundle.
        var catalog = AuthoredCatalog.Load(Authored);
        foreach (var id in new[] { "club-caveman-build", "rock-thrower-build", "shield-defender-build" })
        {
            Assert.Null(LegacyVariantReader.LegacyVersion(File.ReadAllText(Path.Combine(Authored, "variants", id + ".json"))));
            Assert.Equal(ModelVariant.CurrentVersion, catalog.Variants[id].Version);
            Assert.Equal(7, catalog.Variants[id].Overrides.Count);
            Assert.Equal("smug", catalog.Variants[id].Override(PropertyRef.OfAttachment(PersonTemplate.FaceInstance, RigProperties.Expression))!.Selection);
            Assert.NotNull(catalog.Resolve(id));
        }
        Assert.Empty(LegacySourceFields.Remaining(Authored).Where(f => f.Kind == "variant"));
    }
}
