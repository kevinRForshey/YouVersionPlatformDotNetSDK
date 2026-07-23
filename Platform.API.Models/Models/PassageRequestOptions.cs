namespace Platform.API.Models;

/// <summary>
/// Options that control how a passage is fetched from the Platform API.
/// </summary>
public sealed record PassageRequestOptions
{
    /// <summary>Gets the content format to return.</summary>
    /// <value>
    /// One of the <see cref="PassageFormat"/> values. The default is <see cref="PassageFormat.Text"/>.
    /// </value>
    public PassageFormat Format { get; init; } = PassageFormat.Text;

    /// <summary>Gets a value that indicates whether section headings are included in the response.</summary>
    /// <value>
    /// <see langword="true"/> if section headings are included; otherwise, <see langword="false"/>.
    /// Only applies to <see cref="PassageFormat.Html"/>. The default is <see langword="false"/>.
    /// </value>
    public bool IncludeHeadings { get; init; } = false;

    /// <summary>Gets a value that indicates whether footnotes are included in the response.</summary>
    /// <value>
    /// <see langword="true"/> if footnotes are included; otherwise, <see langword="false"/>.
    /// Only applies to <see cref="PassageFormat.Html"/>. The default is <see langword="false"/>.
    /// </value>
    public bool IncludeNotes { get; init; } = false;

    /// <summary>A shared instance representing the default text-only options.</summary>
    public static readonly PassageRequestOptions Default = new();
}
