using FluentAssertions;
using Xunit;

namespace YouVersion.UsfmReferences.Tests;

public sealed class BookCatalogTests
{
    [Theory]
    [InlineData("GEN", Canon.OldTestament)]
    [InlineData("MAL", Canon.OldTestament)]
    [InlineData("MAT", Canon.NewTestament)]
    [InlineData("REV", Canon.NewTestament)]
    [InlineData("TOB", Canon.Apocrypha)]
    [InlineData("ZZZ", Canon.Apocrypha)] // unknown code falls back to apocrypha
    [InlineData("", Canon.Apocrypha)]
    public void GetCanon_ReturnsExpectedCanon(string book, Canon expected)
    {
        BookCatalog.GetCanon(book).Should().Be(expected);
    }

    [Theory]
    [InlineData("GEN", true)]
    [InlineData("gen", false)]  // exact, case-sensitive match only
    [InlineData("ZZZ", false)]
    [InlineData("", false)]
    public void IsKnownBook_IsCaseSensitiveExactMatch(string book, bool expected)
    {
        BookCatalog.IsKnownBook(book).Should().Be(expected);
    }

    [Fact]
    public void OldTestamentBooks_ContainsThirtyNineBooksInCanonicalOrder()
    {
        BookCatalog.OldTestamentBooks.Should().HaveCount(39);
        BookCatalog.OldTestamentBooks.First().Should().Be("GEN");
        BookCatalog.OldTestamentBooks.Last().Should().Be("MAL");
        BookCatalog.OldTestamentBooks.Should().OnlyContain(b => BookCatalog.GetCanon(b) == Canon.OldTestament);
    }

    [Fact]
    public void NewTestamentBooks_ContainsCanonicalOrderStartingAtMatthew()
    {
        // 27 canonical NT books plus the synthetic "LKA" (Luke-Acts combo) code, which is
        // also tagged NewTestament and appended after Revelation.
        BookCatalog.NewTestamentBooks.Should().HaveCount(28);
        BookCatalog.NewTestamentBooks.First().Should().Be("MAT");
        BookCatalog.NewTestamentBooks.Should().Contain("REV");
        BookCatalog.NewTestamentBooks.Should().OnlyContain(b => BookCatalog.GetCanon(b) == Canon.NewTestament);
    }

    [Fact]
    public void Books_IsSupersetOfOldAndNewTestamentBooks()
    {
        BookCatalog.Books.Should().Contain(BookCatalog.OldTestamentBooks);
        BookCatalog.Books.Should().Contain(BookCatalog.NewTestamentBooks);
        BookCatalog.Books.Should().Contain("TOB", "the apocrypha is included in the full book list");
        BookCatalog.Books.Should().OnlyHaveUniqueItems();
    }
}
