using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using SkiaSharp;

namespace App2d;

public sealed class DemoGame : Game2D
{
    private readonly IShader2D _solidShader = new SolidColorShader(new SKColor(40, 210, 170));
    private readonly IShader2D _gradientShader = new LinearGradientShader(
        new SKColor(255, 72, 180),
        new SKColor(75, 220, 255));
    private readonly WorldObject2D _player;
    private readonly WorldObject2D _leftTriangle;
    private readonly WorldObject2D _rightTriangle;
    private readonly WorldObject2D _orbitingCircle;
    private readonly WorldObject2D _pentagon;
    private readonly WorldObject2D _rectangle;
    private readonly WorldObject2D _capsule;
    private readonly WorldObject2D _capsuleBumper;
    private readonly WorldObject2D _clickMarker;
    private readonly PhysicsWorld2D _physics = new()
    {
        Gravity = new Vector2(0f, -650f),
        PositionIterations = 5
    };
    private readonly PhysicsBody2D _playerBody;
    private readonly PhysicsBody2D _capsuleBody;
    private readonly List<WorldObject2D> _walls = [];
    private readonly List<PhysicsBody2D> _movers = [];
    private Vector2 _mouseWorld;
    private bool _gradientEnabled = true;
    private bool _mouseOverPlayer;

    public DemoGame()
    {
        var largeTriangle = ConvexPolygon2D.CreateTriangle(75f);
        var smallTriangle = ConvexPolygon2D.CreateTriangle(45f);

        _player = new WorldObject2D(largeTriangle, _gradientShader);

        _leftTriangle = new WorldObject2D(
            smallTriangle,
            new SolidColorShader(new SKColor(255, 155, 65)));
        _leftTriangle.Transform.Position = new Vector2(-230f, 110f);
        _leftTriangle.Transform.Scale = new Vector2(0.8f, 1.25f);

        _rightTriangle = new WorldObject2D(
            smallTriangle,
            new LinearGradientShader(
                new SKColor(165, 110, 255),
                new SKColor(80, 95, 255)));
        _rightTriangle.Transform.Position = new Vector2(230f, -100f);
        _rightTriangle.Transform.Scale = new Vector2(1.15f, 0.75f);

        _orbitingCircle = new WorldObject2D(
            new Circle2D(52f),
            new LinearGradientShader(
                new SKColor(255, 225, 80),
                new SKColor(255, 95, 55)));
        _orbitingCircle.Transform.Scale = new Vector2(1.4f, 0.7f);

        _pentagon = new WorldObject2D(
            ConvexPolygon2D.CreateRegular(5, 58f, MathF.PI / 2f),
            new SolidColorShader(new SKColor(130, 235, 115)));
        _pentagon.Transform.Position = new Vector2(-330f, -180f);

        _rectangle = new WorldObject2D(
            Rectangle2D.FromSize(new Vector2(150f, 90f)),
            new LinearGradientShader(
                new SKColor(90, 225, 190),
                new SKColor(35, 135, 155)));
        _rectangle.Transform.Position = new Vector2(430f, 110f);

        _capsule = new WorldObject2D(
            new Capsule2D(new Vector2(-65f, 0f), new Vector2(65f, 0f), 28f),
            new LinearGradientShader(
                new SKColor(255, 120, 95),
                new SKColor(255, 205, 85)));
        _capsule.Transform.Position = new Vector2(70f, 390f);
        _capsule.Transform.Rotation = 0.35f;

        _capsuleBumper = new WorldObject2D(
            new Capsule2D(new Vector2(-95f, 0f), new Vector2(95f, 0f), 34f),
            new LinearGradientShader(
                new SKColor(75, 190, 255),
                new SKColor(65, 90, 220)));
        _capsuleBumper.Transform.Position = new Vector2(70f, -170f);
        _capsuleBumper.Transform.Rotation = -0.18f;

        var floor = new WorldObject2D(
            HalfSpace2D.FromPoint(new Vector2(0f, -600f), Vector2.UnitY),
            new SolidColorShader(new SKColor(38, 52, 70)));

        _walls.Add(floor);
        _walls.Add(new WorldObject2D(
            HalfSpace2D.FromPoint(new Vector2(0f, 600f), -Vector2.UnitY),
            new SolidColorShader(new SKColor(38, 52, 70))));
        _walls.Add(new WorldObject2D(
            HalfSpace2D.FromPoint(new Vector2(-850f, 0f), Vector2.UnitX),
            new SolidColorShader(new SKColor(38, 52, 70))));
        _walls.Add(new WorldObject2D(
            HalfSpace2D.FromPoint(new Vector2(850f, 0f), -Vector2.UnitX),
            new SolidColorShader(new SKColor(38, 52, 70))));

        _clickMarker = new WorldObject2D(
            new Circle2D(13f),
            new SolidColorShader(new SKColor(255, 225, 80)))
        {
            IsVisible = false
        };

        foreach (var wall in _walls)
        {
            Scene.Add(wall);
            _physics.AddBody(wall, BodyMotionType2D.Static).Restitution = 0.96f;
        }
        Scene.Add(_leftTriangle);
        Scene.Add(_rightTriangle);
        Scene.Add(_orbitingCircle);
        Scene.Add(_pentagon);
        Scene.Add(_rectangle);
        Scene.Add(_capsuleBumper);
        Scene.Add(_capsule);
        Scene.Add(_clickMarker);
        Scene.Add(_player);

        WorldObject2D[] staticObstacles =
        [
            _leftTriangle,
            _rightTriangle,
            _orbitingCircle,
            _pentagon,
            _rectangle
        ];
        foreach (var obstacle in staticObstacles)
        {
            _physics.AddBody(obstacle, BodyMotionType2D.Static).Restitution = 0.96f;
        }

        _capsuleBody = _physics.AddBody(_capsule, BodyMotionType2D.Dynamic);
        _capsuleBody.LinearVelocity = new Vector2(0f, -40f);
        _capsuleBody.AngularVelocity = 0.55f;
        _capsuleBody.Mass = 2f;
        _capsuleBody.Restitution = 0.62f;

        var capsuleBumperBody = _physics.AddBody(_capsuleBumper, BodyMotionType2D.Dynamic);
        capsuleBumperBody.LinearVelocity = new Vector2(-150f, 85f);
        capsuleBumperBody.AngularVelocity = -0.4f;
        capsuleBumperBody.GravityScale = 0f;
        capsuleBumperBody.Mass = 3f;
        capsuleBumperBody.Restitution = 0.85f;

        // A heavy, gravity-free dynamic body gives controller-like movement while
        // still letting the default solver stop velocity into walls.
        _playerBody = _physics.AddBody(_player, BodyMotionType2D.Dynamic);
        _playerBody.GravityScale = 0f;
        _playerBody.Mass = 1000f;
        _playerBody.Restitution = 0.96f;

        CreateMovers();
    }

    public override string WindowTitle => $"App2d | arrows move | Q/E rotate | click shader + marker | wheel zoom | 12 circles + 12 capsules + 6 AABBs | mouse world ({_mouseWorld.X:0.0}, {_mouseWorld.Y:0.0}) | player hit: {_mouseOverPlayer}";

    public override void Update(FrameTime time, InputState input)
    {
        var dt = time.DeltaSeconds;
        var movement = new Vector2(
            Axis(input, Keys.Left, Keys.Right),
            Axis(input, Keys.Down, Keys.Up));

        if (movement.LengthSquared() > 1f)
            movement = Vector2.Normalize(movement);

        var speed = input.IsKeyDown(Keys.ShiftKey) ? 500f : 240f;
        _playerBody.LinearVelocity = movement * speed;
        _playerBody.AngularVelocity = Axis(input, Keys.Q, Keys.E) * 2.2f;

        _leftTriangle.Transform.Rotation += 0.45f * dt;
        _rightTriangle.Transform.Rotation -= 0.3f * dt;
        _pentagon.Transform.Rotation += 0.2f * dt;

        var orbitTime = (float)time.TotalSeconds * 0.65f;
        _orbitingCircle.Transform.Position = new Vector2(
            MathF.Cos(orbitTime) * 330f,
            MathF.Sin(orbitTime) * 190f);
        _orbitingCircle.Transform.Rotation = orbitTime;

        _physics.Step(dt);

        _mouseWorld = Camera.DeviceToWorld(input.MousePositionDevice);
        _mouseOverPlayer = _player.ContainsWorldPoint(_mouseWorld);

        if (input.MouseWheelDelta != 0f)
        {
            // Zoom around the world point under the cursor instead of the window center.
            var anchorBeforeZoom = _mouseWorld;
            Camera.Zoom *= MathF.Pow(1.1f, input.MouseWheelDelta / 120f);
            Camera.Position += anchorBeforeZoom - Camera.DeviceToWorld(input.MousePositionDevice);
            _mouseWorld = Camera.DeviceToWorld(input.MousePositionDevice);
        }

        if (input.WasMousePressed(MouseButtons.Left))
        {
            _clickMarker.Transform.Position = _mouseWorld;
            _clickMarker.IsVisible = true;
            _gradientEnabled = !_gradientEnabled;
            _player.Shader = _gradientEnabled ? _gradientShader : _solidShader;
        }
    }

    private static float Axis(InputState input, Keys negative, Keys positive) =>
        (input.IsKeyDown(positive) ? 1f : 0f) -
        (input.IsKeyDown(negative) ? 1f : 0f);

    private void CreateMovers()
    {
        SKColor[] colors =
        [
            new SKColor(255, 95, 145),
            new SKColor(80, 205, 255),
            new SKColor(255, 205, 70),
            new SKColor(145, 110, 255),
            new SKColor(80, 225, 155)
        ];
        var random = new Random(20260822);

        for (var i = 0; i < 24; i++)
        {
            var isCapsule = i % 2 == 1;
            var radius = random.NextSingle() * 7f + 11f;
            IShape2D shape = isCapsule
                ? new Capsule2D(
                    new Vector2(-(random.NextSingle() * 22f + 20f), 0f),
                    new Vector2(random.NextSingle() * 22f + 20f, 0f),
                    radius)
                : new Circle2D(radius + 5f);
            var worldObject = new WorldObject2D(
                shape,
                new SolidColorShader(colors[i % colors.Length]));
            worldObject.Transform.Position = new Vector2(
                random.NextSingle() * 1360f - 680f,
                random.NextSingle() * 800f - 400f);
            worldObject.Transform.Rotation = random.NextSingle() * MathF.Tau;

            var angle = random.NextSingle() * MathF.Tau;
            var speed = random.NextSingle() * 450f + 140f;
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;

            var body = _physics.AddBody(worldObject, BodyMotionType2D.Dynamic);
            body.LinearVelocity = velocity;
            body.AngularVelocity = isCapsule
                ? random.NextSingle() * 2.4f - 1.2f
                : 0f;
            body.GravityScale = 0f;
            body.Restitution = 0.96f;
            body.Mass = isCapsule ? 1.5f : 1f;
            _movers.Add(body);
            Scene.Add(worldObject);
        }

        for (var i = 0; i < 6; i++)
        {
            var size = new Vector2(
                random.NextSingle() * 35f + 45f,
                random.NextSingle() * 30f + 35f);
            var worldObject = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(size),
                new SolidColorShader(colors[(i + 2) % colors.Length]));
            worldObject.Transform.Position = new Vector2(
                random.NextSingle() * 1280f - 640f,
                random.NextSingle() * 760f - 380f);

            var angle = random.NextSingle() * MathF.Tau;
            var speed = random.NextSingle() * 110f + 120f;
            var body = _physics.AddBody(worldObject, BodyMotionType2D.Dynamic);
            body.LinearVelocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
            body.GravityScale = 0f;
            body.Restitution = 0.9f;
            body.Mass = 1.8f;
            _movers.Add(body);
            Scene.Add(worldObject);
        }
    }
}
