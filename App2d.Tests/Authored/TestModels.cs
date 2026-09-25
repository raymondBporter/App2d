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

    /// <summary>An empty one-second looping clip authored against this model's own measures, so every ratio is 1.</summary>
    public static MotionClip Clip(ResolvedModel model) => new()
    {
        Id = "test-clip", Name = "Test", Model = model.Base.Id, Loop = true,
        Reference = model.Measures.ToDictionary(p => p.Key, p => p.Value),
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
