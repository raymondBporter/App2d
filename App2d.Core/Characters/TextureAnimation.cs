using App2d.Core.Animation;
using App2d.Core.Shapes;

namespace App2d.Core.Characters;

public sealed record TextureFrame
{
    public string Texture { get; init; } = "";
    public float Seconds { get; init; } = .1f;
    public AxisAlignedRectangle2D? Source { get; init; }
}

/// <summary>Authored frames compiled into the ordinary frame animation. The containing pose supplies time.</summary>
public sealed record TextureAnimation
{
    public List<TextureFrame> Frames { get; init; } = [];
    public bool Loop { get; init; } = true;

    /// <summary>An empty list explicitly clears an inherited sequence.</summary>
    public AnimationClip2D<TextureFrame>? Compile()
    {
        if (Frames is null || Frames.Count > 512) throw new InvalidDataException("Texture animation allows at most 512 frames.");
        if (Frames.Count == 0) return null;
        foreach (var frame in Frames)
        {
            if (frame is null) throw new InvalidDataException("Null texture frame.");
            RenderMaterialDefinition2D.CheckTexture(frame.Texture, "texture frame");
            new Limit(.001f, 600).Check(frame.Seconds, "texture frame seconds");
            if (frame.Source is { } source) source.ValidateUvRect("texture frame source");
        }
        return new(Frames, Frames.Select(f => f.Seconds), Loop);
    }
}
