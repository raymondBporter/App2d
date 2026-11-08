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
        Ensure.ThrowIfNull(background);
        foreach (var layer in background.GetDrawOrder())
        {
            Draw(layer);
        }
    }

    public void Draw(ParallaxLayer2D layer)
    {
        RequireFrame();
        Ensure.ThrowIfNull(layer);
        var gameView = View;
        var view = layer.GetView(_camera);
        var position = view.Position;
        var layerView = view.GetFrameView();
        var visible = layerView.VisibleWorldBounds;
        try
        {
            View = layerView;
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
                Ensure.Finite(bounds.Min);
                Ensure.Finite(bounds.Max);
                var first = Math.Ceiling(((double)visible.Left - bounds.Right) / width);
                var last = Math.Floor(((double)visible.Right - bounds.Left) / width);
                for (var repeat = first; repeat <= last; repeat++)
                {
                    // Moving only the view draws every visible copy, including when zoomed out.
                    view.Position = position - new Vector2((float)(repeat * width), 0f);
                    View = view.GetFrameView();
                    Draw(item);
                }

                view.Position = position;
                View = layerView;
            }
        }
        finally
        {
            View = gameView;
        }
    }
}
