using App2d.Core.Characters;
using System.Text.Json;
using Xunit;

namespace App2d.Tests;

public sealed class CharacterAuthoringSchemaTests
{
    private static string Assets
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "Assets", "Characters");
                if (File.Exists(Path.Combine(path, "catalog.json"))) return path;
            }
            throw new DirectoryNotFoundException("Character authoring assets not found.");
        }
    }
    private static PointLibrary Person => PointLibrary.Load(Path.Combine(Assets, "person", "library.json"));
    private static EntityTypeDefinition Player => EntityTypeDefinition.Load(Path.Combine(Assets, "entities", "player.json"));

    [Fact]
    public void RangeErrorsNameTheField()
    {
        var type = Player; type.Actions["attack"].ActiveStart = 3;
        var error = Assert.Throws<InvalidDataException>(() => type.Validate(Person));
        Assert.Contains("activeStart", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("attack", error.Message);
        var head = new HeadShape { Muzzle = 9 };
        Assert.Contains("muzzle", Assert.Throws<InvalidDataException>(head.Validate).Message, StringComparison.OrdinalIgnoreCase);
        var look = new CharacterAppearance { HipWidth = -1 };
        Assert.Contains("hipWidth", Assert.Throws<InvalidDataException>(look.Validate).Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VocabularyErrorsListTheChoices()
    {
        var type = Player; type.Actions["attack"].Cue = "clang";
        var error = Assert.Throws<InvalidDataException>(() => type.Validate(Person));
        Assert.Contains("clang", error.Message);
        foreach (var cue in EntityVocabulary.SoundCues) Assert.Contains(cue, error.Message);
        foreach (var cue in EntityVocabulary.SoundCues) { type.Actions["attack"].Cue = cue; type.Validate(Person); }
    }

    [Fact]
    public void UnknownMembersAreRejectedInAuthoredFiles()
    {
        var json = File.ReadAllText(Path.Combine(Assets, "entities", "needle.json")).Replace("\"health\":", "\"helth\": 5, \"health\":");
        Assert.Contains("helth", Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EntityTypeDefinition>(json, AuthoredJson.Options)).Message);
    }

    [Fact]
    public void SavesAreSparseButKeepExplicitNonDefaultValues()
    {
        var action = new EntityAction { Clip = "walk", RemoveTravel = false };
        var json = JsonSerializer.Serialize(action, AuthoredJson.Options);
        Assert.Contains("\"clip\"", json); Assert.Contains("\"removeTravel\": false", json);
        Assert.DoesNotContain("hitWidth", json); Assert.DoesNotContain("duration", json);
        var restored = JsonSerializer.Deserialize<EntityAction>(json, AuthoredJson.Options)!;
        Assert.Equal(action, restored);
        var type = Player; var file = Path.Combine(Path.GetTempPath(), "schema-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            type.Save(file);
            var lines = File.ReadAllLines(file).Length;
            Assert.InRange(lines, 100, 200);
            var reloaded = EntityTypeDefinition.Load(file); reloaded.Validate(Person);
            Assert.Equal(JsonSerializer.Serialize(type, AuthoredJson.Options), JsonSerializer.Serialize(reloaded, AuthoredJson.Options));
            Assert.Contains("\"format\": \"app2d-entity-type\"", File.ReadAllText(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void LimitsDriveValidationAndAreSelfConsistent()
    {
        foreach (var limit in new[] { EntityAction.Limits.Duration, EntityTypeDefinition.Limits.Health, HeadShape.Limits.Width, RegionSettings.Limits.Padding, MovementShape.Limits.Width })
        {
            Assert.True(limit.Min <= limit.SoftMin && limit.SoftMin <= limit.SoftMax && limit.SoftMax <= limit.Max);
            limit.Check(limit.Min, "x"); limit.Check(limit.Max, "x");
            Assert.Throws<InvalidDataException>(() => limit.Check(limit.Max + 1, "x"));
            Assert.Throws<InvalidDataException>(() => limit.Check(float.NaN, "x"));
        }
    }

    [Fact]
    public void PersonRigIsVerifiedAgainstTheLibrary()
    {
        var library = Person;
        PersonRig.Verify(library);
        Assert.Equal("head", library.PointNames[PersonRig.Head]);
        Assert.Equal("arm_r_2", library.PointNames[PersonRig.RightHand]);
        var manifest = File.ReadAllText(Path.Combine(Assets, "person", "library.json")).Replace("\"arm_r_2\"", "\"arm_right_end\"");
        var renamed = new PointLibrary(manifest, File.ReadAllBytes(Path.Combine(Assets, "person", "points.bin")));
        Assert.Throws<InvalidDataException>(() => PersonRig.Verify(renamed));
        Assert.Throws<InvalidDataException>(() => new PersonPose(renamed));
    }
}
