using App2d.Core.Geometry.Shapes;
using App2d.Core.Rendering;

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
            var cloud = new WorldObject2D(Capsule2.Builder.Create(
                new Vector2f(-width / 2f, 0f), new Vector2f(width / 2f, 0f), 38f), cloudShader)
            {
                Position = new Vector2f(-850f + i * 470f, 210f + i % 3 * 95f)
            };
            clouds.Scene.Add(cloud);
        }

        for (var i = 0; i < 12; i++)
        {
            var width = 520f + i % 3 * 90f;
            var height = 360f + i % 4 * 55f;
            var mountain = new WorldObject2D(ConvexPolygon2D.Builder.Create(
            [
                new Vector2f(-width / 2f, 0f),
                new Vector2f(0f, height),
                new Vector2f(width / 2f, 0f)
            ]), mountainShader)
            {
                Position = new Vector2f(-1_100f + i * 500f, -520f)
            };
            mountains.Scene.Add(mountain);
        }

        for (var i = 0; i < 13; i++)
        {
            var radius = 185f + i % 3 * 24f;
            var hill = new WorldObject2D(Ellipse2D.Builder.Create(new Vector2f(radius * 1.9f, radius)), hillShader)
            {
                Position = new Vector2f(-1_000f + i * 510f, -525f)
            };
            hills.Scene.Add(hill);
        }

        background.Add(clouds);
        background.Add(mountains);
        background.Add(hills);
    }
}
