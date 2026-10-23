using App2d.Core.Rendering;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Presentation.World;

/// <summary>The side scroller's existing scenery, grouped into independent depth layers.</summary>
public static class SideScrollerBackground2D
{
    public static void AddTo(ParallaxBackground2D background)
    {
        var clouds = new ParallaxLayer2D(depth: 12f) { RepeatWidth = 3_760f };
        var mountains = new ParallaxLayer2D(depth: 4.5f) { RepeatWidth = 6_000f };
        var hills = new ParallaxLayer2D(depth: 1.4f) { RepeatWidth = 6_630f };
        var cloudShader = new SolidColorShader(new Color4b(240, 250, 255, 205));
        var mountainShader = new SolidColorShader(new Color4b(90, 145, 177));
        var hillShader = new SolidColorShader(new Color4b(70, 128, 125));

        for (var i = 0; i < 8; i++)
        {
            var width = 150f + i % 3 * 45f;
            var cloud = new WorldObject2D(Capsule2D.Builder.Create(
                new Vector2(-width / 2f, 0f), new Vector2(width / 2f, 0f), 38f), cloudShader);
            cloud.Transform.Position = new Vector2(-850f + i * 470f, 210f + i % 3 * 95f);
            clouds.Scene.Add(cloud);
        }

        for (var i = 0; i < 12; i++)
        {
            var width = 520f + i % 3 * 90f;
            var height = 360f + i % 4 * 55f;
            var mountain = new WorldObject2D(ConvexPolygon2D.Builder.Create(
            [
                new Vector2(-width / 2f, 0f),
                new Vector2(0f, height),
                new Vector2(width / 2f, 0f)
            ]), mountainShader);
            mountain.Transform.Position = new Vector2(-1_100f + i * 500f, -520f);
            mountains.Scene.Add(mountain);
        }

        for (var i = 0; i < 13; i++)
        {
            var hill = new WorldObject2D(Circle2D.Builder.Create(185f + i % 3 * 24f), hillShader);
            hill.Transform.Position = new Vector2(-1_000f + i * 510f, -525f);
            hill.Transform.Scale = new Vector2(1.9f, 1f);
            hills.Scene.Add(hill);
        }

        background.Add(clouds);
        background.Add(mountains);
        background.Add(hills);
    }
}
