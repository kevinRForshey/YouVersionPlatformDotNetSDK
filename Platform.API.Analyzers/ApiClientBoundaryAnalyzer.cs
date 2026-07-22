using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Platform.API.Analyzers;

/// <summary>
/// Flags direct references to <c>Platform.API.Clients</c>/<c>Platform.API.OAuth</c>/
/// <c>Platform.API.Exceptions</c> types from any assembly that doesn't carry
/// <c>[assembly: Platform.API.Models.AllowsPlatformApiClientAccess]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ApiClientBoundaryAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "YVP0001";

    private const string ExemptionAttributeName = "AllowsPlatformApiClientAccessAttribute";
    private const string ExemptionAttributeNamespace = "Platform.API.Models";

    private static readonly ImmutableHashSet<string> ForbiddenNamespaces = ImmutableHashSet.Create(
        "Platform.API.Clients",
        "Platform.API.OAuth",
        "Platform.API.Exceptions");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Do not reference Platform.API client/OAuth/exception types directly",
        messageFormat: "{0} must only be referenced from Platform.API or Platform.SDK.Services (belongs to '{1}')",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "Platform.SDK.Components and PlatformTestApp must call YouVersion only through " +
            "Platform.SDK.Services; reaching past it into Platform.API.Clients/OAuth/Exceptions couples UI " +
            "code to HTTP/OAuth implementation details that are free to change independently. Assemblies " +
            "carrying [assembly: Platform.API.Models.AllowsPlatformApiClientAccess] (Platform.API and " +
            "Platform.SDK.Services themselves) are exempt; a specific file can be exempted instead via a " +
            "'dotnet_diagnostic.YVP0001.severity = none' override scoped to it in .editorconfig.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            if (IsExemptAssembly(compilationContext.Compilation.Assembly))
                return;

            compilationContext.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
            compilationContext.RegisterSyntaxNodeAction(AnalyzeSimpleName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        });
    }

    private static bool IsExemptAssembly(IAssemblySymbol assembly) =>
        assembly.GetAttributes().Any(a =>
            a.AttributeClass is { Name: ExemptionAttributeName } attributeClass &&
            attributeClass.ContainingNamespace.ToDisplayString() == ExemptionAttributeNamespace);

    private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        if (usingDirective.Name is not { } name)
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
        if (symbol is not INamespaceSymbol namespaceSymbol)
            return;

        var namespaceName = namespaceSymbol.ToDisplayString();
        if (!ForbiddenNamespaces.Contains(namespaceName))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            name.GetLocation(),
            $"The 'using {namespaceName};' directive",
            namespaceName));
    }

    private static void AnalyzeSimpleName(SyntaxNodeAnalysisContext context)
    {
        var nameSyntax = (SimpleNameSyntax)context.Node;

        if (nameSyntax.FirstAncestorOrSelf<DocumentationCommentTriviaSyntax>() is not null)
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(nameSyntax, context.CancellationToken).Symbol;
        if (symbol is not INamedTypeSymbol { ContainingNamespace: { } containingNamespace } namedType)
            return;

        var namespaceName = containingNamespace.ToDisplayString();
        if (!ForbiddenNamespaces.Contains(namespaceName))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            nameSyntax.GetLocation(),
            $"'{namedType.Name}'",
            namespaceName));
    }
}
