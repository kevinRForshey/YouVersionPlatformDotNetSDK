using FluentAssertions;
using Xunit;

namespace Platform.API.Tests.Architecture;

/// <summary>
/// Guards the rest of the dependency chain documented in the README
/// (Models -> API -> Services -> Components -> PlatformTestApp) against
/// reversed or skipped references that <see cref="ApiClientBoundaryTests"/> doesn't cover.
/// </summary>
public sealed class ProjectDependencyDirectionTests
{
    [Fact]
    public void PlatformApiModels_ShouldHaveNoProjectReferences()
    {
        var repoRoot = ArchitectureTestHelpers.FindRepositoryRoot();
        var csproj = Path.Combine(repoRoot, "Platform.API.Models", "Platform.API.Models.csproj");

        File.Exists(csproj).Should().BeTrue($"expected to find {csproj}");

        var references = ArchitectureTestHelpers.GetProjectReferences(csproj);

        references.Should().BeEmpty(
            "Platform.API.Models is the bottom of the dependency chain and must stay a zero-dependency POCO/records package.");
    }

    [Fact]
    public void PlatformSdkServices_ShouldNotReference_ComponentsOrTestApp()
    {
        var repoRoot = ArchitectureTestHelpers.FindRepositoryRoot();
        var csproj = Path.Combine(repoRoot, "Platform.SDK.Services", "Platform.SDK.Services.csproj");

        File.Exists(csproj).Should().BeTrue($"expected to find {csproj}");

        var references = ArchitectureTestHelpers.GetProjectReferences(csproj);

        references.Should().NotContain(
            new[] { "Platform.SDK.Components", "PlatformTestApp" },
            "Platform.SDK.Services sits below Components and PlatformTestApp in the dependency chain and must not reference upward.");
    }

    [Fact]
    public void PlatformSdkComponents_ShouldNotReference_TestApp()
    {
        var repoRoot = ArchitectureTestHelpers.FindRepositoryRoot();
        var csproj = Path.Combine(repoRoot, "Platform.SDK.Components", "Platform.SDK.Components.csproj");

        File.Exists(csproj).Should().BeTrue($"expected to find {csproj}");

        var references = ArchitectureTestHelpers.GetProjectReferences(csproj);

        references.Should().NotContain(
            "PlatformTestApp",
            "Platform.SDK.Components is a reusable library and must not reference the sample host app.");
    }

    [Fact]
    public void PlatformSdkComponents_ShouldReferenceExactlyTheExpectedProjects()
    {
        var repoRoot = ArchitectureTestHelpers.FindRepositoryRoot();
        var csproj = Path.Combine(repoRoot, "Platform.SDK.Components", "Platform.SDK.Components.csproj");

        File.Exists(csproj).Should().BeTrue($"expected to find {csproj}");

        var references = ArchitectureTestHelpers.GetProjectReferences(csproj);

        references.Should().BeEquivalentTo(
            new[] { "BiblePlatform.UsfmReferences", "Platform.API.Models", "Platform.API", "Platform.SDK.Services" },
            "an unpinned new reference here would silently widen Components' dependency surface " +
            "without a corresponding review of the layering rules.");
    }

    [Fact]
    public void PlatformTestApp_ShouldReferenceExactlyTheExpectedProjects()
    {
        var repoRoot = ArchitectureTestHelpers.FindRepositoryRoot();
        var csproj = Path.Combine(repoRoot, "PlatformTestApp", "PlatformTestApp.csproj");

        File.Exists(csproj).Should().BeTrue($"expected to find {csproj}");

        var references = ArchitectureTestHelpers.GetProjectReferences(csproj);

        references.Should().BeEquivalentTo(
            new[] { "Platform.SDK.Components" },
            "PlatformTestApp is the sample host and should reach every lower layer only through " +
            "Platform.SDK.Components, never by adding a direct reference to Platform.API/Platform.SDK.Services.");
    }

    [Fact]
    public void ProjectReferenceGraph_ShouldHaveNoCircularDependencies()
    {
        var repoRoot = ArchitectureTestHelpers.FindRepositoryRoot();

        var csprojFiles = Directory.EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        var graph = csprojFiles.ToDictionary(
            f => Path.GetFileNameWithoutExtension(f),
            f => ArchitectureTestHelpers.GetProjectReferences(f).ToArray());

        var cycles = new List<string>();
        foreach (var project in graph.Keys)
        {
            var path = new List<string> { project };
            var visiting = new HashSet<string> { project };
            if (TryFindCycle(project, graph, visiting, path, out var cycle))
            {
                cycles.Add(string.Join(" -> ", cycle));
            }
        }

        cycles.Should().BeEmpty("the project reference graph must remain a DAG for the layered architecture to hold.");
    }

    private static bool TryFindCycle(
        string project,
        IReadOnlyDictionary<string, string[]> graph,
        HashSet<string> visiting,
        List<string> path,
        out List<string> cycle)
    {
        if (graph.TryGetValue(project, out var references))
        {
            foreach (var reference in references)
            {
                if (!graph.ContainsKey(reference))
                    continue;

                if (!visiting.Add(reference))
                {
                    cycle = [.. path, reference];
                    return true;
                }

                path.Add(reference);
                if (TryFindCycle(reference, graph, visiting, path, out cycle))
                    return true;

                path.RemoveAt(path.Count - 1);
                visiting.Remove(reference);
            }
        }

        cycle = [];
        return false;
    }
}
