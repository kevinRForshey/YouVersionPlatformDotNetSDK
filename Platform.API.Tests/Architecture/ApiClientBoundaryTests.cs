using System.Text.RegularExpressions;
using FluentAssertions;
using Platform.API.Clients;
using Platform.API.Exceptions;
using Platform.API.OAuth;
using Xunit;

namespace Platform.API.Tests.Architecture;

public sealed class ApiClientBoundaryTests
{
    [Fact]
    public void UiProjects_ShouldNotDirectlyReference_PlatformApiClientInterfaces()
    {
        var repoRoot = ArchitectureTestHelpers.FindRepositoryRoot();
        var targetDirectories = new[]
        {
            Path.Combine(repoRoot, "Platform.SDK.Components"),
            Path.Combine(repoRoot, "PlatformTestApp")
        };

        // Discovered from the compiled assembly rather than hardcoded, so a new public
        // client interface is covered automatically instead of silently bypassing this test.
        var forbiddenTypeNames = typeof(IBibleClient).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == "Platform.API.Clients")
            .Select(t => t.Name)
            .ToArray();

        forbiddenTypeNames.Should().NotBeEmpty("the reflection discovery should find at least the known client interfaces");

        var forbiddenPatterns = new List<string>
        {
            @"\busing\s+Platform\.API\.Clients\s*;"
        };
        forbiddenPatterns.AddRange(forbiddenTypeNames.Select(name => $@"\b{Regex.Escape(name)}\b"));

        var violations = new List<string>();

        foreach (var dir in targetDirectories)
        {
            if (!Directory.Exists(dir))
                continue;

            var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var text = ArchitectureTestHelpers.StripComments(File.ReadAllText(file));
                if (forbiddenPatterns.Any(pattern => Regex.IsMatch(text, pattern)))
                {
                    violations.Add(Path.GetRelativePath(repoRoot, file));
                }
            }
        }

        violations.Should().BeEmpty(
            "UI and app projects should call YouVersion only through Platform.SDK.Services abstractions.");
    }

    [Fact]
    public void UiProjects_ShouldNotDirectlyReference_OAuthOrExceptionTypes()
    {
        var repoRoot = ArchitectureTestHelpers.FindRepositoryRoot();
        var targetDirectories = new[]
        {
            Path.Combine(repoRoot, "Platform.SDK.Components"),
            Path.Combine(repoRoot, "PlatformTestApp")
        };

        // Program.cs performs the composition-root OAuth/PKCE handshake, OAuthCallbackHandlers.cs
        // is that same handshake's callback-dispatch logic factored out for readability/testability,
        // and SessionTokenProvider.cs is the app's ITokenProvider implementation -- an explicit SDK
        // extension point. All three are allowed to reference Platform.API.OAuth/Platform.API.Exceptions
        // directly; every other consumer must go through Platform.SDK.Services.
        var exemptFiles = new[]
        {
            Path.Combine(repoRoot, "PlatformTestApp", "Program.cs"),
            Path.Combine(repoRoot, "PlatformTestApp", "Auth", "OAuthCallbackHandlers.cs"),
            Path.Combine(repoRoot, "PlatformTestApp", "Auth", "SessionTokenProvider.cs")
        };

        var forbiddenTypeNames = typeof(ITokenProvider).Assembly.GetTypes()
            .Where(t => t.IsPublic && (t.Namespace == "Platform.API.OAuth" || t.Namespace == "Platform.API.Exceptions"))
            .Select(t => t.Name)
            .ToArray();

        forbiddenTypeNames.Should().NotBeEmpty("the reflection discovery should find at least the known OAuth/exception types");

        var forbiddenPatterns = new List<string>
        {
            @"\busing\s+Platform\.API\.OAuth\s*;",
            @"\busing\s+Platform\.API\.Exceptions\s*;"
        };
        forbiddenPatterns.AddRange(forbiddenTypeNames.Select(name => $@"\b{Regex.Escape(name)}\b"));

        var violations = new List<string>();

        foreach (var dir in targetDirectories)
        {
            if (!Directory.Exists(dir))
                continue;

            var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                .Where(f => !exemptFiles.Contains(f, StringComparer.Ordinal));

            foreach (var file in files)
            {
                var text = ArchitectureTestHelpers.StripComments(File.ReadAllText(file));
                if (forbiddenPatterns.Any(pattern => Regex.IsMatch(text, pattern)))
                {
                    violations.Add(Path.GetRelativePath(repoRoot, file));
                }
            }
        }

        violations.Should().BeEmpty(
            "UI and app projects should call YouVersion OAuth/exception handling only through Platform.SDK.Services abstractions " +
            "(Program.cs and Auth/SessionTokenProvider.cs are the only exempt composition-root/extension-point files).");
    }
}

