using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class PersonMoveSetTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private static readonly ResolvedModel Person = Catalog.Resolve("person");

    [Fact]
    public void AnUpperBodyOverlayOwnsItsChannelsAndLeavesTheLegsToTheBase()
    {
        var walk = Catalog.Animations["person-walk"]; var shot = Catalog.Animations["player-gun-shot"];
        var layer = new PoseLayer(shot, .1, PersonLoadout.UpperBody);
        var layered = PoseEvaluator.Sample(Person, walk, .3, true, new() { InPlace = true, Overlay = layer });
        var legs = PoseEvaluator.Sample(Person, walk, .3, true, new() { InPlace = true });
        var arms = PoseEvaluator.Sample(Person, shot, .1, false, new() { InPlace = true });
        // Legs come from the walk exactly; the shot's hand sits where the shot puts it relative to its shoulder.
        foreach (var control in new[] { "hips", "left-foot", "right-foot", "left-knee", "right-knee" })
            TestModels.Near(legs.World(control), layered.World(control), 1e-5f, control);
        TestModels.Near(arms.World("right-hand") - arms.World("right-shoulder"), layered.World("right-hand") - layered.World("right-shoulder"), 1e-4f, "right hand");
        Assert.NotEqual(legs.World("right-hand"), layered.World("right-hand"));
    }

    [Fact]
    public void AMaskedChannelWithoutAnOverlayTrackRestsInsteadOfFallingBack()
    {
        var walk = Catalog.Animations["person-walk"];
        var empty = new MotionClip { Id = "empty", Name = "Empty", Model = "person", Reference = new() { ["arm"] = 1 } };
        var layered = PoseEvaluator.Sample(Person, walk, .3, true, new() { InPlace = true, Overlay = new(empty, 0, new HashSet<string> { "left-arm" }) });
        var rest = Person.Rest["left-hand"] - Person.Rest["left-shoulder"];
        TestModels.Near(rest, layered.World("left-hand") - layered.World("left-shoulder"), 1e-4f, "left hand at rest from its shoulder");
    }

    [Fact]
    public void TheSwordIsInHandOnlyBetweenDrawAndSheathe()
    {
        var draw = Catalog.Animations["player-sword-draw-slash"]; var sheathe = Catalog.Animations["player-sword-sheathe"]; var idle = Catalog.Animations["player-idle"];
        string SwordAt(MotionClip clip, float t, PersonGear gear = PersonGear.Sword) => PersonLoadout.Worn(clip, t, gear).Single(w => w.Prop == PersonLoadout.Sword).Socket;
        Assert.Equal(PersonLoadout.BackSocket, SwordAt(draw, 0));
        Assert.Equal(PersonLoadout.SwordSocket, SwordAt(draw, .06f));
        Assert.Equal(PersonLoadout.SwordSocket, SwordAt(sheathe, 0));
        Assert.Equal(PersonLoadout.BackSocket, SwordAt(sheathe, .31f));
        Assert.Equal(PersonLoadout.BackSocket, SwordAt(idle, 1));
        Assert.Equal(PersonLoadout.BackSocket, SwordAt(draw, .06f, PersonGear.Gun)); // the gun hand holds the pistol, not the sword
        Assert.Contains((PersonLoadout.Pistol, PersonLoadout.GunSocket), PersonLoadout.Worn(idle, 0, PersonGear.Gun));
        Assert.Empty(PersonLoadout.Worn(idle, 0, PersonGear.None));
    }

    [Fact]
    public void BackViewMarkersMoveTheSheathAcrossTheBack()
    {
        var on = Catalog.Animations["player-climb-on"]; var climb = Catalog.Animations["player-climb"]; var off = Catalog.Animations["player-climb-off"];
        Assert.False(PersonLoadout.SeenFromBehind(on, 0)); Assert.True(PersonLoadout.SeenFromBehind(on, .2f));
        Assert.True(PersonLoadout.SeenFromBehind(climb, .5f));
        Assert.True(PersonLoadout.SeenFromBehind(off, .1f)); Assert.False(PersonLoadout.SeenFromBehind(off, .25f));
        Assert.Equal(PersonLoadout.BackViewSocket, PersonLoadout.Worn(climb, .5f, PersonGear.Sword).First(w => w.Prop == PersonLoadout.Sheath).Socket);
    }

    [Fact]
    public void ContactHoldKeepsAPlantedFootWhileTheBodyMoves()
    {
        var idle = Catalog.Animations["player-idle"]; var hold = new ContactHold();
        var first = hold.Evaluate(Person, idle, 0, true, Vector2.Zero, 1, default);
        var foot = first.World("left-foot");
        var later = hold.Evaluate(Person, idle, .5, true, new(.03f, 0), 1, default);
        TestModels.Near(foot, later.World("left-foot"), 1e-4f, "planted foot");
        hold.Evaluate(Person, idle, .6, true, new(.03f, 0), -1, default);
        Assert.NotEqual(foot, hold.Anchors["left-leg"]); // turning round re-plants
    }
}
