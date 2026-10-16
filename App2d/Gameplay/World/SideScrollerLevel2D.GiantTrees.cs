using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using App2d.Gameplay.World.Vegetation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.World;

public sealed partial class SideScrollerLevel2D
{
    private readonly List<PhysicsBody2D> _giantTreeBranches = [];
    public ImmutableArray<GiantTree2D> GiantTrees { get; private set; } = [];
    public IReadOnlyList<PhysicsBody2D> GiantTreeBranches => _giantTreeBranches;

    /// <summary>Replaces the authored trees and their one-way platforms, publishing new level content.</summary>
    public void ReloadGiantTrees(IEnumerable<GiantTree2D> trees)
    {
        _ = RequireEnvironment();
        var replacements = ValidateTrees(trees);
        RemoveGiantTreeBranches();
        GiantTrees = replacements;
        CreateGiantTreeBranches();
        _definitionRevision++;
    }

    private static ImmutableArray<GiantTree2D> ValidateTrees(IEnumerable<GiantTree2D> trees)
    {
        ArgGuard.ThrowIfNull(trees);
        var result = trees.ToImmutableArray();
        ArgGuard.ThrowIfContainsNull(result.AsSpan(), nameof(trees));
        return result;
    }

    private void CreateGiantTreeBranches()
    {
        var environment = RequireEnvironment();
        foreach (var tree in GiantTrees)
        {
            foreach (var branch in tree.Branches)
            {
                var platform = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(branch.Bounds.Size));
                platform.Transform.Position = branch.Bounds.Center;
                var body = environment.Physics.AddBody(platform, BodyMotionType2D.Static);
                body.CollisionLayer = environment.WorldLayer;
                body.CollisionMask = environment.PlayerLayer | environment.EnemyLayer;
                body.OneWaySurfaceNormal = Vector2.UnitY;
                body.Restitution = 0;
                _giantTreeBranches.Add(body);
            }
        }
    }

    private void RemoveGiantTreeBranches()
    {
        foreach (var body in _giantTreeBranches)
        {
            _environment!.Physics.RemoveBody(body);
        }

        _giantTreeBranches.Clear();
    }
}
