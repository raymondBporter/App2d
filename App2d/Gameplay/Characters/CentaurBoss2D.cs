using App2d.Core.Animation;
using App2d.Core.Characters.Attachments;
using App2d.Core.Characters.Skeletons;
using App2d.Core.Geometry.Shapes;

namespace App2d.Gameplay.Characters;

internal static class CentaurBoss2D
{
    private const float RushStart = .55f;
    private const float BrakeStart = 1.15f;

    public static EnemyType Create()
    {
        var graft = Centaur.Graft;
        var spearMount = graft.GuestMounts[PersonSkeleton.RightGrip];
        var thrust = Centaur.Person(PersonClips.PersonThrust);
        var jab = AimThrust(thrust, spearMount, 1f);
        var chargeThrust = AimThrust(thrust, spearMount, 2f);
        var horseScrape = Centaur.Horse(QuadrupedClips.QuadrupedScrape);
        var riderGear = Attachment.Renumbered(
            [.. PersonPlateArmor.Cuirass(PersonSkeleton.BodyArt), .. PersonPlateArmor.Pauldron(PersonSkeleton.RightShoulderArt),
                .. PersonWardrobe.WildHair(PersonSkeleton.HeadArt, PersonWardrobe.HairBrown)],
            graft.GuestJoints, graft.GuestMounts, Centaur.RiderScale);
        var spear = Attachment.Renumbered(PersonWardrobe.Spear(PersonSkeleton.RightGrip), graft.GuestJoints, graft.GuestMounts, Centaur.RiderScale);
        var character = new Combatant
        {
            Name = "centaur",
            Skeleton = Centaur.Skeleton,
            Body = [.. Centaur.Body.Select(PaintCoat), .. riderGear],
            Face = new(PersonFace.Face, graft.GuestMounts[PersonSkeleton.HeadArt], Centaur.RiderScale, PersonFace.Layer, "angry"),
            Worn = [new("spear", spearMount, spear)],
            Roles = new Dictionary<string, SkeletonClip2D>
            {
                [CharacterAnimator.Idle] = Centaur.Idle,
                [CharacterAnimator.Walk] = Centaur.Horse(QuadrupedClips.QuadrupedWalk),
                [CharacterAnimator.ChargeWindup] = horseScrape,
                [CharacterAnimator.ChargeRush] = Centaur.Horse(QuadrupedClips.QuadrupedRush),
                [CharacterAnimator.ChargeBrake] = Centaur.Horse(QuadrupedClips.QuadrupedBrake),
                [CharacterAnimator.Hit] = Centaur.Person(PersonClips.PersonHit),
                [CharacterAnimator.Death] = Collapse(),
            },
            Actions =
            [
                new(CharacterAnimator.Attack, jab)
                {
                    Mask = Centaur.Rider, BlendIn = .12f, BlendOut = .18f, UninterruptibleUntil = jab.Marker("recover")!.Value,
                    Hits = [new("spear", jab.Marker("strike")!.Value, jab.Marker("recover")!.Value, 0, new(4))],
                    Cues = [new("jab", jab.Marker("strike")!.Value, "swing")],
                },
                new(CharacterAnimator.Charge, chargeThrust)
                {
                    Mask = Centaur.Rider, BlendIn = .16f, BlendOut = .38f, UninterruptibleUntil = BrakeStart,
                    Hits = [new("charge", RushStart, BrakeStart, 1, new(6, "heavy")),
                        new("charge", chargeThrust.Marker("strike")!.Value, BrakeStart, 0, new(6, "heavy"))],
                    Cues = [new("scrape", horseScrape.Marker("scrape")!.Value, "charge-scrape"),
                        new("rush", RushStart, "charge-rush"), new("brake", BrakeStart, "charge-stop")],
                },
            ],
            Movement = Box(1.65f, 2.35f, new(-.22f, 1.175f)),
            Colliders =
            [
                CharacterCollider.OnMount("spear-tip", ColliderUse.Attack, Box(.58f, .34f), spearMount,
                    PersonWardrobe.SpearTip * Centaur.RiderScale),
                CharacterCollider.OnBody("hooves", ColliderUse.Attack, Box(.98f, 1f), new(.76f, .53f)),
                CharacterCollider.OnJoint("body", ColliderUse.Hurt, Box(1.82f, .85f), graft.HostJoints[QuadrupedSkeleton.Body]),
                CharacterCollider.OnJoint("rider", ColliderUse.Hurt, Box(.62f, 1.2f), graft.GuestJoints[PersonSkeleton.Hips], new(0, .56f)),
            ],
        };
        return new(character, new()
        {
            WalkSpeed = 2.35f,
            Range = 2.1f,
            VerticalRange = 1.9f,
            RetreatRange = .95f,
            RetreatSeconds = .34f,
            RetreatPause = .13f,
            ChargeRange = 6.2f,
            ChargeSpeed = 7.8f,
            ChargeStartSeconds = RushStart,
            ChargeEndSeconds = BrakeStart,
            BrakeSeconds = .34f,
            Cooldown = .72f,
            RespectTerrain = true,
        }) { Health = 28, KnockbackResistance = 3.4f };
    }

    private static Attachment PaintCoat(Attachment piece) => piece switch
    {
        MountedShape { Material.Fill: "#79bdb0" } horse => horse with
        {
            Material = horse.Material with { Fill = "#b67f61" },
            Patches = [.. horse.Patches.Select(patch => patch with { Fill = "#7b5248" })],
        },
        MountedShape { Material.Fill: "#527f78" } horse => horse with { Material = horse.Material with { Fill = "#775348" } },
        _ => piece,
    };

    // Keep the borrowed arm and chest motion. Aim the spear down from the taller rig and stretch the windup for the rush.
    private static SkeletonClip2D AimThrust(SkeletonClip2D source, int mount, float timeScale) =>
        new(source.Name, source.Duration * timeScale)
        {
            Joints = [.. source.Joints.Select(track => track with { Move = ScaleTime(track.Move, timeScale), Turn = ScaleTime(track.Turn, timeScale) })],
            Limbs = [.. source.Limbs.Select(track => track with { Target = ScaleTime(track.Target, timeScale), Bend = ScaleTime(track.Bend, timeScale) })],
            Mounts = [.. source.Mounts.Where(track => track.Mount != mount)
                .Select(track => track with { Turn = ScaleTime(track.Turn, timeScale)! }),
                new(mount, Track.Of<Vector3f>(new(0f, new(0f, 0f, -.37f)),
                new(.32f * timeScale, new(0f, 0f, -.25f)), new(.4f * timeScale, new(0f, 0f, -.82f)),
                new(.58f * timeScale, new(0f, 0f, -.82f)), new(source.Duration * timeScale, new(0f, 0f, -.37f))))],
            Markers = [.. source.Markers.Select(marker => marker with { Time = marker.Time * timeScale })],
            Expression = ScaleTime(source.Expression, timeScale),
        };

    private static Track<T>? ScaleTime<T>(Track<T>? track, float timeScale) => track is null || timeScale == 1f ? track :
        new(track.Rule, [.. track.Keys.Select(key => key with { Time = key.Time * timeScale })]);

    private static SkeletonClip2D Collapse()
    {
        var rider = Centaur.Person(PersonClips.PersonDeath);
        var body = Centaur.Graft.HostJoints[QuadrupedSkeleton.Body];
        return new("centaur-collapse", rider.Duration)
        {
            Joints = [.. rider.Joints, new(body,
                Move: Track.Of<Vector2f>(new(0f, Vector2f.Zero), new(.22f, new(0f, .04f)), new(.73f, new(.08f, -.52f))),
                Turn: Track.Linear(new(0f, 0f), new(.24f, .24f), new(.73f, -.13f)))],
            Limbs = [.. rider.Limbs],
            Mounts = rider.Mounts,
            Expression = rider.Expression,
        };
    }

    private static Rectangle2D Box(float width, float height, Vector2f center = default) =>
        Rectangle2D.Builder.FromSize(width, height, center);
}
