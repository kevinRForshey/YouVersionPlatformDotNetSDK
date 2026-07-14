namespace YouVersion.UsfmReferences;

/// <summary>
/// High-level USFM reference operations: book conversions and reference validation.
/// Mirrors the public surface of the Python <c>usfm_references</c> package.
/// </summary>
/// <remarks>
/// Implementations are stateless and thread-safe, so a single instance may be registered
/// as a singleton and shared.
/// </remarks>
public interface IUsfmReferenceService
{
    /// <summary>Returns the canon category of a USFM book code (unknown codes map to apocrypha).</summary>
    Canon ConvertBookToCanon(string book);

    /// <summary>
    /// Returns the USFM book code for an English book name or common abbreviation, or
    /// <c>null</c> when no match is found.
    /// </summary>
    /// <remarks>
    /// Matching is case-insensitive and ignores whitespace and punctuation, so "Genesis",
    /// "gen", "Gen.", and "  GENESIS  " all return "GEN". Numbered books accept arabic, roman,
    /// and word ordinals: "1 Samuel", "1Sam", "I Sam", and "First Samuel" all return "1SA". A
    /// USFM code passed in directly (e.g. "GEN") is returned unchanged.
    /// </remarks>
    string? ConvertBookNameToUsfm(string? name);

    /// <summary>Returns <c>true</c> if the string is a validly structured USFM chapter reference.</summary>
    bool IsValidChapter(string reference);

    /// <summary>Returns <c>true</c> if the string is a valid USFM chapter or intro reference.</summary>
    bool IsValidChapterOrIntro(string reference);

    /// <summary>Returns <c>true</c> if the string is any validly structured USFM reference.</summary>
    bool IsValidUsfm(string reference);

    /// <summary>Returns <c>true</c> if the string is a valid single-verse USFM reference.</summary>
    bool IsValidVerse(string reference);

    /// <summary>
    /// Returns <c>true</c> if the string is a valid set of more than one verse, joined by
    /// <paramref name="delimiter"/> (which is normalized to '+'). All references must be in
    /// the same chapter.
    /// </summary>
    bool IsValidMultiUsfm(string reference, char delimiter = '+');

    /// <summary>Returns <c>true</c> if the string is a validly structured Bible reference passage.</summary>
    bool IsValidPassage(string passage);
}
