using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
using App2d.Presentation.Persons;
using App2d.Presentation.Player;
using App2d.Tests.Authored;
using System.Numerics;

namespace App2d.Tests.Gameplay.Player;

/// <summary>Temporary inventory items exercise the same saved metadata the Studio authors.</summary>
public sealed class EquippablePieceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "equipment-pieces-" + Guid.NewGuid().ToString("N"));
    public EquippablePieceTests()
    {
        foreach (var path in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_root, Path.GetRelativePath(TestModels.AuthoredRoot, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target);
        }
    }
    public void Dispose() => Directory.Delete(_root, true);

    private Piece Add(string id, string slot, bool replace = false, bool mirror = true)
    {
        var piece = new Piece
        {
            Id = id, Name = id,
            InlineGeometry = new() { Id = "inline", Name = "Test geometry", Geometry = Rectangle2D.Builder.FromSize(.12f, .3f) },
            Material = new() { Fill = "#806040" },
            Game = new() { Equipment = new() { Slot = slot, Replace = replace, MirrorRight = mirror } },
        };
        piece.Validate();
        Directory.CreateDirectory(Path.Combine(_root, "pieces"));
        AuthoredAsset.Write(Path.Combine(_root, "pieces", id + ".json"), piece.ToJson());
        return piece;
    }

    private PersonMoves Moves()
    {
        var catalog = AuthoredCatalog.Load(_root);
        Assert.Empty(catalog.Errors);
        return PersonMoves.From(catalog);
    }
    private static int Cell(PlayerWardrobe2D wardrobe, string id) => Enumerable.Range(0, wardrobe.Inventory.Count).Single(i => wardrobe.Inventory[i]?.Id == id);
    private static string[] Owned(PlayerWardrobe2D wardrobe) => [.. wardrobe.Inventory.Concat(Enum.GetValues<WardrobeSlot2D>().Select(wardrobe.Equipped)).OfType<WardrobeItem2D>().Select(i => i.Id).Order()];

    [Theory]
    [InlineData(PersonEquipmentSlots.Shoes, Humanoid.LeftFootSocket, Humanoid.RightFootSocket)]
    [InlineData(PersonEquipmentSlots.Gloves, Humanoid.LeftHandSocket, Humanoid.RightHandSocket)]
    [InlineData(PersonEquipmentSlots.Shins, Humanoid.LeftShinSocket, Humanoid.RightShinSocket)]
    public void SavedPieceAppearsAsOneItemAndEquipsBothSides(string slot, string left, string right)
    {
        Add("test-item", slot);
        var moves = Moves();
        var wardrobeSlot = Enum.Parse<WardrobeSlot2D>(slot, ignoreCase: true);
        var wardrobe = new PlayerWardrobe2D(moves);
        var modelBefore = moves.Model.Base.ToJson();
        var pieceBefore = moves.Props["test-item"].ToJson();
        var owned = Owned(wardrobe);
        Assert.Single(wardrobe.Inventory, i => i?.Id == "test-item");
        Assert.True(wardrobe.Equip(Cell(wardrobe, "test-item"), wardrobeSlot));
        Assert.Equal(new[] { left, right }, wardrobe.Attachments.Select(a => a.Socket.Id));
        Assert.Same(wardrobe.Attachments[0].Asset, wardrobe.Attachments[1].Asset);
        Assert.False(wardrobe.Attachments[0].Instance.Mirror);
        Assert.True(wardrobe.Attachments[1].Instance.Mirror);
        Assert.Empty(wardrobe.Artwork.Parts);
        Assert.True(wardrobe.Unequip(wardrobeSlot));
        Assert.Empty(wardrobe.Attachments);
        Assert.Equal(owned, Owned(wardrobe));
        Assert.Equal(modelBefore, moves.Model.Base.ToJson());
        Assert.Equal(pieceBefore, moves.Props["test-item"].ToJson());
    }

    [Fact]
    public void PairSwapsAndInvalidDropsNeverDuplicateOrLoseItems()
    {
        Add("guard-a", PersonEquipmentSlots.Shins);
        Add("guard-b", PersonEquipmentSlots.Shins, mirror: false);
        var wardrobe = new PlayerWardrobe2D(Moves());
        var owned = Owned(wardrobe);
        Assert.False(wardrobe.Equip(Cell(wardrobe, "guard-a"), WardrobeSlot2D.Gloves));
        Assert.True(wardrobe.Equip(Cell(wardrobe, "guard-a"), WardrobeSlot2D.Shins));
        Assert.True(wardrobe.Equip(Cell(wardrobe, "guard-b"), WardrobeSlot2D.Shins));
        Assert.Equal("guard-b", wardrobe.Equipped(WardrobeSlot2D.Shins)!.Id);
        Assert.Single(wardrobe.Inventory, i => i?.Id == "guard-a");
        Assert.Equal(2, wardrobe.Attachments.Count);
        Assert.All(wardrobe.Attachments, a => Assert.False(a.Instance.Mirror));
        Assert.False(wardrobe.Equip(0, (WardrobeSlot2D)99));
        Assert.False(wardrobe.Unequip((WardrobeSlot2D)99));
        Assert.Equal(owned, Owned(wardrobe));
        Assert.True(wardrobe.Unequip(WardrobeSlot2D.Shins));
        Assert.Equal(owned, Owned(wardrobe));
    }

    [Fact]
    public void ReplacementHidesHandsOnlyForTheEquippedPlayerAndRestoresOnUnequip()
    {
        Add("test-hands", PersonEquipmentSlots.Gloves, replace: true);
        var moves = Moves();
        var first = new PlayerWardrobe2D(moves);
        var second = new PlayerWardrobe2D(moves);
        Assert.True(first.Equip(Cell(first, "test-hands"), WardrobeSlot2D.Gloves));
        var left = moves.Model.Parts.Single(p => p.Id == Humanoid.LeftHand);
        Assert.Null(first.Artwork.Apply(left));
        Assert.Same(left, second.Artwork.Apply(left));
        var pose = PoseEvaluator.Rest(moves.Model);
        var drawing = new PuppetDrawing();
        drawing.Build(moves.Model, pose);
        var fullCount = drawing.Mesh.Count;
        drawing.Build(moves.Model, pose, first.Artwork);
        Assert.True(drawing.Mesh.Count < fullCount);
        Assert.Empty(pose.HiddenParts);
        Assert.True(first.Unequip(WardrobeSlot2D.Gloves));
        drawing.Build(moves.Model, pose, first.Artwork);
        Assert.Equal(fullCount, drawing.Mesh.Count);
    }

    [Fact]
    public void ShinReplacementKeepsTheThighAndDoesNotMutateTheLegDefinition()
    {
        Add("test-shins", PersonEquipmentSlots.Shins, replace: true);
        var moves = Moves();
        var wardrobe = new PlayerWardrobe2D(moves);
        var original = moves.Model.Parts.Single(p => p.Id == Humanoid.LeftLeg);
        var modelBefore = moves.Model.Base.ToJson();
        wardrobe.Equip(Cell(wardrobe, "test-shins"), WardrobeSlot2D.Shins);
        var thigh = wardrobe.Artwork.Apply(original)!;
        Assert.Equal(Humanoid.LeftKnee, thigh.B);
        Assert.Equal(2, Assert.IsType<RoundedPolylineCurve2D>(thigh.Geometry).Points.Length);
        Assert.DoesNotContain(thigh.Bindings!, b => b.Control == Humanoid.LeftFoot);
        foreach (var clip in new[] { moves[PersonMoves.Idle], moves[PersonMoves.Run], moves.Swing(null).Clip })
        {
            var pose = PoseEvaluator.Sample(moves.Model, clip, clip.Duration / 2);
            var contour = PartContours2D.Contour(thigh, moves.Model, pose);
            TestModels.Near(new(pose.World(Humanoid.LeftKnee).X, pose.World(Humanoid.LeftKnee).Y, 0), contour[^1]);
            new PuppetDrawing().Build(moves.Model, pose, wardrobe.Artwork);
        }
        Assert.True(wardrobe.Unequip(WardrobeSlot2D.Shins));
        Assert.Same(original, wardrobe.Artwork.Apply(original));
        Assert.Equal(3, Assert.IsType<RoundedPolylineCurve2D>(original.Geometry).Points.Length);
        Assert.Equal(modelBefore, moves.Model.Base.ToJson());
    }

    [Fact]
    public void ShinPlacementsFollowAnklesAndPointTowardKneesAcrossAnimationsAndFacings()
    {
        Add("test-shins", PersonEquipmentSlots.Shins);
        var moves = Moves();
        var wardrobe = new PlayerWardrobe2D(moves);
        wardrobe.Equip(Cell(wardrobe, "test-shins"), WardrobeSlot2D.Shins);
        foreach (var clip in new[] { moves[PersonMoves.Idle], moves[PersonMoves.Run], moves.Swing(null).Clip })
        foreach (var facing in new[] { -1, 1 })
        {
            var pose = PoseEvaluator.Sample(moves.Model, clip, clip.Duration / 2);
            var actor = new ActorPose(pose, new(3, 2), facing);
            foreach (var attachment in wardrobe.Attachments)
            {
                var left = attachment.Socket.Id == Humanoid.LeftShinSocket;
                var foot = actor.World(left ? Humanoid.LeftFoot : Humanoid.RightFoot);
                var knee = actor.World(left ? Humanoid.LeftKnee : Humanoid.RightKnee);
                var placement = PartPlacement3D.Placement(attachment, actor.Socket(attachment.Socket));
                var origin = placement.TransformPoint(Vector3.Zero);
                TestModels.Near(foot with { Z = 0 }, origin with { Z = 0 });
                var axis = Vector2.Normalize(new(placement.YAxis.X, placement.YAxis.Y));
                var direction = Vector2.Normalize(new(knee.X - foot.X, knee.Y - foot.Y));
                Assert.True(Vector2.Dot(axis, direction) > .999f);
            }
        }
    }

    [Fact]
    public void PlainPiecesAreNotInventoryItemsAndUnknownEquipmentSlotsAreRejected()
    {
        var piece = Add("plain-piece", PersonEquipmentSlots.Shins);
        var roundTrip = Piece.FromJson(piece.ToJson());
        Assert.Equal(PersonEquipmentSlots.Shins, roundTrip.Game!.Equipment!.Slot);
        piece.Game = null;
        AuthoredAsset.Write(Path.Combine(_root, "pieces", piece.Id + ".json"), piece.ToJson());
        Assert.DoesNotContain(new PlayerWardrobe2D(Moves()).Items, i => i.Id == piece.Id);
        piece.Game = new() { Equipment = new() { Slot = "unknown" } };
        Assert.Throws<InvalidDataException>(piece.Validate);
    }

    [Fact]
    public void MaximumLengthAssetIdsStillProduceValidEquipmentInstanceIds()
    {
        var id = new string('a', 64);
        Add(id, PersonEquipmentSlots.Shins);
        var wardrobe = new PlayerWardrobe2D(Moves());
        Assert.True(wardrobe.Equip(Cell(wardrobe, id), WardrobeSlot2D.Shins));
        Assert.All(wardrobe.Attachments, a => a.Instance.Validate("equipment"));
        Assert.Equal(2, wardrobe.Attachments.Select(a => a.Id).Distinct().Count());
    }
}
