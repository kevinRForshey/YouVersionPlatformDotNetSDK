using FluentAssertions;
using Xunit;

namespace BiblePlatform.UsfmReferences.Tests;

public sealed class UsfmReferenceServiceTests
{
    private readonly UsfmReferenceService _service = new();

    // -------------------------------------------------------------------------
    // ConvertBookToCanon
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN", Canon.OldTestament)]
    [InlineData("JHN", Canon.NewTestament)]
    [InlineData("TOB", Canon.Apocrypha)]
    [InlineData("ZZZ", Canon.Apocrypha)] // unknown book falls back to apocrypha
    [InlineData("", Canon.Apocrypha)]
    public void ConvertBookToCanon_ReturnsExpectedCanon(string book, Canon expected)
    {
        _service.ConvertBookToCanon(book).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // ConvertBookNameToUsfm: golden paths
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN", "GEN")]     // USFM code passed straight through
    [InlineData("Genesis", "GEN")] // full name
    [InlineData("gen", "GEN")]     // lowercase abbreviation
    [InlineData("Gn", "GEN")]      // short abbreviation
    public void ConvertBookNameToUsfm_ResolvesSingleBookNames(string name, string expected)
    {
        _service.ConvertBookNameToUsfm(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("  GENESIS  ")]
    [InlineData("Gen.")]
    [InlineData("GeNeSiS")]
    public void ConvertBookNameToUsfm_IsCaseAndPunctuationInsensitive(string name)
    {
        _service.ConvertBookNameToUsfm(name).Should().Be("GEN");
    }

    // -------------------------------------------------------------------------
    // ConvertBookNameToUsfm: numbered books (arabic, roman, word ordinals)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1Sam", "1SA")]
    [InlineData("1 Samuel", "1SA")]
    [InlineData("2 Samuel", "2SA")]
    public void ConvertBookNameToUsfm_ResolvesArabicOrdinals(string name, string expected)
    {
        _service.ConvertBookNameToUsfm(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("I Sam", "1SA")]
    [InlineData("II Samuel", "2SA")]
    [InlineData("III John", "3JN")]
    public void ConvertBookNameToUsfm_ResolvesRomanOrdinals(string name, string expected)
    {
        _service.ConvertBookNameToUsfm(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("First Samuel", "1SA")]
    [InlineData("Second Kings", "2KI")]
    [InlineData("Third John", "3JN")]
    public void ConvertBookNameToUsfm_ResolvesWordOrdinals(string name, string expected)
    {
        _service.ConvertBookNameToUsfm(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("IJohn", "1JN")]
    [InlineData("IIJohn", "2JN")]
    [InlineData("IIIJohn", "3JN")]
    public void ConvertBookNameToUsfm_PrefersLongestRomanOrdinalMatch(string name, string expected)
    {
        // "III" must be matched before the shorter "I"/"II" prefixes, or the resolver would
        // peel off "I" and fail to find a "IIJohn"/"IIIJohn" stem in the numbered-book catalog.
        _service.ConvertBookNameToUsfm(name).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // ConvertBookNameToUsfm: edge cases and failures
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Frobnicate")]
    [InlineData("4 Samuel")]   // out-of-range ordinal for a real stem
    [InlineData("1 Foo")]      // valid-looking ordinal, unknown stem
    [InlineData("1")]          // ordinal with no remaining stem
    [InlineData("0Sam")]       // ordinal value with no matching entry
    public void ConvertBookNameToUsfm_ReturnsNull_ForUnresolvableNames(string? name)
    {
        _service.ConvertBookNameToUsfm(name).Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // IsValidChapter / IsValidChapterOrIntro
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN.1", true)]
    [InlineData("GEN.1.1", false)]      // has a verse, not a bare chapter
    [InlineData("GEN.INTRO1", false)]   // intro, not a chapter
    [InlineData("not a reference", false)]
    public void IsValidChapter_ReturnsExpected(string reference, bool expected)
    {
        _service.IsValidChapter(reference).Should().Be(expected);
    }

    [Theory]
    [InlineData("GEN.1", true)]
    [InlineData("GEN.INTRO1", true)]
    [InlineData("GEN.1.1", false)]
    [InlineData("not a reference", false)]
    public void IsValidChapterOrIntro_ReturnsExpected(string reference, bool expected)
    {
        _service.IsValidChapterOrIntro(reference).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // IsValidUsfm / IsValidPassage (IsValidPassage mirrors IsValidUsfm)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN.1", true)]
    [InlineData("GEN.1.1", true)]
    [InlineData("GEN.1.1-3", true)]
    [InlineData("GEN.INTRO1", true)]
    [InlineData("not a reference", false)]
    [InlineData("", false)]
    public void IsValidUsfmAndIsValidPassage_AgreeOnExpectedResult(string reference, bool expected)
    {
        _service.IsValidUsfm(reference).Should().Be(expected);
        _service.IsValidPassage(reference).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // IsValidVerse
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN.1.1", true)]
    [InlineData("GEN.1.1-3", false)] // a range, not a single verse
    [InlineData("GEN.1", false)]     // a chapter, no verse at all
    [InlineData("not a reference", false)]
    public void IsValidVerse_ReturnsExpected(string reference, bool expected)
    {
        _service.IsValidVerse(reference).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // IsValidMultiUsfm
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN.1.1+GEN.1.3", true)]
    [InlineData("GEN.1.1", false)]     // only a single verse
    [InlineData("GEN.1.1-3", true)]    // a multi-verse range also counts as "multi"
    [InlineData("not a reference", false)]
    public void IsValidMultiUsfm_ReturnsExpected(string reference, bool expected)
    {
        _service.IsValidMultiUsfm(reference).Should().Be(expected);
    }

    [Fact]
    public void IsValidMultiUsfm_NormalizesCustomDelimiter()
    {
        _service.IsValidMultiUsfm("GEN.1.1,GEN.1.3", ',').Should().BeTrue();
    }
}
