using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class InsecureHashAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_MD5_Create()
    {
        var source = """
            using System.Security.Cryptography;
            class Hasher {
                public void ComputeHash() {
                    var md5 = {|MN018:MD5|}.Create();
                }
            }
            """;
        await Verify<InsecureHashAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_MD5_HashData()
    {
        var source = """
            using System.Security.Cryptography;
            using System.Text;
            class Hasher {
                public void ComputeHash(string input) {
                    var hash = {|MN018:MD5|}.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
                }
            }
            """;
        await Verify<InsecureHashAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_SHA256_Create()
    {
        var source = """
            using System.Security.Cryptography;
            class Hasher {
                public void ComputeHash() {
                    var sha256 = {|MN018:SHA256|}.Create();
                }
            }
            """;
        await Verify<InsecureHashAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_SHA256_HashData()
    {
        var source = """
            using System.Security.Cryptography;
            using System.Text;
            class Hasher {
                public byte[] ComputeHash(string input) {
                    return {|MN018:SHA256|}.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
                }
            }
            """;
        await Verify<InsecureHashAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_SHA512_Create()
    {
        var source = """
            using System.Security.Cryptography;
            class Hasher {
                public void ComputeHash() {
                    var sha512 = SHA512.Create();
                }
            }
            """;
        await Verify<InsecureHashAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_SHA512_HashData()
    {
        var source = """
            using System.Security.Cryptography;
            using System.Text;
            class Hasher {
                public byte[] ComputeHash(string input) {
                    return SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
                }
            }
            """;
        await Verify<InsecureHashAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_other_methods()
    {
        var source = """
            using System.Security.Cryptography;
            class Hasher {
                public void Test() {
                    var x = SHA512.Create();
                }
            }
            """;
        await Verify<InsecureHashAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_multiple_insecure_calls()
    {
        var source = """
            using System.Security.Cryptography;
            class Hasher {
                public void ComputeHashes() {
                    var md5 = {|MN018:MD5|}.Create();
                    var sha256Hasher = {|MN018:SHA256|}.Create();
                }
            }
            """;
        await Verify<InsecureHashAnalyzer>.AnalyzerAsync(source);
    }
}

