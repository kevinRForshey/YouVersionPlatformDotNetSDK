using FluentAssertions;
using Xunit;

namespace Platform.API.Tests.Architecture;

public sealed class ApiClientBoundaryTests
{
    [Fact]
    public void UiProjects_ShouldNotDirectlyReference_PlatformApiClientInterfaces()
    {
        var repoRoot = FindRepositoryRoot();
        var targetDirectories = new[]
        {
            Path.Combine(repoRoot, "Platform.SDK.Components"),
            Path.Combine(repoRoot, "PlatformTestApp")
        };

        var forbiddenPatterns = new[]
        {
            @"\busing\s+Platform\.API\.Clients\s*;",
            @"@inject\s+I(?:Bible|Passage|Highlight)Client\b",
            @"\bI(?:Bible|Passage|Highlight)Client\b"
        };

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
                var text = File.ReadAllText(file);
                if (forbiddenPatterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(text, pattern)))
                {
                    violations.Add(Path.GetRelativePath(repoRoot, file));
                }
            }
        }

        violations.Should().BeEmpty(
            "UI and app projects should call YouVersion only through Platform.SDK.Services abstractions.");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "YouVersionPlatform.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output path.");
    }
}


