using App2d.Core.Characters.Authored;
using App2d.Core.Json;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class ControlTransformTests
{
    [Fact]
    public void PoseViewsFollowOneControlStateThroughReplacementAndTranslation()
    {
        var pose = new EvaluatedPose();
        var points = pose.Points;
        var bones = pose.Bones;
        var angles = pose.Angles;
        var frame = Matrix3x2.CreateScale(-2, 3) * Matrix3x2.CreateRotation(.4f) * Matrix3x2.CreateTranslation(1, 2);
        var angle = 3 * MathF.Tau + .4f;
        pose.Place("hand", frame, -.2f, angle);
        Assert.Equal(frame, bones["hand"]);
        Assert.Equal(new Vector3(1, 2, -.2f), points["hand"]);
        Assert.Equal(angle, angles["hand"]);
        Assert.Equal(new Vector2(1, 2), pose.Controls["hand"].Position);
        Assert.Equal(-.2f, pose.Controls["hand"].Layer);

        pose.Place("hand", new(pose.Controls["hand"].Frame.WithOrigin(new(7, 8)), .6f), angle);
        Assert.Equal(new Vector3(7, 8, .6f), points["hand"]);
        Assert.Equal(new Vector2(7, 8), bones["hand"].Translation);
        Assert.Equal(frame.M11, bones["hand"].M11);
        Assert.Equal(frame.M22, bones["hand"].M22);
        Assert.Equal(angle, angles["hand"]);
        Assert.Equal(pose.Controls["hand"].Placement, pose.Placement("hand"));
        Assert.Equal(points["hand"], pose.World("hand"));

        pose.Place("hand", Matrix3x2.Identity, 2, -4 * MathF.Tau);
        Assert.Equal(new Vector3(0, 0, 2), Assert.Single(points).Value);
        Assert.Equal(-4 * MathF.Tau, Assert.Single(angles.Values));
        Assert.False(points.TryGetValue("missing", out var missing));
        Assert.Equal(default, missing);
    }

    [Fact]
    public void SocketTransformKeepsTheSparseAuthoredVocabularyAndOrder()
    {
        const string json = """{"id":"grip","control":"hand","angle":0.7,"layer":-0.2,"offsetY":2,"offsetX":1}""";
        var socket = JsonSerializer.Deserialize<ModelSocket>(json, AuthoredJson.Options)!;
        Assert.Equal(new RigidTransform2D(new(1, 2), .7f), socket.Transform);
        Assert.Equal(-.2f, socket.Layer);
        Assert.Equal(socket.Transform.ToFrame(), socket.LocalPlacement.Frame);
        using var written = JsonDocument.Parse(JsonSerializer.Serialize(socket, AuthoredJson.Options));
        Assert.Equal(["id", "control", "offsetX", "offsetY", "layer", "angle"], written.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(socket, JsonSerializer.Deserialize<ModelSocket>(written.RootElement.GetRawText(), AuthoredJson.Options));

        var identity = new ModelSocket { Id = "grip", Control = "hand" };
        using var sparse = JsonDocument.Parse(JsonSerializer.Serialize(identity, AuthoredJson.Options));
        Assert.Equal(["id", "control"], sparse.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(default, JsonSerializer.Deserialize<ModelSocket>(sparse.RootElement.GetRawText(), AuthoredJson.Options)!.Transform);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ModelSocket>("""{"transform":{}}""", AuthoredJson.Options));
    }
}
