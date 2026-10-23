using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Rendering;

public sealed partial class Renderer2D
{
    public void Draw(ParallaxBackground2D background)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(background);
        foreach (var layer in background.GetDrawOrder())
        {
            Draw(layer);
        }
    }

    public void Draw(ParallaxLayer2D layer)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(layer);
        var gameCamera = _camera;
        var gameBounds = _visibleWorldBounds;
        var view = layer.GetView(gameCamera);
        var position = view.Position;
        var visible = view.VisibleWorldBounds;
        try
        {
            _camera = view;
            _visibleWorldBounds = visible;
            foreach (var item in layer.Scene.GetDrawOrder())
            {
                if (!item.IsVisible)
                {
                    continue;
                }

                if (layer.RepeatWidth is not { } width)
                {
                    Draw(item);
                    continue;
                }

                var bounds = item.WorldBounds;
                StateGuard.ThrowIfNot(bounds.IsFinite, "Horizontally repeated parallax objects need finite bounds.");
                var first = Math.Ceiling(((double)visible.Left - bounds.Right) / width);
                var last = Math.Floor(((double)visible.Right - bounds.Left) / width);
                for (var repeat = first; repeat <= last; repeat++)
                {
                    // Moving only the view draws every visible copy, including when zoomed out.
                    view.Position = position - new Vector2((float)(repeat * width), 0f);
                    _visibleWorldBounds = view.VisibleWorldBounds;
                    Draw(item);
                }

                view.Position = position;
                _visibleWorldBounds = visible;
            }
        }
        finally
        {
            _camera = gameCamera;
            _visibleWorldBounds = gameBounds;
        }
    }
}
