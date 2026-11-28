using App2d.Core.Geometry.Shapes;
using App2d.Core.Rendering;

namespace App2d.Presentation.World;

internal static class CampaignBackground2D
{
    public static void AddTo(ParallaxBackground2D background)
    {
        var sky = new ParallaxLayer2D(18f);
        sky.Scene.Add(new WorldObject2D(Rectangle2D.Builder.Create(new(-4500f, -510f), new(4500f, 500f)),
            new LinearGradientShader(new(63, 113, 130), new(246, 216, 153))));
        AddSunDisk(84f, new(255, 216, 143, 44));
        AddSunDisk(60f, new(255, 226, 161, 80));
        AddSunDisk(47f, new(255, 237, 181));
        background.Add(sky);

        AddRidge(8f, 125f, 77f, .006f, .4f, new(113, 152, 145), new(184, 185, 144));
        AddRidge(3.7f, 38f, 61f, .008f, 1.9f, new(79, 128, 124), new(143, 165, 127));
        AddRidge(1.75f, -63f, 49f, .010f, 4.6f, new(54, 100, 95), new(93, 137, 109));

        void AddSunDisk(float radius, Color4b color)
        {
            sky.Scene.Add(new WorldObject2D(Circle2D.Builder.Create(radius), new SolidColorShader(color))
            {
                Position = new(380f, 197f),
            });
        }

        void AddRidge(float depth, float elevation, float amplitude, float frequency, float phase,
            Color4b crest, Color4b baseColor)
        {
            var layer = new ParallaxLayer2D(depth);
            Span<Vector2f> ridge = stackalloc Vector2f[132];
            for (var i = 0; i < ridge.Length; i++)
            {
                var x = -2300f + i * 57f;
                var angle = x * frequency + phase;
                var contour = .63f * MathF.Sin(angle) + .28f * MathF.Sin(angle * 2.24f) + .09f * MathF.Sin(angle * 6.62f);
                ridge[i] = new(x, elevation + amplitude * contour);
            }

            var shape = SimplePolygon2D.Builder.BelowPolyline(ridge, -650f);
            layer.Scene.Add(new WorldObject2D(shape, new LinearGradientShader(crest, baseColor)));
            background.Add(layer);
        }
    }
}
