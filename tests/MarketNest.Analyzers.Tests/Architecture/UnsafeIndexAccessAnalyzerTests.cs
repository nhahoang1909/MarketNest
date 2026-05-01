using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class UnsafeIndexAccessAnalyzerTests
{
    // -------------------------------------------------------------------------
    // MN036 — triggers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Triggers_when_accessing_Dictionary_by_indexer()
    {
        var source = """
            using System.Collections.Generic;
            class Example
            {
                void Test()
                {
                    var dict = new Dictionary<string, int>();
                    var value = {|MN036:dict["key"]|};
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_accessing_IDictionary_parameter_by_indexer()
    {
        var source = """
            using System.Collections.Generic;
            class Example
            {
                void Test(IDictionary<string, decimal> prices)
                {
                    var price = {|MN036:prices["SKU-001"]|};
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_accessing_IReadOnlyDictionary_by_indexer()
    {
        var source = """
            using System.Collections.Generic;
            class Example
            {
                void Test(IReadOnlyDictionary<string, string> map)
                {
                    var v = {|MN036:map["config_key"]|};
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_chaining_dictionary_access_inline()
    {
        var source = """
            using System.Collections.Generic;
            class Example
            {
                Dictionary<string, int> _map = new();
                int GetValue(string key) => {|MN036:_map[key]|};
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    // -------------------------------------------------------------------------
    // MN036 — no triggers (safe / unrelated patterns)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task No_trigger_for_list_index_access()
    {
        // Lists/arrays are not flagged — out-of-range patterns are a separate concern
        var source = """
            using System.Collections.Generic;
            class Example
            {
                void Test()
                {
                    var list = new List<int> { 1, 2, 3 };
                    var v = list[0];
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_array_index_access()
    {
        var source = """
            class Example
            {
                void Test()
                {
                    int[] arr = [1, 2, 3];
                    var v = arr[1];
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_string_indexer()
    {
        var source = """
            class Example
            {
                void Test(string s)
                {
                    char c = s[0];
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_TryGetValue_usage()
    {
        // Safe pattern — no diagnostic expected
        var source = """
            using System.Collections.Generic;
            class Example
            {
                void Test(Dictionary<string, int> dict)
                {
                    if (dict.TryGetValue("key", out var val))
                    {
                        _ = val;
                    }
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_GetValueOrDefault_usage()
    {
        var source = """
            using System.Collections.Generic;
            class Example
            {
                void Test(Dictionary<string, int> dict)
                {
                    var v = dict.GetValueOrDefault("key", 0);
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_IReadOnlyList_index_access()
    {
        var source = """
            using System.Collections.Generic;
            class Example
            {
                void Test(IReadOnlyList<string> items)
                {
                    if (items.Count > 0)
                    {
                        var first = items[0];
                    }
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_dictionary_write_assignment()
    {
        // dict[key] = value is a write — safe and intentional, must not be flagged
        var source = """
            using System.Collections.Generic;
            class Example
            {
                void Build(Dictionary<string, string> dict)
                {
                    dict["key"] = "value";
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_custom_non_dictionary_indexer()
    {
        var source = """
            class Matrix
            {
                public int this[int row, int col] => 0;
            }
            class Example
            {
                void Test(Matrix m)
                {
                    var v = m[0, 0];
                }
            }
            """;
        await Verify<UnsafeIndexAccessAnalyzer>.AnalyzerAsync(source);
    }
}

