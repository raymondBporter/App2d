using App2d.Core.Geometry.Measurements;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Physics.Constraints;
using App2d.Core.Spatial;

namespace App2d.Demos;

internal sealed class HexWheelSimulation
{
    internal const float DefaultTileRadius = .26f;
    internal const float DefaultInnerRadius = 2.5f;
    internal const float DefaultJointFrequency = 30f;
    internal const float DefaultJointDamping = .9f;
    internal const float OuterRadius = 4.8f;
    internal const float GroundY = 0f;
    internal const float StepSeconds = 1f / 120f;
    private const double SqrtThree = 1.7320508075688772d;
    private static readonly (int Column, int Row)[] ForwardNeighbors = [(1, 0), (0, 1), (1, -1)];

    private readonly List<RigidBody2D> _tiles = [];
    private readonly List<SoftPointJoint2D> _seams = [];
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
        World.CollisionSystem.CellSize = 2f * tileRadius;
        var floor = World.AddBody(new(HalfSpace2D.Builder.Create(Vector2f.UnitY, GroundY)), BodyMotionType2D.Static);
        floor.Friction = .95f;
        floor.Restitution = 0f;
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
                _estimatedInertia += tileInertia + body.Mass * squared;
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
                    var alongEdge = Vector2f.Normalize(offset.PerpCcw) * (.45f * tileRadius);
                    AddEdgeJoints(tile, neighbor, midpoint, alongEdge);
                }

                facesHole |= CellOffset(cell.Column + column, cell.Row + row).LengthSquared() < innerRadius * innerRadius
                    || CellOffset(cell.Column - column, cell.Row - row).LengthSquared() < innerRadius * innerRadius;
            }

            if (facesHole)
            {
                _drivenTiles.Add(tile);
            }
        }

        SetSuspension(DefaultJointFrequency, DefaultJointDamping);
    }

    internal PhysicsWorld2D World { get; } = new()
    {
        Gravity = new(0f, -9.81f),
        MaxSubstepSeconds = StepSeconds,
        PositionIterations = 2,
        VelocityIterations = 8,
    };

    internal ConvexPolygon2D Hexagon { get; }
    internal IReadOnlyList<RigidBody2D> Tiles => _tiles;
    internal IReadOnlyList<RigidBody2D> DrivenTiles => _drivenTiles;
    internal IReadOnlyList<SoftPointJoint2D> Seams => _seams;
    internal RigidBody2D? GrabbedTile => _grab?.Second;
    internal float TileRadius { get; }
    internal float InnerRadius { get; }

    internal Vector2f DriveCenter
    {
        get
        {
            var weightedCenter = Vector2f.Zero;
            var mass = 0f;
            foreach (var tile in _drivenTiles)
            {
                weightedCenter += tile.WorldCenterOfMass * tile.Mass;
                mass += tile.Mass;
            }
            return mass > 0f ? weightedCenter / mass : weightedCenter;
        }
    }

    internal void SetSuspension(float frequency, float damping)
    {
        foreach (var joint in _seams)
        {
            joint.FrequencyHertz = frequency;
            joint.DampingRatio = damping;
        }
    }

    internal void Step(float? motorSpeed)
    {
        if (motorSpeed is { } target)
        {
            ApplyDrive(target);
        }
        World.Step(StepSeconds);
    }

    private void ApplyDrive(float targetSpeed)
    {
        var center = DriveCenter;
        var inertia = 0f;
        var angularMomentum = 0f;
        foreach (var tile in _drivenTiles)
        {
            var radius = tile.WorldCenterOfMass - center;
            inertia += tile.Mass * radius.LengthSquared();
            angularMomentum += tile.Mass * Vector2f.Dot(radius.PerpCcw, tile.LinearVelocity);
        }
        if (inertia <= 0f)
            return;

        var currentSpeed = angularMomentum / inertia;
        var acceleration = Math.Clamp(8f * (targetSpeed - currentSpeed), -5f, 5f);
        var torquePerInertia = _estimatedInertia * acceleration / inertia;
        foreach (var tile in _drivenTiles)
        {
            // Each force is tangent to the current radius. The mass-weighted center makes their sum zero.
            var tangent = (tile.WorldCenterOfMass - center).PerpCcw;
            tile.AddForce(tangent * (torquePerInertia * tile.Mass));
        }
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

    internal void MoveGrab(Vector2f point) => _cursor.WorldObject.Position = point;

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
        var spatial = new SpatialObject2D(shape)
        {
            Position = center
        };
        var body = World.AddBody(spatial, motion);
        body.FreezeRotation = false;
        body.Friction = .8f;
        body.Restitution = 0f;
        return body;
    }

    private void AddEdgeJoints(RigidBody2D first, RigidBody2D second, Vector2f midpoint, Vector2f alongEdge)
    {
        var near = new SoftPointJoint2D(first, second, midpoint + alongEdge, -midpoint + alongEdge);
        var far = new SoftPointJoint2D(first, second, midpoint - alongEdge, -midpoint - alongEdge);
        _seams.Add(near);
        _seams.Add(far);
        World.Constraints.Add(near);
        World.Constraints.Add(far);
    }

    private Vector2f CellOffset(int column, int row) =>
        new((float)(1.5d * TileRadius * column), (float)(SqrtThree * TileRadius * (row + column / 2d)));
}
