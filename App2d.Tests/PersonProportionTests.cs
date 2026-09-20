using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using App2d.Core.Characters;
using Xunit;

namespace App2d.Tests;

/// <summary>Limb and hip proportions are retargeted per pose from the shared motion; nothing is baked per body type.</summary>
public sealed class PersonProportionTests
{
    // Export space: Y down, ground (pivot) at y = 100, 100 pixels per unit. Hips sit half a unit above the ground.
    private static readonly Vector3[] Rest =
    [
        new(0, 10, 0),                                     // 0 head
        new(-8, 50, 0), new(8, 50, 0),                     // 1,2 body bottom a/b
        new(-10, 20, 0), new(10, 20, 0),                   // 3,4 body top a/b
        new(0, 50, 5), new(2, 75, 5), new(2, 100, 5),      // 5-7 left leg: hip, knee, foot
        new(0, 50, -5), new(-2, 75, -5), new(-2, 100, -5), // 8-10 right leg
        new(0, 22, 10), new(5, 40, 12), new(5, 60, 14),    // 11-13 left arm: shoulder, elbow, hand
        new(0, 22, -10), new(-5, 40, -12), new(-5, 60, -14), // 14-16 right arm
        new(0, 0, 0),                                      // 17 head up
        new(-5, 60, -14), new(-5, 30, -14),                // 18,19 right sword grip/tip
        new(5, 60, 14), new(5, 30, 14),                    // 20,21 left sword grip/tip
        new(5, 60, -14), new(15, 60, 14),                  // 22,23 sword width controls
    ];

    private static PointLibrary Library()
    {
        var names = new[] { "head", "body_bottom_a", "body_bottom_b", "body_top_a", "body_top_b", "leg_l_0", "leg_l_1", "leg_l_2", "leg_r_0", "leg_r_1", "leg_r_2",
            "arm_l_0", "arm_l_1", "arm_l_2", "arm_r_0", "arm_r_1", "arm_r_2", "head_up", "sword_r_grip", "sword_r_tip", "sword_l_grip", "sword_l_tip", "sword_r_width", "sword_l_width" };
        var data = new byte[2 * 24 * 3 * 4];
        for (var sample = 0; sample < 2; sample++) for (var i = 0; i < 24; i++)
        {
            var at = (sample * 24 + i) * 12;
            BitConverter.TryWriteBytes(data.AsSpan(at), Rest[i].X); BitConverter.TryWriteBytes(data.AsSpan(at + 4), Rest[i].Y); BitConverter.TryWriteBytes(data.AsSpan(at + 8), Rest[i].Z);
        }
        var spec = new
        {
            version = 1, format = "app2d-point-library", id = "person-test", label = "Person test", anatomy = "person", pointNames = names,
            drawing = new { pivot = new[] { 0f, 100f }, pixelsPerUnit = 100f, weaponLength = 1f, parts = new[] { new { kind = "circle", radius = 8f } } },
            dataSha256 = Convert.ToHexString(SHA256.HashData(data)),
            clips = new Dictionary<string, object> { ["idle"] = new
            {
                label = "Idle", loop = true, duration = 1.0, times = new[] { 0.0, 1.0 }, sampleCount = 2, byteOffset = 0, byteLength = data.Length,
                encoding = new { type = "float32-le", layout = "sample,point,xyz", origin = new[] { 0f, 0f, 0f }, step = 1f }
            } }
        };
        return new(JsonSerializer.Serialize(spec), data);
    }

    private static Vector3[] Pose(CharacterAppearance look)
    {
        var library = Library(); var raw = new Vector3[24]; library.Clips["idle"].Sample(0, raw);
        var pose = new PersonPose(library); pose.Transform(raw, look); var points = new Vector3[24]; pose.CopyTo(points); return points;
    }

    private static void Close(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 4); Assert.Equal(expected.Y, actual.Y, 4); Assert.Equal(expected.Z, actual.Z, 4);
    }

    [Fact]
    public void LegLengthScalesBothLegSegmentsAndKeepsRestFeetOnGround()
    {
        var before = Pose(new()); var after = Pose(new() { LegLength = 1.5f });
        Close((before[6] - before[5]) * 1.5f, after[6] - after[5]);
        Close((before[7] - before[6]) * 1.5f, after[7] - after[6]);
        Close((before[10] - before[9]) * 1.5f, after[10] - after[9]);
        Assert.Equal(0, after[7].Y, 4); Assert.Equal(0, after[10].Y, 4);
        // Everything above the legs rises by the extra leg reach: half a unit of leg times 0.5.
        Close(before[5] + new Vector3(0, .25f, 0), after[5]);
        Close(before[1] + new Vector3(0, .25f, 0), after[1]);
        Close(before[0] + new Vector3(0, .25f, 0), after[0]);
        Close(before[13] + new Vector3(0, .25f, 0), after[13]);
    }

    [Fact]
    public void ArmLengthScalesFromTheShoulderAndCarriesTheWeapon()
    {
        var before = Pose(new()); var after = Pose(new() { ArmLength = 1.5f });
        Close(before[11], after[11]); Close(before[14], after[14]);
        Close((before[12] - before[11]) * 1.5f, after[12] - after[11]);
        Close((before[13] - before[12]) * 1.5f, after[13] - after[12]);
        Close((before[16] - before[15]) * 1.5f, after[16] - after[15]);
        Close(after[13], after[20]); Close(after[16], after[18]);
        Close(after[20] + new Vector3(0, 1, 0), after[21]);
        Close(before[7], after[7]); Close(before[0], after[0]);
    }

    [Fact]
    public void HipWidthSpreadsTorsoBottomAndLegRootsAboutTheirMidpoints()
    {
        var before = Pose(new()); var after = Pose(new() { HipWidth = 1.5f });
        Close(new(-.12f, .5f, 0), after[1]); Close(new(.12f, .5f, 0), after[2]);
        Close(new(0, .5f, .075f), after[5]); Close(new(0, .5f, -.075f), after[8]);
        // Legs move rigidly with their roots.
        Close(before[6] + new Vector3(0, 0, .025f), after[6]); Close(before[7] + new Vector3(0, 0, .025f), after[7]);
        Close(before[9] - new Vector3(0, 0, .025f), after[9]); Close(before[10] - new Vector3(0, 0, .025f), after[10]);
        Close(before[3], after[3]); Close(before[4], after[4]); Close(before[0], after[0]);
        Assert.Equal(0, after[7].Y, 4);
    }

    [Fact]
    public void HipWidthFollowsTheHipAxisInAnyFacing()
    {
        var after = Pose(new() { HipWidth = 1.5f, Flip = true });
        Close(new(.12f, .5f, 0), after[1]); Close(new(-.12f, .5f, 0), after[2]);
        Close(new(0, .5f, .075f), after[5]); Close(new(0, .5f, -.075f), after[8]);
    }

    [Fact]
    public void RejectsProportionsOutsideTheSupportedRange()
    {
        new CharacterAppearance { ArmLength = 1.5f, HipWidth = 1.5f, LegLength = 1.5f }.Validate();
        Assert.Throws<InvalidDataException>(() => new CharacterAppearance { ArmLength = 5 }.Validate());
        Assert.Throws<InvalidDataException>(() => new CharacterAppearance { HipWidth = -.1f }.Validate());
        Assert.Throws<InvalidDataException>(() => new CharacterAppearance { ArmLength = float.NaN }.Validate());
    }
}
