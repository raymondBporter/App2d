using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Simplification;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App2d.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseShapeBuilderCodeFixProvider)), Shared]
public sealed class UseShapeBuilderCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [ShapeConstructionAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var creation = root?.FindNode(context.Span, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>();
        if (creation is null || model?.GetOperation(creation, context.CancellationToken) is not IObjectCreationOperation operation ||
            operation.Type is not INamedTypeSymbol shape || !GeometrySymbols.IsBuiltInGeometry(shape, model.Compilation) ||
            creation.ArgumentList is null && creation.Initializer is null)
        {
            return;
        }

        if (creation.Initializer is not null)
        {
            var validator = model.Compilation.GetTypeByMetadataName("App2d.Core.Geometry.GeometryValidation2D");
            if (validator is null) return;
            var validatorReference = (ExpressionSyntax)SyntaxGenerator.GetGenerator(context.Document).TypeExpression(validator)
                .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
            var target = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                validatorReference, SyntaxFactory.IdentifierName("Validated"));
            var candidate = SyntaxFactory.InvocationExpression(target,
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(creation.WithoutTrivia()))));
            context.RegisterCodeFix(CodeAction.Create("Validate the constructed geometry",
                token => ReplaceAsync(context.Document, creation, candidate, token),
                equivalenceKey: ShapeConstructionAnalyzer.DiagnosticId), context.Diagnostics);
            return;
        }

        // Retaining the original argument list preserves named arguments, comments, and evaluation order.
        if (creation.ArgumentList is null) return;
        var builder = shape.GetTypeMembers("Builder").SingleOrDefault();
        if (builder is null)
        {
            return;
        }

        foreach (var factory in builder.GetMembers("Create").OfType<IMethodSymbol>())
        {
            if (!factory.IsStatic || factory.DeclaredAccessibility != Accessibility.Public ||
                !SymbolEqualityComparer.Default.Equals(factory.ReturnType, shape))
            {
                continue;
            }

            // Generate the type from its symbol so Roslyn can add its namespace import
            // and simplify it without changing which type the call refers to.
            var shapeReference = (ExpressionSyntax)SyntaxGenerator.GetGenerator(context.Document).TypeExpression(shape)
                .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
            var target = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, shapeReference, SyntaxFactory.IdentifierName("Builder")),
                SyntaxFactory.IdentifierName("Create"));
            var candidate = SyntaxFactory.InvocationExpression(target, creation.ArgumentList);
            // Speculative binding also checks implicit conversions, optional arguments, overloads, and aliases.
            var bound = model.GetSpeculativeSymbolInfo(creation.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression);
            if (!SymbolEqualityComparer.Default.Equals(bound.Symbol, factory))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use " + shape.Name + ".Builder.Create",
                    cancellationToken => ReplaceAsync(context.Document, creation, candidate, cancellationToken),
                    equivalenceKey: ShapeConstructionAnalyzer.DiagnosticId),
                context.Diagnostics);
            break;
        }
    }

    private static async Task<Document> ReplaceAsync(Document document, BaseObjectCreationExpressionSyntax creation,
        InvocationExpressionSyntax replacement, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(creation,
            replacement.WithTriviaFrom(creation).WithAdditionalAnnotations(Simplifier.Annotation)));
    }
}
