using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XVector2 = Microsoft.Xna.Framework.Vector2;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class Renderer3DTests
{
    private static TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> Triangle(Color4b color)
    {
        var mesh = new TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b>((p, c, uv) => new(new XVector3(p.X, p.Y, p.Z), c, new XVector2(uv.X, uv.Y)));
        mesh.Triangle(new(-.7f, -.7f, 0), new(.7f, -.7f, 0), new(0, .7f, 0), color);
        return mesh;
    }

    [Fact]
    public void SharedDepthWorksInEitherDrawOrderAndTransformChangesReuseBuffers()
    {
        using var graphics = new GraphicsTestContext(DepthFormat.Depth24);
        using var renderer = new Renderer3D(graphics.Device);
        var camera = Camera3D.Orthographic(new(0, 0, 4), Vector3.Zero, Vector3.UnitY, 2, 2);
        var back = Triangle(Color4b.Red);
        var front = Triangle(Color4b.Lime);
        var closer = Frame3D.FromRotation(Vector3.Zero, Vector3.UnitZ);
        renderer.Clear(Color4b.Black);
        renderer.Draw(front, closer, camera);
        renderer.Draw(back, Frame3D.Identity, camera);
        Assert.Equal(Color4b.Lime, graphics.ReadPixels()[64 * 128 + 64]);
        renderer.Clear(Color4b.Black);
        renderer.Draw(back, Frame3D.Identity, camera);
        renderer.Draw(front, closer, camera);
        Assert.Equal(Color4b.Lime, graphics.ReadPixels()[64 * 128 + 64]);
        Assert.Equal(2, renderer.MeshUploadCount);
        var orbit = Camera3D.Perspective(new(.3f, .2f, 4), Vector3.Zero, Vector3.UnitY, 1);
        renderer.Clear(Color4b.Black);
        renderer.Draw(front, Frame3D.FromRotation(new(0, .2f, 0)), orbit);
        Assert.Equal(2, renderer.MeshUploadCount);
        front.Clear();
        front.Triangle(new(-.5f, -.5f, 0), new(.5f, -.5f, 0), new(0, .5f, 0), Color4b.Blue);
        renderer.Draw(front, closer, camera);
        Assert.Equal(3, renderer.MeshUploadCount);
    }

    [Fact]
    public void SubmissionRestoresDeviceStateAndEnforcesTheCacheBudget()
    {
        using var graphics = new GraphicsTestContext(DepthFormat.Depth24);
        using var renderer = new Renderer3D(graphics.Device) { MeshCacheBudgetBytes = 0 };
        var camera = Camera3D.Perspective(new(0, 0, 4), Vector3.Zero, Vector3.UnitY, 1);
        graphics.Device.DepthStencilState = DepthStencilState.None;
        graphics.Device.BlendState = BlendState.Additive;
        graphics.Device.RasterizerState = RasterizerState.CullClockwise;
        renderer.Draw(Triangle(Color4b.White), Frame3D.Identity, camera);
        Assert.Same(DepthStencilState.None, graphics.Device.DepthStencilState);
        Assert.Same(BlendState.Additive, graphics.Device.BlendState);
        Assert.Same(RasterizerState.CullClockwise, graphics.Device.RasterizerState);
        Assert.Equal(0, renderer.CachedMeshBytes);
    }
}
