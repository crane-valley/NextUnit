using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NextUnit.Analyzers.CodeFixes;

/// <summary>
/// Code fix provider that changes async void test methods to async Task.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidTestCodeFixProvider))]
[Shared]
public sealed class AsyncVoidTestCodeFixProvider : CodeFixProvider
{
    private const string Title = "Change return type to Task";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("NU0001");

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
                createChangedDocument: c => ChangeToTaskAsync(context.Document, methodDeclaration, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> ChangeToTaskAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Create the new Task return type
        var taskType = SyntaxFactory.ParseTypeName("Task")
            .WithLeadingTrivia(methodDeclaration.ReturnType.GetLeadingTrivia())
            .WithTrailingTrivia(methodDeclaration.ReturnType.GetTrailingTrivia());

        // Replace the return type
        var newMethodDeclaration = methodDeclaration.WithReturnType(taskType);

        // Replace the node in the syntax tree
        var newRoot = root.ReplaceNode(methodDeclaration, newMethodDeclaration);

        // Add using directive if needed
        var compilationUnit = newRoot as CompilationUnitSyntax;
        if (compilationUnit is not null)
        {
            var hasTaskUsing = compilationUnit.Usings
                .Any(u => u.Name?.ToString() == "System.Threading.Tasks");

            if (!hasTaskUsing)
            {
                var usingDirective = SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName("System.Threading.Tasks"))
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                newRoot = compilationUnit.AddUsings(usingDirective);
            }
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
