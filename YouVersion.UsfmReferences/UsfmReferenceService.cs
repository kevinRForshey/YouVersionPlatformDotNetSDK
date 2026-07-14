namespace YouVersion.UsfmReferences;

/// <summary>
/// Default, stateless implementation of <see cref="IUsfmReferenceService"/>.
/// </summary>
public sealed class UsfmReferenceService : IUsfmReferenceService
{
    // Roman ordinals checked longest-first so "iii"/"ii" win over the "i" prefix.
    private static readonly KeyValuePair<string, int>[] RomanOrdinalsByLength =
        BookCatalog.RomanOrdinals.OrderByDescending(kv => kv.Key.Length).ToArray();

    /// <inheritdoc />
    public Canon ConvertBookToCanon(string book) => BookCatalog.GetCanon(book);

    /// <inheritdoc />
    public string? ConvertBookNameToUsfm(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        string stripped = name.Trim();

        // A USFM code
        if (BookCatalog.IsKnownBook(stripped))
        {
            return stripped;
        }

        string key = NormalizeKey(stripped);
        if (key.Length == 0)
        {
            return null;
        }

        if (BookCatalog.BookNames.TryGetValue(key, out string? code))
        {
            return code;
        }

        // Numbered books: peel the leading ordinal and resolve the remaining stem.
        if (TrySplitLeadingOrdinal(key, out int number, out string stem)
            && BookCatalog.NumberedBooks.TryGetValue(stem, out var ordinals)
            && ordinals.TryGetValue(number, out string? numberedCode))
        {
            return numberedCode;
        }

        return null;
    }

    /// <inheritdoc />
    public bool IsValidChapter(string reference) =>
        Reference.TryFromString(reference, out var r) && r.IsChapter();

    /// <inheritdoc />
    public bool IsValidChapterOrIntro(string reference) =>
        Reference.TryFromString(reference, out var r) && (r.IsChapter() || r.IsIntro());

    /// <inheritdoc />
    public bool IsValidUsfm(string reference) =>
        Reference.TryFromString(reference, out _);

    /// <inheritdoc />
    public bool IsValidVerse(string reference) =>
        Reference.TryFromString(reference, out var r) && r.IsSingleVerse();

    /// <inheritdoc />
    public bool IsValidMultiUsfm(string reference, char delimiter = '+')
    {
        string normalized = reference.Replace(delimiter, '+');
        return Reference.TryFromString(normalized, out var r) && r.ToSingleVerses().Count > 1;
    }

    /// <inheritdoc />
    public bool IsValidPassage(string passage) => IsValidUsfm(passage);

    /// <summary>Lowercases and strips all non-alphanumeric characters from a book name.</summary>
    private static string NormalizeKey(string value)
    {
        string lowered = value.ToLowerInvariant();
        var builder = new System.Text.StringBuilder(lowered.Length);
        foreach (char c in lowered)
        {
            if (char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Splits a leading ordinal off a normalized book key. Recognizes arabic digits
    /// ("1john"), roman forms I/II/III ("iijohn"), and the words First/Second/Third
    /// ("firstjohn"). Returns the first form that leaves a non-empty remainder.
    /// </summary>
    private static bool TrySplitLeadingOrdinal(string key, out int number, out string stem)
    {
        // Leading arabic digits with a non-empty remainder.
        int digitCount = 0;
        while (digitCount < key.Length && char.IsAsciiDigit(key[digitCount]))
        {
            digitCount++;
        }

        if (digitCount > 0 && digitCount < key.Length
            && int.TryParse(key.AsSpan(0, digitCount), out number))
        {
            stem = key[digitCount..];
            return true;
        }

        foreach (var (word, value) in BookCatalog.OrdinalWords)
        {
            if (key.Length > word.Length && key.StartsWith(word, StringComparison.Ordinal))
            {
                number = value;
                stem = key[word.Length..];
                return true;
            }
        }

        foreach (var (roman, value) in RomanOrdinalsByLength)
        {
            if (key.Length > roman.Length && key.StartsWith(roman, StringComparison.Ordinal))
            {
                number = value;
                stem = key[roman.Length..];
                return true;
            }
        }

        
        number = 0;
        stem = string.Empty;
        return false;
    }
}
