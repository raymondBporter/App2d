using App2d.Core.Rendering;
using App2d.Core.Shapes;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Trials;

public sealed partial class TrialsGame2D
{
    private void CreateBackground()
    {
        var clouds = new ParallaxLayer2D(depth: 12f) { RepeatWidth = 1_800f };
        var mountains = new ParallaxLayer2D(depth: 4f) { RepeatWidth = 1_800f };
        var hills = new ParallaxLayer2D(depth: 1f) { RepeatWidth = 1_800f };
        var cloudShader = new SolidColorShader(new XnaColor(109, 129, 157, 100));
        var mountainShader = new SolidColorShader(new XnaColor(40, 57, 76));
        var hillShader = new SolidColorShader(new XnaColor(48, 78, 78));

        for (var i = 0; i < 5; i++)
        {
            var cloud = new WorldObject2D(Circle2D.Builder.Create(22f + i % 2 * 8f), cloudShader);
            cloud.Transform.Position = new Vector2(-720f + i * 360f, 210f + i % 3 * 25f);
            cloud.Transform.Scale = new Vector2(2.8f, 1f);
            clouds.Scene.Add(cloud);
        }

        for (var i = 0; i < 3; i++)
        {
            var mountain = new WorldObject2D(ConvexPolygon2D.Builder.Create([new Vector2(-360f, -60f), new Vector2(-50f, 180f + i * 35f), new Vector2(360f, -60f)]), mountainShader);
            mountain.Transform.Position = new Vector2(-600f + i * 600f, 0f);
            mountains.Scene.Add(mountain);

            var hill = new WorldObject2D(Circle2D.Builder.Create(190f), hillShader);
            hill.Transform.Position = new Vector2(-600f + i * 600f, -130f);
            hill.Transform.Scale = new Vector2(2f, 1f);
            hills.Scene.Add(hill);
        }

        Background.Add(clouds);
        Background.Add(mountains);
        Background.Add(hills);
    }
}
