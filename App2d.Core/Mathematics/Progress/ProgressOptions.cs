using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Authored curve presets. Ease direction and travel are selected separately.</summary>
[JsonConverter(typeof(EasingCurveJsonConverter))]
public enum EasingCurve
{
    Linear, Smooth, Smoother, Smoothest, Quad, Cubic, Quart, Quint, Sine, Step
}

/// <summary>Shapes the rate across a single leg of travel. InOut eases at both ends of the leg.</summary>
[JsonConverter(typeof(EasingDirectionJsonConverter))]
public enum EasingDirection
{
    In, Out, InOut
}

/// <summary>Chooses the starting endpoint and whether a normalized cycle takes one leg or a round trip.</summary>
[JsonConverter(typeof(ProgressTravelJsonConverter))]
public enum ProgressTravel
{
    Forward, Reverse, PingPong, ReversePingPong
}

public sealed class EasingCurveJsonConverter() : JsonStringEnumConverter<EasingCurve>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false);
public sealed class EasingDirectionJsonConverter() : JsonStringEnumConverter<EasingDirection>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false);
public sealed class ProgressTravelJsonConverter() : JsonStringEnumConverter<ProgressTravel>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false);
