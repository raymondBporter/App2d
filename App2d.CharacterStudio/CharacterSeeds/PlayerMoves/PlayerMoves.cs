using App2d.Core.Mathematics.Progress;
using App2d.Core.Animation;
using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.CharacterSeeds;
using App2d.CharacterStudio.CharacterSeeds.PlayerMoves;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.CharacterStudio.PlayerMoves;

/// <summary>
/// The player's move set on the authored Person, written as key poses. Right-facing, turned toward the viewer by the
/// model's shared hip/shoulder yaw; the near (left) limbs are toward the camera and the far right hand carries the sword
/// and gun. The sword lives in a sheath on the back: only attacks draw it (marker "sword-draw") and the sheathe clip
/// returns it ("sword-sheathe"). Upper-body clips key only chest, head, shoulders and arms so they can be layered on
/// any legs. Durations fit the current gameplay windows noted on each clip.
/// </summary>
internal static partial class PlayerMoves
{
    public const string Sword = PersonLoadout.Sword, Sheath = PersonLoadout.Sheath, Pistol = PersonLoadout.Pistol;

    // Stance: the walk's contact spacing. Feet rest at y = 0.025 (sole on the ground).
    private const float Back = -.125f;
    private const float Front = .125f;
    private const float Ground = .025f;

    // Standing pelvis offset. The rest pose already bends the knees about 20 degrees, so standing tall sits slightly above
    // rest: over the stance feet this leaves 6-8 degrees (0.015 locks the front leg), and every 0.01 lower adds roughly 5.
    internal const float StanceX = -.01f;
    internal const float StanceY = .012f;
    // Sheathed sword: grip in the chest frame and the blade's direction. Worn diagonally across the back, so in this
    // profile it foreshortens to a near-vertical line hidden behind the torso: the hilt sits below the shoulder line
    // (a guard at neck height read as part of the neck) and only the scabbard's tip shows below the back.
    private static readonly Vector2 SheathGrip = new(-.1f, -.08f);
    private const float SheathAngle = -MathF.PI / 2 - .12f;
    // The same sheath seen from behind: hilt over the right shoulder (screen left once turned), tip toward the left hip,
    // drawn across the back.
    private static readonly Vector2 BackViewGrip = new(-.13f, .12f);
    private const float BackViewAngle = -MathF.PI / 2 + .55f;

    public static IEnumerable<ModelSocket> Sockets() =>
    [
        new ModelSocket()
        {
            Id = Humanoid.BackSocket,
            Control = Humanoid.Chest,
            Frame = Humanoid.Chest,
            OffsetX = SheathGrip.X,
            OffsetY = SheathGrip.Y,
            OffsetZ = .2f,
            Angle = SheathAngle
        },
        new ModelSocket()
        {
            Id = Humanoid.BackViewSocket,
            Control = Humanoid.Chest,
            Frame = Humanoid.Chest,
            OffsetX = BackViewGrip.X,
            OffsetY = BackViewGrip.Y,
            OffsetZ = -.2f,
            Angle = BackViewAngle
        },
        new ModelSocket()
        {
            Id = Humanoid.SwordSocket,
            Control = Humanoid.RightHand,
            Frame = Humanoid.RightShoulder
        },
        new ModelSocket()
        {
            Id = Humanoid.GunSocket,
            Control = Humanoid.RightHand,
            Frame = Humanoid.RightShoulder
        },
    ];

    public static IEnumerable<Part> Props() =>
    [
        SwordProp(),
        SheathProp(),
        PistolProp()
    ];

    public static IEnumerable<MotionClip> Clips(ResolvedModel m) =>
    [
        Idle(m),
        Jump(m),
        Fall(m),
        Land(m),
        Dash(m),
        ClimbTurn(m, onto: true),
        Climb(m),
        ClimbTurn(m, onto: false),
        WallGrip(m),
        BalanceForward(m),
        BalanceBackward(m),
        Hit(m),
        Death(m),
        Celebrate(m),
        .. SwingLab.GameClips(m),
        Sheathe(m),
        DownAttack(m),
        GunAim(m),
        GunShot(m),
        GunWallShot(m),
        GunCharge(m),
        HealGather(m),
    ];

    /// <summary>Adds the move set's sockets to the Person model, then writes props and clips beside it.</summary>
    public static void Write(string authoredRoot, SeedReport? report = null)
    {
        report ??= new();
        var modelPath = Path.Combine(authoredRoot, "models", "person.json");
        var person = CharacterModel.FromJson(File.ReadAllText(modelPath));
        foreach (var socket in Sockets())
        {
            person.Sockets.RemoveAll(s => s.Id == socket.Id);
            person.Sockets.Add(socket);
        }
        report.Write(modelPath, person.ToJson());
        var model = StudyProportions(person);
        foreach (var prop in Props())
        {
            report.Write(Path.Combine(authoredRoot, "parts", prop.Id + ".json"), prop.ToJson());
        }

        report.Write(Path.Combine(authoredRoot, "profiles", GameProfile.GameId + ".json"), Profile().ToJson());

        var clips = Clips(model).Select(WeaponMotion).ToDictionary(c => c.Id);
        foreach (var clip in clips.Values)
        {
            report.Write(Path.Combine(authoredRoot, "animations", clip.Id + ".json"), clip.ToJson());
        }

        var hero = Hero(ResolvedModel.From(person, null, PersonTemplate.PartOf), clips);
        report.Write(Path.Combine(authoredRoot, "parts", hero.Id + ".json"), hero.ToJson());
        // The cinder gunner: an enemy person on the same base, shooting the player's pistol with a deliberate raise and fire.
        report.Write(Path.Combine(authoredRoot, "animations", "person-pistol-shot.json"), WeaponMotion(PistolShot(model)).ToJson());
        var cinder = ModelVariant.FromJson(File.ReadAllText(Path.Combine(authoredRoot, "variants", "cinder.json")));
        var gunner = CinderGunner(ResolvedModel.From(person, cinder, PersonTemplate.PartOf));
        report.Write(Path.Combine(authoredRoot, "parts", gunner.Id + ".json"), gunner.ToJson());
        // The maul: a broad brute on the Heavy set with a one-handed hammer and a slow overhead slam.
        report.Write(Path.Combine(authoredRoot, "parts", "hammer.json"), HammerProp().ToJson());
        report.Write(Path.Combine(authoredRoot, "animations", "person-hammer-slam.json"), WeaponMotion(HammerSlam(model)).ToJson());
        var brute = ModelVariant.FromJson(File.ReadAllText(Path.Combine(authoredRoot, "variants", "brute.json")));
        var maul = MaulBrute(ResolvedModel.From(person, brute, PersonTemplate.PartOf));
        report.Write(Path.Combine(authoredRoot, "parts", maul.Id + ".json"), maul.ToJson());
        PersonWardrobe.Write(authoredRoot, report: report);
    }

    /// <summary>
    /// The Person at the prototype study's proportions, on which these absolute pose studies were authored: the shipped Person
    /// baked the study's legs at <see cref="PersonTemplate.LegLength"/>, so the inverse legs value restores them, and the clips'
    /// reference measures then retarget the legs on every build, including after regeneration (P11b: the canonical rig itself,
    /// no point-control rebuild).
    /// </summary>
    internal static ResolvedModel StudyProportions(CharacterModel person) =>
        ResolvedModel.From(person, new PersonBuild { Legs = 1 / PersonTemplate.LegLength }.Apply(person, "study", "Study proportions"), PersonTemplate.PartOf);

    /// <summary>A short-hafted war hammer: grip at the origin, haft along +X, the head's centre is its tip.</summary>
    public static Part HammerProp() => HammerArt();

    /// <summary>
    /// A slow overhead slam, 1.5 s: the hammer climbs over and behind the head (anticipation) and hangs there, then drives down
    /// ahead of the feet with a deep sink at 0.78 s ("strike") and stays down until 0.95 s ("recover") before hauling back up.
    /// </summary>
    private static MotionClip HammerSlam(ResolvedModel m) => New(m, "person-hammer-slam", "Hammer slam", 1.5f, false)
        .Key(0, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).RightHandAt(.3f, .95f).LeftHand(.05f, -.6f).Blade(.3f))
        .Key(.55f, k => k.Hips(-.06f, -.02f).Chest(.25f).Head(-.02f).RightHandAt(-.15f, 2.05f).LeftHand(.1f, -.45f).Blade(2.4f))
        .Key(.7f, k => k.Hips(-.06f, -.02f).Chest(.28f).Head(-.03f).RightHandAt(-.18f, 2.08f).LeftHand(.12f, -.44f).Blade(2.55f), ProgressMapKind.Linear)
        .Key(.78f, k => k.Hips(.08f, -.22f).Chest(-.4f).Head(.12f).RightHandAt(.62f, .95f).LeftHand(-.2f, -.4f).Blade(-.9f))
        .Key(.95f, k => k.Hips(.08f, -.24f).Chest(-.42f).Head(.14f).RightHandAt(.62f, .9f).LeftHand(-.22f, -.4f).Blade(-1.05f))
        .Key(1.5f, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).RightHandAt(.3f, .95f).LeftHand(.05f, -.6f).Blade(.3f))
        .Plant(Humanoid.LeftLeg, 0, 1.5f, -.2f)
        .Plant(Humanoid.RightLeg, 0, 1.5f, .2f)
        .Marker("windup", .1f)
        .Marker("strike", .78f)
        .Marker("recover", .95f)
        .Face(0, "focused")
        .Face(.5f, "determined")
        .Face(.78f, "angry")
        .Build();

    /// <summary>Heavy motion, heavy hits: walks in slowly and slams for 5 when the player is within 1.3 units. Knockback barely moves it.</summary>
    public static Part MaulBrute(ResolvedModel model) => new()
    {
        Usage = "character",
        Id = "maul-brute",
        Name = "Maul brute",
        Model = model.Id,
        Equipment =
        [
            new EquipmentBinding()
            {
                Prop = "hammer",
                Socket = Humanoid.SwordSocket
            }
        ],
        Geometry =
        [
            EntityAuthoring.FitMovement(model),
            new PartGeometry()
            {
                Id = "hammer-head",
                Prop = "hammer",
                Geometry = Rectangle2D.Builder.FromSize(1.2f, 1f),
                Point = EquipmentPoints.Tip,
                Collision = new GeometryCollision()
            },
            .. StarterContent.PersonHurt(model)
        ],
        Behavior = new()
        {
            Damage = new() { Health = 22, KnockbackResistance = 3 },
            Strikes = new(StringComparer.Ordinal)
            {
                ["hammer-head"] = new StrikeDef()
                {
                    Damage = 5,
                    Sound = "heavy"
                }
            },
            MotionSet = "heavy",
            Controller = new ControllerConfig()
            {
                Kind = EntityControllers.Walker,
                WalkSpeed = 1.1f,
                Range = 1.3f,
                Cooldown = 1
            },
            Actions =
            [
                new EntityActionDef()
                {
                    Id = EntityControllers.Attack,
                    Clip = "person-hammer-slam",
                    Hits =
                    [
                        new HitWindow()
                        {
                            Id = "hammer-head",
                            Collider = "hammer-head",
                            Start = new()
                            {
                                Marker = "strike"
                            },
                            Finish = new()
                            {
                                Marker = "recover"
                            }
                        }
                    ],
                    Events =
                    [
                        new ActionEvent()
                        {
                            Id = "heavy",
                            At = new()
                            {
                                Marker = "strike"
                            },
                            Sound = "heavy"
                        }
                    ],
                },
            ],
            Colliders = new Dictionary<string, string>()
            {
                ["hammer-head"] = ColliderUses.Attack,
                ["body"] = ColliderUses.Hurt,
                ["head"] = ColliderUses.Hurt,
                ["legs"] = ColliderUses.Hurt
            }
        },
    };

    /// <summary>
    /// An enemy's readable pistol shot, 1.1 s: raise to the aim pose, hold, fire at 0.6 s with the player's kick, settle and
    /// lower. Whole body, with both feet planted; reuses the gun clips' aim and recoil poses. It keeps the lower, bent-knee
    /// stance rather than the player's <see cref="StanceY"/>: raising it lifts the muzzle and the bolts pass over the player.
    /// </summary>
    private static MotionClip PistolShot(ResolvedModel m) => New(m, "person-pistol-shot", "Pistol shot", 1.1f, false)
        .Key(0, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).RightHand(.08f, -.585f).LeftHand(.05f, -.6f).Blade(0))
        .Key(.35f, k => k.Hips(-.02f, -.03f).Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0))
        .Key(.6f, k => k.Hips(-.02f, -.035f).Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0), ProgressMapKind.Linear)
        .Key(.63f, k => k.Chest(.06f).Head(.05f).RightHand(.54f, 0).LeftHand(.01f, -.6f).Blade(.38f))
        .Key(.85f, k => k.Hips(-.02f, -.03f).Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0))
        .Key(1.1f, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).RightHand(.08f, -.585f).LeftHand(.05f, -.6f).Blade(0))
        .Plant(Humanoid.LeftLeg, 0, 1.1f, Back).Plant(Humanoid.RightLeg, 0, 1.1f, Front)
        .Marker("fire", .6f)
        .Face(0, "focused").Face(.6f, "determined").Face(.85f, "focused")
        .Build();

    /// <summary>Keeps its distance and shoots: walks in to 4.5 units, then fires an 8 unit/s bolt from the pistol's muzzle.</summary>
    public static Part CinderGunner(ResolvedModel model) => new()
    {
        Usage = "character",
        Id = "cinder-gunner",
        Name = "Cinder gunner",
        Model = model.Id,
        Equipment =
        [
            new()
            {
                Prop = PersonLoadout.Pistol,
                Socket = Humanoid.GunSocket
            }
        ],
        Geometry = [EntityAuthoring.FitMovement(model), .. StarterContent.PersonHurt(model)],
        Behavior = new()
        {
            Damage = new() { Health = 7 },
            Strikes = new(StringComparer.Ordinal) { [EntityControllers.Attack] = new() { Damage = 2 } },
            MotionSet = "standard",
            Controller = new()
            {
                Kind = EntityControllers.Walker,
                WalkSpeed = 1.4f,
                Range = 4.5f,
                Cooldown = 1
            },
            Actions =
        [
            new()
            {
                Id = EntityControllers.Attack,
                Clip = "person-pistol-shot",
                Events =
                [
                    new ActionEvent()
                    {
                        Id = EntityControllers.Fire,
                        At = new ActionTime() { Marker = "fire" },
                        Sound = "shot"
                    }
                ],
                Projectile = new()
                {
                    Speed = 8,
                    Shape = Rectangle2D.Builder.FromSize(.3f, .12f),
                    Lifetime = 3
                },
            },
        ]
        ,
            Colliders = new() { ["body"] = ColliderUses.Hurt, ["head"] = ColliderUses.Hurt, ["legs"] = ColliderUses.Hurt }
        },
    };

    /// <summary>
    /// The player profile (P9c): the hero entity, the Person model, every director role bound to its clip, the shipped gear
    /// and which authored entity each world placement kind spawns. The game and the Person presentation read this, not a switch.
    /// </summary>
    public static GameProfile Profile() => new()
    {
        Roles = new(StringComparer.Ordinal)
        {
            [PersonRoles.Idle] = "player-idle",
            [PersonRoles.Walk] = "person-walk",
            [PersonRoles.Run] = "person-run",
            [PersonRoles.Jump] = "player-jump",
            [PersonRoles.Fall] = "player-fall",
            [PersonRoles.Land] = "player-land",
            [PersonRoles.Dash] = "player-dash",
            [PersonRoles.ClimbOn] = "player-climb-on",
            [PersonRoles.Climb] = "player-climb",
            [PersonRoles.ClimbOff] = "player-climb-off",
            [PersonRoles.WallGrip] = "player-wall-grip",
            [PersonRoles.BalanceForward] = "player-balance-forward",
            [PersonRoles.BalanceBackward] = "player-balance-backward",
            [PersonRoles.Hit] = "player-hit",
            [PersonRoles.Death] = "player-death",
            [PersonRoles.Celebrate] = "player-celebrate",
            [PersonRoles.Sheathe] = "player-sword-sheathe",
            [PersonRoles.DownAttack] = "player-sword-down-attack",
            [PersonRoles.GunAim] = "player-gun-aim",
            [PersonRoles.GunShot] = "player-gun-shot",
            [PersonRoles.GunWallShot] = "player-gun-wall-shot",
            [PersonRoles.GunCharge] = "player-gun-charge",
            [PersonRoles.HealGather] = "player-heal-gather",
        },
        Spawns = new(StringComparer.Ordinal)
        {
            ["Shieldback"] = "spear-guard",
            ["GreenDinosaur"] = "stalker-pest",
            ["Rival"] = "cinder-gunner",
            ["BoilerBrute"] = "maul-brute",
            ["ClubCaveman"] = "club-caveman",
            ["RockThrower"] = "rock-thrower",
            ["BabyTriceratops"] = "baby-triceratops",
            ["ShieldDefender"] = "shield-defender",
            ["Pterosaur"] = "flying-pterosaur",
        },
    };

    /// <summary>The player's bolt: 30 x 10 px at 1250 px/s for 1.5 s at the player's drawn scale (about 40 px per unit).</summary>
    private static ProjectileDef PlayerBolt => new()
    {
        Speed = 31,
        Shape = Rectangle2D.Builder.FromSize(.75f, .25f),
        Lifetime = 1.5f
    };

    /// <summary>
    /// The game's traversal player. Person2D moves it; this entity supplies what the move set's clips already say: the
    /// sword combo (the side cut drawn from the back, then backhand and forehand alternating for as long as the button is
    /// pressed, each with its put-away), each swing's strike → recover window and hit rectangle, and the muzzle at each
    /// shot's fire marker. Health and the movement box are the player's too.
    /// </summary>
    public static Part Hero(ResolvedModel model, IReadOnlyDictionary<string, MotionClip> clips) => new()
    {
        Usage = "character",
        Id = "hero",
        Name = "Hero",
        Model = PersonTemplate.Id,
        Equipment =
        [
            new EquipmentBinding()
            {
                Prop = PersonLoadout.Sword,
                Socket = Humanoid.SwordSocket
            },
            new EquipmentBinding()
            {
                Prop = PersonLoadout.Pistol,
                Socket = Humanoid.GunSocket
            }
        ],
        Geometry =
        [
            new PartGeometry()
            {
                Id = ColliderUses.Movement,
                Collision = new(),
                A = PartLocation.Origin,
                Geometry = EntityAuthoring.MovementBox(.55f, 1.705f)
            },
            BladeCollider(model, clips, EntityControllers.Attack, "player-sword-side-cut"),
            BladeCollider(model, clips, EntityControllers.FollowUp, "player-sword-backhand"),
            BladeCollider(model, clips, Forehand, "player-sword-forehand") ,
            .. StarterContent.PersonHurt(model)
        ],
        Behavior = new()
        {
            Damage = new() { Health = 30 },
            Strikes = new(StringComparer.Ordinal)
            {
                ["blade"] = new() { Damage = 2, Impact = ImpactKinds.Sword },
                [EntityControllers.Shoot] = new() { Damage = 2 },
                [EntityControllers.WallShot] = new() { Damage = 2 },
            },
            MotionSet = "standard",
            Roles = new()
            {
                ["idle"] = "player-idle",
                ["jump"] = "player-jump",
                ["fall"] = "player-fall",
                ["hit"] = "player-hit",
                ["death"] = "player-death"
            },
            Controller = new()
            {
                Kind = EntityControllers.Traversal,
                WalkSpeed = 1.8f,
                RunSpeed = 4.2f,
                Range = 1.2f,
                Cooldown = 0
            },
            Actions =
            [
                Swing(EntityControllers.Attack, SwingLab.SideCutClip, EntityControllers.FollowUp, SwingLab.PutAwayClip),
                Swing(EntityControllers.FollowUp, SwingLab.BackhandClip, Forehand, SwingLab.PutAwayBackhandClip),
                Swing(Forehand, SwingLab.ForehandClip, EntityControllers.FollowUp, SwingLab.PutAwayClip),
                new()
                {
                    Id = EntityControllers.Shoot,
                    Clip = "player-gun-shot",
                    Events =
                    [
                        new ActionEvent()
                        {
                            Id = EntityControllers.Fire,
                            At = new() { Marker = "fire" },
                            Sound = "shot"
                        }
                    ],
                    Projectile = PlayerBolt
                },
                new EntityActionDef()
                {
                    Id = EntityControllers.WallShot,
                    Clip = "player-gun-wall-shot",
                    Events =
                    [
                        new ActionEvent()
                        {
                            Id = EntityControllers.Fire,
                            At = new() { Marker = "fire" },
                            Sound = "shot"
                        }
                    ],
                    Projectile = PlayerBolt
                },
            ],
            Colliders = new Dictionary<string, string>()
            {
                ["attack-blade"] = ColliderUses.Attack,
                ["follow-up-blade"] = ColliderUses.Attack,
                ["forehand-blade"] = ColliderUses.Attack,
                ["body"] = ColliderUses.Hurt,
                ["head"] = ColliderUses.Hurt,
                ["legs"] = ColliderUses.Hurt
            }
        },
    };

    /// <summary>The combo's forehand: the backhand's next swing, chaining back to the backhand.</summary>
    private const string Forehand = "forehand";

    /// <summary>
    /// One sword swing of the combo: its clip, the swing a further press plays, the clip that puts the sword away after it,
    /// and its hit, a rectangle fixed to the player that bounds what the blade sweeps in front of the body.
    /// </summary>
    private static EntityActionDef Swing(string id, string clip, string next, string recovery) => new()
    {
        Id = id,
        Clip = clip,
        Next = next,
        Recovery = recovery,
        Hits = [new() { Id = "blade", Collider = id + "-blade", Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" } }],
        Events = [new() { Id = "swing", At = new() { Marker = "strike" }, Sound = "swing" }],
    };

    /// <summary>A swing's attack collider: the rectangle, fixed to the player, that bounds what the blade sweeps in front of the body between the strike and the recovery.</summary>
    private static PartGeometry BladeCollider(ResolvedModel model, IReadOnlyDictionary<string, MotionClip> clips, string action, string clip)
    {
        var swing = clips[clip];
        float At(string marker) => swing.Markers.Single(m => m.Id == marker).Time;
        var socket = model.Base.Sockets.Single(s => s.Id == Humanoid.SwordSocket);
        var sword = SwordProp();
        var (min, max) = SwingLab.HitRectangle(model, swing, At(PersonLoadout.SwooshMarker), At("strike"), At("recover"), t => SwingLab.Blade(model, swing, t, sword, socket))
            ?? throw new InvalidOperationException($"'{clip}': the blade never gets in front of the player.");
        static float R(float v) => MathF.Round(v, 3);
        return new PartGeometry()
        {
            Id = action + "-blade",
            OffsetX = R((min.X + max.X) / 2),
            OffsetY = R((min.Y + max.Y) / 2),
            Geometry = Rectangle2D.Builder.Create(min, max),
            Collision = new GeometryCollision()
        };
    }

    private static MoveBuilder New(ResolvedModel m, string id, string name, float duration, bool loop) => new(m, id, name, duration, loop);

    /// <summary>The sheathed grip's actor-space position and blade angle for a body with hips offset (dx, dy) and this chest turn.</summary>
    internal static (Vector2 Grip, float Angle) Hilt(float hipsDx, float hipsDy, float chestTurn)
    {
        var (sin, cos) = MathF.SinCos(chestTurn);
        var chest = new Vector2(hipsDx - .65f * sin, 1 + hipsDy + .65f * cos);
        return (chest + new Vector2(SheathGrip.X * cos - SheathGrip.Y * sin, SheathGrip.X * sin + SheathGrip.Y * cos), chestTurn + SheathAngle);
    }

    // ---- Locomotion ---------------------------------------------------------------------------------------------

    /// <summary>A ready stance breathing on a 2.4 s loop. Arms drift a beat behind the chest so the loop never looks pumped.</summary>
    private static MotionClip Idle(ResolvedModel m) => New(m, "player-idle", "Idle", 2.4f, true)
        .Key(0, k => k.Hips(StanceX, StanceY).Chest(-.03f).Head(.02f))
        .Key(1.2f, k => k.Hips(StanceX, StanceY - .008f).Chest(-.005f).Head(-.01f))
        .Key(2.4f, k => k.Hips(StanceX, StanceY).Chest(-.03f).Head(.02f))
        .Key(0, k => k.LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Key(.35f, k => k.LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Key(1.55f, k => k.LeftHand(.035f, -.615f).RightHand(.065f, -.6f))
        .Key(2.4f, k => k.LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Plant(Humanoid.LeftLeg, 0, 2.4f, Back).Plant(Humanoid.RightLeg, 0, 2.4f, Front)
        .Build();

    /// <summary>
    /// Gameplay leaves the ground on the press, so there is no crouch: the clip opens on the push (toes still low, arms
    /// swept back), snaps to full extension, then tucks through the rise and holds. "launch" is frame one.
    /// </summary>
    private static MotionClip Jump(ResolvedModel m) => New(m, "player-jump", "Jump", .5f, false)
        .Key(0, k => k.Hips(.02f, -.08f).Chest(-.22f).Head(.12f).LeftFoot(-.2f, .06f).RightFoot(.06f, .03f).LeftHand(-.28f, -.48f).RightHand(-.22f, -.5f))
        .Key(.08f, k => k.Hips(0, .02f).Chest(-.04f).Head(.02f).LeftFoot(-.14f, .02f).RightFoot(.02f, .1f).LeftHand(.12f, .2f).RightHand(.2f, .22f), ProgressMapKind.Linear)
        .Key(.3f, k => k.Hips(0, .06f).Chest(-.1f).Head(.06f).LeftFoot(-.18f, .46f).RightFoot(.12f, .52f).LeftHand(.18f, -.2f).RightHand(.24f, -.16f))
        .Key(.5f, k => k.Hips(0, .05f).Chest(-.08f).Head(.05f).LeftFoot(-.15f, .38f).RightFoot(.1f, .44f).LeftHand(.14f, -.26f).RightHand(.2f, -.22f))
        .Marker("launch", 0).Marker("apex", .3f)
        .Face(0, "determined").Face(.3f, "focused")
        .Build();

    /// <summary>Descending: legs reach for the ground, staggered, and the arms float up nearly straight for balance on a slow 0.6 s cycle.</summary>
    private static MotionClip Fall(ResolvedModel m) => New(m, "player-fall", "Fall", .6f, true)
        .Key(0, k => k.Hips(0, .02f).Chest(.06f).Head(-.1f).LeftFoot(-.12f, .12f).RightFoot(.15f, .26f).LeftHand(-.34f, .5f).RightHand(.4f, .46f))
        .Key(.3f, k => k.Hips(0, .03f).Chest(.08f).Head(-.13f).LeftFoot(-.1f, .17f).RightFoot(.13f, .21f).LeftHand(-.3f, .54f).RightHand(.36f, .5f))
        .Key(.6f, k => k.Hips(0, .02f).Chest(.06f).Head(-.1f).LeftFoot(-.12f, .12f).RightFoot(.15f, .26f).LeftHand(-.34f, .5f).RightHand(.4f, .46f))
        .Face(0, "focused")
        .Build();

    /// <summary>Soft landing: absorb into a deep crouch over planted feet, then rise into the idle's first pose.</summary>
    private static MotionClip Land(ResolvedModel m) => New(m, "player-land", "Land", .36f, false)
        .Key(0, k => k.Hips(-.02f, -.18f).Chest(-.28f).Head(.16f).LeftHand(.24f, -.44f).RightHand(.3f, -.4f))
        .Key(.09f, k => k.Hips(-.02f, -.24f).Chest(-.34f).Head(.2f).LeftHand(.26f, -.4f).RightHand(.32f, -.36f))
        .Key(.36f, k => k.Hips(StanceX, StanceY).Chest(-.03f).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Plant(Humanoid.LeftLeg, 0, .36f, Back).Plant(Humanoid.RightLeg, 0, .36f, Front)
        .Marker("impact", 0)
        .Face(0, "strained").Face(.2f, "relaxed")
        .Build();

    /// <summary>
    /// Gameplay dashes for 0.16 s: snap into a low lunge (body nearly flat, back leg long, arms swept back), hold, and
    /// rise over the next 0.12 s. "dash-end" marks where gameplay stops the dash.
    /// </summary>
    private static MotionClip Dash(ResolvedModel m) => New(m, "player-dash", "Dash", .28f, false)
        .Key(0, k => k.Hips(.02f, -.1f).Chest(-.35f).Head(.18f).LeftFoot(-.25f, .06f).RightFoot(.15f, .05f).LeftHand(-.25f, -.5f).RightHand(-.18f, -.5f))
        .Key(.04f, k => k.Hips(.06f, -.26f).Chest(-.72f).Head(.38f).LeftFoot(-.56f, .12f).RightFoot(.3f, .06f).LeftHand(-.48f, -.32f).RightHand(-.42f, -.28f))
        .Key(.16f, k => k.Hips(.07f, -.25f).Chest(-.68f).Head(.36f).LeftFoot(-.54f, .1f).RightFoot(.31f, .06f).LeftHand(-.5f, -.28f).RightHand(-.44f, -.25f))
        .Key(.28f, k => k.Hips(.02f, -.07f).Chest(-.2f).Head(.1f).LeftFoot(-.2f, .04f).RightFoot(.15f, .03f).LeftHand(-.1f, -.56f).RightHand(.02f, -.56f))
        .Marker("dash-end", .16f)
        .Face(0, "determined")
        .Build();

    // ---- Turning ----------------------------------------------------------------------------------------------------
    // There is no perspective at this size, so turning the body is orthographic: each girdle's near-to-far axis spins in
    // the ground plane, its points slide left and right on screen, and near and far trade depth order on the way. The
    // rest pose has that axis at 60 degrees (pointing right and away); 90 is pure profile; 180 has the back square to
    // the viewer, with the right side on screen left.

    /// <summary>Deltas from rest (x, z) for the four girdle controls at axis angle <paramref name="degrees"/>.</summary>
    private static (Vector2 LeftShoulder, Vector2 RightShoulder, Vector2 LeftHip, Vector2 RightHip) Girdles(ResolvedModel m, float degrees)
    {
        const float shoulderHalf = .18f, hipHalf = .12f, setback = -.025f;
        var a = float.DegreesToRadians(degrees);
        var axis = Polar2D.Direction(a);
        // The shoulders sit slightly behind the torso's centerline, along the facing direction (the axis turned back 90).
        var mid = axis.PerpCw * setback;
        Vector2 Delta(string id, Vector2 at)
        {
            var rest = m.Rest[id];
            return new(at.X - rest.X, at.Y - rest.Z);
        }
        return (Delta(Humanoid.LeftShoulder, mid - axis * shoulderHalf), Delta(Humanoid.RightShoulder, mid + axis * shoulderHalf),
            Delta(Humanoid.LeftHip, -axis * hipHalf), Delta(Humanoid.RightHip, axis * hipHalf));
    }

    /// <summary>
    /// Keys the girdles at an axis angle. <paramref name="facingAway"/> (0..1) brings the chest toward the viewer so the
    /// back-view sheath draws in front of the torso, and settles the hips just in front of the torso box.
    /// </summary>
    private static PoseKey Turned(this PoseKey k, ResolvedModel m, float degrees, float facingAway)
    {
        var (ls, rs, lh, rh) = Girdles(m, degrees);
        var hipDepth = -.05f * facingAway;
        return k.Chest(-.02f * facingAway, 0, 0, -.35f * facingAway)
            .Move(Humanoid.LeftShoulder, ls.X, 0, ls.Y).Move(Humanoid.RightShoulder, rs.X, 0, rs.Y)
            .Move(Humanoid.LeftHip, lh.X, 0, lh.Y + hipDepth).Move(Humanoid.RightHip, rh.X, 0, rh.Y + hipDepth);
    }

    /// <summary>
    /// Turns only the shoulder girdle to an axis angle (rest 60): body rotation into a swing without moving the hips. Lower
    /// angles bring the far (sword) shoulder forward; higher ones wind it back.
    /// </summary>
    internal static PoseKey Shoulders(this PoseKey k, ResolvedModel m, float degrees)
    {
        var (ls, rs, _, _) = Girdles(m, degrees);
        return k.Move(Humanoid.LeftShoulder, ls.X, 0, ls.Y).Move(Humanoid.RightShoulder, rs.X, 0, rs.Y);
    }

    private const float Rail = .36f, RungHigh = 1.95f, RungLow = 1.45f, StepHigh = .6f, StepLow = .05f;

    /// <summary>
    /// Stepping onto the ladder (<paramref name="onto"/>) or off it (the same keys reversed). The girdles sweep from the
    /// rest turn to the back view in 0.15 s, the face gives way to the back of the head once past profile, and the sheath
    /// swaps to the back-view socket as the back comes round; then the hands reach up onto the rails and the right foot
    /// finds a rung, landing exactly on the climb's first pose. Mid-turn the body stands tall with its arms hanging
    /// straight, and that is where the right elbow and knee switch to the climb's mirrored bend, so the switch never shows.
    /// </summary>
    private static MotionClip ClimbTurn(ResolvedModel m, bool onto)
    {
        const float duration = .3f;
        float T(float t) => onto ? t : duration - t;
        var b = New(m, onto ? "player-climb-on" : "player-climb-off", onto ? "Turn onto ladder" : "Turn off ladder", duration, false);
        float[] angles = [60, 84, 108, 132, 156, 180];
        for (var i = 0; i < angles.Length; i++)
        {
            var degrees = angles[i];
            var away = Math.Clamp((degrees - 100) / 60, 0, 1);
            b.Key(T(i * .03f), k => k.Turned(m, degrees, away), ProgressMapKind.Linear);
        }
        for (var step = 1; step <= 5; step++)
        {
            var hand = Vector2.FromPolar(.64f, float.DegreesToRadians(-90 + 22.5f * step));
            b.Key(T(.15f + step * .07f / 6), k => k.LeftHand(hand.X, hand.Y).RightHand(-hand.X, hand.Y), ProgressMapKind.Linear);
        }
        return b
            .Key(T(0), k => k.Hips(StanceX, StanceY).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f).LeftFoot(Back, Ground).RightFoot(Front, Ground), ProgressMapKind.Linear)
            .Key(T(.09f), k => k.Hips(0, .01f).LeftHand(0, -.645f).RightHand(0, -.645f).LeftFoot(.08f, Ground).RightFoot(-.08f, Ground), ProgressMapKind.Linear)
            // Arms stay straight while they swing out and up (a straight arm hides which way its elbow would fold), and
            // bend only once the hands are above the shoulders, where the elbows then point outward.
            .Key(T(.15f), k => k.Hips(0, -.03f).Head(0).LeftHand(.08f, -.64f).RightHand(-.08f, -.64f).LeftFoot(.1f, Ground).RightFoot(-.1f, Ground), ProgressMapKind.Linear)
            .Key(T(.22f), k => k.Hips(0, -.07f).LeftHand(.46f, .43f).RightHand(-.46f, .38f).LeftFoot(.1f, .03f).RightFoot(-.1f, .3f), ProgressMapKind.Linear)
            .Key(T(.3f), k => k.Turned(m, 180, 1).Hips(0, -.1f).Head(0).LeftHandAt(Rail, RungHigh).RightHandAt(-Rail, RungLow).LeftFoot(.1f, StepLow).RightFoot(-.1f, StepHigh), ProgressMapKind.Linear)
            // Profile side (the chains' own bends) before the straight moment, mirrored after; reversed when stepping off.
            .Bend(Humanoid.RightArm, 0, onto ? -1 : 1).Bend(Humanoid.RightLeg, 0, onto ? 1 : -1)
            .Bend(Humanoid.RightArm, T(.09f), onto ? 1 : -1).Bend(Humanoid.RightLeg, T(.09f), onto ? -1 : 1)
            .Marker(onto ? MoveBuilder.ProfileViewMarker : MoveBuilder.BackViewMarker, 0)
            .Marker(onto ? MoveBuilder.BackViewMarker : MoveBuilder.ProfileViewMarker, onto ? .09f : duration - .09f)
            .Face(0, onto ? "relaxed" : "none").Face(onto ? .06f : duration - .06f, onto ? "none" : "relaxed")
            .Build();
    }

    /// <summary>
    /// Seen from behind, the end of the turn onto the ladder: back square to the viewer (right side on screen left), face
    /// hidden, sheath across the back. A hand on each rail and hand over hand, 0.8 s per cycle; a gripping hand or foot
    /// travels down at the ladder's speed while the free ones reach up with a small outward arc. Opposite hand and foot
    /// move together.
    /// </summary>
    private static MotionClip Climb(ResolvedModel m)
    {
        var b = New(m, "player-climb", "Climb", .8f, true);
        foreach (var t in new[] { 0f, .8f })
        {
            b.Key(t, k => k.Turned(m, 180, 1).Hips(0, -.1f).Head(0).LeftHandAt(Rail, RungHigh).RightHandAt(-Rail, RungLow).LeftFoot(.1f, StepLow).RightFoot(-.1f, StepHigh), ProgressMapKind.Linear);
        }

        return b
            .Key(.2f, k => k.Hips(0, -.08f).LeftHandAt(Rail, (RungHigh + RungLow) / 2).RightHandAt(-Rail - .08f, 1.8f).LeftFoot(.16f, .38f).RightFoot(-.1f, (StepHigh + StepLow) / 2), ProgressMapKind.Linear)
            .Key(.4f, k => k.Hips(0, -.1f).LeftHandAt(Rail, RungLow).RightHandAt(-Rail, RungHigh).LeftFoot(.1f, StepHigh).RightFoot(-.1f, StepLow), ProgressMapKind.Linear)
            .Key(.6f, k => k.Hips(0, -.08f).LeftHandAt(Rail + .08f, 1.8f).RightHandAt(-Rail, (RungHigh + RungLow) / 2).LeftFoot(.1f, (StepHigh + StepLow) / 2).RightFoot(-.16f, .38f), ProgressMapKind.Linear)
            // From behind, the right side is on screen left: its elbow and knee must bend outward, so mirror them.
            .Bend(Humanoid.RightArm, 0, 1).Bend(Humanoid.RightLeg, 0, -1)
            .Marker(MoveBuilder.BackViewMarker, 0).Marker("rung-left", .4f).Marker("rung-right", 0)
            .Face(0, "none")
            .Build();
    }

    /// <summary>
    /// Gameplay faces away from the wall while gripping, so the wall is behind (x = -0.3): back and one foot against it,
    /// the near hand braced flat on the wall, the far (weapon) hand free. A slow 1.2 s settle.
    /// </summary>
    private static MotionClip WallGrip(ResolvedModel m)
    {
        var b = New(m, "player-wall-grip", "Wall grip", 1.2f, true);
        // Per the user's sketch: hips and one foot against the wall, torso leaning out in a diagonal, head up and out,
        // the near arm reaching straight back to the wall at shoulder height, the far arm and leg dangling free.
        foreach (var t in new[] { 0f, 1.2f })
        {
            b.Key(t, k => k.Hips(-.08f, -.1f).Chest(-.5f).Head(.3f).LeftHandAt(-.75f, 2.5f).RightHand(.2f, -.5f).LeftFoot(-.4f, .06f).RightFoot(.0f, .2f));
        }

        return b
            .Key(.6f, k => k.Hips(-.08f, -.115f).Chest(-.48f).Head(.28f).LeftHandAt(-.75f, 2.48f).RightHand(.24f, -.46f).LeftFoot(-.4f, .05f).RightFoot(.02f, .18f))
            .Face(0, "strained")
            .Build();
    }

    /// <summary>Windmill: the hands trace circles around their shoulders, opposite in phase, while the chest tips.</summary>
    private static MoveBuilder Windmill(MoveBuilder b, float direction, float lean, float wobble)
    {
        const int steps = 8;
        const float radius = .42f;
        for (var i = 0; i <= steps; i++)
        {
            var t = b.Duration * i / steps;
            var phase = MathF.Tau * i / steps * direction;
            var left = Polar2D.ToCartesian(radius, phase + .4f);
            var right = Polar2D.ToCartesian(radius, phase + .4f + MathF.PI);
            var tip = lean + wobble * MathF.Sin(MathF.Tau * i / steps);
            b.Key(t, k => k.Chest(tip).Head(-.22f - tip * .5f).LeftHand(left.X, left.Y - .1f).RightHand(right.X, right.Y - .1f), ProgressMapKind.Linear);
        }
        return b;
    }

    /// <summary>Edge in front of the toes: weight thrown back, arms windmilling backward, eyes on the drop.</summary>
    private static MotionClip BalanceForward(ResolvedModel m) => Windmill(New(m, "player-balance-forward", "Balance, edge ahead", 1, true), 1, .12f, .08f)
        .Key(0, k => k.Hips(-.06f, -.03f)).Key(.5f, k => k.Hips(-.08f, -.04f)).Key(1, k => k.Hips(-.06f, -.03f))
        .Plant(Humanoid.LeftLeg, 0, 1, -.08f).Plant(Humanoid.RightLeg, 0, 1, .06f)
        .Face(0, "panic")
        .Build();

    /// <summary>Edge behind the heels: leaning forward over the toes, arms windmilling forward.</summary>
    private static MotionClip BalanceBackward(ResolvedModel m) => Windmill(New(m, "player-balance-backward", "Balance, edge behind", 1, true), -1, -.24f, .08f)
        .Key(0, k => k.Hips(.05f, -.04f)).Key(.5f, k => k.Hips(.07f, -.05f)).Key(1, k => k.Hips(.05f, -.04f))
        .Plant(Humanoid.LeftLeg, 0, 1, -.04f).Plant(Humanoid.RightLeg, 0, 1, .1f)
        .Face(0, "worried")
        .Build();

    // ---- Reactions ----------------------------------------------------------------------------------------------

    /// <summary>Snap back from the chest with the arms flung forward, then recover to the idle stance.</summary>
    private static MotionClip Hit(ResolvedModel m) => New(m, "player-hit", "Hit", .35f, false)
        .Key(0, k => k.Hips(StanceX, StanceY).Chest(-.03f).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Key(.05f, k => k.Hips(-.07f, -.06f).Chest(.36f).Head(.3f).LeftHand(.3f, -.22f).RightHand(.36f, -.16f), ProgressMapKind.Linear)
        .Key(.18f, k => k.Hips(-.05f, -.06f).Chest(.14f).Head(.1f).LeftHand(.18f, -.42f).RightHand(.22f, -.38f))
        .Key(.35f, k => k.Hips(StanceX, StanceY).Chest(-.03f).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Plant(Humanoid.LeftLeg, 0, .35f, Back).Plant(Humanoid.RightLeg, 0, .35f, Front)
        .Face(0, "hurt").Face(.24f, "strained")
        .Build();

    /// <summary>
    /// Struck, stagger back a step as the knees go, topple backward onto the back with one small bounce, and lie still
    /// with the arms spread on the ground. Ends held.
    /// </summary>
    private static MotionClip Death(ResolvedModel m) => New(m, "player-death", "Death", 1.6f, false)
        .Key(0, k => k.Hips(StanceX, StanceY).Chest(-.03f).Head(.02f).LeftFoot(Back, Ground).RightFoot(Front, Ground).LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Key(.07f, k => k.Hips(-.08f, -.06f).Chest(.4f).Head(.32f).LeftFoot(Back, Ground).RightFoot(Front, Ground).LeftHand(.32f, -.2f).RightHand(.38f, -.14f), ProgressMapKind.Linear)
        .Key(.34f, k => k.Hips(-.24f, -.18f, .12f).Chest(.3f).Head(.2f).LeftFoot(-.38f, Ground).RightFoot(.02f, .06f).LeftHand(.2f, .05f).RightHand(.28f, .1f))
        .Key(.68f, k => k.Hips(-.46f, -.56f, .85f).Chest(.22f).Head(.14f).LeftFoot(-.1f, .04f).RightFoot(.22f, .2f).LeftHand(.1f, .3f).RightHand(.25f, .28f), ProgressMapKind.Linear)
        .Key(.92f, k => k.Hips(-.6f, -.83f, 1.45f).Chest(.08f).Head(.12f).LeftFoot(.26f, .08f).RightFoot(.33f, .14f).LeftHandAt(-.95f, .2f).RightHandAt(-.7f, .32f), ProgressMapKind.Linear)
        .Key(1.06f, k => k.Hips(-.6f, -.76f, 1.38f).Chest(.12f).Head(.2f).LeftFoot(.28f, .1f).RightFoot(.34f, .17f).LeftHandAt(-.98f, .16f).RightHandAt(-.72f, .3f))
        .Key(1.3f, k => k.Hips(-.6f, -.83f, 1.5f).Chest(.06f).Head(.1f).LeftFoot(.3f, .04f).RightFoot(.36f, .07f).LeftHandAt(-1.02f, .05f).RightHandAt(-.66f, .04f))
        .Key(1.6f, k => k.Hips(-.6f, -.83f, 1.5f).Chest(.06f).Head(.1f).LeftFoot(.3f, .04f).RightFoot(.36f, .07f).LeftHandAt(-1.02f, .05f).RightHandAt(-.66f, .04f))
        .Marker("impact", .92f)
        .Face(0, "hurt").Face(.92f, "knocked-out")
        .Build();

    /// <summary>Crouch, hop with both arms thrown up in a V either side of the head, land and pump twice. Ends held, arms up.</summary>
    private static MotionClip Celebrate(ResolvedModel m) => New(m, "player-celebrate", "Celebrate", 1.4f, false)
        .Key(0, k => k.Hips(StanceX, StanceY).Chest(-.03f).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f).LeftFoot(Back, Ground).RightFoot(Front, Ground))
        .Key(.16f, k => k.Hips(-.01f, -.16f).Chest(-.18f).Head(.1f).LeftHand(-.12f, -.5f).RightHand(-.08f, -.52f).LeftFoot(Back, Ground).RightFoot(Front, Ground))
        .Key(.4f, k => k.Hips(0, .12f).Chest(.06f).Head(.08f).LeftHand(-.32f, .5f).RightHand(.3f, .5f).LeftFoot(-.12f, .18f).RightFoot(.1f, .24f), ProgressMapKind.Linear)
        .Key(.62f, k => k.Hips(-.01f, -.12f).Chest(-.08f).Head(.06f).LeftHand(-.3f, .42f).RightHand(.28f, .42f).LeftFoot(Back, Ground).RightFoot(Front, Ground))
        .Key(.86f, k => k.Hips(StanceX, StanceY).Chest(.03f).Head(.08f).LeftHand(-.32f, .5f).RightHand(.3f, .5f))
        .Key(1.02f, k => k.LeftHand(-.28f, .32f).RightHand(.26f, .32f))
        .Key(1.18f, k => k.LeftHand(-.32f, .5f).RightHand(.3f, .5f))
        .Key(1.4f, k => k.Hips(StanceX, StanceY).Chest(.03f).Head(.08f).LeftHand(-.32f, .49f).RightHand(.3f, .49f))
        .Plant(Humanoid.LeftLeg, 0, .3f, Back).Plant(Humanoid.RightLeg, 0, .3f, Front)
        .Plant(Humanoid.LeftLeg, .62f, 1.4f, Back).Plant(Humanoid.RightLeg, .62f, 1.4f, Front)
        .Face(0, "happy").Face(.3f, "delighted")
        .Build();

    // ---- Sword --------------------------------------------------------------------------------------------------

    /// <summary>
    /// From guard, twirl the blade up and over the back (one continuous turn) and slide it home ("sword-sheathe"), then
    /// drop the hand into the idle. Movement can cut it short once the sword is home.
    /// </summary>
    private static MotionClip Sheathe(ResolvedModel m)
    {
        var (hilt, sheathed) = Hilt(StanceX, StanceY, .02f);
        return New(m, "player-sword-sheathe", "Sword sheathe", .42f, false)
            .Key(0, k => k.Hips(.03f, -.07f).Chest(-.12f).Head(.08f).RightHandAt(.34f, 1.2f).LeftHand(-.08f, -.54f).Blade(-.22f))
            .Key(.12f, k => k.Hips(.01f, -.05f).Chest(-.02f).Head(.06f).RightHandAt(.14f, 2.0f).LeftHand(0, -.58f).Blade(1.6f), ProgressMapKind.Linear)
            .Key(.22f, k => k.Hips(StanceX, StanceY).Chest(.04f).Head(.04f).RightHandAt(-.12f, 2.1f).LeftHand(.04f, -.6f).Blade(3.5f), ProgressMapKind.Linear)
            .Key(.3f, k => k.Hips(StanceX, StanceY).Chest(.02f).Head(.02f).RightHandAt(hilt.X, hilt.Y).LeftHand(.05f, -.6f).Blade(sheathed + MathF.Tau))
            .Key(.42f, k => k.Hips(StanceX, StanceY).Chest(-.03f).Head(.02f).RightHand(.08f, -.585f).LeftHand(.05f, -.6f).Blade(sheathed + MathF.Tau))
            .Plant(Humanoid.LeftLeg, 0, .42f, Back).Plant(Humanoid.RightLeg, 0, .42f, Front)
            .Marker("sword-sheathe", .3f)
            .Face(0, "focused").Face(.3f, "relaxed")
            .Build();
    }

    /// <summary>
    /// Airborne downward stab. Gameplay damages from frame one (0.25 s, damage to 0.083 s), so the draw is a two-frame
    /// whip from the back straight to point-down, both hands on the hilt, legs tucked clear. Ends drawn.
    /// </summary>
    private static MotionClip DownAttack(ResolvedModel m)
    {
        var (hilt, sheathed) = Hilt(0, .04f, .0f);
        return New(m, "player-sword-down-attack", "Sword downward stab", .25f, false)
            .Key(0, k => k.Hips(0, .04f).Chest(0).Head(0).RightHandAt(hilt.X, hilt.Y).LeftHand(.1f, -.4f).Blade(sheathed).LeftFoot(-.14f, .36f).RightFoot(.1f, .42f), ProgressMapKind.Linear)
            .Key(.04f, k => k.Hips(0, .06f).Chest(-.2f).Head(-.16f).RightHandAt(.16f, 1.08f).LeftHandAt(.14f, 1.16f).Blade(-MathF.PI / 2).LeftFoot(-.18f, .52f).RightFoot(.08f, .56f))
            .Key(.18f, k => k.Hips(0, .06f).Chest(-.22f).Head(-.18f).RightHandAt(.16f, 1.06f).LeftHandAt(.14f, 1.14f).Blade(-MathF.PI / 2).LeftFoot(-.18f, .54f).RightFoot(.08f, .58f))
            .Key(.25f, k => k.Hips(0, .05f).Chest(-.12f).Head(-.08f).RightHandAt(.2f, 1.14f).LeftHand(.06f, -.5f).Blade(-1.25f).LeftFoot(-.15f, .42f).RightFoot(.1f, .46f))
            .Marker("sword-draw", 0).Marker("strike", .02f).Marker("recover", .083f)
            .Face(0, "determined")
            .Build();
    }

    // ---- Gun ----------------------------------------------------------------------------------------------------

    /// <summary>Standing aim: far arm straight out at shoulder height, pistol level, the rest of the body breathing on 2 s.</summary>
    private static MotionClip GunAim(ResolvedModel m) => New(m, "player-gun-aim", "Gun aim", 2, true)
        .Key(0, k => k.Hips(-.02f, StanceY).Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0))
        .Key(1, k => k.Hips(-.02f, StanceY - .008f).Chest(.015f).Head(.01f).RightHand(.645f, -.03f).LeftHand(.01f, -.61f).Blade(.01f))
        .Key(2, k => k.Hips(-.02f, StanceY).Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0))
        .Plant(Humanoid.LeftLeg, 0, 2, Back).Plant(Humanoid.RightLeg, 0, 2, Front)
        .Face(0, "focused")
        .Build();

    /// <summary>Upper body only, so it layers on aim, walk or run legs: the muzzle kicks up on "fire" and settles in 0.2 s.</summary>
    private static MotionClip GunShot(ResolvedModel m) => New(m, "player-gun-shot", "Gun shot (upper body)", .2f, false)
        .Key(0, k => k.Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0), ProgressMapKind.Linear)
        .Key(.03f, k => k.Chest(.06f).Head(.05f).RightHand(.54f, 0).LeftHand(.01f, -.6f).Blade(.38f))
        .Key(.2f, k => k.Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0))
        .Marker("fire", 0)
        .Face(0, "determined").Face(.14f, "focused")
        .Build();

    /// <summary>Shooting from the wall grip: the free far arm snaps out away from the wall and kicks on "fire".</summary>
    private static MotionClip GunWallShot(ResolvedModel m) => New(m, "player-gun-wall-shot", "Gun wall shot", .3f, false)
        .Key(0, k => k.Hips(-.1f, -.2f).Chest(.08f).Head(-.02f).LeftHandAt(-.31f, 1.3f).RightHand(.645f, -.02f).LeftFoot(-.3f, .38f).RightFoot(-.18f, .06f).Blade(0), ProgressMapKind.Linear)
        .Key(.03f, k => k.Chest(.13f).Head(.02f).RightHand(.52f, .01f).Blade(.4f))
        .Key(.3f, k => k.Hips(-.1f, -.2f).Chest(.08f).Head(-.02f).LeftHandAt(-.31f, 1.3f).RightHand(.645f, -.02f).LeftFoot(-.3f, .38f).RightFoot(-.18f, .06f).Blade(0))
        .Marker("fire", 0)
        .Face(0, "determined")
        .Build();

    // ---- Props --------------------------------------------------------------------------------------------------

    public static Part SwordProp() => CartoonSwordArt();
    public static Part SheathProp() => SheathArt();
    public static Part PistolProp() => PistolArt();
}
