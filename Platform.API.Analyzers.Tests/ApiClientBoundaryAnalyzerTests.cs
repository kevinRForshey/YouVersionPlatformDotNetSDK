using System.Threading.Tasks;
using Xunit;
using static Platform.API.Analyzers.Tests.CSharpAnalyzerVerifier<Platform.API.Analyzers.ApiClientBoundaryAnalyzer>;

namespace Platform.API.Analyzers.Tests;

public sealed class ApiClientBoundaryAnalyzerTests
{
    [Fact]
    public async Task UsingDirective_ForForbiddenNamespace_IsFlagged()
    {
        const string source = """
            using {|#0:Platform.API.Clients|};

            namespace Platform.API.Clients
            {
                public interface IBibleClient
                {
                }
            }

            namespace ConsumerApp
            {
                public class Consumer
                {
                }
            }
            """;

        await VerifyAnalyzerAsync(
            source,
            Diagnostic().WithLocation(0).WithArguments("The 'using Platform.API.Clients;' directive", "Platform.API.Clients"));
    }

    [Fact]
    public async Task QualifiedTypeReference_ForForbiddenNamespace_IsFlagged()
    {
        const string source = """
            namespace Platform.API.Clients
            {
                public interface IBibleClient
                {
                }
            }

            namespace ConsumerApp
            {
                public class Consumer
                {
                    public Platform.API.Clients.{|#0:IBibleClient|}? Client { get; set; }
                }
            }
            """;

        await VerifyAnalyzerAsync(
            source,
            Diagnostic().WithLocation(0).WithArguments("'IBibleClient'", "Platform.API.Clients"));
    }

    [Fact]
    public async Task GenericTypeReference_ForForbiddenNamespace_IsFlagged()
    {
        const string source = """
            namespace Platform.API.Clients
            {
                public interface IGenericClient<T>
                {
                }
            }

            namespace ConsumerApp
            {
                public class Consumer
                {
                    public Platform.API.Clients.{|#0:IGenericClient<int>|}? Client { get; set; }
                }
            }
            """;

        await VerifyAnalyzerAsync(
            source,
            Diagnostic().WithLocation(0).WithArguments("'IGenericClient'", "Platform.API.Clients"));
    }

    [Theory]
    [InlineData("Platform.API.OAuth", "ITokenProvider")]
    [InlineData("Platform.API.Exceptions", "BibleApiException")]
    public async Task QualifiedTypeReference_ForOtherForbiddenNamespaces_IsFlagged(string ns, string typeName)
    {
        var source = $$"""
            namespace {{ns}}
            {
                public class {{typeName}}
                {
                }
            }

            namespace ConsumerApp
            {
                public class Consumer
                {
                    public {{ns}}.{|#0:{{typeName}}|}? Value { get; set; }
                }
            }
            """;

        await VerifyAnalyzerAsync(
            source,
            Diagnostic().WithLocation(0).WithArguments($"'{typeName}'", ns));
    }

    [Fact]
    public async Task ExemptAssembly_ForForbiddenNamespace_IsNotFlagged()
    {
        const string source = """
            [assembly: Platform.API.Models.AllowsPlatformApiClientAccess]

            namespace Platform.API.Models
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly)]
                public sealed class AllowsPlatformApiClientAccessAttribute : System.Attribute
                {
                }
            }

            namespace Platform.API.Clients
            {
                public interface IBibleClient
                {
                }
            }

            namespace ConsumerApp
            {
                using Platform.API.Clients;

                public class Consumer
                {
                    public IBibleClient? Client { get; set; }
                }
            }
            """;

        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NonForbiddenNamespace_IsNotFlagged()
    {
        const string source = """
            namespace SomeOther.Namespace
            {
                public interface ISomething
                {
                }
            }

            namespace ConsumerApp
            {
                using SomeOther.Namespace;

                public class Consumer
                {
                    public ISomething? Thing { get; set; }
                }
            }
            """;

        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DocCommentCrefReference_ForForbiddenNamespace_IsNotFlagged()
    {
        const string source = """
            namespace Platform.API.Clients
            {
                public interface IBibleClient
                {
                }
            }

            namespace ConsumerApp
            {
                /// <summary>
                /// See <see cref="Platform.API.Clients.IBibleClient"/> for details.
                /// </summary>
                public class Consumer
                {
                }
            }
            """;

        await VerifyAnalyzerAsync(source);
    }
}
