using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace App2d.Analyzers;

/// <summary>Keep shape validation at the builder boundary, even when shapes are value types.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ShapeConstructionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP2D001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Construct shapes through their builder",
        "Use {0}.Builder to construct and validate this shape",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Shape constructors bypass builder validation. Use the shape's Builder.Create factory instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeCreation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type is not INamedTypeSymbol shape || !ShapeSymbols.IsBuiltInShape(shape, context.Compilation))
        {
            return;
        }

        // Only the shape implementation and its own nested builder may use the raw constructor.
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

internal static class ShapeSymbols
{
    public static bool IsBuiltInShape(INamedTypeSymbol type, Compilation compilation)
    {
        if (type.ContainingNamespace.ToDisplayString() != "App2d.Core.Shapes" ||
            type.ContainingType is not null ||
            !SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName("App2d.Core.Shapes." + type.Name)))
        {
            return false;
        }

        return type.Name is "Circle2D" or "Ellipse2D" or "Capsule2D" or "Rectangle2D" or
            "AxisAlignedRectangle2D" or "RoundedRectangle2D" or "Triangle2D" or "ConvexPolygon2D" or
            "SimplePolygon2D" or "HalfSpace2D" or "CompositeShape2D";
    }
}
