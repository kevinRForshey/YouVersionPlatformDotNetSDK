using FluentAssertions;
using Xunit;

namespace YouVersion.UsfmReferences.Tests;

public sealed class ReferenceTests
{
    // -------------------------------------------------------------------------
    // FromString: golden paths
    // -------------------------------------------------------------------------

    [Fact]
    public void FromString_ParsesWholeChapter()
    {
        var reference = Reference.FromString("GEN.1");

        reference.Book.Should().Be("GEN");
        reference.Chapter.Should().Be(1);
        reference.Section.Should().Be(0);
        reference.Intro.Should().Be(0);
        reference.Verses.Should().BeEmpty();
        reference.IsChapter().Should().BeTrue();
    }

    [Fact]
    public void FromString_ParsesSingleVerse()
    {
        var reference = Reference.FromString("GEN.1.1");

        reference.Verses.Should().Equal(new VerseRange(1, 1));
        reference.IsSingleVerse().Should().BeTrue();
    }

    [Fact]
    public void FromString_ParsesVerseRange()
    {
        var reference = Reference.FromString("GEN.1.1-3");

        reference.Verses.Should().Equal(new VerseRange(1, 3));
        reference.IsVerseRange().Should().BeTrue();
    }

    [Fact]
    public void FromString_ParsesMultipleNonAdjacentVerses()
    {
        var reference = Reference.FromString("GEN.1.1+GEN.1.3");

        reference.Verses.Should().Equal(new VerseRange(1, 1), new VerseRange(3, 3));
    }

    [Fact]
    public void FromString_ParsesIntroChapter()
    {
        var reference = Reference.FromString("GEN.INTRO1");

        reference.Intro.Should().Be(1);
        reference.Chapter.Should().Be(0);
        reference.IsIntro().Should().BeTrue();
    }

    [Fact]
    public void FromString_ParsesChapterWithSection()
    {
        var reference = Reference.FromString("GEN.1_1");

        reference.Chapter.Should().Be(1);
        reference.Section.Should().Be(1);
        reference.Verses.Should().BeEmpty();
        reference.IsChapter().Should().BeTrue();
    }

    [Fact]
    public void FromString_ParsesChapterWithSectionAndVerse()
    {
        var reference = Reference.FromString("GEN.1_1.1");

        reference.Chapter.Should().Be(1);
        reference.Section.Should().Be(1);
        reference.Verses.Should().Equal(new VerseRange(1, 1));
    }

    // -------------------------------------------------------------------------
    // FromString: verse range normalization (sorting, merging)
    // -------------------------------------------------------------------------

    [Fact]
    public void FromString_MergesAdjacentRanges()
    {
        var reference = Reference.FromString("GEN.1.1-3+GEN.1.4-6");

        reference.Verses.Should().Equal(new VerseRange(1, 6));
    }

    [Fact]
    public void FromString_MergesAdjacentSingleVerses()
    {
        var reference = Reference.FromString("GEN.1.1+GEN.1.2");

        reference.Verses.Should().Equal(new VerseRange(1, 2));
    }

    [Fact]
    public void FromString_MergesOverlappingRanges()
    {
        var reference = Reference.FromString("GEN.1.5+GEN.1.1-6");

        reference.Verses.Should().Equal(new VerseRange(1, 6));
    }

    [Fact]
    public void FromString_SortsOutOfOrderVerses()
    {
        var reference = Reference.FromString("GEN.1.10+GEN.1.1");

        reference.Verses.Should().Equal(new VerseRange(1, 1), new VerseRange(10, 10));
    }

    [Fact]
    public void FromString_DoesNotMergeRangesWithGapGreaterThanOne()
    {
        var reference = Reference.FromString("GEN.1.1+GEN.1.3");

        reference.Verses.Should().HaveCount(2);
    }

    [Fact]
    public void FromString_CollapsesExplicitEqualRangeToSingleVerse()
    {
        var reference = Reference.FromString("GEN.1.5-5");

        reference.IsSingleVerse().Should().BeTrue();
        reference.ToString().Should().Be("GEN.1.5");
    }

    // -------------------------------------------------------------------------
    // FromString: a chapter-only part collapses the whole reference to a
    // chapter, silently discarding any verse parts joined alongside it.
    // -------------------------------------------------------------------------

    [Fact]
    public void FromString_ChapterPartFirst_DiscardsJoinedVerses()
    {
        var reference = Reference.FromString("GEN.1+GEN.1.5");

        reference.Should().Be(Reference.FromString("GEN.1"));
        reference.IsChapter().Should().BeTrue();
    }

    [Fact]
    public void FromString_ChapterPartLast_DiscardsJoinedVerses()
    {
        var reference = Reference.FromString("GEN.1.5+GEN.1");

        reference.Should().Be(Reference.FromString("GEN.1"));
        reference.IsChapter().Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // FromString: leading zeros / numeric edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void FromString_AllowsLeadingZerosInChapter()
    {
        var reference = Reference.FromString("GEN.007");

        reference.Chapter.Should().Be(7);
    }

    [Theory]
    [InlineData("GEN.999")]
    [InlineData("GEN.1.999")]
    public void FromString_AllowsUpperBoundaryValues(string usfm)
    {
        var act = () => Reference.FromString(usfm);
        act.Should().NotThrow();
    }

    [Fact]
    public void FromString_ThrowsForChapterNumberOverflow()
    {
        var act = () => Reference.FromString("GEN.99999999999999999999");
        act.Should().Throw<FormatException>();
    }

    // -------------------------------------------------------------------------
    // FromString: structural and value errors
    // -------------------------------------------------------------------------

    [Fact]
    public void FromString_ThrowsForNullInput()
    {
        var act = () => Reference.FromString(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromString_ThrowsForEmptyOrWhitespace(string usfm)
    {
        var act = () => Reference.FromString(usfm);
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("XXX.1")]        // unknown book
    [InlineData("GE.1")]         // wrong-length book code
    [InlineData("gen.1")]        // lowercase: FromString is case-sensitive
    [InlineData("GEN.1.1.1")]    // too many dot-separated parts
    [InlineData("GEN.0")]        // chapter below range
    [InlineData("GEN.1000")]     // chapter at/above range
    [InlineData("GEN.abc")]      // non-numeric chapter
    [InlineData("GEN.1.0")]      // verse below range
    [InlineData("GEN.1.1000")]   // verse at/above range
    [InlineData("GEN.1.5-3")]    // reversed verse range
    [InlineData("GEN.1.-5")]     // malformed negative-looking verse
    [InlineData("GEN.INTRO")]    // intro with no number
    [InlineData("GEN.INTRO0")]   // intro below range
    [InlineData("GEN.INTROX")]   // non-numeric intro
    [InlineData("GEN.1_0")]      // section below range
    [InlineData("GEN.1_a")]      // non-numeric section
    public void FromString_ThrowsForInvalidUsfm(string usfm)
    {
        var act = () => Reference.FromString(usfm);
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("GEN.1.1+GEN.2.1")]      // different chapter
    [InlineData("GEN.1.1+EXO.1.1")]      // different book
    [InlineData("GEN.1_1.1+GEN.1_2.1")]  // different section
    public void FromString_ThrowsWhenJoinedPartsDisagreeOnChapter(string usfm)
    {
        var act = () => Reference.FromString(usfm);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void FromString_BookOnlyReference_ParsesButIsUnrenderable()
    {
        var reference = Reference.FromString("GEN");

        reference.Book.Should().Be("GEN");
        reference.IsChapter().Should().BeFalse();
        reference.IsIntro().Should().BeFalse();
        reference.IsSingleVerse().Should().BeFalse();
        reference.IsVerseRange().Should().BeFalse();

        var act = () => reference.ToString();
        act.Should().Throw<InvalidOperationException>();
    }

    // -------------------------------------------------------------------------
    // TryFromString
    // -------------------------------------------------------------------------

    [Fact]
    public void TryFromString_ReturnsTrueAndReference_ForValidUsfm()
    {
        bool ok = Reference.TryFromString("GEN.1.1", out var reference);

        ok.Should().BeTrue();
        reference.Should().Be(Reference.FromString("GEN.1.1"));
    }

    [Fact]
    public void TryFromString_ReturnsFalse_ForInvalidUsfm()
    {
        bool ok = Reference.TryFromString("not a reference", out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void TryFromString_ReturnsFalse_ForNull()
    {
        bool ok = Reference.TryFromString(null, out _);

        ok.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // ToString round trips
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN.1")]
    [InlineData("GEN.1_1")]
    [InlineData("GEN.INTRO1")]
    [InlineData("GEN.1.1")]
    [InlineData("GEN.1.1-3")]
    [InlineData("GEN.1_1.1")]
    [InlineData("GEN.1.1+GEN.1.3")]
    public void ToString_RoundTripsExactInput(string usfm)
    {
        Reference.FromString(usfm).ToString().Should().Be(usfm);
    }

    // -------------------------------------------------------------------------
    // Predicates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN.1", true, false, false, false)]
    [InlineData("GEN.INTRO1", false, true, false, false)]
    [InlineData("GEN.1.1", false, false, true, false)]
    [InlineData("GEN.1.1-3", false, false, false, true)]
    public void Predicates_ClassifyReferenceKindExclusively(
        string usfm, bool isChapter, bool isIntro, bool isSingleVerse, bool isVerseRange)
    {
        var reference = Reference.FromString(usfm);

        reference.IsChapter().Should().Be(isChapter);
        reference.IsIntro().Should().Be(isIntro);
        reference.IsSingleVerse().Should().Be(isSingleVerse);
        reference.IsVerseRange().Should().Be(isVerseRange);
    }

    // -------------------------------------------------------------------------
    // ToChapterOrIntro / ToSingleVerses / ToVerseRanges
    // -------------------------------------------------------------------------

    [Fact]
    public void ToChapterOrIntro_StripsVerses()
    {
        var reference = Reference.FromString("GEN.1.1-3");

        var chapterOnly = reference.ToChapterOrIntro();

        chapterOnly.IsChapter().Should().BeTrue();
        chapterOnly.Verses.Should().BeEmpty();
        chapterOnly.Should().Be(Reference.FromString("GEN.1"));
    }

    [Fact]
    public void ToSingleVerses_ExpandsRangeIntoOnePerVerse()
    {
        var reference = Reference.FromString("GEN.1.1-3");

        var singles = reference.ToSingleVerses();

        singles.Should().HaveCount(3);
        singles.Select(r => r.ToString()).Should().Equal("GEN.1.1", "GEN.1.2", "GEN.1.3");
        singles.Should().OnlyContain(r => r.IsSingleVerse());
    }

    [Fact]
    public void ToSingleVerses_ExpandsMultipleRangesInOrder()
    {
        var reference = Reference.FromString("GEN.1.1-2+GEN.1.5");

        var singles = reference.ToSingleVerses();

        singles.Select(r => r.ToString()).Should().Equal("GEN.1.1", "GEN.1.2", "GEN.1.5");
    }

    [Fact]
    public void ToSingleVerses_ReturnsEmpty_ForChapterOnlyReference()
    {
        var reference = Reference.FromString("GEN.1");

        reference.ToSingleVerses().Should().BeEmpty();
    }

    [Fact]
    public void ToVerseRanges_SplitsIntoOneReferencePerContiguousRange()
    {
        var reference = Reference.FromString("GEN.1.1-3+GEN.1.5");

        var ranges = reference.ToVerseRanges();

        ranges.Select(r => r.ToString()).Should().Equal("GEN.1.1-3", "GEN.1.5");
    }

    [Fact]
    public void ToVerseRanges_ReturnsEmpty_ForChapterOnlyReference()
    {
        var reference = Reference.FromString("GEN.1");

        reference.ToVerseRanges().Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Constructor normalization (verses passed directly, out of order)
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_SortsAndMergesAdjacentVerses()
    {
        var reference = new Reference("GEN", 1, verses: [new VerseRange(3, 3), new VerseRange(1, 2)]);

        reference.Verses.Should().Equal(new VerseRange(1, 3));
    }

    [Fact]
    public void Constructor_SortsNonAdjacentVersesWithoutMerging()
    {
        var reference = new Reference("GEN", 1, verses: [new VerseRange(5, 5), new VerseRange(1, 3)]);

        reference.Verses.Should().Equal(new VerseRange(1, 3), new VerseRange(5, 5));
    }

    [Fact]
    public void Constructor_DefaultsToEmptyVerses_WhenNoneProvided()
    {
        var reference = new Reference("GEN", 1);

        reference.Verses.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_DefaultParameterless_IsInertAndUnrenderable()
    {
        var reference = new Reference();

        reference.Book.Should().Be(string.Empty);
        reference.Chapter.Should().Be(0);
        reference.Verses.Should().BeEmpty();
        reference.IsChapter().Should().BeFalse();
        reference.IsIntro().Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Canon
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GEN", Canon.OldTestament)]
    [InlineData("JHN", Canon.NewTestament)]
    [InlineData("TOB", Canon.Apocrypha)]
    [InlineData("", Canon.Apocrypha)]
    public void Canon_ReflectsBookCatalog(string book, Canon expected)
    {
        new Reference(book).Canon.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    [Fact]
    public void Equals_IsOrderIndependentForVerses()
    {
        var a = new Reference("GEN", 1, verses: [new VerseRange(1, 1), new VerseRange(3, 3)]);
        var b = new Reference("GEN", 1, verses: [new VerseRange(3, 3), new VerseRange(1, 1)]);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Theory]
    [InlineData("GEN.1", "EXO.1")]        // different book
    [InlineData("GEN.1", "GEN.2")]        // different chapter
    [InlineData("GEN.1_1", "GEN.1_2")]    // different section
    [InlineData("GEN.INTRO1", "GEN.INTRO2")] // different intro
    [InlineData("GEN.1.1", "GEN.1.2")]    // different verses
    public void Equals_ReturnsFalse_ForDifferingComponents(string left, string right)
    {
        Reference.FromString(left).Should().NotBe(Reference.FromString(right));
    }

    [Fact]
    public void Equals_ReturnsFalse_ForNull()
    {
        var reference = Reference.FromString("GEN.1");

        reference.Equals(null).Should().BeFalse();
        (reference == null).Should().BeFalse();
        (null == reference).Should().BeFalse();
    }

    [Fact]
    public void Equals_ReturnsTrue_ForSameInstance()
    {
        var reference = Reference.FromString("GEN.1");

#pragma warning disable CS1718 // intentional self-comparison to exercise the ReferenceEquals fast path
        (reference == reference).Should().BeTrue();
#pragma warning restore CS1718
    }

    [Fact]
    public void NullEquality_BothNull_IsTrue()
    {
        Reference? left = null;
        Reference? right = null;

        (left == right).Should().BeTrue();
    }
}
