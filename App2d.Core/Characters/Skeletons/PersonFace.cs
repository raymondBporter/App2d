using App2d.Core.Animation;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;

namespace App2d.Core.Characters;

/// <summary>The person's face, for the head mount of <see cref="PersonSkeleton"/>: eyes, brows and a mouth, and every expression they can make.</summary>
public static class PersonFace
{
    public static Face Face { get; } = new(new(0.065f, 0f), 3.7f,
        [
            new("left-pupil", new() { Fill = "#222b32" }, 0f, Blink: Track.Of<Vector2f>(new(0f, new(1f, 0.8f)), new(3.54f, new(1f, 0.8f)), new(3.62f, new(1f, 0f)), new(3.7f, new(1f, 0.8f)))),
            new("left-white", new() { Fill = "#ffffff" }, -0.002f),
            new("left-dot", new() { Fill = "#222b32" }, -0.004f),
            new("left-lid", new() { Fill = "#222b32", StrokeWidth = 0.027f }),
            new("left-cross", new() { Fill = "#222b32" }),
            new("left-brow", new() { Fill = "#222b32", StrokeWidth = 0.0324f }),
            new("right-pupil", new() { Fill = "#222b32" }, 0f, Blink: Track.Of<Vector2f>(new(0f, new(1f, 0.8f)), new(3.54f, new(1f, 0.8f)), new(3.62f, new(1f, 0f)), new(3.7f, new(1f, 0.8f)))),
            new("right-white", new() { Fill = "#ffffff" }, -0.002f),
            new("right-dot", new() { Fill = "#222b32" }, -0.004f),
            new("right-lid", new() { Fill = "#222b32", StrokeWidth = 0.027f }),
            new("right-cross", new() { Fill = "#222b32" }),
            new("right-brow", new() { Fill = "#222b32", StrokeWidth = 0.0324f }),
            new("mouth", new() { Fill = "#222b32", StrokeWidth = 0.0311f }),
            new("mouth-open", new() { Fill = "#222b32" }),
        ],
        [
            new("relaxed", true,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0252f, 0.0377f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0252f, 0.0377f)),
                ("mouth", Curve(-0.047f, -0.116f, 0.065f, -0.116f, 0.177f, -0.116f))),
            new("happy", false,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0252f, 0.0377f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0252f, 0.0377f)),
                ("mouth", Curve(-0.047f, -0.116f, 0.065f, -0.1876f, 0.177f, -0.116f))),
            new("focused", true,
                ("left-pupil", Ellipse(-0.0442f, 0.0696f, 0.0252f, 0.0377f)),
                ("right-pupil", Ellipse(0.1854f, 0.0696f, 0.0252f, 0.0377f)),
                ("mouth", Curve(-0.047f, -0.116f, 0.065f, -0.0885f, 0.177f, -0.116f))),
            new("determined", false,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0252f, 0.0113f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0252f, 0.0113f)),
                ("mouth", Curve(-0.0302f, -0.116f, 0.065f, -0.0885f, 0.1602f, -0.116f))),
            new("angry", false,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0252f, 0.0264f)),
                ("left-brow", Line(-0.103f, 0.2059f, 0.0034f, 0.1305f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0252f, 0.0264f)),
                ("right-brow", Line(0.1266f, 0.1305f, 0.233f, 0.2059f)),
                ("mouth", Curve(-0.047f, -0.116f, 0.065f, -0.0168f, 0.177f, -0.116f))),
            new("worried", true,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0252f, 0.0377f)),
                ("left-brow", Line(-0.103f, 0.1488f, 0.0034f, 0.2016f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0252f, 0.0377f)),
                ("right-brow", Line(0.1266f, 0.2016f, 0.233f, 0.1488f)),
                ("mouth", Curve(-0.019f, -0.116f, 0.065f, -0.0168f, 0.149f, -0.116f))),
            new("surprised", false,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0493f, 0.0822f)),
                ("left-white", Ellipse(-0.0498f, 0.0696f, 0.0379f, 0.0691f)),
                ("left-dot", Ellipse(-0.0498f, 0.0696f, 0.0145f, 0.015f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0493f, 0.0822f)),
                ("right-white", Ellipse(0.1798f, 0.0696f, 0.0379f, 0.0691f)),
                ("right-dot", Ellipse(0.1798f, 0.0696f, 0.0145f, 0.015f)),
                ("mouth-open", Ellipse(0.065f, -0.1218f, 0.0616f, 0.0597f))),
            new("panic", false,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0493f, 0.1005f)),
                ("left-white", Ellipse(-0.0498f, 0.0696f, 0.0379f, 0.0844f)),
                ("left-dot", Ellipse(-0.0498f, 0.0696f, 0.0119f, 0.0123f)),
                ("left-brow", Line(-0.103f, 0.1828f, 0.0034f, 0.2092f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0493f, 0.1005f)),
                ("right-white", Ellipse(0.1798f, 0.0696f, 0.0379f, 0.0844f)),
                ("right-dot", Ellipse(0.1798f, 0.0696f, 0.0119f, 0.0123f)),
                ("right-brow", Line(0.1266f, 0.2092f, 0.233f, 0.1828f)),
                ("mouth-open", Ellipse(0.065f, -0.1218f, 0.1232f, 0.0899f))),
            new("hurt", false,
                ("left-lid", Curve(-0.0946f, 0.0754f, -0.0498f, 0.0754f, -0.005f, 0.0754f)),
                ("right-lid", Curve(0.135f, 0.0638f, 0.1798f, 0.0638f, 0.2246f, 0.0638f)),
                ("mouth-open", Ellipse(0.065f, -0.1218f, 0.1232f, 0.0333f))),
            new("strained", false,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0252f, 0.0132f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0252f, 0.0132f)),
                ("mouth", Curve(-0.0582f, -0.116f, 0.065f, -0.0499f, 0.1882f, -0.116f))),
            new("smug", false,
                ("left-pupil", Ellipse(-0.0428f, 0.0797f, 0.0252f, 0.0149f)),
                ("right-pupil", Ellipse(0.1868f, 0.0595f, 0.0252f, 0.0228f)),
                ("mouth", Curve(-0.047f, -0.1302f, 0.065f, -0.1766f, 0.177f, -0.1018f))),
            new("confused", false,
                ("left-pupil", Ellipse(-0.0568f, 0.0841f, 0.0252f, 0.0245f)),
                ("left-brow", Line(-0.103f, 0.1943f, 0.0034f, 0.1943f)),
                ("right-pupil", Ellipse(0.1728f, 0.0551f, 0.0252f, 0.0358f)),
                ("right-brow", Line(0.1266f, 0.1421f, 0.233f, 0.1421f)),
                ("mouth", Curve(-0.0022f, -0.1363f, 0.065f, -0.0664f, 0.1322f, -0.0957f))),
            new("delighted", false,
                ("left-lid", Curve(-0.0946f, 0.0696f, -0.0498f, 0.1479f, -0.005f, 0.0696f)),
                ("right-lid", Curve(0.135f, 0.0696f, 0.1798f, 0.1479f, 0.2246f, 0.0696f)),
                ("mouth-open", Ellipse(0.065f, -0.1218f, 0.1344f, 0.0748f))),
            new("tired", false,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0252f, 0.0113f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0252f, 0.0113f)),
                ("mouth", Curve(-0.0134f, -0.116f, 0.065f, -0.0444f, 0.1434f, -0.116f))),
            new("blink", false,
                ("left-lid", Curve(-0.0946f, 0.0696f, -0.0498f, 0.0696f, -0.005f, 0.0696f)),
                ("right-lid", Curve(0.135f, 0.0696f, 0.1798f, 0.0696f, 0.2246f, 0.0696f)),
                ("mouth", Curve(-0.047f, -0.116f, 0.065f, -0.116f, 0.177f, -0.116f))),
            new("knocked-out", false,
                ("left-cross", Polygon(new(-0.0878f, 0.0999f), new(-0.0791f, 0.109f), new(-0.0498f, 0.0786f), new(-0.0205f, 0.109f), new(-0.0118f, 0.0999f), new(-0.0411f, 0.0696f), new(-0.0118f, 0.0393f), new(-0.0205f, 0.0302f), new(-0.0498f, 0.0606f), new(-0.0791f, 0.0302f), new(-0.0878f, 0.0393f), new(-0.0585f, 0.0696f))),
                ("right-cross", Polygon(new(0.1418f, 0.0999f), new(0.1505f, 0.109f), new(0.1798f, 0.0786f), new(0.2091f, 0.109f), new(0.2178f, 0.0999f), new(0.1885f, 0.0696f), new(0.2178f, 0.0393f), new(0.2091f, 0.0302f), new(0.1798f, 0.0606f), new(0.1505f, 0.0302f), new(0.1418f, 0.0393f), new(0.1711f, 0.0696f))),
                ("mouth-open", Ellipse(0.065f, -0.1218f, 0.0728f, 0.0371f))),
            new("grumpy", false,
                ("left-pupil", Ellipse(-0.0498f, 0.0696f, 0.0252f, 0.0264f)),
                ("left-brow", Line(-0.103f, 0.2059f, 0.0034f, 0.1305f)),
                ("right-pupil", Ellipse(0.1798f, 0.0696f, 0.0252f, 0.0264f)),
                ("right-brow", Line(0.1266f, 0.1305f, 0.233f, 0.2059f)),
                ("mouth", Curve(-0.047f, -0.116f, 0.065f, -0.0168f, 0.177f, -0.116f))),
        ]);

    /// <summary>A filled ellipse: its centre, then its two radii.</summary>
    private static Ellipse2D Ellipse(float x, float y, float radiusX, float radiusY) => Ellipse2D.Builder.Create(new(radiusX, radiusY), new(x, y));

    /// <summary>A curved stroke: its start, the point it bends toward, and its end.</summary>
    private static QuadraticBezier2D Curve(float startX, float startY, float bendX, float bendY, float endX, float endY) =>
        QuadraticBezier2D.Builder.Create(new(startX, startY), new(bendX, bendY), new(endX, endY));

    /// <summary>A straight stroke from one point to another.</summary>
    private static LineSegmentCurve2D Line(float startX, float startY, float endX, float endY) => LineSegmentCurve2D.Builder.Create(new(startX, startY), new(endX, endY));

    /// <summary>A filled polygon through its corners.</summary>
    private static SimplePolygon2D Polygon(params Vector2f[] corners) => SimplePolygon2D.Builder.Create(corners);

    /// <summary>How far the face sits in front of the head it is drawn on.</summary>
    public const float Layer = -0.122f;
    /// <summary>The expression a person wears when nothing asks for another.</summary>
    public const string Default = "relaxed";
}
