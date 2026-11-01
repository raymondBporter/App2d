using App2d.Core.Characters.Drawing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Rendering.Characters;

/// <summary>Draws a <see cref="PuppetDrawing"/>'s batches, loading their images; <see cref="DrawActor"/> owns an actor's depth layer.</summary>
public sealed class PointCharacterRenderer(GraphicsDevice device) : LayeredMeshRenderer(device)
{
    private readonly Dictionary<string, Texture2D> _images = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Draws one actor layered against itself only: a new depth layer, the swoosh (if any) behind, then the puppet.</summary>
    public void DrawActor(PuppetDrawing drawing, Matrix projection, Matrix world, LayeredMesh? swoosh = null)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ClearDepth();
        if (swoosh is { Count: > 0 })
        {
            Draw(swoosh, projection, world);
        }

        Draw(drawing, projection, world);
    }

    public void Draw(PuppetDrawing drawing, Matrix projection, Matrix world)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        ArgumentNullException.ThrowIfNull(drawing);
        foreach (var batch in drawing.Batches)
        {
            Texture2D? texture = null;
            if (batch.Texture is { } path)
            {
                var full = IO.FilePaths.ResolveUnderRoot(drawing.TextureRoot, path);
                if (!_images.TryGetValue(full, out texture))
                {
                    using var decoded = Textures.Texture2D.Load(full);
                    texture = new(Device, decoded.Width, decoded.Height);
                    try
                    {
                        texture.SetData(decoded.CopyPixels());
                    }
                    catch
                    {
                        texture.Dispose();
                        throw;
                    }
                    _images.Add(full, texture);
                }
            }
            Draw(drawing.Mesh, projection, world, texture, writeDepth: batch.WriteDepth, start: batch.Start, count: batch.Count, additive: batch.Blend == "additive");
        }
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var texture in _images.Values)
        {
            texture.Dispose();
        }

        base.Dispose(disposing);
    }
}
