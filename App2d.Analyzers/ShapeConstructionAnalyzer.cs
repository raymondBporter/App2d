using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace App2d.Analyzers;

/// <summary>Keep shape and curve validation at the builder boundary.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ShapeConstructionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP2D001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Construct geometry through its builder",
        "Use {0}.Builder to construct and validate this geometry",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Shape and curve constructors bypass builder validation. Use the type's Builder.Create factory instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeCreation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type is not INamedTypeSymbol shape || !GeometrySymbols.IsBuiltInGeometry(shape, context.Compilation))
        {
            return;
        }

        IOperation parent = creation;
        while (parent.Parent is IConversionOperation or IArgumentOperation)
        {
            parent = parent.Parent;
        }
        if (parent.Parent is IInvocationOperation validation &&
            validation.TargetMethod.Name == "Validated" &&
            validation.TargetMethod.ContainingType.ToDisplayString() == "App2d.Core.Geometry.GeometryValidation2D")
        {
            return;
        }

        // Only the geometry implementation and its own nested builder may use the raw constructor.
        // A consumer class merely named "Builder" does not receive an exemption.
        for (var owner = context.ContainingSymbol.ContainingType; owner is not null; owner = owner.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(owner, shape))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation(), shape.Name));
    }
}

internal static class GeometrySymbols
{
    public static bool IsBuiltInGeometry(INamedTypeSymbol type, Compilation compilation)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        if (type.ContainingType is not null ||
            !SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName(ns + "." + type.Name)))
        {
            return false;
        }

        return ns switch
        {
            "App2d.Core.Shapes" => type.Name is "Circle2D" or "Ellipse2D" or "Capsule2D" or "Rectangle2D" or
                "AxisAlignedRectangle2D" or "RoundedRectangle2D" or "Triangle2D" or "ConvexPolygon2D" or
                "SimplePolygon2D" or "HalfSpace2D" or "CompositeShape2D",
            "App2d.Core.Geometry.Curves" => type.Name is "LineSegmentCurve2D" or "PolylineCurve2D" or
                "Arc2D" or "QuadraticBezier2D" or "CubicBezier2D" or "BSpline2D",
            _ => false
        };
    }
}
