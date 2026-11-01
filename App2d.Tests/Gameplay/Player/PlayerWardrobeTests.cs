using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Characters.Geometry;
using App2d.Presentation.Persons;
using App2d.Presentation.Player;
using App2d.Tests.Authored;
using System.Numerics;

namespace App2d.Tests.Gameplay.Player;

public sealed class PlayerWardrobeTests
{
    private static PersonMoves Moves() => PersonMoves.From(AuthoredCatalog.Load(TestModels.AuthoredRoot));

    private static string[] Owned(PlayerWardrobe2D wardrobe) => wardrobe.Inventory
        .Concat(Enum.GetValues<WardrobeSlot2D>().Select(wardrobe.Equipped))
        .OfType<WardrobeItem2D>().Select(i => i.Id).Order().ToArray();

    [Fact]
    public void EquippingSwappingAndRemovingKeepEveryItemExactlyOnce()
    {
        var wardrobe = new PlayerWardrobe2D(Moves());
        var owned = Owned(wardrobe);
        Assert.Equal(36, wardrobe.Inventory.Count);
        Assert.True(wardrobe.Equip(0, WardrobeSlot2D.Shoes));
        Assert.Null(wardrobe.Inventory[0]);
        Assert.Equal("boot", wardrobe.Equipped(WardrobeSlot2D.Shoes)!.Id);
        Assert.True(wardrobe.Equip(1, WardrobeSlot2D.Shoes));
        Assert.Equal("boot", wardrobe.Inventory[1]!.Id);
        Assert.Equal("sandal", wardrobe.Equipped(WardrobeSlot2D.Shoes)!.Id);
        Assert.True(wardrobe.Unequip(WardrobeSlot2D.Shoes, 1));
        Assert.Equal("sandal", wardrobe.Inventory[1]!.Id);
        Assert.Equal("boot", wardrobe.Equipped(WardrobeSlot2D.Shoes)!.Id);
        Assert.True(wardrobe.Equip(2, WardrobeSlot2D.Gloves));
        Assert.True(wardrobe.Unequip(WardrobeSlot2D.Gloves));
        Assert.True(wardrobe.Unequip(WardrobeSlot2D.Shoes, 35));
        Assert.True(wardrobe.Move(35, 24));
        Assert.Equal(owned, Owned(wardrobe));
        Assert.All(Enum.GetValues<WardrobeSlot2D>(), slot => Assert.Null(wardrobe.Equipped(slot)));
    }

    [Fact]
    public void IncompatibleAndInvalidDropsLeaveInventoryAndEquipmentAlone()
    {
        var wardrobe = new PlayerWardrobe2D(Moves());
        var owned = Owned(wardrobe);
        Assert.False(wardrobe.Equip(0, WardrobeSlot2D.Gloves));
        Assert.False(wardrobe.Equip(-1, WardrobeSlot2D.Shoes));
        Assert.False(wardrobe.Equip(36, WardrobeSlot2D.Shoes));
        Assert.False(wardrobe.Equip(34, WardrobeSlot2D.Gloves));
        Assert.True(wardrobe.Equip(0, WardrobeSlot2D.Shoes));
        Assert.False(wardrobe.Unequip(WardrobeSlot2D.Shoes, 2));
        Assert.False(wardrobe.Move(0, 2));
        Assert.Equal("boot", wardrobe.Equipped(WardrobeSlot2D.Shoes)!.Id);
        Assert.Equal("glove-leather", wardrobe.Inventory[2]!.Id);
        Assert.Equal(owned, Owned(wardrobe));
    }

    [Fact]
    public void EquippedPairsFollowBothLimbsAcrossPlayerAnimationsAndFacings()
    {
        var moves = Moves();
        var wardrobe = new PlayerWardrobe2D(moves);
        wardrobe.Equip(0, WardrobeSlot2D.Shoes);
        wardrobe.Equip(3, WardrobeSlot2D.Gloves);
        var drawing = new PuppetDrawing();
        foreach (var clip in new[] { moves[PersonMoves.Idle], moves[PersonMoves.Run], moves.Swing(null).Clip })
        {
            var seconds = clip.Duration / 2;
            var local = PoseEvaluator.Sample(moves.Model, clip, seconds);
            var props = wardrobe.Dress(clip, seconds, PersonGear.Sword);
            foreach (var facing in new[] { -1, 1 })
            {
                var actor = new ActorPose(local, new(3, 2), facing);
                foreach (var (socket, control) in new[]
                {
                    (Humanoid.LeftFootSocket, Humanoid.LeftFoot), (Humanoid.RightFootSocket, Humanoid.RightFoot),
                    (Humanoid.LeftHandSocket, Humanoid.LeftHand), (Humanoid.RightHandSocket, Humanoid.RightHand)
                })
                {
                    var item = Assert.Single(wardrobe.Attachments, p => p.Socket.Id == socket);
                    var origin = PartPlacement3D.Point(actor.Socket(item.Socket), item.Asset, item.Asset.Point(item.Asset.Pivot)!.Value);
                    var limb = actor.World(control);
                    TestModels.Near(limb with { Z = 0 }, origin with { Z = 0 });
                }
            }
            drawing.Build(moves.Model, local);
            var bare = drawing.Mesh.Count;
            foreach (var (prop, socket) in props) drawing.AddAttachment(prop, new ActorPose(local, Vector2.Zero, 1), socket);
            foreach (var attachment in wardrobe.Attachments) drawing.AddAttachment(attachment, new ActorPose(local, Vector2.Zero, 1));
            Assert.True(drawing.Mesh.Count > bare);
            Assert.Contains(props, p => p.Prop.Id == moves.Gear.Sword);
        }
    }

    [Fact]
    public void APlayersLoadoutDoesNotEditSharedPartsOrAnotherPlayer()
    {
        var moves = Moves();
        var first = new PlayerWardrobe2D(moves);
        var second = new PlayerWardrobe2D(moves);
        var modelJson = moves.Model.Base.ToJson();
        var partJson = moves.Props["glove-steel"].ToJson();
        first.Equip(3, WardrobeSlot2D.Gloves);
        Assert.Null(second.Equipped(WardrobeSlot2D.Gloves));
        Assert.Equal("glove-steel", second.Inventory[3]!.Id);
        Assert.Equal(modelJson, moves.Model.Base.ToJson());
        Assert.Equal(partJson, moves.Props["glove-steel"].ToJson());
    }
}
