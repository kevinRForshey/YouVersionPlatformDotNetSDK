using System.Text.RegularExpressions;

namespace Platform.API.Tests.Architecture;

internal static class ArchitectureTestHelpers
{
    public static string FindRepositoryRoot()
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
        var matches = Regex.Matches(text, @"<ProjectReference\s+Include=""([^""]+)""");
        foreach (Match match in matches)
        {
            // csproj paths use '\' regardless of OS; Path.GetFileNameWithoutExtension only
            // splits on '/' on non-Windows, so normalize separators before extracting the name.
            var normalized = match.Groups[1].Value.Replace('\\', '/');
            yield return Path.GetFileNameWithoutExtension(normalized);
        }
    }
}
