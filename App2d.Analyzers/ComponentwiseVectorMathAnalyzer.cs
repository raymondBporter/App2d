using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace App2d.Analyzers;

/// <summary>
/// Reports <c>new T(a.X + b.X, a.Y + b.Y)</c> and its scalar forms when T defines the matching operator.
/// Only floating-point vectors are considered: integer vector operators may be checked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComponentwiseVectorMathAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP2D005";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use the vector operator instead of component-wise arithmetic",
        "Component-wise arithmetic on '{0}' can be written as '{1}'",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The vector operator keeps the value packed; rebuilding it per component can split it across registers.");

    private enum Shape
    {
        VectorVector,
        VectorScalar,
        ScalarVector
    }

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
            VectorSymbols.ComponentType(target, "X") is not { SpecialType: SpecialType.System_Single or SpecialType.System_Double } componentType ||
            VectorSymbols.ComponentArguments(creation, VectorSymbols.Dimension(target)) is not { } values)
        {
            return;
        }

        BinaryOperatorKind? kind = null;
        Shape? shape = null;
        string? leftKey = null, rightKey = null;
        IOperation? left = null, right = null;
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] is not IBinaryOperation { OperatorMethod: null } binary ||
                binary.OperatorKind is not (BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or
                    BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide) ||
                kind is not null && binary.OperatorKind != kind)
            {
                return;
            }

            kind = binary.OperatorKind;
            var name = VectorSymbols.ComponentNames[index];
            Shape current;
            IOperation currentLeft, currentRight;
            string? currentLeftKey, currentRightKey;
            if (Component(binary.LeftOperand, name, target) is { } leftVector &&
                Component(binary.RightOperand, name, target) is { } rightVector)
            {
                current = Shape.VectorVector;
                (currentLeft, currentRight) = (leftVector, rightVector);
            }
            else if (Component(binary.LeftOperand, name, target) is { } vector && IsScalar(binary.RightOperand, componentType) &&
                     kind is BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide)
            {
                current = Shape.VectorScalar;
                (currentLeft, currentRight) = (vector, binary.RightOperand);
            }
            else if (kind is BinaryOperatorKind.Multiply && IsScalar(binary.LeftOperand, componentType) &&
                     Component(binary.RightOperand, name, target) is { } scaled)
            {
                current = Shape.ScalarVector;
                (currentLeft, currentRight) = (binary.LeftOperand, scaled);
            }
            else
            {
                return;
            }

            currentLeftKey = VectorSymbols.StableKey(currentLeft);
            currentRightKey = VectorSymbols.StableKey(currentRight);
            if (currentLeftKey is null || currentRightKey is null) return;
            if (shape is null)
            {
                (shape, left, right, leftKey, rightKey) = (current, currentLeft, currentRight, currentLeftKey, currentRightKey);
            }
            else if (current != shape || currentLeftKey != leftKey || currentRightKey != rightKey)
            {
                return;
            }
        }

        if (kind is not { } operatorKind || shape is not { } finalShape || left is null || right is null) return;
        ITypeSymbol leftType = finalShape == Shape.ScalarVector ? componentType : target;
        ITypeSymbol rightType = finalShape == Shape.VectorScalar ? componentType : target;
        if (!HasOperator(target, OperatorName(operatorKind), leftType, rightType)) return;

        var expression = Operand(left) + " " + OperatorToken(operatorKind) + " " + Operand(right);
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(VectorSymbols.FixKindProperty, VectorSymbols.ReplaceFix)
            .Add(VectorSymbols.ExpressionProperty, expression);
        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation(), properties,
            target.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), expression));
    }

    /// <summary>The vector whose component <paramref name="name"/> this operand reads, if it is a T.</summary>
    private static IOperation? Component(IOperation operand, string name, ITypeSymbol target) =>
        VectorSymbols.TryGetComponent(operand, out var instance, out var componentName) && componentName == name &&
        SymbolEqualityComparer.Default.Equals(instance.Type, target)
            ? instance
            : null;

    /// <summary>Parenthesizes anything that is not a simple name, member access, or literal; the code fix removes unneeded parentheses.</summary>
    private static string Operand(IOperation operand)
    {
        var text = VectorSymbols.Text(operand);
        return operand is IInstanceReferenceOperation { IsImplicit: true } || operand.Syntax is IdentifierNameSyntax or
            MemberAccessExpressionSyntax or LiteralExpressionSyntax or ThisExpressionSyntax or ParenthesizedExpressionSyntax
            ? text
            : "(" + text + ")";
    }

    private static bool IsScalar(IOperation operand, ITypeSymbol componentType) =>
        SymbolEqualityComparer.Default.Equals(operand.Type, componentType);

    private static bool HasOperator(INamedTypeSymbol type, string name, ITypeSymbol left, ITypeSymbol right) =>
        type.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.MethodKind == MethodKind.UserDefinedOperator && method.Parameters.Length == 2 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, left) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, right) &&
            SymbolEqualityComparer.Default.Equals(method.ReturnType, type));

    private static string OperatorName(BinaryOperatorKind kind) => kind switch
    {
        BinaryOperatorKind.Add => WellKnownMemberNames.AdditionOperatorName,
        BinaryOperatorKind.Subtract => WellKnownMemberNames.SubtractionOperatorName,
        BinaryOperatorKind.Multiply => WellKnownMemberNames.MultiplyOperatorName,
        _ => WellKnownMemberNames.DivisionOperatorName
    };

    private static string OperatorToken(BinaryOperatorKind kind) => kind switch
    {
        BinaryOperatorKind.Add => "+",
        BinaryOperatorKind.Subtract => "-",
        BinaryOperatorKind.Multiply => "*",
        _ => "/"
    };
}
