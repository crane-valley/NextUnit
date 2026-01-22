using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NextUnit.Analyzers.CodeFixes;

/// <summary>
/// Code fix provider that changes non-public test methods to public.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestMethodVisibilityCodeFixProvider))]
[Shared]
public sealed class TestMethodVisibilityCodeFixProvider : CodeFixProvider
{
    private const string Title = "Make method public";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("NU0002");

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var node = root.FindToken(diagnosticSpan.Start).Parent;
        var methodDeclaration = node?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();

        if (methodDeclaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: c => MakePublicAsync(context.Document, methodDeclaration, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> MakePublicAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Remove existing access modifiers (private, protected, internal)
        var accessModifierKinds = new[]
        {
            SyntaxKind.PrivateKeyword,
            SyntaxKind.ProtectedKeyword,
            SyntaxKind.InternalKeyword
        };

        var newModifiers = methodDeclaration.Modifiers
            .Where(m => !accessModifierKinds.Contains(m.Kind()))
            .ToList();

        // Determine leading trivia for the public keyword
        var leadingTrivia = methodDeclaration.Modifiers.Count > 0
            ? methodDeclaration.Modifiers[0].LeadingTrivia
            : methodDeclaration.ReturnType.GetLeadingTrivia();

        // Create the public modifier with appropriate trivia
        var publicModifier = SyntaxFactory.Token(SyntaxKind.PublicKeyword)
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.Space);

        // Insert public at the beginning
        newModifiers.Insert(0, publicModifier);

        // If we removed leading trivia from existing modifiers, we need to fix them
        if (newModifiers.Count > 1)
        {
            // Remove leading trivia from the second modifier since public now has it
            newModifiers[1] = newModifiers[1].WithLeadingTrivia(SyntaxTriviaList.Empty);
        }

        var newMethodDeclaration = methodDeclaration
            .WithModifiers(SyntaxFactory.TokenList(newModifiers));

        // If we took trivia from return type, remove it there too
        if (methodDeclaration.Modifiers.Count == 0)
        {
            newMethodDeclaration = newMethodDeclaration
                .WithReturnType(newMethodDeclaration.ReturnType.WithLeadingTrivia(SyntaxTriviaList.Empty));
        }

        var newRoot = root.ReplaceNode(methodDeclaration, newMethodDeclaration);

        return document.WithSyntaxRoot(newRoot);
    }
}
