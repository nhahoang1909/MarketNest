// tests/MarketNest.Analyzers.Tests/Naming/PrivateFieldNamingAnalyzerTests.cs
using MarketNest.Analyzers.Naming;
using MarketNest.Analyzers.CodeFixes;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class PrivateFieldNamingAnalyzerTests
{
    [Fact]
    public async Task Triggers_when_private_field_has_no_underscore()
    {
        var source = """
            class C {
                private int {|MN001:count|};
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_private_field_uses_PascalCase()
    {
        var source = """
            class C {
                private string {|MN001:Name|};
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_valid_underscore_camelCase()
    {
        var source = """
            class C {
                private int _count;
                private readonly string _name;
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_const_field()
    {
        var source = """
            class C {
                private const int MaxRetry = 3;
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_private_static_readonly()
    {
        var source = """
            class C {
                private static readonly string Prefix = "mn";
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_adds_underscore_prefix()
    {
        var source = """
            class C {
                private int {|MN001:count|};
            }
            """;
        var fixedSource = """
            class C {
                private int _count;
            }
            """;
        await VerifyFix<PrivateFieldNamingAnalyzer, PrivateFieldNamingCodeFix>
            .CodeFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task CodeFix_lowercases_PascalCase_and_adds_underscore()
    {
        var source = """
            class C {
                private string {|MN001:Name|};
            }
            """;
        var fixedSource = """
            class C {
                private string _name;
            }
            """;
        await VerifyFix<PrivateFieldNamingAnalyzer, PrivateFieldNamingCodeFix>
            .CodeFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task Triggers_when_private_readonly_field_has_bad_name()
    {
        var source = """
            class C {
                private readonly string {|MN001:BadName|};
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_single_underscore_discard_field()
    {
        var source = """
            class C {
                private int _;
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_removes_m_prefix_and_adds_underscore()
    {
        var source = """
            class C {
                private int {|MN001:m_count|};
            }
            """;
        var fixedSource = """
            class C {
                private int _count;
            }
            """;
        await VerifyFix<PrivateFieldNamingAnalyzer, PrivateFieldNamingCodeFix>
            .CodeFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task Triggers_when_private_static_non_readonly_field_has_bad_name()
    {
        var source = """
            class C {
                private static int {|MN001:Counter|};
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_valid_private_static_non_readonly()
    {
        var source = """
            class C {
                private static int _counter;
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }
}
