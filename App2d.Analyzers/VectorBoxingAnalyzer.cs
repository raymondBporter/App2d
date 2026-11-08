using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace App2d.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VectorBoxingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP2D002";

    private static readonly string[] VectorMetadataNames =
    [
        "System.Numerics.Vector2", "System.Numerics.Vector3", "System.Numerics.Vector4",
        "System.Numerics.Vector`1", "App2d.Core.Mathematics.Vector2i"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid boxing vector values",
        "Conversion from '{0}' to '{1}' uses boxing",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Keep vector values concrete or pass them through a constrained generic API to avoid boxing.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            var vectorInterface = startContext.Compilation.GetTypeByMetadataName("App2d.Core.Mathematics.IVector`2");
            var vectors = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var metadataName in VectorMetadataNames)
            {
                if (startContext.Compilation.GetTypeByMetadataName(metadataName) is { } type)
                    vectors.Add(type);
            }

            if (vectorInterface is null && vectors.Count == 0) return;

            var knownVectors = vectors.ToImmutable();
            startContext.RegisterOperationAction(
                operationContext => AnalyzeConversion(operationContext, vectorInterface, knownVectors),
                OperationKind.Conversion);
        });
    }

    private static void AnalyzeConversion(OperationAnalysisContext context, INamedTypeSymbol? vectorInterface,
        ImmutableHashSet<ISymbol> knownVectors)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (conversion.Operand.Type is not { IsValueType: true } source ||
            conversion.Type is not { IsReferenceType: true } destination ||
            conversion.Operand.ConstantValue is { HasValue: true, Value: null })
        {
            return;
        }

        var valueType = source;
        if (source is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            if (conversion.Operand is IDefaultValueOperation or IObjectCreationOperation { Arguments.IsEmpty: true } or
                IConversionOperation { OperatorMethod: null, Operand.ConstantValue: { HasValue: true, Value: null } })
            {
                return;
            }

            valueType = nullable.TypeArguments[0];
        }

        var isVector = knownVectors.Contains(valueType.OriginalDefinition) ||
            vectorInterface is not null && (HasVectorContract(valueType, vectorInterface) ||
                valueType is ITypeParameterSymbol parameter && parameter.ConstraintTypes.Any(
                    constraint => HasVectorContract(constraint, vectorInterface)));
        if (!isVector || !((CSharpCompilation)context.Compilation).ClassifyConversion(source, destination).IsBoxing)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, conversion.Syntax.GetLocation(),
            source.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            destination.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool HasVectorContract(ITypeSymbol type, INamedTypeSymbol vectorInterface) =>
        SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, vectorInterface) ||
        type.AllInterfaces.Any(implemented => SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, vectorInterface));
}
