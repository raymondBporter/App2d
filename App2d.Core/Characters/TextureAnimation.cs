using App2d.Core.Animation;

namespace App2d.Core.Characters;

/// <summary>Normalized image coordinates, measured from the top left; independent of the rectangle's world size.</summary>
public sealed record TextureRegion
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; } = 1;
    public float Height { get; init; } = 1;

    public void Validate(string field)
    {
        new Limit(0, 1).Check(X, field + ".x");
        new Limit(0, 1).Check(Y, field + ".y");
        new Limit(.00001f, 1).Check(Width, field + ".width");
        new Limit(.00001f, 1).Check(Height, field + ".height");
        if (X + Width > 1 || Y + Height > 1) throw new InvalidDataException(field + ": region extends outside the image.");
    }
}

public sealed record TextureFrame
{
    public string Texture { get; init; } = "";
    public float Seconds { get; init; } = .1f;
    public TextureRegion? Source { get; init; }
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
            frame.Source?.Validate("texture frame source");
        }
        return new(Frames, Frames.Select(f => f.Seconds), Loop);
    }
}
