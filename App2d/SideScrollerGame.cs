using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision.Contacts;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using App2d.Engine.Tiles;
using App2d.Gameplay;
using SkiaSharp;

namespace App2d;

public sealed class SideScrollerGame : Game2D
{
    private static readonly PlayerWeapon2D[] WeaponOrder =
    [
        PlayerWeapon2D.Sword,
        PlayerWeapon2D.BionicArm,
        PlayerWeapon2D.BallAndChain
    ];

    private const float TileSize = 64f;
    private const float RunSpeed = 430f;
    private const float GroundAcceleration = 3_600f;
    private const float AirAcceleration = 1_450f;
    private const float JumpSpeed = 760f;
    private const float CoyoteDuration = 0.11f;
    private const float JumpBufferDuration = 0.12f;
    // The grapple only hooks surfaces whose contact normal points down, i.e.
    // platform undersides and ceilings. Bionic Commando hooks ceilings, bro.
    private const float GrappleLatchMaxNormalY = -0.9f;
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;
    private const uint EnemyLayer = 1u << 2;
    private const int FireballPoolSize = 16;

    private readonly PhysicsWorld2D _physics = new()
    {
        Gravity = new Vector2(0f, -1_900f),
        MaxSubstepSeconds = 1f / 120f,
        PositionIterations = 3,
        VelocityIterations = 2
    };
    private readonly TileMap2D _tileMap;
    private readonly IShader2D _playerNormalShader = new LinearGradientShader(
        new SKColor(255, 232, 92),
        new SKColor(255, 116, 70));
    private readonly IShader2D _playerHurtShader = new SolidColorShader(new SKColor(255, 245, 245));
    private readonly IShader2D _platformTextureShader;
    private readonly IShader2D _fireballTextureShader;
    private readonly WorldObject2D _player;
    private readonly WorldObject2D _playerEye;
    private readonly PhysicsBody2D _playerBody;
    private readonly Health2D _playerHealth = new(5);
    private readonly SwordAttack2D _sword;
    private readonly GrappleArm2D _grappleArm;
    private readonly BallAndChain2D _ballAndChain;
    private readonly List<ParallaxItem> _parallaxItems = [];
    private readonly List<WorldObject2D> _platforms = [];
    private readonly List<PatrolEnemy2D> _enemies = [];
    private readonly List<Projectile2D> _fireballs = [];
    private readonly Vector2 _spawnPoint;
    private readonly float _goalX;
    private float _coyoteTime;
    private float _jumpBufferTime;
    private float _fireballCooldown;
    private float _playerInvulnerability;
    private float _facing = 1f;
    private PlayerWeapon2D _activeWeapon = PlayerWeapon2D.Sword;
    private int _defeatedEnemies;
    private bool _isGrounded;
    private bool _reachedGoal;

    public SideScrollerGame()
    {
        Camera.Zoom = 1.35f;
        _platformTextureShader = new TextureShader2D(
            Textures.Load("mossy-stone.png"),
            new Vector2(512f, 512f));
        _fireballTextureShader = new TextureShader2D(
            Textures.Load("ember-energy.png"),
            new Vector2(96f, 96f),
            SKShaderTileMode.Mirror,
            SKShaderTileMode.Mirror);
        _tileMap = CreateLevel();
        _spawnPoint = TileCenter(4f, 2f) + new Vector2(0f, 38f);
        _goalX = TileCenter(116f, 0f).X;
        Camera.Position = _spawnPoint + new Vector2(180f, 150f);

        CreateParallaxBackground();
        CreatePlatforms();
        CreateGoal();

        _player = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(46f, 70f)),
            _playerNormalShader);
        _player.Transform.Position = _spawnPoint;
        Scene.Add(_player);

        _playerEye = new WorldObject2D(
            new Circle2D(5f),
            new SolidColorShader(new SKColor(30, 39, 56)));
        Scene.Add(_playerEye);
        UpdatePlayerEye();

        _playerBody = _physics.AddBody(_player, BodyMotionType2D.Dynamic);
        _playerBody.Restitution = 0f;
        _playerBody.Mass = 1f;
        _playerBody.CollisionLayer = PlayerLayer;
        _playerBody.CollisionMask = WorldLayer;

        CreateEnemies();
        CreateFireballPool();
        _sword = CreateSword();
        _grappleArm = new GrappleArm2D(Scene, _physics, _playerBody);
        _ballAndChain = new BallAndChain2D(Scene, _physics, _playerBody, PlayerLayer, WorldLayer);

        UpdateParallax();
    }

    public override string WindowTitle =>
        $"App2d Side Scroller | weapon: {ActiveWeaponName} | Ctrl+wheel switch | left click attack | right click fire | HP: {_playerHealth.Current}/{_playerHealth.Maximum} | enemies: {_defeatedEnemies}/{_enemies.Count} | broad pairs: {_physics.LastCandidatePairCount}{(_reachedGoal ? " | GOAL! BRO!" : string.Empty)}";

    public override void Update(FrameTime time, InputState input)
    {
        var dt = time.DeltaSeconds;
        _fireballCooldown = Math.Max(0f, _fireballCooldown - dt);
        _playerInvulnerability = Math.Max(0f, _playerInvulnerability - dt);
        _player.Shader = _playerInvulnerability > 0f && time.FrameNumber % 6 < 3
            ? _playerHurtShader
            : _playerNormalShader;

        if (input.IsControlDown && input.MouseWheelDelta != 0f)
            CycleWeapon(input.MouseWheelDelta);

        foreach (var enemy in _enemies)
            enemy.Update(dt);

        _isGrounded = _physics.IsTouching(_playerBody, Vector2.UnitY, 0.55f);
        _coyoteTime = _isGrounded
            ? CoyoteDuration
            : Math.Max(0f, _coyoteTime - dt);

        var jumpPressed =
            input.WasKeyPressed(Keys.Space) ||
            input.WasKeyPressed(Keys.W) ||
            input.WasKeyPressed(Keys.Up);
        if (jumpPressed)
            _jumpBufferTime = JumpBufferDuration;
        else
            _jumpBufferTime = Math.Max(0f, _jumpBufferTime - dt);

        var inputX = Axis(input, Keys.A, Keys.D) + Axis(input, Keys.Left, Keys.Right);
        inputX = Math.Clamp(inputX, -1f, 1f);
        if (MathF.Abs(inputX) > 0.01f)
            _facing = MathF.Sign(inputX);

        var acceleration = _isGrounded ? GroundAcceleration : AirAcceleration;
        var velocityX = _playerBody.LinearVelocity.X;
        // Airborne momentum above run speed (a grapple fling) is only lost by
        // steering against it, never by the run-speed clamp.
        var keepAirMomentum = !_isGrounded &&
            MathF.Abs(velocityX) > RunSpeed &&
            (inputX == 0f || MathF.Sign(inputX) == MathF.Sign(velocityX));
        if (!keepAirMomentum)
        {
            velocityX = MoveTowards(velocityX, inputX * RunSpeed, acceleration * dt);
        }
        _playerBody.LinearVelocity = new Vector2(velocityX, _playerBody.LinearVelocity.Y);

        if (_jumpBufferTime > 0f && _coyoteTime > 0f)
        {
            _playerBody.LinearVelocity = new Vector2(velocityX, JumpSpeed);
            _jumpBufferTime = 0f;
            _coyoteTime = 0f;
            _isGrounded = false;
        }

        var jumpReleased =
            input.WasKeyReleased(Keys.Space) ||
            input.WasKeyReleased(Keys.W) ||
            input.WasKeyReleased(Keys.Up);
        if (jumpReleased && _playerBody.LinearVelocity.Y > 0f)
        {
            _playerBody.LinearVelocity = new Vector2(
                _playerBody.LinearVelocity.X,
                _playerBody.LinearVelocity.Y * 0.45f);
        }

        if (input.WasKeyPressed(Keys.J) || input.WasMousePressed(MouseButtons.Left))
            TryUseActiveWeapon(input);
        if (input.WasKeyPressed(Keys.K) || input.WasMousePressed(MouseButtons.Right))
            TryLaunchFireball();

        _grappleArm.Update(dt);
        TryLatchGrappleArm();
        _ballAndChain.UpdateBeforePhysics(dt);
        _physics.Step(dt);

        _sword.Update(dt, _player.Transform.Position, _facing);
        ResolveSwordHits();
        _grappleArm.UpdateVisuals();
        ResolveGrappleArmHits();
        _ballAndChain.UpdateAfterPhysics(_physics);
        ResolveBallAndChainHits();
        UpdateFireballs(dt);
        ResolveEnemyTouches();

        if (_player.Transform.Position.Y < _tileMap.WorldBounds.Min.Y - 260f)
            Respawn();

        if (_player.Transform.Position.X >= _goalX)
            _reachedGoal = true;

        UpdatePlayerEye();
        UpdateCamera(dt);
        UpdateParallax();
    }

    public override void Render(Renderer2D renderer)
    {
        renderer.Clear(new SKColor(103, 196, 235));
        renderer.Draw(Scene);
        renderer.DrawScreenLabel($"WEAPON: {ActiveWeaponStatus}   CTRL + WHEEL", new Vector2(24f, 24f));
    }

    private static TileMap2D CreateLevel()
    {
        var map = new TileMap2D(120, 18, TileSize, new Vector2(-512f, -640f));

        map.Fill(0, 0, 120, 2);
        map.Fill(14, 0, 3, 2, false);
        map.Fill(33, 0, 3, 2, false);
        map.Fill(79, 0, 4, 2, false);
        map.Fill(108, 0, 3, 2, false);

        map.Fill(0, 2, 1, 12);
        map.Fill(119, 2, 1, 12);
        map.Fill(7, 4, 6, 1);
        map.Fill(18, 6, 5, 1);
        map.Fill(26, 3, 6, 1);
        map.Fill(38, 7, 7, 1);
        map.Fill(49, 4, 6, 1);
        map.Fill(59, 9, 6, 1);
        map.Fill(69, 5, 9, 1);
        map.Fill(84, 3, 5, 1);
        map.Fill(92, 7, 7, 1);
        map.Fill(102, 4, 6, 1);
        map.Fill(112, 3, 4, 1);

        return map;
    }

    private void CreatePlatforms()
    {
        var grassShader = new SolidColorShader(new SKColor(101, 205, 116));
        var groundTop = _tileMap.Origin.Y + TileSize * 2f;

        foreach (var bounds in _tileMap.CollisionRectangles)
        {
            var platform = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(bounds.Size),
                _platformTextureShader);
            platform.Transform.Position = bounds.Center;
            Scene.Add(platform);
            _platforms.Add(platform);

            var body = _physics.AddBody(platform, BodyMotionType2D.Static);
            body.Restitution = 0f;
            body.CollisionLayer = WorldLayer;
            body.CollisionMask = PlayerLayer | EnemyLayer;
            body.IsOneWayPlatform =
                bounds.Size.Y <= TileSize + 0.01f &&
                bounds.Min.Y >= groundTop + 0.01f;

            const float capHeight = 9f;
            var cap = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(new Vector2(bounds.Size.X, capHeight)),
                grassShader);
            cap.Transform.Position = new Vector2(
                bounds.Center.X,
                bounds.Max.Y - capHeight / 2f);
            Scene.Add(cap);
        }
    }

    private void CreateEnemies()
    {
        var hitShader = new SolidColorShader(new SKColor(255, 245, 245));
        var coralShader = new LinearGradientShader(
            new SKColor(255, 101, 137),
            new SKColor(179, 48, 102));
        var violetShader = new LinearGradientShader(
            new SKColor(178, 125, 255),
            new SKColor(91, 61, 178));
        Span<EnemySpawn> spawns =
        [
            new(10f, 7f, 12f, 105f),
            new(22f, 18f, 31f, 120f),
            new(42f, 37f, 50f, 112f),
            new(57f, 52f, 67f, 135f),
            new(72f, 68f, 77f, 105f),
            new(87f, 84f, 91f, 125f),
            new(99f, 93f, 106f, 138f),
            new(114f, 111f, 118f, 115f)
        ];

        for (var i = 0; i < spawns.Length; i++)
        {
            var spawn = spawns[i];
            IShader2D normalShader = i % 2 == 0 ? coralShader : violetShader;
            var worldObject = new WorldObject2D(
                new Capsule2D(new Vector2(-19f, 0f), new Vector2(19f, 0f), 22f),
                normalShader);
            worldObject.Transform.Position = TileCenter(spawn.TileX, 2f);
            Scene.Add(worldObject);

            var body = _physics.AddBody(worldObject, BodyMotionType2D.Dynamic);
            body.Restitution = 0f;
            body.Mass = 1.25f;
            body.CollisionLayer = EnemyLayer;
            body.CollisionMask = WorldLayer;

            _enemies.Add(new PatrolEnemy2D(
                worldObject,
                body,
                TileCenter(spawn.MinTileX, 0f).X,
                TileCenter(spawn.MaxTileX, 0f).X,
                spawn.Speed,
                3,
                normalShader,
                hitShader));
        }
    }

    private void CreateFireballPool()
    {
        for (var i = 0; i < FireballPoolSize; i++)
        {
            var worldObject = new WorldObject2D(new Circle2D(13f), _fireballTextureShader)
            {
                IsVisible = false
            };
            _fireballs.Add(new Projectile2D(worldObject));
            Scene.Add(worldObject);
        }
    }

    private SwordAttack2D CreateSword()
    {
        var worldObject = new WorldObject2D(
            new Capsule2D(Vector2.Zero, new Vector2(68f, 0f), 8f),
            new LinearGradientShader(
                new SKColor(250, 253, 255),
                new SKColor(113, 172, 211)))
        {
            IsVisible = false
        };
        Scene.Add(worldObject);
        return new SwordAttack2D(worldObject);
    }

    private string ActiveWeaponName => _activeWeapon switch
    {
        PlayerWeapon2D.Sword => "SWORD",
        PlayerWeapon2D.BionicArm => "BIONIC ARM",
        PlayerWeapon2D.BallAndChain => "BALL & CHAIN",
        _ => throw new ArgumentOutOfRangeException(nameof(_activeWeapon))
    };

    private string ActiveWeaponStatus
    {
        get
        {
            if (_grappleArm.IsLatched)
                return "BIONIC ARM - SWINGING - CLICK TO RELEASE";
            if (_ballAndChain.IsLanded)
                return "BALL & CHAIN - CLICK TO YANK";
            if (_ballAndChain.IsFlying)
                return "BALL & CHAIN - THROWN (CLICK TO YANK)";
            return ActiveWeaponName;
        }
    }

    private void CycleWeapon(float wheelDelta)
    {
        var currentIndex = Array.IndexOf(WeaponOrder, _activeWeapon);
        var direction = wheelDelta > 0f ? 1 : -1;
        var nextIndex = (currentIndex + direction + WeaponOrder.Length) % WeaponOrder.Length;
        _activeWeapon = WeaponOrder[nextIndex];

        _sword.Cancel();
        if (_activeWeapon != PlayerWeapon2D.BionicArm)
            _grappleArm.BeginRetract();
        if (_activeWeapon != PlayerWeapon2D.BallAndChain)
            _ballAndChain.Cancel();
    }

    private void TryUseActiveWeapon(InputState input)
    {
        switch (_activeWeapon)
        {
            case PlayerWeapon2D.Sword:
                _sword.TryStart();
                return;
            case PlayerWeapon2D.BionicArm:
                UseGrappleArm(input);
                return;
            case PlayerWeapon2D.BallAndChain:
                UseBallAndChain(input);
                return;
        }
    }

    private void UseGrappleArm(InputState input)
    {
        if (_grappleArm.IsLatched)
        {
            _grappleArm.Release();
            return;
        }

        if (_grappleArm.IsActive)
        {
            _grappleArm.BeginRetract();
            return;
        }

        var origin = _player.Transform.Position;
        var target = input.WasMousePressed(MouseButtons.Left)
            ? Camera.DeviceToWorld(input.MousePositionDevice)
            : origin + Vector2.Normalize(new Vector2(_facing, 1.25f)) * _grappleArm.MaxReach;
        if (MathF.Abs(target.X - origin.X) > 1f)
            _facing = MathF.Sign(target.X - origin.X);
        _grappleArm.TryFire(target);
    }

    private void UseBallAndChain(InputState input)
    {
        if (_ballAndChain.TryYank())
            return;
        if (_ballAndChain.IsActive)
            return;

        var origin = _player.Transform.Position;
        var target = input.WasMousePressed(MouseButtons.Left)
            ? Camera.DeviceToWorld(input.MousePositionDevice)
            : origin + new Vector2(_facing * 300f, 190f);
        if (MathF.Abs(target.X - origin.X) > 1f)
            _facing = MathF.Sign(target.X - origin.X);
        _ballAndChain.TryThrow(target);
    }

    private void TryLaunchFireball()
    {
        if (_fireballCooldown > 0f)
            return;

        foreach (var fireball in _fireballs)
        {
            if (fireball.IsActive)
                continue;

            fireball.Launch(_player.Transform.Position + new Vector2(_facing * 43f, 6f), new Vector2(_facing * 920f, 0f), lifetime: 2.25f);
            _fireballCooldown = 0.22f;
            return;
        }
    }

    private void ResolveSwordHits()
    {
        if (!_sword.IsActive)
            return;

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive || enemy.LastSwordAttackId == _sword.AttackId || !Intersects(_sword.WorldObject, enemy.WorldObject))
            {
                continue;
            }

            enemy.LastSwordAttackId = _sword.AttackId;
            DamageEnemy(enemy, damage: 2, knockback: new Vector2(_facing * 520f, 285f));
        }
    }

    private void ResolveGrappleArmHits()
    {
        if (!_grappleArm.IsExtending)
            return;

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive ||
                enemy.LastBionicArmAttackId == _grappleArm.AttackId ||
                !Intersects(_grappleArm.Head, enemy.WorldObject))
            {
                continue;
            }

            enemy.LastBionicArmAttackId = _grappleArm.AttackId;
            var direction = _grappleArm.Head.Transform.Position - _player.Transform.Position;
            direction = direction.LengthSquared() > float.Epsilon
                ? Vector2.Normalize(direction)
                : new Vector2(_facing, 0f);
            DamageEnemy(
                enemy,
                damage: 2,
                knockback: direction * 540f + new Vector2(0f, 210f));
            _grappleArm.BeginRetract();
            break;
        }
    }

    private void ResolveBallAndChainHits()
    {
        if (!_ballAndChain.DealsDamage)
            return;

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive ||
                enemy.LastBallAndChainAttackId == _ballAndChain.AttackId ||
                !Intersects(_ballAndChain.Ball, enemy.WorldObject))
            {
                continue;
            }

            enemy.LastBallAndChainAttackId = _ballAndChain.AttackId;
            DamageEnemy(
                enemy,
                damage: 3,
                knockback: _ballAndChain.TravelDirection * 520f + new Vector2(0f, 230f));
            // The ball is heavy; it plows through instead of retracting.
        }
    }

    private void TryLatchGrappleArm()
    {
        if (!_grappleArm.IsExtending)
            return;

        foreach (var platform in _platforms)
        {
            if (!_grappleArm.Head.WorldBounds.Intersects(platform.WorldBounds) ||
                !ShapeCollision2D.TryGetContact(_grappleArm.Head, platform, out var contact))
            {
                continue;
            }

            if (contact.Normal.Y <= GrappleLatchMaxNormalY)
            {
                var resolvedHeadPosition = contact.Point + contact.Normal * _grappleArm.HeadRadius;
                if (_grappleArm.TryLatch(resolvedHeadPosition))
                    return;
            }

            // Hit a top or side surface: the hook clanks off and comes back.
            _grappleArm.BeginRetract();
            return;
        }
    }

    private void UpdateFireballs(float deltaSeconds)
    {
        foreach (var fireball in _fireballs)
        {
            if (!fireball.IsActive)
                continue;

            fireball.Update(deltaSeconds);
            if (!fireball.IsActive)
                continue;

            var hit = false;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive || !Intersects(fireball.WorldObject, enemy.WorldObject))
                    continue;

                float direction = MathF.Sign(fireball.Velocity.X);
                DamageEnemy(
                    enemy,
                    damage: 1,
                    knockback: new Vector2(direction * 390f, 190f));
                hit = true;
                break;
            }

            if (!hit)
            {
                foreach (var platform in _platforms)
                {
                    if (!Intersects(fireball.WorldObject, platform))
                        continue;
                    hit = true;
                    break;
                }
            }

            if (hit)
                fireball.Deactivate();
        }
    }

    private void ResolveEnemyTouches()
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive || !_player.WorldBounds.Intersects(enemy.WorldObject.WorldBounds) || !ShapeCollision2D.TryGetContact(_player, enemy.WorldObject, out var contact))
                continue;

            _player.Transform.Position += contact.MinimumTranslationVector;
            if (_playerInvulnerability > 0f)
                continue;

            _playerHealth.Damage(1);
            _playerInvulnerability = 0.9f;
            float knockbackDirection = MathF.Sign(_player.Transform.Position.X - enemy.WorldObject.Transform.Position.X);
            if (knockbackDirection == 0f)
                knockbackDirection = -_facing;
            _playerBody.LinearVelocity = new Vector2(knockbackDirection * 470f, 410f);

            if (!_playerHealth.IsAlive)
            {
                _playerHealth.Reset();
                Respawn();
            }
            break;
        }
    }

    private void DamageEnemy(PatrolEnemy2D enemy, int damage, Vector2 knockback)
    {
        var wasAlive = enemy.IsAlive;
        enemy.TakeDamage(damage, knockback);
        if (wasAlive && !enemy.IsAlive)
            _defeatedEnemies++;
    }

    private static bool Intersects(WorldObject2D first, WorldObject2D second) => first.WorldBounds.Intersects(second.WorldBounds) && ShapeCollision2D.TryGetContact(first, second, out _);

    private void CreateParallaxBackground()
    {
        var cloudShader = new SolidColorShader(new SKColor(240, 250, 255, 205));
        var farMountainShader = new SolidColorShader(new SKColor(90, 145, 177));
        var nearHillShader = new SolidColorShader(new SKColor(70, 128, 125));

        for (var i = 0; i < 8; i++)
        {
            var width = 150f + i % 3 * 45f;
            AddParallax(
                new Capsule2D(new Vector2(-width / 2f, 0f), new Vector2(width / 2f, 0f), 38f),
                cloudShader,
                new Vector2(-850f + i * 470f, 210f + i % 3 * 95f),
                0.08f);
        }

        for (var i = 0; i < 12; i++)
        {
            var width = 520f + i % 3 * 90f;
            var height = 360f + i % 4 * 55f;
            var mountain = new ConvexPolygon2D(
            [
                new Vector2(-width / 2f, 0f),
                new Vector2(0f, height),
                new Vector2(width / 2f, 0f)
            ]);
            AddParallax(
                mountain,
                farMountainShader,
                new Vector2(-1_100f + i * 500f, -520f),
                0.18f);
        }

        for (var i = 0; i < 13; i++)
        {
            AddParallax(new Circle2D(185f + i % 3 * 24f), nearHillShader, new Vector2(-1_000f + i * 510f, -525f), 0.42f, new Vector2(1.9f, 1f));
        }
    }

    private void CreateGoal()
    {
        var groundTop = _tileMap.Origin.Y + TileSize * 2f;
        var x = _goalX;
        var pole = new WorldObject2D(new Capsule2D(Vector2.Zero, new Vector2(0f, 190f), 5f), new SolidColorShader(new SKColor(238, 242, 232)));
        pole.Transform.Position = new Vector2(x, groundTop);
        Scene.Add(pole);

        var flag = new WorldObject2D(
            new ConvexPolygon2D(
            [
                Vector2.Zero,
                new Vector2(92f, -30f),
                new Vector2(0f, -60f)
            ]),
            new SolidColorShader(new SKColor(255, 79, 120)));
        flag.Transform.Position = new Vector2(x, groundTop + 185f);
        Scene.Add(flag);
    }

    private void AddParallax(
        IShape2D shape,
        IShader2D shader,
        Vector2 anchor,
        float scrollFactor,
        Vector2? scale = null)
    {
        var worldObject = new WorldObject2D(shape, shader);
        worldObject.Transform.Scale = scale ?? Vector2.One;
        _parallaxItems.Add(new ParallaxItem(worldObject, anchor, scrollFactor));
        Scene.Add(worldObject);
    }

    private void UpdateCamera(float dt)
    {
        var lookAhead = Math.Clamp(_playerBody.LinearVelocity.X * 0.32f, -210f, 210f);
        var target = _player.Transform.Position + new Vector2(lookAhead, 165f);
        var halfView = Camera.ViewportSize / (2f * Camera.Zoom);
        var levelBounds = _tileMap.WorldBounds;
        target.X = ClampViewCenter(target.X, levelBounds.Min.X, levelBounds.Max.X, halfView.X);
        target.Y = ClampViewCenter(target.Y, levelBounds.Min.Y, levelBounds.Max.Y, halfView.Y);

        var blend = 1f - MathF.Exp(-5.5f * dt);
        Camera.Position = Vector2.Lerp(Camera.Position, target, blend);
    }

    private void UpdateParallax()
    {
        foreach (var item in _parallaxItems)
        {
            item.Object.Transform.Position = new Vector2(
                item.Anchor.X + Camera.Position.X * (1f - item.ScrollFactor),
                item.Anchor.Y + Camera.Position.Y * (1f - item.ScrollFactor * 0.3f));
        }
    }

    private void UpdatePlayerEye()
    {
        _playerEye.Transform.Position = _player.Transform.Position + new Vector2(_facing * 12f, 11f);
    }

    private void Respawn()
    {
        _player.Transform.Position = _spawnPoint;
        _playerBody.LinearVelocity = Vector2.Zero;
        _playerBody.AngularVelocity = 0f;
        _sword.Cancel();
        _grappleArm.Cancel();
        _ballAndChain.Cancel();
        foreach (var fireball in _fireballs)
            fireball.Deactivate();
        _playerInvulnerability = Math.Max(_playerInvulnerability, 0.35f);
        _reachedGoal = false;
    }

    private Vector2 TileCenter(float x, float y) =>
        _tileMap.Origin + new Vector2((x + 0.5f) * TileSize, (y + 0.5f) * TileSize);

    private static float Axis(InputState input, Keys negative, Keys positive) =>
        (input.IsKeyDown(positive) ? 1f : 0f) -
        (input.IsKeyDown(negative) ? 1f : 0f);

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }

    private static float ClampViewCenter(float value, float min, float max, float halfExtent)
    {
        if (max - min <= halfExtent * 2f)
            return (min + max) / 2f;
        return Math.Clamp(value, min + halfExtent, max - halfExtent);
    }

    private readonly record struct ParallaxItem(WorldObject2D Object, Vector2 Anchor, float ScrollFactor);

    private readonly record struct EnemySpawn(float TileX, float MinTileX, float MaxTileX, float Speed);
}
