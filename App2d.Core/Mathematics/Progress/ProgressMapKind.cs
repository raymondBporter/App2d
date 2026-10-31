using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Stable authored names for the built-in progress maps.</summary>
[JsonConverter(typeof(ProgressMapKindJsonConverter))]
public enum ProgressMapKind
{
    Linear, Reverse, PingPong, Smooth, Smoother, Smoothest,
    EaseInQuad, EaseOutQuad, EaseInOutQuad,
    EaseInCubic, EaseOutCubic, EaseInOutCubic,
    EaseInQuart, EaseOutQuart, EaseInOutQuart,
    EaseInQuint, EaseOutQuint, EaseInOutQuint,
    EaseInSine, EaseOutSine, EaseInOutSine,
    Step, ReversePingPong
}

public sealed class ProgressMapKindJsonConverter() : JsonStringEnumConverter<ProgressMapKind>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false);
