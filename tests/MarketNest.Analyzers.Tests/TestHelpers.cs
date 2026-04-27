using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace MarketNest.Analyzers.Tests;

internal static class Verify<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static Task AnalyzerAsync(string source)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> { TestCode = source };
        return test.RunAsync();
    }
}

internal static class VerifyFix<TAnalyzer, TFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TFix : CodeFixProvider, new()
{
    public static Task CodeFixAsync(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource
        };
        return test.RunAsync();
    }
}
