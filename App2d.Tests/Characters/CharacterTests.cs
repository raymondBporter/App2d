using App2d.Core.Characters;
using App2d.Core.Objects;
using App2d.Core.Rendering;
using App2d.Gameplay.Characters;

namespace App2d.Tests.Characters;

/// <summary>The game's cast and the character code it runs on: posing, drawing, colliders, the animator and joined skeletons.</summary>
public sealed class CharacterTests
{
    private static IEnumerable<Combatant> Cast() => GameCharacters.Enemies.Select(e => e.Character).Prepend(GameCharacters.Hero);

    [Fact]
    public void EveryCharacterPosesAndPaintsThroughEveryClipItPlays()
    {
        foreach (var character in Cast())
        {
            var clips = character.Roles.Values.Concat(character.Actions.Select(a => a.Clip)).Concat(character.Actions.Select(a => a.Recovery).OfType<App2d.Core.Animation.SkeletonClip2D>()).Distinct();
            foreach (var clip in clips)
            {
                for (var step = 0; step <= 4; step++)
                {
                    var pose = CharacterPose.Sample(character.Skeleton, clip, clip.Duration * step / 4);
                    Assert.All(pose.World, joint => Assert.True(float.IsFinite(joint.Translation.X) && float.IsFinite(joint.Translation.Y) && float.IsFinite(joint.Rotation), $"{character.Name}, {clip.Name}"));
                    var mesh = new LayeredMesh();
                    AttachmentPainter.Paint(mesh, pose.Posed, character.Drawn(pose));
                    Assert.True(mesh.Count > 0, $"{character.Name}, {clip.Name} drew nothing.");
                }
            }
            Assert.True(character.Height > .3f, $"{character.Name} has no height.");
        }
    }

    [Fact]
    public void CollidersTurnRoundWithTheCharacter()
    {
        foreach (var character in Cast())
        {
            var pose = CharacterPose.Sample(character.Skeleton, character.Clip(CharacterAnimator.Idle)!, .2);
            var position = new Vector2f(4, 1);
            foreach (var collider in character.Colliders)
            {
                var (right, left) = (collider.Place(pose.At(position, 1), 10), collider.Place(pose.At(position, -1), 10));
                // Facing the other way, a collider is the same distance from the feet on the other side, and flipped.
                Assert.Equal(right.Translation.X - position.X * 10, -(left.Translation.X - position.X * 10), 3);
                Assert.Equal(right.Translation.Y, left.Translation.Y, 3);
                Assert.NotEqual(right.IsMirrored, left.IsMirrored);
            }
        }
    }

    [Fact]
    public void AnAttackReportsEachCueOnceAndOpensItsHitBetweenItsMarkers()
    {
        var character = GameCharacters.ClubCaveman.Character;
        var animator = new CharacterAnimator(character);
        Assert.True(animator.TryStart(CharacterAnimator.Attack));
        Assert.False(animator.TryStart("no-such-action"));
        var attack = animator.Action!;
        var hit = attack.Hits.Single();
        var events = new List<CharacterEvent>();
        var cues = new List<string>();
        var open = 0;
        while (!animator.ActionComplete)
        {
            events.Clear();
            animator.Step(1 / 120f, Vector2f.Zero, 1, CharacterAnimator.Idle, 0, false, events);
            cues.AddRange(events.Where(e => e.IsCue).Select(e => e.Name));
            if (animator.ActiveHits().Any())
            {
                open++;
                Assert.True(animator.ActionTime >= hit.Start && animator.PreviousActionTime < hit.Finish);
            }
        }
        Assert.Equal(attack.Cues.OrderBy(c => c.Seconds).Select(c => c.Name), cues);
        Assert.InRange(open, (int)((hit.Finish - hit.Start) * 120) - 1, (int)((hit.Finish - hit.Start) * 120) + 2);
        animator.EndAction();
        Assert.Null(animator.Action);
    }

    [Fact]
    public void APlantedFootStaysWhereItLandedWhileTheBodyMoves()
    {
        var skeleton = PersonSkeleton.Skeleton;
        var walk = PersonClips.PersonWalk;
        var contact = walk.Contacts[0];
        var time = (contact.Start + contact.Finish) / 2;
        var foot = skeleton.Limbs[contact.Limb].End;
        var planted = new PlantedLimbs();
        var first = planted.Sample(skeleton, walk, time, true, Vector2f.Zero, 1);
        var moved = planted.Sample(skeleton, walk, time, true, new(.08f, 0), 1);
        var free = CharacterPose.Sample(skeleton, walk, time, true).At(new(.08f, 0), 1);

        Vector2f Foot(CharacterPose pose) => pose.Place(pose.World[foot].Translation);
        Assert.True(Vector2f.Distance(Foot(first), Foot(moved)) < 1e-3f, $"The planted foot moved from {Foot(first)} to {Foot(moved)}.");
        Assert.True(Vector2f.Distance(Foot(first), Foot(free)) > .05f);
        planted.Clear();
        Assert.True(Vector2f.Distance(Foot(planted.Sample(skeleton, walk, time, true, new(.08f, 0), 1)), Foot(free)) < 1e-4f);
    }

    [Fact]
    public void TheCentaurIsTheQuadrupedWithoutItsHeadAndThePersonFromTheWaistUp()
    {
        var graft = Centaur.Graft;
        Assert.Equal(SkeletonGraftGone, graft.HostJoints[QuadrupedSkeleton.Head]);
        Assert.Equal(SkeletonGraftGone, graft.GuestJoints[PersonSkeleton.LeftFoot]);
        Assert.Equal(graft.HostJoints[QuadrupedSkeleton.Body], Centaur.Skeleton.Joints[graft.GuestJoints[PersonSkeleton.Hips]].Parent);
        // Four legs under it and two arms on top; the person's legs went with its hips.
        Assert.Equal(6, Centaur.Skeleton.Limbs.Length);
        Assert.DoesNotContain(Centaur.Body, piece => piece.Name is "left-leg" or "right-foot");
        Assert.Contains(Centaur.Body, piece => piece.Name == "left-arm");

        var local = new App2d.Core.Mathematics.Transforms.RigidTransform2D[Centaur.Skeleton.Count];
        Centaur.Pose(Centaur.Horse(QuadrupedClips.QuadrupedWalk), .3f, Centaur.Person(PersonClips.PersonHammerSlam), .9f, local);
        var slam = local[graft.GuestJoints[PersonSkeleton.Chest]];
        Centaur.Pose(Centaur.Horse(QuadrupedClips.QuadrupedWalk), .3f, Centaur.Person(PersonClips.PersonIdle), 0, local);
        // The rider's clip owns the chest; the horse's does not.
        Assert.NotEqual(slam, local[graft.GuestJoints[PersonSkeleton.Chest]]);
    }

    private const int SkeletonGraftGone = App2d.Core.Kinematics.SkeletonGraft2D.Gone;

    [Fact]
    public void FlippingAPieceTwiceGivesItBack()
    {
        var pose = CharacterPose.Rest(PersonSkeleton.Skeleton);
        const int mount = PersonSkeleton.LeftShinArt;
        System.Collections.Immutable.ImmutableArray<Attachment>[] things =
        [
            PersonWardrobe.Boot(mount), PersonWardrobe.Sandal(mount), PersonWardrobe.GloveSteel(mount), PersonWardrobe.CopperGreaves(mount), PersonWardrobe.Sword(mount),
            PersonWardrobe.Pistol(mount), PersonWardrobe.Sheath(mount), PersonWardrobe.CavemanBeard(mount, PersonWardrobe.HairRusset, large: true),
            PersonWardrobe.WildHair(mount, PersonWardrobe.HairBrown), PersonWardrobe.HideWrap(mount, PersonWardrobe.HideOchre, wide: true),
        ];
        foreach (var pieces in things)
        {
            LayeredMesh once = new(), twice = new(), plain = new();
            AttachmentPainter.Paint(plain, pose.Posed, pieces);
            AttachmentPainter.Paint(once, pose.Posed, [.. pieces.Select(p => p.Mirrored())]);
            AttachmentPainter.Paint(twice, pose.Posed, [.. pieces.Select(p => p.Mirrored().Mirrored())]);
            Assert.Equal(plain.Count, once.Count);
            // Flipped, it reaches as far to the other side of its mount; flipped back, it covers what it did.
            Assert.True((plain.Min - twice.Min).Length() < 1e-4f && (plain.Max - twice.Max).Length() < 1e-4f);
        }
    }

    [Fact]
    public void AnObjectFileRoundTripsAndABodyOfSeveralShapesCollidesAsOne()
    {
        var dumbbell = ObjectAsset2D.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "objects", "dumbbell.json"));
        Assert.Equal(3, dumbbell.Shapes.Count);
        Assert.IsType<App2d.Core.Geometry.Shapes.CompositeShape2D>(dumbbell.CollisionShape());
        Assert.Equal(dumbbell.ToJson(), ObjectAsset2D.FromJson(dumbbell.ToJson()).ToJson());
        Assert.Equal(3, new ObjectShader2D(dumbbell).Paints.Length);
        Assert.Throws<InvalidDataException>(() => (dumbbell with { Shapes = [] }).Validate());
    }
}
