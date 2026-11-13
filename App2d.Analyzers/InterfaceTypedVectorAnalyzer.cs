using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace App2d.Analyzers;

/// <summary>
/// Reports declarations whose type is IVector itself (or an array of it). Storing a vector as the
/// interface boxes it and turns every member call into interface dispatch; generic code should take a
/// type parameter constrained to IVector instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterfaceTypedVectorAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP2D003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not type vector values as the vector interface",
        "'{0}' is declared as '{1}', which boxes the vector; use a type parameter constrained to the interface",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Write 'void M<T, R>(T value) where T : unmanaged, IVector<T, R>' instead of 'void M(IVector<...> value)'.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            if (VectorSymbols.Create(startContext.Compilation) is not { Interface: not null } symbols) return;

            startContext.RegisterSymbolAction(symbolContext =>
            {
                // Local functions are handled by the operation action below.
                if (symbolContext.Symbol is IMethodSymbol { MethodKind: not MethodKind.LocalFunction } method)
                    AnalyzeMethod(symbolContext.ReportDiagnostic, method, symbols);
            }, SymbolKind.Method);
            startContext.RegisterSymbolAction(symbolContext => AnalyzeField(symbolContext, symbols), SymbolKind.Field);
            startContext.RegisterSymbolAction(symbolContext => AnalyzeProperty(symbolContext, symbols), SymbolKind.Property);
            startContext.RegisterOperationAction(operationContext =>
            {
                var local = ((IVariableDeclaratorOperation)operationContext.Operation).Symbol;
                Report(operationContext.ReportDiagnostic, local, local.Type, symbols);
            }, OperationKind.VariableDeclarator);
            // Symbol actions do not visit local functions, so analyze them as operations.
            startContext.RegisterOperationAction(operationContext =>
                    AnalyzeMethod(operationContext.ReportDiagnostic, ((ILocalFunctionOperation)operationContext.Operation).Symbol, symbols),
                OperationKind.LocalFunction);
        });
    }

    private static void AnalyzeMethod(System.Action<Diagnostic> report, IMethodSymbol method, VectorSymbols symbols)
    {
        // Accessors are covered by their property; overrides and interface implementations
        // cannot change a signature declared elsewhere.
        if (method.IsImplicitlyDeclared || method.AssociatedSymbol is not null || method.IsOverride ||
            !method.ExplicitInterfaceImplementations.IsEmpty ||
            method.MethodKind is MethodKind.LambdaMethod or MethodKind.PropertyGet or MethodKind.PropertySet)
        {
            return;
        }

        if (!method.ReturnsVoid) Report(report, method, method.ReturnType, symbols);
        foreach (var parameter in method.Parameters)
            Report(report, parameter, parameter.Type, symbols);
    }

    private static void AnalyzeField(SymbolAnalysisContext context, VectorSymbols symbols)
    {
        var field = (IFieldSymbol)context.Symbol;
        if (!field.IsImplicitlyDeclared) Report(context.ReportDiagnostic, field, field.Type, symbols);
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, VectorSymbols symbols)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.IsImplicitlyDeclared || property.IsOverride || !property.ExplicitInterfaceImplementations.IsEmpty) return;
        Report(context.ReportDiagnostic, property, property.Type, symbols);
        foreach (var parameter in property.Parameters)
            Report(context.ReportDiagnostic, parameter, parameter.Type, symbols);
    }

    private static void Report(System.Action<Diagnostic> report, ISymbol symbol, ITypeSymbol type, VectorSymbols symbols)
    {
        var element = type;
        while (element is IArrayTypeSymbol array)
            element = array.ElementType;
        if (!symbols.IsVectorInterface(element) || symbol.Locations.IsEmpty || !symbol.Locations[0].IsInSource) return;

        report(Diagnostic.Create(Rule, symbol.Locations[0], symbol.Name,
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }
}
