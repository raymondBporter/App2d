using App2d.Core.Animation;
using App2d.Core.Characters.Skeletons;
using App2d.Core.Geometry.Shapes;
using App2d.Gameplay.World;

namespace App2d.Gameplay.Characters;

/// <summary>How a flying enemy flies: the height it hovers above its target, and its speeds when cruising, diving and skimming low.</summary>
public sealed record EnemyFlight(float HoverHeight = 3.2f, float Speed = 3, float DiveSpeed = 10, float LowSpeed = .8f);

/// <summary>How an enemy moves and picks its fights. Lengths are in its character's units and speeds in those units per second.</summary>
public sealed record EnemyControl
{
    public float WalkSpeed { get; init; } = 1.5f;
    /// <summary>How near its target must be, sideways and in height, before it attacks.</summary>
    public float Range { get; init; } = 1.5f;
    public float VerticalRange { get; init; } = 2;
    /// <summary>The wait after an attack before the next.</summary>
    public float Cooldown { get; init; } = 1;
    /// <summary>Stops at ledges and walls instead of walking off or into them.</summary>
    public bool RespectTerrain { get; init; }
    /// <summary>Backs away once when its target comes this near, for this long, then waits.</summary>
    public float RetreatRange { get; init; }
    public float RetreatSeconds { get; init; } = .35f;
    public float RetreatPause { get; init; } = .65f;
    /// <summary>The speed it rushes at during its attack, between the action's "rush" and "brake" cues, or these times without them.</summary>
    public float ChargeSpeed { get; init; }
    /// <summary>Starts a separate charge action outside melee range and within this distance. Zero uses the attack's own motion.</summary>
    public float ChargeRange { get; init; }
    public float ChargeStartSeconds { get; init; }
    public float ChargeEndSeconds { get; init; }
    public float BrakeSeconds { get; init; } = .3f;
    public EnemyFlight? Flight { get; init; }
}

/// <summary>A kind of enemy: the character it is, how it behaves and how much it can take.</summary>
public sealed record EnemyType(Combatant Character, EnemyControl Control)
{
    public int Health { get; init; } = 3;
    /// <summary>A knockback is divided by this.</summary>
    public float KnockbackResistance { get; init; } = 1;
    /// <summary>How long it looks hurt after a hit.</summary>
    public float HurtSeconds { get; init; } = .45f;
    /// <summary>How long a hit staggers it when it has no hit clip.</summary>
    public float StaggerSeconds { get; init; } = .3f;
    /// <summary>The index of the collider it guards with, or -1. The guard is up until the attack's <see cref="GuardOpenCue"/>.</summary>
    public int Guard { get; init; } = -1;
    public string GuardOpenCue { get; init; } = "guard-open";
    public string? BlockSound { get; init; } = "shield-block";
}

/// <summary>
/// The game's cast: the player and every kind of enemy. Each is a skeleton, what is drawn on it, the clips it plays, its
/// actions with their hits, and the shapes that follow it. Lengths are in the skeleton's units, about 1.8 for a standing person.
/// </summary>
public static class GameCharacters
{
    /// <summary>A hit that is open between two of its clip's markers.</summary>
    private static HitWindow Hit(string group, SkeletonClip2D clip, string from, string to, int collider, Strike strike) =>
        new(group, clip.Marker(from)!.Value, clip.Marker(to)!.Value, collider, strike);

    /// <summary>Something that happens at one of its clip's markers.</summary>
    private static ActionCue Cue(string name, SkeletonClip2D clip, string marker, string? sound = null) => new(name, clip.Marker(marker)!.Value, sound);

    private static Rectangle2D Box(float width, float height, Vector2f center = default) => Rectangle2D.Builder.FromSize(width, height, center);

    /// <summary>How much damage the player can take.</summary>
    public const int HeroHealth = 30;

    /// <summary>The player: a person with a sword, a pistol and short hair. Its sword swings chain one into the next.</summary>
    public static Combatant Hero { get; } = CreateHero();

    private static Combatant CreateHero()
    {
        var character = new Combatant
        {
            Name = "hero",
            Skeleton = PersonSkeleton.Skeleton,
            Body = PersonBody.Standard,
            Face = new(PersonFace.Face, PersonSkeleton.HeadArt, 1, PersonFace.Layer),
            Worn =
            [
                new("sword", PersonSkeleton.SwordHand, PersonWardrobe.Sword(PersonSkeleton.SwordHand)),
                new("pistol", PersonSkeleton.GunHand, PersonWardrobe.Pistol(PersonSkeleton.GunHand), new(.26f, .06f)),
                new("hair-short", PersonSkeleton.HeadArt, PersonWardrobe.HairShort(PersonSkeleton.HeadArt), Behind: PersonWardrobe.HairShortBack(PersonSkeleton.HeadArt)),
            ],
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = PersonClips.PlayerIdle,
                ["walk"] = PersonClips.PersonWalk,
                ["run"] = PersonClips.PersonRun,
                ["jump"] = PersonClips.PlayerJump,
                ["hit"] = PersonClips.PlayerHit,
                ["death"] = PersonClips.PlayerDeath,
                ["fall"] = PersonClips.PlayerFall,
                ["land"] = PersonClips.PlayerLand,
                ["dash"] = PersonClips.PlayerDash,
                ["climb-on"] = PersonClips.PlayerClimbOn,
                ["climb"] = PersonClips.PlayerClimb,
                ["climb-off"] = PersonClips.PlayerClimbOff,
                ["wall-grip"] = PersonClips.PlayerWallGrip,
                ["balance-forward"] = PersonClips.PlayerBalanceForward,
                ["balance-backward"] = PersonClips.PlayerBalanceBackward,
                ["celebrate"] = PersonClips.PlayerCelebrate,
                ["sheathe"] = PersonClips.PlayerSwordSheathe,
                ["down-attack"] = PersonClips.PlayerSwordDownAttack,
                ["gun-aim"] = PersonClips.PlayerGunAim,
                ["gun-shot"] = PersonClips.PlayerGunShot,
                ["gun-wall-shot"] = PersonClips.PlayerGunWallShot,
                ["gun-charge"] = PersonClips.PlayerGunCharge,
                ["heal-gather"] = PersonClips.PlayerHealGather,
            },
            Actions =
            [
                new("attack", PersonClips.PlayerSwordSideCut) { Hits = [Hit("blade", PersonClips.PlayerSwordSideCut, "strike", "recover", 0, new(2, Sword: true))], Cues = [Cue("swing", PersonClips.PlayerSwordSideCut, "strike", "swing")], Next = "follow-up", Recovery = PersonClips.PlayerSwordPutAway },
                new("follow-up", PersonClips.PlayerSwordBackhand) { Hits = [Hit("blade", PersonClips.PlayerSwordBackhand, "strike", "recover", 1, new(2, Sword: true))], Cues = [Cue("swing", PersonClips.PlayerSwordBackhand, "strike", "swing")], Next = "forehand", Recovery = PersonClips.PlayerSwordPutAwayBackhand },
                new("forehand", PersonClips.PlayerSwordForehand) { Hits = [Hit("blade", PersonClips.PlayerSwordForehand, "strike", "recover", 2, new(2, Sword: true))], Cues = [Cue("swing", PersonClips.PlayerSwordForehand, "strike", "swing")], Next = "follow-up", Recovery = PersonClips.PlayerSwordPutAway },
                new("shoot", PersonClips.PlayerGunShot) { Cues = [Cue("fire", PersonClips.PlayerGunShot, "fire", "shot")], Projectile = new(new(.75f, .25f), new(2), Speed: 31, Lifetime: 1.5f) },
                new("wall-shot", PersonClips.PlayerGunWallShot) { Cues = [Cue("fire", PersonClips.PlayerGunWallShot, "fire", "shot")], Projectile = new(new(.75f, .25f), new(2), Speed: 31, Lifetime: 1.5f) },
            ],
            Movement = Box(.55f, 1.705f, new(0, .8525f)),
            Colliders =
            [
                CharacterCollider.OnBody("attack-blade", ColliderUse.Attack, Box(1.274f, .667f), new(1.143f, .874f)),
                CharacterCollider.OnBody("follow-up-blade", ColliderUse.Attack, Box(1.342f, .559f), new(.937f, .946f)),
                CharacterCollider.OnBody("forehand-blade", ColliderUse.Attack, Box(1.524f, .667f), new(1.018f, .874f)),
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(.46f, .93f), PersonSkeleton.Hips, new(-.02165f, .325f)),
                CharacterCollider.OnJoint("head", ColliderUse.Hurt, Box(.6f, .6f), PersonSkeleton.Head),
                CharacterCollider.OnJoint("legs", ColliderUse.Hurt, Box(.332f, .92f), PersonSkeleton.LeftHip, new(.096f, -.39f)),
            ],
        };
        return character;
    }

    /// <summary>A tall, thin guard who thrusts a spear.</summary>
    public static EnemyType SpearGuard { get; } = CreateSpearGuard();

    private static EnemyType CreateSpearGuard()
    {
        var skeleton = PersonSkeleton.Build(1.2f, 1.12f, 1.15f, .8f);
        var character = new Combatant
        {
            Name = "spear-guard",
            Skeleton = skeleton,
            Body = PersonBody.Build(1.12f, .8f, .92f, shirt: "#d9e2ef"),
            Face = new(PersonFace.Face, PersonSkeleton.HeadArt, .92f, PersonFace.Layer),
            Worn =
            [
                new("spear", PersonSkeleton.RightGrip, PersonWardrobe.Spear(PersonSkeleton.RightGrip)),
            ],
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = PersonClips.PersonIdle,
                ["walk"] = PersonClips.PersonWalk,
                ["hit"] = PersonClips.PersonHit,
                ["death"] = PersonClips.PersonDeath,
            },
            Actions =
            [
                new("attack", PersonClips.PersonThrust) { Hits = [Hit("spear-tip", PersonClips.PersonThrust, "strike", "recover", 0, new())], Cues = [Cue("swing", PersonClips.PersonThrust, "strike", "swing")] },
            ],
            Movement = Box(.5f, 1.866f, new(0, .933f)),
            Colliders =
            [
                CharacterCollider.OnMount("spear-tip", ColliderUse.Attack, Box(.42f, .26f), PersonSkeleton.RightGrip, new(1.24f, 0)),
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(.424f, 1.008f), PersonSkeleton.Hips, new(-.01732f, .364f)),
                CharacterCollider.OnJoint("head", ColliderUse.Hurt, Box(.6f, .6f), PersonSkeleton.Head),
                CharacterCollider.OnJoint("legs", ColliderUse.Hurt, Box(.3224f, 1.076f), PersonSkeleton.LeftHip, new(.0912f, -.468f)),
                CharacterCollider.OnMount("reach", ColliderUse.Trigger, Circle2D.Builder.Create(.35f), PersonSkeleton.RightGrip, new(1.38f, 0)),
            ],
        };
        return new(character, new() { WalkSpeed = .9f, Range = 2.1f, Cooldown = 1.1f });
    }

    /// <summary>A three-legged pest that lunges with its stinger.</summary>
    public static EnemyType StalkerPest { get; } = CreateStalkerPest();

    private static EnemyType CreateStalkerPest()
    {
        var character = new Combatant
        {
            Name = "stalker-pest",
            Skeleton = StalkerSkeleton.Skeleton,
            Body = StalkerBody.Body,
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = StalkerClips.StalkerIdle,
                ["walk"] = StalkerClips.StalkerWalk,
                ["hit"] = StalkerClips.StalkerHit,
                ["death"] = StalkerClips.StalkerDeath,
            },
            Actions =
            [
                new("attack", StalkerClips.StalkerLunge) { Hits = [Hit("sting", StalkerClips.StalkerLunge, "strike", "recover", 0, new())], Cues = [Cue("bite", StalkerClips.StalkerLunge, "strike", "bite")] },
            ],
            Movement = Box(1, 1.1f, new(0, .55f)),
            Colliders =
            [
                CharacterCollider.OnMount("sting", ColliderUse.Attack, Box(.3f, .3f), StalkerSkeleton.Stinger),
                CharacterCollider.OnJoint("shell", ColliderUse.Hurt, Box(1.1f, .5f), StalkerSkeleton.Body, new(0, -.05f)),
                CharacterCollider.OnJoint("legs", ColliderUse.Hurt, Box(.95f, .5f), StalkerSkeleton.Knee0, new(.275f, -.2f)),
            ],
        };
        return new(character, new() { WalkSpeed = .6f, Range = 1.05f, Cooldown = 1.4f }) { Health = 2 };
    }

    /// <summary>A gunner in a spotted hide who shoots a pistol.</summary>
    public static EnemyType CinderGunner { get; } = CreateCinderGunner();

    private static EnemyType CreateCinderGunner()
    {
        var skeleton = PersonSkeleton.Build(.9f, .95f, .95f, 1.05f);
        var character = new Combatant
        {
            Name = "cinder-gunner",
            Skeleton = skeleton,
            Body = PersonBody.Build(.95f, 1.05f, 1.05f, skin: "#f3dcc6", shirt: "#b97549", spottedHide: true),
            Face = new(PersonFace.Face, PersonSkeleton.HeadArt, 1.05f, PersonFace.Layer, "smug"),
            Worn =
            [
                new("pistol", PersonSkeleton.GunHand, PersonWardrobe.Pistol(PersonSkeleton.GunHand), new(.26f, .06f)),
                new("hide-wrap", PersonSkeleton.BodyArt, PersonWardrobe.HideWrap(PersonSkeleton.BodyArt, PersonWardrobe.HideRusset)),
                new("hair", PersonSkeleton.HeadArt, PersonWardrobe.WildHair(PersonSkeleton.HeadArt, PersonWardrobe.HairRusset, large: true)),
                new("beard", PersonSkeleton.HeadArt, PersonWardrobe.CavemanBeard(PersonSkeleton.HeadArt, PersonWardrobe.HairRusset, large: true)),
                new("sandal-left", PersonSkeleton.LeftFootArt, PersonWardrobe.Sandal(PersonSkeleton.LeftFootArt)),
                new("sandal-right", PersonSkeleton.RightFootArt, PersonWardrobe.Sandal(PersonSkeleton.RightFootArt)),
            ],
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = PersonClips.PersonIdle,
                ["walk"] = PersonClips.PersonWalk,
                ["run"] = PersonClips.PersonRun,
                ["jump"] = PersonClips.PersonJump,
                ["hit"] = PersonClips.PersonHit,
                ["death"] = PersonClips.PersonDeath,
            },
            Actions =
            [
                new("attack", PersonClips.PersonPistolShot) { Cues = [Cue("fire", PersonClips.PersonPistolShot, "fire", "shot")], Projectile = new(new(.3f, .12f), new(2)) },
            ],
            Movement = Box(.353f, 2.01f, new(0, 1.005f)),
            Colliders =
            [
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(.469f, .8975f), PersonSkeleton.Hips, new(-.02273f, .30875f)),
                CharacterCollider.OnJoint("head", ColliderUse.Hurt, Box(.6f, .6f), PersonSkeleton.Head),
                CharacterCollider.OnJoint("legs", ColliderUse.Hurt, Box(.3308f, .842f), PersonSkeleton.LeftHip, new(.0954f, -.351f)),
            ],
        };
        return new(character, new() { WalkSpeed = 1.4f, Range = 4.5f }) { Health = 7 };
    }

    /// <summary>A broad brute who slams a hammer.</summary>
    public static EnemyType MaulBrute { get; } = CreateMaulBrute();

    private static EnemyType CreateMaulBrute()
    {
        var skeleton = PersonSkeleton.Build(.95f, 1.05f, 1.1f, 1.45f);
        var character = new Combatant
        {
            Name = "maul-brute",
            Skeleton = skeleton,
            Body = PersonBody.Build(1.05f, 1.45f, .95f, skin: "#ead6c1", shirt: "#ead6c1"),
            Face = new(PersonFace.Face, PersonSkeleton.HeadArt, .95f, PersonFace.Layer, "angry"),
            Worn =
            [
                new("hammer", PersonSkeleton.SwordHand, PersonWardrobe.Hammer(PersonSkeleton.SwordHand)),
                new("hide-wrap", PersonSkeleton.BodyArt, PersonWardrobe.HideWrap(PersonSkeleton.BodyArt, PersonWardrobe.HideOchre, wide: true)),
                new("hair", PersonSkeleton.HeadArt, PersonWardrobe.WildHair(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown)),
                new("beard", PersonSkeleton.HeadArt, PersonWardrobe.CavemanBeard(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown)),
                new("boot-left", PersonSkeleton.LeftFootArt, PersonWardrobe.Boot(PersonSkeleton.LeftFootArt)),
                new("boot-right", PersonSkeleton.RightFootArt, PersonWardrobe.Boot(PersonSkeleton.RightFootArt)),
            ],
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = PersonClips.PersonIdle,
                ["walk"] = PersonClips.PersonHeavyWalk,
                ["run"] = PersonClips.PersonRun,
                ["hit"] = PersonClips.PersonHit,
                ["death"] = PersonClips.PersonDeath,
            },
            Actions =
            [
                new("attack", PersonClips.PersonHammerSlam) { Hits = [Hit("hammer-head", PersonClips.PersonHammerSlam, "strike", "recover", 0, new(5, "heavy"))], Cues = [Cue("heavy", PersonClips.PersonHammerSlam, "strike", "heavy")] },
            ],
            Movement = Box(.319f, 2.123f, new(0, 1.0615f)),
            Colliders =
            [
                CharacterCollider.OnMount("hammer-head", ColliderUse.Attack, Box(1.2f, 1), PersonSkeleton.SwordHand, new(.8f, 0)),
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(.541f, .9625f), PersonSkeleton.Hips, new(-.03139f, .34125f)),
                CharacterCollider.OnJoint("head", ColliderUse.Hurt, Box(.6f, .6f), PersonSkeleton.Head),
                CharacterCollider.OnJoint("legs", ColliderUse.Hurt, Box(.3824f, .881f), PersonSkeleton.LeftHip, new(.1212f, -.3705f)),
            ],
        };
        return new(character, new() { WalkSpeed = 1.1f, Range = 1.3f }) { Health = 22, KnockbackResistance = 3 };
    }

    /// <summary>A caveman who runs in and slams a club.</summary>
    public static EnemyType ClubCaveman { get; } = CreateClubCaveman();

    private static EnemyType CreateClubCaveman()
    {
        var skeleton = PersonSkeleton.Build(.9f, .95f, .95f, 1.05f);
        var character = new Combatant
        {
            Name = "club-caveman",
            Skeleton = skeleton,
            Body = PersonBody.Build(.95f, 1.05f, 1.05f, skin: "#f3dcc6", shirt: "#d7a348", spottedHide: true),
            Face = new(PersonFace.Face, PersonSkeleton.HeadArt, 1.05f, PersonFace.Layer, "smug"),
            Worn =
            [
                new("wooden-club", PersonSkeleton.SwordHand, PersonWardrobe.WoodenClub(PersonSkeleton.SwordHand)),
                new("hide-wrap", PersonSkeleton.BodyArt, PersonWardrobe.HideWrap(PersonSkeleton.BodyArt, PersonWardrobe.HideOchre)),
                new("hair", PersonSkeleton.HeadArt, PersonWardrobe.WildHair(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown)),
                new("beard", PersonSkeleton.HeadArt, PersonWardrobe.CavemanBeard(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown)),
            ],
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = PersonClips.PersonIdle,
                ["walk"] = PersonClips.PersonRun,
                ["run"] = PersonClips.PersonRun,
                ["jump"] = PersonClips.PersonJump,
                ["hit"] = PersonClips.PersonHit,
                ["death"] = PersonClips.PersonDeath,
            },
            Actions =
            [
                new("attack", PersonClips.ClubCavemanSlam) { Hits = [Hit("club-head", PersonClips.ClubCavemanSlam, "strike", "recover", 0, new(3, "heavy"))], Cues = [Cue("club-lift", PersonClips.ClubCavemanSlam, "windup", "club-windup"), Cue("heavy", PersonClips.ClubCavemanSlam, "strike", "heavy")] },
            ],
            Movement = Box(.353f, 2.01f, new(0, 1.005f)),
            Colliders =
            [
                CharacterCollider.OnMount("club-head", ColliderUse.Attack, Box(.7f, .65f), PersonSkeleton.SwordHand, new(.98f, 0)),
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(.469f, .8975f), PersonSkeleton.Hips, new(-.02273f, .30875f)),
                CharacterCollider.OnJoint("head", ColliderUse.Hurt, Box(.6f, .6f), PersonSkeleton.Head),
                CharacterCollider.OnJoint("legs", ColliderUse.Hurt, Box(.3308f, .842f), PersonSkeleton.LeftHip, new(.0954f, -.351f)),
            ],
        };
        return new(character, new() { WalkSpeed = 2.5f, Range = 1.15f, VerticalRange = .75f, Cooldown = .35f, RespectTerrain = true }) { Health = 9 };
    }

    /// <summary>A caveman who keeps his distance and lobs rocks from a bag.</summary>
    public static EnemyType RockThrower { get; } = CreateRockThrower();

    private static EnemyType CreateRockThrower()
    {
        var skeleton = PersonSkeleton.Build(.9f, .95f, 1.05f, 1.05f);
        var character = new Combatant
        {
            Name = "rock-thrower",
            Skeleton = skeleton,
            Body = PersonBody.Build(.95f, 1.05f, 1.05f, skin: "#f3dcc6", shirt: "#f3dcc6"),
            Face = new(PersonFace.Face, PersonSkeleton.HeadArt, 1.05f, PersonFace.Layer, "smug"),
            Worn =
            [
                new("throwing-rock", PersonSkeleton.SwordHand, PersonWardrobe.ThrowingRock(PersonSkeleton.SwordHand), new(0, .17f)),
                new("hide-wrap", PersonSkeleton.BodyArt, PersonWardrobe.HideWrap(PersonSkeleton.BodyArt, PersonWardrobe.HideRusset)),
                new("rock-thrower-bag", PersonSkeleton.BodyArt, PersonWardrobe.RockThrowerBag(PersonSkeleton.BodyArt)),
                new("hair", PersonSkeleton.HeadArt, PersonWardrobe.WildHair(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown, large: true)),
                new("beard", PersonSkeleton.HeadArt, PersonWardrobe.CavemanBeard(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown, large: true)),
            ],
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = PersonClips.PersonIdle,
                ["walk"] = PersonClips.PersonRun,
                ["run"] = PersonClips.PersonRun,
                ["jump"] = PersonClips.PersonJump,
                ["hit"] = PersonClips.PersonHit,
                ["death"] = PersonClips.PersonDeath,
            },
            Actions =
            [
                new("attack", PersonClips.RockThrowerThrow) { Cues = [Cue("bag-search", PersonClips.RockThrowerThrow, "search", "rock-search"), Cue("fire", PersonClips.RockThrowerThrow, "release", "rock-throw")], Projectile = new(new(.36f, .36f), new(2), Gravity: 15, FlightSeconds: .85f) },
            ],
            Movement = Box(.353f, 2.01f, new(0, 1.005f)),
            Colliders =
            [
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(.469f, .8975f), PersonSkeleton.Hips, new(-.02273f, .30875f)),
                CharacterCollider.OnJoint("head", ColliderUse.Hurt, Box(.6f, .6f), PersonSkeleton.Head),
                CharacterCollider.OnJoint("legs", ColliderUse.Hurt, Box(.3308f, .842f), PersonSkeleton.LeftHip, new(.0954f, -.351f)),
            ],
        };
        return new(character, new() { WalkSpeed = 1.8f, Range = 7, VerticalRange = 3, Cooldown = .7f, RetreatRange = 2, RespectTerrain = true }) { Health = 6 };
    }

    /// <summary>A baby triceratops that lowers its head and charges.</summary>
    public static EnemyType BabyTriceratops { get; } = CreateBabyTriceratops();

    private static EnemyType CreateBabyTriceratops()
    {
        var character = new Combatant
        {
            Name = "baby-triceratops",
            Skeleton = QuadrupedSkeleton.Baby,
            Body = QuadrupedBodies.BabyTriceratops,
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = BabyTriceratopsClips.BabyTriceratopsIdle,
                ["walk"] = BabyTriceratopsClips.BabyTriceratopsWalk,
                ["run"] = BabyTriceratopsClips.BabyTriceratopsRun,
                ["scrape"] = BabyTriceratopsClips.BabyTriceratopsScrape,
                ["head-down"] = BabyTriceratopsClips.BabyTriceratopsHeadDown,
                ["rush"] = BabyTriceratopsClips.BabyTriceratopsRush,
                ["brake"] = BabyTriceratopsClips.BabyTriceratopsBrake,
                ["recover"] = BabyTriceratopsClips.BabyTriceratopsRecover,
                ["hit"] = BabyTriceratopsClips.BabyTriceratopsHit,
                ["death"] = BabyTriceratopsClips.BabyTriceratopsDeath,
            },
            Actions =
            [
                new("attack", BabyTriceratopsClips.BabyTriceratopsCharge) { Hits = [Hit("horns", BabyTriceratopsClips.BabyTriceratopsCharge, "rush", "brake", 0, new(3, "hit"))], Cues = [Cue("scrape", BabyTriceratopsClips.BabyTriceratopsCharge, "scrape", "charge-scrape"), Cue("rush", BabyTriceratopsClips.BabyTriceratopsCharge, "rush", "charge-rush"), Cue("brake", BabyTriceratopsClips.BabyTriceratopsCharge, "brake", "charge-stop")] },
            ],
            Movement = Box(1.6f, .95f, new(-.1f, .475f)),
            Colliders =
            [
                CharacterCollider.OnMount("horns", ColliderUse.Attack, Box(.75f, .65f), QuadrupedSkeleton.HeadArt),
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(1.169f, .532f), QuadrupedSkeleton.Body, new(.3395f, -.021f)),
            ],
        };
        return new(character, new() { WalkSpeed = 1.2f, Range = 4.5f, VerticalRange = .8f, Cooldown = .7f, ChargeSpeed = 5, ChargeStartSeconds = 1, ChargeEndSeconds = 1.85f, RespectTerrain = true }) { Health = 8, KnockbackResistance = 1.5f };
    }

    /// <summary>A caveman behind a shell shield, who opens his guard only to bash with it.</summary>
    public static EnemyType ShieldDefender { get; } = CreateShieldDefender();

    private static EnemyType CreateShieldDefender()
    {
        var skeleton = PersonSkeleton.Build(.9f, .95f, .95f, 1.05f);
        var character = new Combatant
        {
            Name = "shield-defender",
            Skeleton = skeleton,
            Body = PersonBody.Build(.95f, 1.05f, 1.05f, skin: "#f3dcc6", shirt: "#d7a348", spottedHide: true),
            Face = new(PersonFace.Face, PersonSkeleton.HeadArt, 1.05f, PersonFace.Layer, "smug"),
            Worn =
            [
                new("scavenged-shell", PersonSkeleton.LeftGrip, PersonWardrobe.ScavengedShell(PersonSkeleton.LeftGrip)),
                new("hide-wrap", PersonSkeleton.BodyArt, PersonWardrobe.HideWrap(PersonSkeleton.BodyArt, PersonWardrobe.HideOchre)),
                new("hair", PersonSkeleton.HeadArt, PersonWardrobe.WildHair(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown)),
                new("beard", PersonSkeleton.HeadArt, PersonWardrobe.CavemanBeard(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown)),
            ],
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = PersonClips.ShieldDefenderIdle,
                ["walk"] = PersonClips.ShieldDefenderWalk,
                ["run"] = PersonClips.PersonRun,
                ["jump"] = PersonClips.PersonJump,
                ["hit"] = PersonClips.PersonHit,
                ["death"] = PersonClips.PersonDeath,
            },
            Actions =
            [
                new("attack", PersonClips.ShieldDefenderBash) { Hits = [Hit("shell-bash", PersonClips.ShieldDefenderBash, "strike", "recover", 0, new(2, "hit"))], Cues = [Cue("guard-open", PersonClips.ShieldDefenderBash, "pullback", "shield-pullback"), Cue("bash", PersonClips.ShieldDefenderBash, "strike", "shield-bash")] },
            ],
            Movement = Box(.353f, 2.01f, new(0, 1.005f)),
            Colliders =
            [
                CharacterCollider.OnMount("shell-bash", ColliderUse.Attack, Box(.9f, 1.4f), PersonSkeleton.LeftGrip),
                CharacterCollider.OnMount("guard", ColliderUse.Guard, Box(.9f, 1.4f), PersonSkeleton.LeftGrip),
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(.469f, .8975f), PersonSkeleton.Hips, new(-.02273f, .30875f)),
                CharacterCollider.OnJoint("head", ColliderUse.Hurt, Box(.6f, .6f), PersonSkeleton.Head),
                CharacterCollider.OnJoint("legs", ColliderUse.Hurt, Box(.3308f, .842f), PersonSkeleton.LeftHip, new(.0954f, -.351f)),
            ],
        };
        return new(character, new() { WalkSpeed = .8f, Range = 1.1f, VerticalRange = .75f, Cooldown = .5f, RespectTerrain = true }) { Health = 8, KnockbackResistance = 1.5f, Guard = 1 };
    }

    /// <summary>A pterosaur that hovers, then swoops.</summary>
    public static EnemyType FlyingPterosaur { get; } = CreateFlyingPterosaur();

    private static EnemyType CreateFlyingPterosaur()
    {
        var character = new Combatant
        {
            Name = "flying-pterosaur",
            Skeleton = PterosaurSkeleton.Skeleton,
            Body = PterosaurBody.Body,
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                ["idle"] = PterosaurClips.PterosaurHover,
                ["hover"] = PterosaurClips.PterosaurHover,
                ["fold"] = PterosaurClips.PterosaurFold,
                ["dive"] = PterosaurClips.PterosaurDive,
                ["brake"] = PterosaurClips.PterosaurBrake,
                ["low-recovery"] = PterosaurClips.PterosaurLowRecovery,
                ["reset"] = PterosaurClips.PterosaurReset,
                ["hit"] = PterosaurClips.PterosaurHit,
                ["death"] = PterosaurClips.PterosaurDeath,
            },
            Actions =
            [
                new("attack", PterosaurClips.PterosaurSwoop) { Hits = [Hit("beak", PterosaurClips.PterosaurSwoop, "rush", "brake", 0, new(2, "hit"))], Cues = [Cue("screech", PterosaurClips.PterosaurSwoop, "screech", "pterosaur-screech"), Cue("commit", PterosaurClips.PterosaurSwoop, "commit"), Cue("rush", PterosaurClips.PterosaurSwoop, "rush", "pterosaur-rush"), Cue("brake", PterosaurClips.PterosaurSwoop, "brake", "pterosaur-brake"), Cue("low", PterosaurClips.PterosaurSwoop, "low"), Cue("reset", PterosaurClips.PterosaurSwoop, "reset", "pterosaur-reset")] },
            ],
            Movement = Box(.85f, .6f, new(0, .7f)),
            Colliders =
            [
                CharacterCollider.OnMount("beak", ColliderUse.Attack, Box(.65f, .5f), PterosaurSkeleton.HeadArt),
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(.62f, .37f), PterosaurSkeleton.Body, new(.15f, .025f)),
            ],
        };
        return new(character, new() { Range = 3.2f, Cooldown = .8f, Flight = new() }) { Health = 4, KnockbackResistance = .8f };
    }

    public static EnemyType CentaurBoss { get; } = CentaurBoss2D.Create();

    /// <summary>The Centaur enemy type used by existing placements.</summary>
    public static EnemyType StandingCentaur => CentaurBoss;

    /// <summary>The kind of enemy a thing placed in a level is, or null when that thing is not one of these.</summary>
    public static EnemyType? Enemy(WorldThingKind2D kind) => kind switch
    {
        WorldThingKind2D.Shieldback => SpearGuard,
        WorldThingKind2D.GreenDinosaur => StalkerPest,
        WorldThingKind2D.Rival => CinderGunner,
        WorldThingKind2D.BoilerBrute => MaulBrute,
        WorldThingKind2D.ClubCaveman => ClubCaveman,
        WorldThingKind2D.RockThrower => RockThrower,
        WorldThingKind2D.BabyTriceratops => BabyTriceratops,
        WorldThingKind2D.ShieldDefender => ShieldDefender,
        WorldThingKind2D.Pterosaur => FlyingPterosaur,
        WorldThingKind2D.Centaur => CentaurBoss,
        _ => null,
    };

    /// <summary>Every kind of enemy.</summary>
    public static IReadOnlyList<EnemyType> Enemies { get; } = [SpearGuard, StalkerPest, CinderGunner, MaulBrute, ClubCaveman, RockThrower, BabyTriceratops, ShieldDefender, FlyingPterosaur, CentaurBoss];
}
