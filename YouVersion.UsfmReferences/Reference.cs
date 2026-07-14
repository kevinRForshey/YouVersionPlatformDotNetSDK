namespace YouVersion.UsfmReferences;

/// <summary>
/// An immutable USFM scripture reference: book, chapter, section, intro, and verse ranges.
/// </summary>
/// <remarks>
/// On construction, verse ranges are sorted and intersecting or adjacent ranges are merged,
/// matching the Python dataclass's <c>__post_init__</c> normalization.
/// <para>Examples:</para>
/// <list type="bullet">
///   <item><c>Reference.FromString("GEN.1")</c> — whole chapter</item>
///   <item><c>Reference.FromString("GEN.1.1-3")</c> — verse range</item>
///   <item><c>Reference.FromString("GEN.1.1+GEN.1.3")</c> — multiple verses</item>
///   <item><c>Reference.FromString("GEN.INTRO1")</c> — intro chapter</item>
///   <item><c>Reference.FromString("GEN.1_1.1")</c> — chapter with section</item>
/// </list>
/// </remarks>
public sealed class Reference : IEquatable<Reference>
{
    private readonly VerseRange[] _verses;

    /// <summary>The USFM book code (e.g. "GEN"), or empty when unset.</summary>
    public string Book { get; }

    /// <summary>The chapter number, or 0 when the reference is not chapter-based.</summary>
    public int Chapter { get; }

    /// <summary>The section (sub-chapter) number, or 0 when absent.</summary>
    public int Section { get; }

    /// <summary>The intro number, or 0 when the reference is not an intro.</summary>
    public int Intro { get; }

    /// <summary>The normalized (sorted, merged) verse ranges; empty for chapter/intro references.</summary>
    public IReadOnlyList<VerseRange> Verses => _verses;

    /// <summary>The canon category of <see cref="Book"/>.</summary>
    public Canon Canon => BookCatalog.GetCanon(Book);

    /// <summary>
    /// Creates a reference. Verse ranges are normalized (sorted and merged) on construction,
    /// so callers may pass them in any order.
    /// </summary>
    public Reference(
        string book = "",
        int chapter = 0,
        int section = 0,
        int intro = 0,
        IEnumerable<VerseRange>? verses = null)
    {
        Book = book;
        Chapter = chapter;
        Section = section;
        Intro = intro;
        _verses = NormalizeVerses(verses);
    }

    /// <summary>
    /// Parses a USFM reference string, throwing on any structural or value error.
    /// </summary>
    /// <exception cref="FormatException">The reference string is invalid.</exception>
    public static Reference FromString(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        s = s.Trim();
        if (s.Length == 0)
        {
            throw new FormatException("empty reference");
        }

        string[] parts = s.Split('+');
        var first = ParsePart(parts[0]);

        string book = first.Book;
        int chapter = first.Chapter;
        int section = first.Section;
        int intro = first.Intro;

        var verses = new List<VerseRange>();
        if (first.Verse is { } firstVerse)
        {
            verses.Add(firstVerse);
        }

        // is_chapter() on the first part: chapter present with no verses.
        bool isFullChapter = chapter > 0 && verses.Count == 0;

        for (int i = 1; i < parts.Length; i++)
        {
            var part = ParsePart(parts[i]);
            bool partIsChapter = part.Chapter > 0 && part.Verse is null;
            if (partIsChapter)
            {
                isFullChapter = true;
            }

            // All parts must resolve to the same chapter-or-intro (book/chapter/section/intro).
            if (book != part.Book || chapter != part.Chapter ||
                section != part.Section || intro != part.Intro)
            {
                throw new FormatException("references must be in the same chapter");
            }

            if (part.Verse is { } v)
            {
                verses.Add(v);
            }
        }

        return isFullChapter
            ? new Reference(book, chapter, section, intro)
            : new Reference(book, chapter, section, intro, verses);
    }

    /// <summary>
    /// Attempts to parse a USFM reference string without throwing.
    /// </summary>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    public static bool TryFromString(string? s, out Reference reference)
    {
        if (s is not null)
        {
            try
            {
                reference = FromString(s);
                return true;
            }
            catch (FormatException)
            {
                // Fall through to the failure result below.
            }
        }

        reference = null!;
        return false;
    }

    private readonly record struct ParsedPart(string Book, int Chapter, int Section, int Intro, VerseRange? Verse);

    private static ParsedPart ParsePart(string s)
    {
        string[] parts = s.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            throw new FormatException($"invalid USFM code {s}");
        }

        string book = parts[0];
        if (book.Length != 3 || !BookCatalog.IsKnownBook(book))
        {
            throw new FormatException($"invalid USFM book code {book}");
        }

        if (parts.Length == 1)
        {
            return new ParsedPart(book, 0, 0, 0, null);
        }

        var (chapter, section, intro) = ParseChapter(parts[1]);
        VerseRange? verse = parts.Length == 3 ? ParseVerseRange(parts[2]) : null;
        return new ParsedPart(book, chapter, section, intro, verse);
    }

    private static (int Chapter, int Section, int Intro) ParseChapter(string chapterStr)
    {
        if (chapterStr.StartsWith("INTRO", StringComparison.Ordinal))
        {
            string num = chapterStr[5..];
            if (!TryParsePositiveInt(num, out int introValue) || introValue < 1)
            {
                throw new FormatException($"invalid intro reference {chapterStr}");
            }

            return (0, 0, introValue);
        }

        // chapter[_section]
        string[] parts = chapterStr.Split('_', 2);
        if (!TryParsePositiveInt(parts[0], out int chapter) || chapter is < 1 or >= 1000)
        {
            throw new FormatException($"invalid chapter {chapterStr}");
        }

        int section = 0;
        if (parts.Length == 2)
        {
            if (!TryParsePositiveInt(parts[1], out section) || section < 1)
            {
                throw new FormatException($"invalid section {chapterStr}");
            }
        }

        return (chapter, section, 0);
    }

    private static VerseRange ParseVerseRange(string verseStr)
    {
        string[] parts = verseStr.Split('-', 2);
        if (!TryParsePositiveInt(parts[0], out int start))
        {
            throw new FormatException($"invalid verse range {verseStr}");
        }

        int end;
        if (parts.Length == 2)
        {
            if (!TryParsePositiveInt(parts[1], out end))
            {
                throw new FormatException($"invalid verse range {verseStr}");
            }
        }
        else
        {
            end = start;
        }

        if (!(start >= 1 && start <= end && end < 1000))
        {
            throw new FormatException($"invalid verse range {verseStr}");
        }

        return new VerseRange(start, end);
    }

    /// <summary>
    /// Parses a string of one or more ASCII digits into a non-negative <see cref="int"/>.
    /// Returns <c>false</c> for empty input, non-digit characters, or values that overflow
    /// <see cref="int"/>. This mirrors Python's <c>str.isdigit()</c> guard while keeping
    /// out-of-range numerics (e.g. an absurdly long chapter) as parse failures rather than
    /// throwing — the same outcome the Python range checks ultimately produce.
    /// </summary>
    private static bool TryParsePositiveInt(string s, out int value)
    {
        value = 0;
        if (s.Length == 0)
        {
            return false;
        }

        foreach (char c in s)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return int.TryParse(s, out value);
    }

    private static VerseRange[] NormalizeVerses(IEnumerable<VerseRange>? verses)
    {
        if (verses is null)
        {
            return [];
        }

        var sorted = verses.ToList();
        if (sorted.Count == 0)
        {
            return [];
        }

        sorted.Sort();
        var merged = new List<VerseRange> { sorted[0] };
        for (int i = 1; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var last = merged[^1];
            if (current.Start <= last.End + 1)
            {
                merged[^1] = new VerseRange(last.Start, Math.Max(last.End, current.End));
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged.ToArray();
    }

    /// <summary>Returns <c>true</c> if the reference is a whole chapter (no verses).</summary>
    public bool IsChapter() => Chapter > 0 && _verses.Length == 0;

    /// <summary>Returns <c>true</c> if the reference is an intro.</summary>
    public bool IsIntro() => Intro > 0;

    /// <summary>Returns <c>true</c> if the reference is a single verse.</summary>
    public bool IsSingleVerse() =>
        Chapter > 0 && _verses.Length == 1 && _verses[0].Start == _verses[0].End;

    /// <summary>Returns <c>true</c> if the reference is a single multi-verse range.</summary>
    public bool IsVerseRange() =>
        Chapter > 0 && _verses.Length == 1 && _verses[0].Start != _verses[0].End;

    /// <summary>Returns a reference for only the chapter or intro (verses removed).</summary>
    public Reference ToChapterOrIntro() => new(Book, Chapter, Section, Intro);

    /// <summary>Expands the reference into one single-verse reference per verse.</summary>
    public IReadOnlyList<Reference> ToSingleVerses()
    {
        var result = new List<Reference>();
        foreach (var range in _verses)
        {
            for (int v = range.Start; v <= range.End; v++)
            {
                result.Add(new Reference(Book, Chapter, Section, Intro, [new VerseRange(v, v)]));
            }
        }

        return result;
    }

    /// <summary>Splits the reference into one reference per contiguous verse range.</summary>
    public IReadOnlyList<Reference> ToVerseRanges() =>
        _verses.Select(r => new Reference(Book, Chapter, Section, Intro, [r])).ToArray();

    /// <summary>
    /// Returns the USFM string representation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The reference is in an unrenderable state (no chapter, intro, or verses) — for example a
    /// book-only reference such as the one produced by parsing "GEN". This mirrors the Python
    /// <c>__str__</c> contract, which raises in the same case.
    /// </exception>
    public override string ToString()
    {
        if (_verses.Length > 0)
        {
            string chapterStr = ChapterString();
            return string.Join(
                "+",
                _verses.Select(r =>
                {
                    string range = r.Start == r.End ? $"{r.Start}" : $"{r.Start}-{r.End}";
                    return $"{Book}.{chapterStr}.{range}";
                }));
        }

        if (Chapter > 0)
        {
            return $"{Book}.{ChapterString()}";
        }

        if (Intro > 0)
        {
            return $"{Book}.INTRO{Intro}";
        }

        throw new InvalidOperationException(
            "Reference is in an invalid state (missing chapter, intro, or verses).");
    }

    private string ChapterString() => Section > 0 ? $"{Chapter}_{Section}" : $"{Chapter}";

    /// <inheritdoc />
    public bool Equals(Reference? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Book == other.Book
            && Chapter == other.Chapter
            && Section == other.Section
            && Intro == other.Intro
            && _verses.AsSpan().SequenceEqual(other._verses);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Reference);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Book);
        hash.Add(Chapter);
        hash.Add(Section);
        hash.Add(Intro);
        foreach (var range in _verses)
        {
            hash.Add(range);
        }

        return hash.ToHashCode();
    }

    /// <summary>Value equality operator.</summary>
    public static bool operator ==(Reference? left, Reference? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Value inequality operator.</summary>
    public static bool operator !=(Reference? left, Reference? right) => !(left == right);
}
