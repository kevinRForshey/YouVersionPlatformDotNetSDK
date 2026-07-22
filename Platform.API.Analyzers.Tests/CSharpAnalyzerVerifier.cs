using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Platform.API.Analyzers.Tests;

/// <summary>
/// Uses <see cref="DefaultVerifier"/> rather than the xunit-specific verifier package: the latter
/// (Microsoft.CodeAnalysis.Testing.Verifiers.XUnit 1.1.2) was compiled against xunit.assert 2.3.0's
/// <c>EqualException</c> constructor, which the repo's xunit 2.9.3 removed, so every mismatch would
/// throw <see cref="System.MissingMethodException"/> at runtime instead of a real test failure.
/// <see cref="DefaultVerifier"/> throws a plain <see cref="System.InvalidOperationException"/>
/// instead, which xunit still reports as a normal test failure.
/// </summary>
internal static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static DiagnosticResult Diagnostic() =>
        CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic();

    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}
