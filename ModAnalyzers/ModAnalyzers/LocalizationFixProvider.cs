using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;

namespace ModAnalyzers;


[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LocalizationFixProvider)), Shared]
public class LocalizationFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(LocalizationAnalyzer.DiagnosticId);

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Dictionary<string, string?> missingKeys = [];
        string? locFiles = null;
        //Should be the same for all, as all diagnostics should share the same span.
        //Realistically should only apply to one file at a time.
        
        foreach (var diagnostic in context.Diagnostics)
        {
            var properties = diagnostic.Properties;

            foreach (var entry in properties)
            {
                if (entry.Key == "LOCFILES") locFiles = entry.Value;
                else
                {
                    missingKeys.Add(entry.Key, entry.Value);
                }
            }
        }

        if (locFiles == null) return;
        if (missingKeys.Count == 0) return;
        
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;
        
        var diagnosticSpan = context.Diagnostics.First().Location.SourceSpan;
        var classDeclaration = root.FindToken(diagnosticSpan.Start).Parent;
        if (classDeclaration == null) return;
        
        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(Resources.STS001CodeFixTitle, locFiles),
                createChangedDocument: c => GeneratingMissingKeyComment(context.Document, classDeclaration, missingKeys, c),
                equivalenceKey: nameof(Resources.STS001CodeFixTitle)
            ),
            context.Diagnostics
        );

        /*
        // 'SourceSpan' of 'Location' is the highlighted area. We're going to use this area to find the 'SyntaxNode' to rename.
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Get the root of Syntax Tree that contains the highlighted diagnostic.
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // Find SyntaxNode corresponding to the diagnostic.
        var diagnosticNode = root?.FindNode(diagnosticSpan);

        // To get the required metadata, we should match the Node to the specific type: 'ClassDeclarationSyntax'.
        if (diagnosticNode is not ClassDeclarationSyntax declaration)
            return;

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(Resources.STS001CodeFixTitle, "asdf", CommonName),
                createChangedSolution: c => SanitizeCompanyNameAsync(context.Document, declaration, c),
                equivalenceKey: nameof(Resources.STS001CodeFixTitle)),
            diagnostic);*/
    }

    private async Task<Document> GeneratingMissingKeyComment(Document document, SyntaxNode classDef, Dictionary<string, string?> missingKeys,
        CancellationToken cancellationToken)
    {
        StringBuilder commentBuilder = new();
        
        var first = true;
        foreach (var entry in missingKeys)
        {
            if (first) first = false;
            else commentBuilder.AppendLine(",");
            commentBuilder.Append($"  \"{entry.Key}\": \"{entry.Value}\"");
        }
        commentBuilder.AppendLine();

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
        var comment = SyntaxFactory.Comment(commentBuilder.ToString());
        
        editor.ReplaceNode(classDef, (node, generator) =>
        {
            if (node.HasLeadingTrivia)
            {
                var trivia = node.GetLeadingTrivia().Add(comment);
                return node.WithLeadingTrivia(trivia);
            }
            else
            {
                return node.WithLeadingTrivia(comment);
            }
        });
        
        return document.WithSyntaxRoot(editor.GetChangedRoot());
    }
}