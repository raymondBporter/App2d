using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.IO;
using App2d.Core.Json;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>One cutout point or Bézier handle follows a control frame with its rest offset; a rounded path point pins to the control position.</summary>
public sealed record PartPointBinding
{
    public int Point { get; set; }
    public string Control { get; set; } = "";
    /// <summary>Optional control defining local +Y. Null follows translation only.</summary>
    public string? Toward { get; set; }
}
