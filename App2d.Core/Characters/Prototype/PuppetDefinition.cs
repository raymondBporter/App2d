using App2d.Core.IO;
using App2d.Core.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.Core.Characters;

public sealed record PuppetControl
{
    public string Id { get; set; } = "point";
    public PuppetPoint Rest { get; set; }
}

public sealed record PuppetBone
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed record PuppetChain
{
    public string Root { get; set; } = "";
    public string Joint { get; set; } = "";
    public string End { get; set; } = "";
    public int Bend { get; set; } = 1;
}

public sealed record PuppetKey
{
    public float Time { get; set; }
    public PuppetPoint Position { get; set; }
    public Dictionary<string, PuppetPoint> Points { get; set; } = [];
}

/// <summary>A world-space target during a closed contact interval. Looping advances the target by the clip's travel.</summary>
public sealed record PuppetContact
{
    public string End { get; set; } = "";
    public float Start { get; set; }
    public float Finish { get; set; } = .5f;
    public PuppetPoint Target { get; set; }
}

public sealed record PuppetMotion
{
    public string Name { get; set; } = "New motion";
    public float Duration { get; set; } = 1;
    public bool Loop { get; set; }
    public List<PuppetKey> Keys { get; set; } = [];
    public List<PuppetContact> Contacts { get; set; } = [];
}

public sealed class PuppetDefinition
{
    public const string FormatId = "app2d-puppet";
    /// <summary>The current prototype puppet schema; other versions are refused.</summary>
    public const int CurrentVersion = 6;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = "New character";
    public List<PuppetControl> Controls { get; set; } = [];
    public List<PuppetBone> Bones { get; set; } = [];
    public List<PuppetChain> Chains { get; set; } = [];
    public List<PartGeometry> Parts { get; set; } = [];
    public List<PuppetMotion> Motions { get; set; } = [new()];
    public static JsonSerializerOptions JsonOptions => JsonProfiles.Puppet;
}
