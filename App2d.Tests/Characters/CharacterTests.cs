using App2d.Core.Characters;
using App2d.Core.Characters.Attachments;
using App2d.Core.Characters.Skeletons;
using App2d.Core.Objects;
using App2d.Core.Rendering;
using App2d.Gameplay.Characters;
using App2d.Presentation.Persons;
using App2d.Presentation.Player;

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
            var clips = character.Roles.Values.Concat(character.Actions.Select(a => a.Clip)).Concat(character.Actions.Select(a => a.Recovery).OfType<Core.Animation.SkeletonClip2D>()).Distinct();
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
    public void TheHeroStartsInPlateAndEachPieceCanReturnToInventory()
    {
        var moves = new PersonMoves(GameCharacters.Hero);
        var wardrobe = new PlayerWardrobe2D(moves);
        var idle = moves[PersonMoves.Idle];
        var pose = CharacterPose.Sample(moves.Skeleton, idle, .5).Showing("relaxed", .5);
        var iconPose = CharacterPose.Rest(PlayerWardrobe2D.IconSkeleton);
        var dressed = wardrobe.Dress(pose, idle, .5f, PersonGear.None);
        var mesh = new LayeredMesh();

        foreach (var slot in Enum.GetValues<WardrobeSlot2D>())
        {
            var item = Assert.IsType<WardrobeItem2D>(wardrobe.Equipped(slot));
            Assert.All(item.Worn, piece => Assert.Contains(piece, dressed));

            mesh.Clear();
            AttachmentPainter.Paint(mesh, iconPose.Posed, item.Icon);
            Assert.True(mesh.Count > 0, $"{item.Name} has no icon geometry.");
            mesh.Clear();
            AttachmentPainter.Paint(mesh, pose.Posed, item.Worn);
            Assert.True(mesh.Count > 0, $"{item.Name} has no worn geometry.");

            Assert.True(wardrobe.Unequip(slot));
            var bareSlot = wardrobe.Dress(pose, idle, .5f, PersonGear.None);
            Assert.All(item.Worn, piece => Assert.DoesNotContain(piece, bareSlot));
            if (slot == WardrobeSlot2D.Head)
            {
                var face = moves.Hero.Face!.Attachments("relaxed", .5);
                var hair = moves.Hero.Worn.Single(item => item.Mount == PersonSkeleton.HeadArt).Pieces;
                Assert.All(face.Concat(hair), piece =>
                {
                    Assert.DoesNotContain(piece, dressed);
                    Assert.Contains(piece, bareSlot);
                });
            }

            var cell = Assert.Single(Enumerable.Range(0, wardrobe.Inventory.Count), i => wardrobe.Inventory[i] == item);
            Assert.True(wardrobe.Equip(cell, slot));
            Assert.Same(item, wardrobe.Equipped(slot));
            Assert.Null(wardrobe.Inventory[cell]);
        }

        var steelGreaves = wardrobe.Equipped(WardrobeSlot2D.Shins);
        var copperCell = Assert.Single(Enumerable.Range(0, wardrobe.Inventory.Count), i => wardrobe.Inventory[i]?.Id == "copper-greaves");
        Assert.True(wardrobe.Equip(copperCell, WardrobeSlot2D.Shins));
        Assert.Equal("copper-greaves", wardrobe.Equipped(WardrobeSlot2D.Shins)?.Id);
        Assert.Same(steelGreaves, wardrobe.Inventory[copperCell]);
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

        var local = new Core.Mathematics.Transforms.RigidTransform2D[Centaur.Skeleton.Count];
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
    public void AFaceKeepsWhatEachExpressionDrawsAndBuildsItAfreshOnlyMidBlink()
    {
        foreach (var character in Cast().Where(c => c.Face is not null))
        {
            var face = character.Face!;
            var rest = CharacterPose.Rest(character.Skeleton);
            foreach (var expression in face.Face.Expressions)
            {
                Assert.True(face.Has(expression.Name));
                // With the eyes open the very same pieces are handed out frame after frame.
                Assert.True(face.Attachments(expression.Name, 1.25) == face.Attachments(expression.Name, 2.5));
                // Kept or built afresh, before, during and after a blink, a face paints the same.
                foreach (var clock in new[] { 0, 1.25, 3.58, 3.62, 3.66, 7.3 })
                {
                    var (kept, fresh) = (new LayeredMesh(4096), new LayeredMesh(4096));
                    AttachmentPainter.Paint(kept, rest.Posed, face.Attachments(expression.Name, clock));
                    AttachmentPainter.Paint(fresh, rest.Posed, face.Face.Attachments(expression, face.Mount, clock, face.Size, face.Layer));
                    Assert.Equal(fresh.Count, kept.Count);
                    for (var i = 0; i < kept.Count; i++) Assert.True((kept.Vertices[i].Position - fresh.Vertices[i].Position).Length() < 1e-6f, $"{character.Name}, {expression.Name} at {clock}");
                }
            }
            // "relaxed" blinks: two pupils and a mouth, and only the mouth at the moment the eyes are shut. "happy" does not.
            Assert.Equal(3, face.Attachments("relaxed", 1).Length);
            Assert.Single(face.Attachments("relaxed", 3.62));
            Assert.Equal(3, face.Attachments("happy", 3.62).Length);
            Assert.False(face.Has("no-such-expression"));
        }
    }

    [Fact]
    public void AFaceRefusesAnExpressionItCannotDraw()
    {
        static Face With(FaceExpression expression) => new(default, 1, [new("eye", new() { Fill = "#000000" })], [expression]);
        var eye = App2d.Core.Geometry.Shapes.Ellipse2D.Builder.Create(new(1, 2));
        Assert.Single(new CharacterFace(With(new("open", false, ("eye", eye))), 0).Attachments("open", 0));
        // A piece the face does not have, and a kind of geometry a face has no way to place.
        Assert.Throws<InvalidDataException>(() => new CharacterFace(With(new("open", false, ("nose", eye))), 0));
        Assert.Throws<InvalidDataException>(() => new CharacterFace(With(new("open", false, ("eye", App2d.Core.Geometry.Shapes.Circle2D.Builder.Create(1)))), 0));
    }

    [Fact]
    public void AnObjectFileRoundTripsAndABodyOfSeveralShapesCollidesAsOne()
    {
        var dumbbell = ObjectAsset2D.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "objects", "dumbbell.json"));
        Assert.Equal(3, dumbbell.Shapes.Count);
        Assert.IsType<Core.Geometry.Shapes.CompositeShape2D>(dumbbell.CollisionShape());
        Assert.Equal(dumbbell.ToJson(), ObjectAsset2D.FromJson(dumbbell.ToJson()).ToJson());
        Assert.Equal(3, new ObjectShader2D(dumbbell).Paints.Length);
        Assert.Throws<InvalidDataException>(() => (dumbbell with { Shapes = [] }).Validate());
    }
}
