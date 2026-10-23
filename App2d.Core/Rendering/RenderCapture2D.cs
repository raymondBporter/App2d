using App2d.Core.Validation;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Rendering;

/// <summary>
/// Offscreen captures for proofs, smokes and studies: the standard review target, a scoped binding that restores the
/// previous targets, and PNG output.
/// </summary>
public static class RenderCapture2D
{
    /// <summary>Creates a colour target with a 24-bit depth buffer and 4x multisampling whose contents are discarded between binds.</summary>
    public static RenderTarget2D CreateTarget(GraphicsDevice device, int width, int height, DepthFormat depthFormat = DepthFormat.Depth24, int multiSampleCount = 4)
    {
        ArgGuard.ThrowIfNull(device);
        ArgGuard.Positive(width);
        ArgGuard.Positive(height);
        ArgGuard.NonNegative(multiSampleCount);
        return new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, depthFormat, multiSampleCount, RenderTargetUsage.DiscardContents);
    }

    /// <summary>Binds the target, optionally clearing it, until the scope is disposed; disposal restores the targets bound before.</summary>
    public static Scope UseRenderTarget(this GraphicsDevice device, RenderTarget2D target, Color4b? clearColor = null)
    {
        ArgGuard.ThrowIfNull(device);
        ArgGuard.ThrowIfNull(target);
        var previous = device.GetRenderTargets();
        device.SetRenderTarget(target);
        if (clearColor is { } color)
        {
            device.Clear(color);
        }

        return new Scope(device, previous);
    }

    /// <summary>Writes the target as a PNG at its own size. A bound target is unbound first so multisampled content resolves.</summary>
    public static void SavePng(this RenderTarget2D target, string path)
    {
        ArgGuard.ThrowIfNull(target);
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        var device = target.GraphicsDevice;
        foreach (var binding in device.GetRenderTargets())
        {
            if (binding.RenderTarget == target)
            {
                device.SetRenderTarget(null);
                break;
            }
        }

        using var stream = File.Create(path);
        target.SaveAsPng(stream, target.Width, target.Height);
    }

    /// <summary>Clears the target, runs the draw calls into it, restores the previous targets and writes the PNG.</summary>
    public static void SavePng(this RenderTarget2D target, string path, Color4b clearColor, Action draw)
    {
        ArgGuard.ThrowIfNull(target);
        ArgGuard.ThrowIfNull(draw);
        using (target.GraphicsDevice.UseRenderTarget(target, clearColor))
        {
            draw();
        }

        target.SavePng(path);
    }

    /// <summary>Draws into a new standard target of the given size and writes it as a PNG.</summary>
    public static void SavePng(GraphicsDevice device, int width, int height, string path, Color4b clearColor, Action draw)
    {
        using var target = CreateTarget(device, width, height);
        target.SavePng(path, clearColor, draw);
    }

    /// <summary>Restores the render targets that were bound before <see cref="UseRenderTarget"/>.</summary>
    public sealed class Scope(GraphicsDevice device, RenderTargetBinding[] previous) : IDisposable
    {
        public void Dispose() => device.SetRenderTargets(previous);
    }
}
