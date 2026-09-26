using App2d.Core.Characters;
using System.Numerics;

namespace App2d.CharacterStudio.PlayerMoves;

/// <summary>
/// The player's move set on the authored Person, written as key poses. Right-facing, turned toward the viewer by the
/// model's shared hip/shoulder yaw; the near (left) limbs are toward the camera and the far right hand carries the sword
/// and gun. The sword lives in a sheath on the back: only attacks draw it (marker "sword-draw") and the sheathe clip
/// returns it ("sword-sheathe"). Upper-body clips key only chest, head, shoulders and arms so they can be layered on
/// any legs. Durations fit the current gameplay windows noted on each clip.
/// </summary>
internal static class PlayerMoves
{
    public const string Sword = "sword", Sheath = "sheath", Pistol = "pistol";

    // Stance: the walk's contact spacing. Feet rest at y = 0.025 (sole on the ground).
    private const float Back = -.125f, Front = .125f, Ground = .025f;
    // Sheathed sword: grip in the chest frame and the blade's direction (down, tipped slightly back).
    private static readonly Vector2 SheathGrip = new(-.22f, .12f);
    private const float SheathAngle = -MathF.PI / 2 - .1f;

    public static IEnumerable<ModelSocket> Sockets() =>
    [
        new() { Id = MoveBuilder.BackSocket, Control = "chest", Frame = "chest", OffsetX = SheathGrip.X, OffsetY = SheathGrip.Y, Angle = SheathAngle },
        new() { Id = MoveBuilder.SwordSocket, Control = "right-hand", Frame = "right-shoulder" },
        new() { Id = MoveBuilder.GunSocket, Control = "right-hand", Frame = "right-shoulder" },
    ];

    public static IEnumerable<PropAsset> Props() => [SwordProp(), SheathProp(), PistolProp()];

    public static IEnumerable<MotionClip> Clips(ResolvedModel m) =>
    [
        Idle(m), Jump(m), Fall(m), Land(m), Dash(m),
        Climb(m), WallGrip(m), BalanceForward(m), BalanceBackward(m),
        Hit(m), Death(m), Celebrate(m),
        DrawSlash(m), Slash(m), Sheathe(m), DownAttack(m),
        GunAim(m), GunShot(m), GunWallShot(m),
    ];

    /// <summary>Adds the move set's sockets to the Person model, then writes props and clips beside it.</summary>
    public static void Write(string authoredRoot)
    {
        var modelPath = Path.Combine(authoredRoot, "models", "person.json");
        var person = CharacterModel.FromJson(File.ReadAllText(modelPath));
        foreach (var socket in Sockets()) { person.Sockets.RemoveAll(s => s.Id == socket.Id); person.Sockets.Add(socket); }
        person.Save(modelPath);
        var model = ResolvedModel.From(person);
        foreach (var prop in Props()) prop.Save(Path.Combine(authoredRoot, "props", prop.Id + ".json"));
        foreach (var clip in Clips(model)) clip.Save(Path.Combine(authoredRoot, "animations", clip.Id + ".json"));
    }

    private static MoveBuilder New(ResolvedModel m, string id, string name, float duration, bool loop) => new(m, id, name, duration, loop);

    /// <summary>The sheathed grip's actor-space position and blade angle for a body with hips offset (dx, dy) and this chest turn.</summary>
    private static (Vector2 Grip, float Angle) Hilt(float hipsDx, float hipsDy, float chestTurn)
    {
        var (sin, cos) = MathF.SinCos(chestTurn);
        var chest = new Vector2(hipsDx - .65f * sin, 1 + hipsDy + .65f * cos);
        return (chest + new Vector2(SheathGrip.X * cos - SheathGrip.Y * sin, SheathGrip.X * sin + SheathGrip.Y * cos), chestTurn + SheathAngle);
    }

    // ---- Locomotion ---------------------------------------------------------------------------------------------

    /// <summary>A ready stance breathing on a 2.4 s loop. Arms drift a beat behind the chest so the loop never looks pumped.</summary>
    private static MotionClip Idle(ResolvedModel m) => New(m, "player-idle", "Idle", 2.4f, true)
        .Key(0, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f))
        .Key(1.2f, k => k.Hips(-.01f, -.055f).Chest(-.005f).Head(-.01f))
        .Key(2.4f, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f))
        .Key(0, k => k.LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Key(.35f, k => k.LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Key(1.55f, k => k.LeftHand(.035f, -.615f).RightHand(.065f, -.6f))
        .Key(2.4f, k => k.LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Plant("left-leg", 0, 2.4f, Back).Plant("right-leg", 0, 2.4f, Front)
        .Build();

    /// <summary>
    /// Gameplay leaves the ground on the press, so there is no crouch: the clip opens on the push (toes still low, arms
    /// swept back), snaps to full extension, then tucks through the rise and holds. "launch" is frame one.
    /// </summary>
    private static MotionClip Jump(ResolvedModel m) => New(m, "player-jump", "Jump", .5f, false)
        .Key(0, k => k.Hips(.02f, -.08f).Chest(-.22f).Head(.12f).LeftFoot(-.2f, .06f).RightFoot(.06f, .03f).LeftHand(-.28f, -.48f).RightHand(-.22f, -.5f))
        .Key(.08f, k => k.Hips(0, .02f).Chest(-.04f).Head(.02f).LeftFoot(-.14f, .02f).RightFoot(.02f, .1f).LeftHand(.12f, .2f).RightHand(.2f, .22f), ClipEase.Linear)
        .Key(.3f, k => k.Hips(0, .06f).Chest(-.1f).Head(.06f).LeftFoot(-.18f, .46f).RightFoot(.12f, .52f).LeftHand(.18f, -.2f).RightHand(.24f, -.16f))
        .Key(.5f, k => k.Hips(0, .05f).Chest(-.08f).Head(.05f).LeftFoot(-.15f, .38f).RightFoot(.1f, .44f).LeftHand(.14f, -.26f).RightHand(.2f, -.22f))
        .Marker("launch", 0).Marker("apex", .3f)
        .Face(0, "determined").Face(.3f, "focused")
        .Build();

    /// <summary>Descending: legs reach for the ground, staggered, and the arms float up for balance on a slow 0.6 s cycle.</summary>
    private static MotionClip Fall(ResolvedModel m) => New(m, "player-fall", "Fall", .6f, true)
        .Key(0, k => k.Hips(0, .02f).Chest(.06f).Head(-.1f).LeftFoot(-.12f, .12f).RightFoot(.15f, .26f).LeftHand(-.22f, .02f).RightHand(.26f, .06f))
        .Key(.3f, k => k.Hips(0, .03f).Chest(.08f).Head(-.13f).LeftFoot(-.1f, .17f).RightFoot(.13f, .21f).LeftHand(-.27f, .12f).RightHand(.3f, .15f))
        .Key(.6f, k => k.Hips(0, .02f).Chest(.06f).Head(-.1f).LeftFoot(-.12f, .12f).RightFoot(.15f, .26f).LeftHand(-.22f, .02f).RightHand(.26f, .06f))
        .Face(0, "focused")
        .Build();

    /// <summary>Soft landing: absorb into a deep crouch over planted feet, then rise into the idle's first pose.</summary>
    private static MotionClip Land(ResolvedModel m) => New(m, "player-land", "Land", .36f, false)
        .Key(0, k => k.Hips(-.02f, -.18f).Chest(-.28f).Head(.16f).LeftHand(.24f, -.44f).RightHand(.3f, -.4f))
        .Key(.09f, k => k.Hips(-.02f, -.24f).Chest(-.34f).Head(.2f).LeftHand(.26f, -.4f).RightHand(.32f, -.36f))
        .Key(.36f, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Plant("left-leg", 0, .36f, Back).Plant("right-leg", 0, .36f, Front)
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

    /// <summary>
    /// Hand over hand up a ladder behind the body: the near hand on the left rail, the far hand on the right, so neither
    /// arm crosses the face. 0.8 s per cycle; a gripping hand or foot travels down at the ladder's speed while the free
    /// ones reach up with a small outward arc. Opposite hand and foot move together.
    /// </summary>
    private static MotionClip Climb(ResolvedModel m)
    {
        const float nearRail = -.44f, farRail = .36f, high = 1.95f, low = 1.45f, footHigh = .6f, footLow = .05f;
        var b = New(m, "player-climb", "Climb", .8f, true);
        foreach (var t in new[] { 0f, .8f })
            b.Key(t, k => k.Hips(.02f, -.1f).Chest(-.04f).Head(.1f).LeftHandAt(nearRail, high).RightHandAt(farRail, low).LeftFoot(-.06f, footLow).RightFoot(.1f, footHigh), ClipEase.Linear);
        return b
            .Key(.2f, k => k.Hips(.02f, -.08f).Chest(-.03f).Head(.12f).LeftHandAt(nearRail, (high + low) / 2).RightHandAt(farRail - .09f, 1.8f).LeftFoot(-.01f, .38f).RightFoot(.1f, (footHigh + footLow) / 2), ClipEase.Linear)
            .Key(.4f, k => k.Hips(.02f, -.1f).Chest(-.04f).Head(.1f).LeftHandAt(nearRail, low).RightHandAt(farRail, high).LeftFoot(-.06f, footHigh).RightFoot(.1f, footLow), ClipEase.Linear)
            .Key(.6f, k => k.Hips(.02f, -.08f).Chest(-.03f).Head(.12f).LeftHandAt(nearRail + .09f, 1.8f).RightHandAt(farRail, (high + low) / 2).LeftFoot(-.06f, (footHigh + footLow) / 2).RightFoot(.15f, .38f), ClipEase.Linear)
            .Marker("rung-left", 0).Marker("rung-right", .4f)
            .Face(0, "focused")
            .Build();
    }

    /// <summary>
    /// Gameplay faces away from the wall while gripping, so the wall is behind (x = -0.3): back and one foot against it,
    /// the near hand braced flat on the wall, the far (weapon) hand free. A slow 1.2 s settle.
    /// </summary>
    private static MotionClip WallGrip(ResolvedModel m)
    {
        var b = New(m, "player-wall-grip", "Wall grip", 1.2f, true);
        foreach (var t in new[] { 0f, 1.2f })
            b.Key(t, k => k.Hips(-.1f, -.2f).Chest(.1f).Head(-.06f).LeftHandAt(-.31f, 1.3f).RightHand(.1f, -.48f).LeftFoot(-.3f, .38f).RightFoot(-.18f, .06f));
        return b
            .Key(.6f, k => k.Hips(-.1f, -.215f).Chest(.12f).Head(-.08f).LeftHandAt(-.31f, 1.28f).RightHand(.12f, -.46f).LeftFoot(-.3f, .38f).RightFoot(-.19f, .04f))
            .Face(0, "strained")
            .Build();
    }

    /// <summary>Windmill: the hands trace circles around their shoulders, opposite in phase, while the chest tips.</summary>
    private static MoveBuilder Windmill(MoveBuilder b, float direction, float lean, float wobble)
    {
        const int steps = 8; const float radius = .42f;
        for (var i = 0; i <= steps; i++)
        {
            var t = b.Duration * i / steps; var phase = MathF.Tau * i / steps * direction;
            var left = new Vector2(MathF.Cos(phase + .4f), MathF.Sin(phase + .4f)) * radius;
            var right = new Vector2(MathF.Cos(phase + .4f + MathF.PI), MathF.Sin(phase + .4f + MathF.PI)) * radius;
            var tip = lean + wobble * MathF.Sin(MathF.Tau * i / steps);
            b.Key(t, k => k.Chest(tip).Head(-.22f - tip * .5f).LeftHand(left.X, left.Y - .1f).RightHand(right.X, right.Y - .1f), ClipEase.Linear);
        }
        return b;
    }

    /// <summary>Edge in front of the toes: weight thrown back, arms windmilling backward, eyes on the drop.</summary>
    private static MotionClip BalanceForward(ResolvedModel m) => Windmill(New(m, "player-balance-forward", "Balance, edge ahead", 1, true), 1, .12f, .08f)
        .Key(0, k => k.Hips(-.06f, -.03f)).Key(.5f, k => k.Hips(-.08f, -.04f)).Key(1, k => k.Hips(-.06f, -.03f))
        .Plant("left-leg", 0, 1, -.08f).Plant("right-leg", 0, 1, .06f)
        .Face(0, "panic")
        .Build();

    /// <summary>Edge behind the heels: leaning forward over the toes, arms windmilling forward.</summary>
    private static MotionClip BalanceBackward(ResolvedModel m) => Windmill(New(m, "player-balance-backward", "Balance, edge behind", 1, true), -1, -.24f, .08f)
        .Key(0, k => k.Hips(.05f, -.04f)).Key(.5f, k => k.Hips(.07f, -.05f)).Key(1, k => k.Hips(.05f, -.04f))
        .Plant("left-leg", 0, 1, -.04f).Plant("right-leg", 0, 1, .1f)
        .Face(0, "worried")
        .Build();

    // ---- Reactions ----------------------------------------------------------------------------------------------

    /// <summary>Snap back from the chest with the arms flung forward, then recover to the idle stance.</summary>
    private static MotionClip Hit(ResolvedModel m) => New(m, "player-hit", "Hit", .35f, false)
        .Key(0, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Key(.05f, k => k.Hips(-.07f, -.06f).Chest(.36f).Head(.3f).LeftHand(.3f, -.22f).RightHand(.36f, -.16f), ClipEase.Linear)
        .Key(.18f, k => k.Hips(-.05f, -.06f).Chest(.14f).Head(.1f).LeftHand(.18f, -.42f).RightHand(.22f, -.38f))
        .Key(.35f, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Plant("left-leg", 0, .35f, Back).Plant("right-leg", 0, .35f, Front)
        .Face(0, "hurt").Face(.24f, "strained")
        .Build();

    /// <summary>
    /// Struck, stagger back a step as the knees go, topple backward onto the back with one small bounce, and lie still
    /// with the arms spread on the ground. Ends held.
    /// </summary>
    private static MotionClip Death(ResolvedModel m) => New(m, "player-death", "Death", 1.6f, false)
        .Key(0, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).LeftFoot(Back, Ground).RightFoot(Front, Ground).LeftHand(.05f, -.6f).RightHand(.08f, -.585f))
        .Key(.07f, k => k.Hips(-.08f, -.06f).Chest(.4f).Head(.32f).LeftFoot(Back, Ground).RightFoot(Front, Ground).LeftHand(.32f, -.2f).RightHand(.38f, -.14f), ClipEase.Linear)
        .Key(.34f, k => k.Hips(-.24f, -.18f, .12f).Chest(.3f).Head(.2f).LeftFoot(-.38f, Ground).RightFoot(.02f, .06f).LeftHand(.2f, .05f).RightHand(.28f, .1f))
        .Key(.68f, k => k.Hips(-.46f, -.56f, .85f).Chest(.22f).Head(.14f).LeftFoot(-.1f, .04f).RightFoot(.22f, .2f).LeftHand(.1f, .3f).RightHand(.25f, .28f), ClipEase.Linear)
        .Key(.92f, k => k.Hips(-.6f, -.83f, 1.45f).Chest(.08f).Head(.12f).LeftFoot(.26f, .08f).RightFoot(.33f, .14f).LeftHandAt(-.95f, .2f).RightHandAt(-.7f, .32f), ClipEase.Linear)
        .Key(1.06f, k => k.Hips(-.6f, -.76f, 1.38f).Chest(.12f).Head(.2f).LeftFoot(.28f, .1f).RightFoot(.34f, .17f).LeftHandAt(-.98f, .16f).RightHandAt(-.72f, .3f))
        .Key(1.3f, k => k.Hips(-.6f, -.83f, 1.5f).Chest(.06f).Head(.1f).LeftFoot(.3f, .04f).RightFoot(.36f, .07f).LeftHandAt(-1.02f, .05f).RightHandAt(-.66f, .04f))
        .Key(1.6f, k => k.Hips(-.6f, -.83f, 1.5f).Chest(.06f).Head(.1f).LeftFoot(.3f, .04f).RightFoot(.36f, .07f).LeftHandAt(-1.02f, .05f).RightHandAt(-.66f, .04f))
        .Marker("impact", .92f)
        .Face(0, "hurt").Face(.92f, "knocked-out")
        .Build();

    /// <summary>Crouch, hop with both arms thrown up in a V either side of the head, land and pump twice. Ends held, arms up.</summary>
    private static MotionClip Celebrate(ResolvedModel m) => New(m, "player-celebrate", "Celebrate", 1.4f, false)
        .Key(0, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).LeftHand(.05f, -.6f).RightHand(.08f, -.585f).LeftFoot(Back, Ground).RightFoot(Front, Ground))
        .Key(.16f, k => k.Hips(-.01f, -.16f).Chest(-.18f).Head(.1f).LeftHand(-.12f, -.5f).RightHand(-.08f, -.52f).LeftFoot(Back, Ground).RightFoot(Front, Ground))
        .Key(.4f, k => k.Hips(0, .12f).Chest(.06f).Head(.08f).LeftHand(-.32f, .5f).RightHand(.3f, .5f).LeftFoot(-.12f, .18f).RightFoot(.1f, .24f), ClipEase.Linear)
        .Key(.62f, k => k.Hips(-.01f, -.12f).Chest(-.08f).Head(.06f).LeftHand(-.3f, .42f).RightHand(.28f, .42f).LeftFoot(Back, Ground).RightFoot(Front, Ground))
        .Key(.86f, k => k.Hips(-.01f, -.04f).Chest(.03f).Head(.08f).LeftHand(-.32f, .5f).RightHand(.3f, .5f))
        .Key(1.02f, k => k.LeftHand(-.28f, .32f).RightHand(.26f, .32f))
        .Key(1.18f, k => k.LeftHand(-.32f, .5f).RightHand(.3f, .5f))
        .Key(1.4f, k => k.Hips(-.01f, -.04f).Chest(.03f).Head(.08f).LeftHand(-.32f, .49f).RightHand(.3f, .49f))
        .Plant("left-leg", 0, .3f, Back).Plant("right-leg", 0, .3f, Front)
        .Plant("left-leg", .62f, 1.4f, Back).Plant("right-leg", .62f, 1.4f, Front)
        .Face(0, "happy").Face(.3f, "delighted")
        .Build();

    // ---- Sword --------------------------------------------------------------------------------------------------

    /// <summary>
    /// Gameplay swing: 0.35 s, damage 0.10–0.27 s. The hand finds the hilt over the shoulder ("sword-draw"), and pulling
    /// the blade out becomes the wind-up: overhead, then a forward arc down through "strike" into a low guard. Ends drawn.
    /// </summary>
    private static MotionClip DrawSlash(ResolvedModel m)
    {
        var (hilt, sheathed) = Hilt(-.03f, -.04f, .1f);
        return New(m, "player-sword-draw-slash", "Sword draw and slash", .35f, false)
            .Key(0, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).RightHand(.08f, -.585f).LeftHand(.05f, -.6f).Blade(sheathed))
            .Key(.05f, k => k.Hips(-.03f, -.04f).Chest(.1f).Head(.02f).RightHandAt(hilt.X, hilt.Y).LeftHand(.12f, -.52f).Blade(sheathed), ClipEase.Linear)
            .Key(.1f, k => k.Hips(-.03f, -.05f).Chest(.16f).Head(.04f).RightHandAt(.04f, 2.16f).LeftHand(.18f, -.44f).Blade(2.1f), ClipEase.Linear)
            .Key(.17f, k => k.Hips(.02f, -.06f).Chest(-.12f).Head(.08f).RightHandAt(.42f, 1.86f).LeftHand(-.05f, -.5f).Blade(.62f), ClipEase.Linear)
            .Key(.22f, k => k.Hips(.06f, -.08f).Chest(-.3f).Head(.16f).RightHandAt(.52f, 1.38f).LeftHand(-.2f, -.48f).Blade(-.42f), ClipEase.Linear)
            .Key(.27f, k => k.Hips(.06f, -.09f).Chest(-.32f).Head(.18f).RightHandAt(.36f, 1.04f).LeftHand(-.22f, -.46f).Blade(-1.2f))
            .Key(.35f, k => k.Hips(.03f, -.07f).Chest(-.12f).Head(.08f).RightHandAt(.34f, 1.2f).LeftHand(-.08f, -.54f).Blade(-.22f))
            .Plant("left-leg", 0, .35f, -.16f).Plant("right-leg", 0, .35f, .16f)
            .Marker("sword-draw", .05f).Marker("strike", .2f).Marker("recover", .27f)
            .Face(0, "focused").Face(.12f, "determined")
            .Build();
    }

    /// <summary>A follow-up with the sword already out: drop low behind, then a rising cut forward and up, back to guard. Same 0.35 s window.</summary>
    private static MotionClip Slash(ResolvedModel m) => New(m, "player-sword-slash", "Sword rising slash", .35f, false)
        .Key(0, k => k.Hips(.03f, -.07f).Chest(-.12f).Head(.08f).RightHandAt(.34f, 1.2f).LeftHand(-.08f, -.54f).Blade(-.22f))
        .Key(.09f, k => k.Hips(-.02f, -.1f).Chest(.08f).Head(.04f).RightHandAt(-.1f, 1.0f).LeftHand(.16f, -.46f).Blade(-2.5f), ClipEase.Linear)
        .Key(.16f, k => k.Hips(.03f, -.08f).Chest(-.08f).Head(.08f).RightHandAt(.38f, 1.26f).LeftHand(-.1f, -.5f).Blade(-.3f), ClipEase.Linear)
        .Key(.21f, k => k.Hips(.06f, -.06f).Chest(-.18f).Head(.12f).RightHandAt(.46f, 1.7f).LeftHand(-.2f, -.46f).Blade(.7f), ClipEase.Linear)
        .Key(.27f, k => k.Hips(.05f, -.05f).Chest(-.1f).Head(.1f).RightHandAt(.28f, 2.02f).LeftHand(-.16f, -.5f).Blade(1.55f))
        .Key(.35f, k => k.Hips(.03f, -.07f).Chest(-.12f).Head(.08f).RightHandAt(.34f, 1.2f).LeftHand(-.08f, -.54f).Blade(-.22f))
        .Plant("left-leg", 0, .35f, -.16f).Plant("right-leg", 0, .35f, .16f)
        .Marker("sword-draw", 0).Marker("strike", .18f).Marker("recover", .27f)
        .Face(0, "determined")
        .Build();

    /// <summary>
    /// From guard, twirl the blade up and over the back (one continuous turn) and slide it home ("sword-sheathe"), then
    /// drop the hand into the idle. Movement can cut it short once the sword is home.
    /// </summary>
    private static MotionClip Sheathe(ResolvedModel m)
    {
        var (hilt, sheathed) = Hilt(-.01f, -.04f, .02f);
        return New(m, "player-sword-sheathe", "Sword sheathe", .42f, false)
            .Key(0, k => k.Hips(.03f, -.07f).Chest(-.12f).Head(.08f).RightHandAt(.34f, 1.2f).LeftHand(-.08f, -.54f).Blade(-.22f))
            .Key(.12f, k => k.Hips(.01f, -.05f).Chest(-.02f).Head(.06f).RightHandAt(.14f, 2.0f).LeftHand(0, -.58f).Blade(1.6f), ClipEase.Linear)
            .Key(.22f, k => k.Hips(-.01f, -.04f).Chest(.04f).Head(.04f).RightHandAt(-.12f, 2.1f).LeftHand(.04f, -.6f).Blade(3.5f), ClipEase.Linear)
            .Key(.3f, k => k.Hips(-.01f, -.04f).Chest(.02f).Head(.02f).RightHandAt(hilt.X, hilt.Y).LeftHand(.05f, -.6f).Blade(sheathed + MathF.Tau))
            .Key(.42f, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).RightHand(.08f, -.585f).LeftHand(.05f, -.6f).Blade(sheathed + MathF.Tau))
            .Plant("left-leg", 0, .42f, Back).Plant("right-leg", 0, .42f, Front)
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
            .Key(0, k => k.Hips(0, .04f).Chest(0).Head(0).RightHandAt(hilt.X, hilt.Y).LeftHand(.1f, -.4f).Blade(sheathed).LeftFoot(-.14f, .36f).RightFoot(.1f, .42f), ClipEase.Linear)
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
        .Key(0, k => k.Hips(-.02f, -.03f).Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0))
        .Key(1, k => k.Hips(-.02f, -.042f).Chest(.015f).Head(.01f).RightHand(.645f, -.03f).LeftHand(.01f, -.61f).Blade(.01f))
        .Key(2, k => k.Hips(-.02f, -.03f).Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0))
        .Plant("left-leg", 0, 2, Back).Plant("right-leg", 0, 2, Front)
        .Face(0, "focused")
        .Build();

    /// <summary>Upper body only, so it layers on aim, walk or run legs: the muzzle kicks up on "fire" and settles in 0.2 s.</summary>
    private static MotionClip GunShot(ResolvedModel m) => New(m, "player-gun-shot", "Gun shot (upper body)", .2f, false)
        .Key(0, k => k.Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0), ClipEase.Linear)
        .Key(.03f, k => k.Chest(.06f).Head(.05f).RightHand(.54f, 0).LeftHand(.01f, -.6f).Blade(.38f))
        .Key(.2f, k => k.Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.02f, -.6f).Blade(0))
        .Marker("fire", 0)
        .Face(0, "determined").Face(.14f, "focused")
        .Build();

    /// <summary>Shooting from the wall grip: the free far arm snaps out away from the wall and kicks on "fire".</summary>
    private static MotionClip GunWallShot(ResolvedModel m) => New(m, "player-gun-wall-shot", "Gun wall shot", .3f, false)
        .Key(0, k => k.Hips(-.1f, -.2f).Chest(.08f).Head(-.02f).LeftHandAt(-.31f, 1.3f).RightHand(.645f, -.02f).LeftFoot(-.3f, .38f).RightFoot(-.18f, .06f).Blade(0), ClipEase.Linear)
        .Key(.03f, k => k.Chest(.13f).Head(.02f).RightHand(.52f, .01f).Blade(.4f))
        .Key(.3f, k => k.Hips(-.1f, -.2f).Chest(.08f).Head(-.02f).LeftHandAt(-.31f, 1.3f).RightHand(.645f, -.02f).LeftFoot(-.3f, .38f).RightFoot(-.18f, .06f).Blade(0))
        .Marker("fire", 0)
        .Face(0, "determined")
        .Build();

    // ---- Props --------------------------------------------------------------------------------------------------

    /// <summary>A straight single-edged sword, grip at the origin, blade along +X. Depth +0.2 keeps it behind the torso when sheathed.</summary>
    public static PropAsset SwordProp() => new()
    {
        Id = Sword, Name = "Sword", Grip = new(0, 0), Tip = new(.82f, 0),
        Shapes =
        [
            new() { Kind = "stroke", Points = [new(-.1f, 0, .2f), new(.05f, 0, .2f)], Width = .05f, Fill = "#6b4a2b" },
            new() { Kind = "stroke", Points = [new(.05f, -.065f, .2f), new(.05f, .065f, .2f)], Width = .035f, Fill = "#8c9299" },
            new() { Kind = "polygon", Points = [new(.07f, -.028f, .2f), new(.75f, -.028f, .2f), new(.82f, .018f, .2f), new(.07f, .028f, .2f)], Fill = "#e3e9ec" },
        ],
    };

    /// <summary>Scabbard on the back socket, drawn just in front of the sheathed blade so only the hilt shows.</summary>
    public static PropAsset SheathProp() => new()
    {
        Id = Sheath, Name = "Sheath", Grip = new(0, 0), Tip = new(.85f, 0),
        Shapes =
        [
            new() { Kind = "polygon", Points = [new(.075f, -.04f, .19f), new(.8f, -.036f, .19f), new(.85f, 0, .19f), new(.8f, .036f, .19f), new(.075f, .04f, .19f)], Fill = "#5b4634" },
            new() { Kind = "stroke", Points = [new(.08f, -.042f, .185f), new(.08f, .042f, .185f)], Width = .028f, Fill = "#8c9299" },
        ],
    };

    /// <summary>A compact pistol, grip at the origin, barrel along +X, muzzle at the barrel's end.</summary>
    public static PropAsset PistolProp() => new()
    {
        Id = Pistol, Name = "Pistol", Grip = new(0, 0), Tip = new(.26f, .06f), Muzzle = new(.26f, .06f),
        Shapes =
        [
            new() { Kind = "polygon", Points = [new(-.04f, .025f), new(.26f, .025f), new(.26f, .095f), new(-.06f, .095f)], Fill = "#5d6368" },
            new() { Kind = "polygon", Points = [new(-.045f, .03f), new(.03f, .03f), new(.005f, -.09f), new(-.065f, -.09f)], Fill = "#8a4f2c" },
        ],
    };
}
