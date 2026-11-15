using App2d.Core.Geometry.Operations.MassProperties;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Physics.Constraints;
using App2d.Core.Shapes;
using App2d.Core.Spatial;

namespace App2d.Demos;

internal sealed class HexWheelSimulation
{
    internal const float DefaultTileRadius = .26f;
    internal const float DefaultInnerRadius = 2.5f;
    internal const float OuterRadius = 4.8f;
    internal const float GroundY = 0f;
    internal const float StepSeconds = 1f / 120f;
    private const double SqrtThree = 1.7320508075688772d;
    private static readonly (int Column, int Row)[] ForwardNeighbors = [(1, 0), (0, 1), (1, -1)];

    private readonly List<RigidBody2D> _tiles = [];
    private readonly List<SoftPointJoint2D> _seams = [];
    private readonly List<SoftPointJoint2D> _driveJoints = [];
    private readonly List<RigidBody2D> _drivenTiles = [];
    private readonly RigidBody2D _cursor;
    private readonly float _estimatedInertia;
    private SoftPointJoint2D? _grab;

    internal HexWheelSimulation(float tileRadius, float innerRadius, float dropHeight)
    {
        TileRadius = tileRadius;
        InnerRadius = innerRadius;
        Hexagon = ConvexPolygon2D.Builder.CreateRegularHexagon(tileRadius);
        IShape2D sharedShape = Hexagon;
        var tileInertia = (float)ShapeMassProperties2D.Calculate(sharedShape, 1f).WithMass(1f).MomentOfInertia;
        var center = new Vector2f(0f, GroundY + OuterRadius + tileRadius + dropHeight);
        HubRadius = innerRadius - tileRadius;

        World.CollisionSystem.CellSize = 2f * tileRadius;
        var floor = World.AddBody(new(HalfSpace2D.Builder.Create(Vector2f.UnitY, GroundY)), BodyMotionType2D.Static);
        floor.Friction = .85f;
        floor.Restitution = 0f;
        Hub = AddBody(Circle2D.Builder.Create(HubRadius), center, BodyMotionType2D.Dynamic);
        Hub.SetMassFromShape(16f);
        Hub.IsCollider = false;
        _cursor = AddBody(Circle2D.Builder.Create(tileRadius), center, BodyMotionType2D.Kinematic);
        _cursor.IsCollider = false;

        var tilesByCell = new Dictionary<(int Column, int Row), RigidBody2D>();
        var columnLimit = (int)Math.Ceiling(OuterRadius / (1.5d * tileRadius));
        var rowLimit = OuterRadius / (SqrtThree * tileRadius);
        for (var column = -columnLimit; column <= columnLimit; column++)
        {
            var stagger = column / 2d;
            var minRow = (int)Math.Ceiling(-rowLimit - stagger);
            var maxRow = (int)Math.Floor(rowLimit - stagger);
            for (var row = minRow; row <= maxRow; row++)
            {
                var localCenter = CellOffset(column, row);
                var squared = localCenter.LengthSquared();
                if (squared < innerRadius * innerRadius || squared > OuterRadius * OuterRadius)
                    continue;

                var body = AddBody(sharedShape, center + localCenter, BodyMotionType2D.Dynamic);
                body.MomentOfInertia = tileInertia;
                tilesByCell.Add((column, row), body);
                _tiles.Add(body);
            }
        }

        foreach (var (cell, tile) in tilesByCell)
        {
            // A shared edge is visited once. The opposite three directions are covered by the other tile.
            var facesHole = false;
            foreach (var (column, row) in ForwardNeighbors)
            {
                if (tilesByCell.TryGetValue((cell.Column + column, cell.Row + row), out var neighbor))
                {
                    var offset = CellOffset(column, row);
                    var midpoint = offset / 2f;
                    var alongEdge = Vector2f.Normalize(offset.PerpCcw) * (tileRadius / 3f);
                    AddEdgeJoints(tile, neighbor, midpoint, -midpoint, alongEdge, _seams);
                }

                facesHole |= CellOffset(cell.Column + column, cell.Row + row).LengthSquared() < innerRadius * innerRadius
                    || CellOffset(cell.Column - column, cell.Row - row).LengthSquared() < innerRadius * innerRadius;
            }

            if (facesHole)
            {
                var localCenter = CellOffset(cell.Column, cell.Row);
                var alongEdge = Vector2f.Normalize(localCenter.PerpCcw) * (tileRadius / 3f);
                AddEdgeJoints(Hub, tile, localCenter, Vector2f.Zero, alongEdge, _driveJoints);
                _drivenTiles.Add(tile);
            }
        }

        _estimatedInertia = Hub.MomentOfInertia + _tiles.Count * (innerRadius * innerRadius + OuterRadius * OuterRadius) / 2f;
        SetSuspension(8f, .8f);
    }

    internal PhysicsWorld2D World { get; } = new()
    {
        Gravity = new(0f, -9.81f),
        MaxSubstepSeconds = StepSeconds,
        PositionIterations = 2,
        VelocityIterations = 4,
    };

    internal ConvexPolygon2D Hexagon { get; }
    internal IReadOnlyList<RigidBody2D> Tiles => _tiles;
    internal IReadOnlyList<RigidBody2D> DrivenTiles => _drivenTiles;
    internal IReadOnlyList<SoftPointJoint2D> Seams => _seams;
    internal RigidBody2D Hub { get; }
    internal RigidBody2D? GrabbedTile => _grab?.Second;
    internal float TileRadius { get; }
    internal float InnerRadius { get; }
    internal float HubRadius { get; }

    internal void SetSuspension(float frequency, float damping)
    {
        foreach (var joint in _seams)
        {
            joint.FrequencyHertz = frequency;
            joint.DampingRatio = damping;
        }
        foreach (var joint in _driveJoints)
        {
            joint.FrequencyHertz = frequency;
            joint.DampingRatio = damping;
        }
    }

    internal void Step(float? motorSpeed)
    {
        if (motorSpeed is { } target)
        {
            // A speed controller applies torque to the hub. The joints transmit it to the inner tiles.
            var acceleration = Math.Clamp(4f * (target - Hub.AngularVelocity), -3f, 3f);
            Hub.AddTorque(_estimatedInertia * acceleration);
        }
        World.Step(StepSeconds);
    }

    internal bool BeginGrab(Vector2f point)
    {
        EndGrab();
        foreach (var tile in _tiles)
        {
            if (!tile.WorldObject.ContainsWorldPoint(point))
                continue;

            MoveGrab(point);
            _grab = new(_cursor, tile, Vector2f.Zero, tile.WorldObject.CollisionPose.InverseTransformPoint(point))
            {
                FrequencyHertz = 5f,
                DampingRatio = 1f,
            };
            World.Constraints.Add(_grab);
            return true;
        }
        return false;
    }

    internal void MoveGrab(Vector2f point) => _cursor.WorldObject.Transform.Position = point;

    internal void EndGrab()
    {
        if (_grab is { } joint)
        {
            World.Constraints.Remove(joint);
            _grab = null;
        }
    }

    private RigidBody2D AddBody(IShape2D shape, Vector2f center, BodyMotionType2D motion)
    {
        var spatial = new SpatialObject2D(shape);
        spatial.Transform.Position = center;
        var body = World.AddBody(spatial, motion);
        body.FreezeRotation = false;
        body.Friction = .6f;
        body.Restitution = 0f;
        return body;
    }

    private void AddEdgeJoints(RigidBody2D first, RigidBody2D second, Vector2f firstMidpoint, Vector2f secondMidpoint,
        Vector2f alongEdge, List<SoftPointJoint2D> joints)
    {
        var near = new SoftPointJoint2D(first, second, firstMidpoint + alongEdge, secondMidpoint + alongEdge);
        var far = new SoftPointJoint2D(first, second, firstMidpoint - alongEdge, secondMidpoint - alongEdge);
        joints.Add(near);
        joints.Add(far);
        World.Constraints.Add(near);
        World.Constraints.Add(far);
    }

    private Vector2f CellOffset(int column, int row) =>
        new((float)(1.5d * TileRadius * column), (float)(SqrtThree * TileRadius * (row + column / 2d)));
}
