using System.Text.RegularExpressions;

namespace Platform.API.Tests.Architecture;

internal static class ArchitectureTestHelpers
{
    public static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BiblePlatform.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output path.");
    }

    /// <summary>
    /// Strips C#/Razor comments so a type name mentioned only in a doc comment or a
    /// code comment doesn't register as a false-positive architecture violation.
    /// </summary>
    public static string StripComments(string source)
    {
        var noBlockComments = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var noRazorComments = Regex.Replace(noBlockComments, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        var noLineComments = Regex.Replace(noRazorComments, @"///.*$|(?<!:)//.*$", string.Empty, RegexOptions.Multiline);
        return noLineComments;
    }

    public static IEnumerable<string> GetProjectReferences(string csprojPath)
    {
        var text = File.ReadAllText(csprojPath);
        var matches = Regex.Matches(text, @"<ProjectReference\s+([^>]*?)/?>");
        foreach (Match match in matches)
        {
            var attributes = match.Groups[1].Value;

            // OutputItemType="Analyzer" ReferenceOutputAssembly="false" references (e.g. Platform.API.Analyzers)
            // are dev-time tooling, not a real assembly dependency -- they don't participate in the
            // Models -> API -> Services -> Components -> PlatformTestApp layering this graph enforces.
            if (Regex.IsMatch(attributes, @"ReferenceOutputAssembly\s*=\s*""false"""))
                continue;

            var includeMatch = Regex.Match(attributes, @"Include\s*=\s*""([^""]+)""");
            if (!includeMatch.Success)
                continue;

            // csproj paths use '\' regardless of OS; Path.GetFileNameWithoutExtension only
            // splits on '/' on non-Windows, so normalize separators before extracting the name.
            var normalized = includeMatch.Groups[1].Value.Replace('\\', '/');
            yield return Path.GetFileNameWithoutExtension(normalized);
        }
    }
}
