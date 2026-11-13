using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace App2d.Analyzers;

/// <summary>
/// Reports <c>new T(v.X, v.Y)</c> when it only copies or converts <c>v</c>. The value is already a
/// copy, or T has a constructor that takes the source vector whole.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VectorRebuildAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP2D004";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not rebuild a vector from its own components",
        "'{0}' rebuilds '{1}' one component at a time; use '{2}'",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Copy the vector or pass it to the converting constructor so it can stay in one register.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            if (VectorSymbols.Create(startContext.Compilation) is not { } symbols) return;
            startContext.RegisterOperationAction(operationContext => Analyze(operationContext, symbols), OperationKind.ObjectCreation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, VectorSymbols symbols)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type is not INamedTypeSymbol target || !symbols.IsVector(target) ||
            VectorSymbols.IsInside(context.ContainingSymbol, target) ||
            VectorSymbols.ComponentArguments(creation, VectorSymbols.Dimension(target)) is not { } values)
        {
            return;
        }

        IOperation? source = null;
        string? sourceKey = null;
        for (var index = 0; index < values.Length; index++)
        {
            if (!VectorSymbols.TryGetComponent(UnwrapWidening(values[index]), out var instance, out var name) ||
                name != VectorSymbols.ComponentNames[index] || VectorSymbols.StableKey(instance) is not { } key)
            {
                return;
            }

            if (sourceKey is null)
            {
                source = instance;
                sourceKey = key;
            }
            else if (key != sourceKey)
            {
                return;
            }
        }

        if (source?.Type is not { } sourceType || !symbols.IsVector(sourceType) ||
            VectorSymbols.Dimension(sourceType) != values.Length)
        {
            return;
        }

        var sourceText = VectorSymbols.Text(source);
        string fixKind;
        string suggestion;
        if (SymbolEqualityComparer.Default.Equals(sourceType, target))
        {
            // A widening conversion cannot appear between identical component types, so this is a plain copy.
            if (values.Any(value => value is IConversionOperation)) return;
            fixKind = VectorSymbols.ReplaceFix;
            suggestion = sourceText;
        }
        else
        {
            var within = (ISymbol?)context.ContainingSymbol.ContainingType ?? context.Compilation.Assembly;
            var converter = target.InstanceConstructors.FirstOrDefault(constructor =>
                constructor.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, sourceType) &&
                context.Compilation.IsSymbolAccessibleWithin(constructor, within));
            if (converter is null) return;
            fixKind = VectorSymbols.ArgumentFix;
            suggestion = "new " + target.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "(" + sourceText + ")";
        }

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(VectorSymbols.FixKindProperty, fixKind)
            .Add(VectorSymbols.ExpressionProperty, sourceText);
        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation(), properties,
            creation.Syntax.ToString(), sourceText, suggestion));
    }

    /// <summary>Looks through implicit numeric widening such as float to double, which a converting constructor also performs.</summary>
    private static IOperation UnwrapWidening(IOperation value) =>
        value is IConversionOperation { IsImplicit: true, Conversion.IsNumeric: true } conversion ? conversion.Operand : value;
}
