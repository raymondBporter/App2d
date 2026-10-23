using App2d.Core.Characters;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Physics.Motion;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

/// <summary>Authored lifts for tests: a kinematic one-way rectangle that ping-pongs along <c>travel</c> at the August platform's pace.</summary>
internal static class ObjectPlatforms
{
    public static ObjectPlacement2D Place(string id, Vector2 position, Vector2 travel, float speed, float width = 96f, float height = 16f) =>
        new(id, Asset(id, travel, speed, width, height), position);

    public static ObjectAsset2D Asset(string id, Vector2 travel, float speed, float width = 96f, float height = 16f) =>
        new()
        {
            Id = id,
            Name = id,
            Geometry = Rectangle2D.Builder.Create(new(-width / 2f, -height / 2f), new(width / 2f, height / 2f)),
            Material = new RenderMaterialDefinition2D { Fill = "#25d2be" },
            Physics = new PhysicsBodySettings2D { MotionType = BodyMotionType2D.Kinematic, Restitution = 0f, TransfersContactMotion = true },
            Surface = new ObjectSurface2D { OneWayNormal = Vector2.UnitY },
            Motion = new CurveMotionDefinition2D
            {
                Curve = LineSegmentCurve2D.Builder.Create(Vector2.Zero, travel),
                PeriodSeconds = 2 * travel.Length() / speed,
                Progress = [ProgressMapKind.PingPong, ProgressMapKind.Smooth]
            }
        };
}
