using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace App2d.Analyzers.CodeFixes;

/// <summary>
/// Applies the vector expression an analyzer computed: replaces <c>new T(...)</c> with it, or passes it as the
/// constructor's only argument. Parentheses are added and then simplified away where precedence allows.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(VectorExpressionCodeFixProvider)), Shared]
public sealed class VectorExpressionCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [VectorRebuildAnalyzer.DiagnosticId, ComponentwiseVectorMathAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(VectorSymbols.FixKindProperty, out var kind) || kind is null ||
                !diagnostic.Properties.TryGetValue(VectorSymbols.ExpressionProperty, out var text) || text is null)
            {
                continue;
            }

            var creation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>();
            if (creation is null) continue;

            var title = kind == VectorSymbols.ArgumentFix ? "Pass '" + text + "' whole" : "Use '" + text + "'";
            context.RegisterCodeFix(
                CodeAction.Create(title, token => ApplyAsync(context.Document, creation, kind, text, token),
                    equivalenceKey: diagnostic.Id + "." + kind),
                diagnostic);
        }
    }

    private static async Task<Document> ApplyAsync(Document document, BaseObjectCreationExpressionSyntax creation,
        string kind, string text, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var expression = SyntaxFactory.ParseExpression(text);
        SyntaxNode replacement = kind == VectorSymbols.ArgumentFix
            ? creation.WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(expression))))
            : SyntaxFactory.ParenthesizedExpression(expression).WithAdditionalAnnotations(Simplifier.Annotation);
        return document.WithSyntaxRoot(root!.ReplaceNode(creation, replacement.WithTriviaFrom(creation)));
    }
}
