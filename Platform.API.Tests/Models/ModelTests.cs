using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Platform.API.Models;
using Xunit;

namespace Platform.API.Tests.Models;

public sealed class PagedResultTests
{
    [Fact]
    public void PagedResult_DefaultData_IsEmpty()
    {
        var result = new PagedResult<string>();
        result.Data.Should().BeEmpty();
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public void PagedResult_InitProperties_AreSet()
    {
        var result = new PagedResult<int>
        {
            Data = new List<int> { 1, 2, 3 },
            NextPageToken = "next"
        };

        result.Data.Should().HaveCount(3);
        result.NextPageToken.Should().Be("next");
    }

    [Fact]
    public void PagedResult_DeserializesFromJson_Correctly()
    {
        const string json = """
            { "data": [10, 20], "next_page_token": "tok-abc" }
            """;

        var result = JsonSerializer.Deserialize<PagedResult<int>>(json)!;

        result.Data.Should().HaveCount(2);
        result.Data[0].Should().Be(10);
        result.NextPageToken.Should().Be("tok-abc");
    }

    [Fact]
    public void PagedResult_DeserializesNullNextPageToken_Correctly()
    {
        const string json = """{"data":[],"next_page_token":null}""";

        var result = JsonSerializer.Deserialize<PagedResult<int>>(json)!;

        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public void PagedResult_SupportsWithExpression_ForImmutableUpdates()
    {
        var original = new PagedResult<string>
        {
            Data = new[] { "a" },
            NextPageToken = "tok"
        };

        var updated = original with { NextPageToken = "new-tok" };

        updated.NextPageToken.Should().Be("new-tok");
        updated.Data.Should().BeSameAs(original.Data);
        original.NextPageToken.Should().Be("tok"); // original unchanged
    }
}

public sealed class PassageRequestOptionsTests
{
    [Fact]
    public void Default_HasTextFormat_AndNoExtras()
    {
        var opts = PassageRequestOptions.Default;

        opts.Format.Should().Be(PassageFormat.Text);
        opts.IncludeHeadings.Should().BeFalse();
        opts.IncludeNotes.Should().BeFalse();
    }

    [Fact]
    public void PassageRequestOptions_CanBeConfiguredForHtml()
    {
        var opts = new PassageRequestOptions
        {
            Format = PassageFormat.Html,
            IncludeHeadings = true,
            IncludeNotes = true
        };

        opts.Format.Should().Be(PassageFormat.Html);
        opts.IncludeHeadings.Should().BeTrue();
        opts.IncludeNotes.Should().BeTrue();
    }

    [Fact]
    public void PassageRequestOptions_WithExpression_ProducesNewInstance()
    {
        var original = PassageRequestOptions.Default;
        var updated = original with { Format = PassageFormat.Html };

        updated.Format.Should().Be(PassageFormat.Html);
        original.Format.Should().Be(PassageFormat.Text); // original unchanged
    }

    [Fact]
    public void Default_IsSingletonInstance()
    {
        PassageRequestOptions.Default.Should().BeSameAs(PassageRequestOptions.Default);
    }
}

public sealed class BibleVersionTests
{
    [Fact]
    public void BibleVersion_DeserializesFromJson_Correctly()
    {
        const string json = """
            {
              "id": 3034, "abbreviation": "BSB", "localized_abbreviation": "BSB",
              "title": "Berean Standard Bible", "localized_title": "Berean Standard Bible",
              "language_tag": "en", "copyright": "Public Domain",
              "promotional_content": "Free to use", "publisher_url": null,
              "books": ["GEN","EXO"],
              "youversion_deep_link": "https://www.bible.com/versions/3034"
            }
            """;

        var version = JsonSerializer.Deserialize<BibleVersion>(json)!;

        version.Id.Should().Be(3034);
        version.Abbreviation.Should().Be("BSB");
        version.LanguageTag.Should().Be("en");
        version.Copyright.Should().Be("Public Domain");
        version.Books.Should().HaveCount(2);
        version.Books[0].Should().Be("GEN");
        version.PublisherUrl.Should().BeNull();
        version.YouVersionDeepLink.Should().Contain("3034");
    }

    [Fact]
    public void BibleVersion_Books_DefaultsToEmpty()
    {
        var version = new BibleVersion();
        version.Books.Should().BeEmpty();
    }
}

public sealed class PassageTests
{
    [Fact]
    public void Passage_DeserializesFromJson_Correctly()
    {
        const string json = """
            { "id": "JHN.3.16", "content": "For God so loved...", "reference": "John 3:16" }
            """;

        var passage = JsonSerializer.Deserialize<Passage>(json)!;

        passage.Id.Should().Be("JHN.3.16");
        passage.Content.Should().Contain("God so loved");
        passage.Reference.Should().Be("John 3:16");
    }

    [Fact]
    public void Passage_WithExpression_PreservesUnchangedProperties()
    {
        var original = new Passage { Id = "GEN.1.1", Content = "In the beginning...", Reference = "Genesis 1:1" };
        var updated = original with { Content = "Updated content" };

        updated.Id.Should().Be("GEN.1.1");
        updated.Reference.Should().Be("Genesis 1:1");
        original.Content.Should().Be("In the beginning..."); // original unchanged
    }
}

public sealed class HighlightTests
{
    [Fact]
    public void Highlight_DeserializesFromJson_Correctly()
    {
        const string json = """
            { "bible_id": 3034, "passage_id": "JHN.3.16", "color": "44aa44" }
            """;

        var highlight = JsonSerializer.Deserialize<Highlight>(json)!;

        highlight.BibleId.Should().Be(3034);
        highlight.PassageId.Should().Be("JHN.3.16");
        highlight.Color.Should().Be("44aa44");
    }

    [Fact]
    public void Highlight_DefaultValues_AreEmpty()
    {
        var h = new Highlight();
        h.PassageId.Should().BeEmpty();
        h.BibleId.Should().Be(0);
        h.Color.Should().BeEmpty();
    }
}

public sealed class VerseTests
{
    [Fact]
    public void Verse_DeserializesFromJson_Correctly()
    {
        const string json = """
            { "usfm": "JHN.3.16", "human": "John 3:16", "text": "For God so loved..." }
            """;

        var verse = JsonSerializer.Deserialize<Verse>(json)!;

        verse.Usfm.Should().Be("JHN.3.16");
        verse.Human.Should().Be("John 3:16");
        verse.Text.Should().Contain("God so loved");
    }

    [Fact]
    public void Verse_DefaultValues_AreEmpty()
    {
        var verse = new Verse();
        verse.Usfm.Should().BeEmpty();
        verse.Human.Should().BeEmpty();
        verse.Text.Should().BeEmpty();
    }
}

public sealed class BibleVersionSummaryTests
{
    [Fact]
    public void BibleVersionSummary_DeserializesCopyright_Correctly()
    {
        const string json = """
            {
              "id": 3034, "abbreviation": "BSB", "localized_abbreviation": "BSB",
              "title": "Berean Standard Bible", "localized_title": "Berean Standard Bible",
              "language_tag": "en", "copyright": "Public Domain"
            }
            """;

        var summary = JsonSerializer.Deserialize<BibleVersionSummary>(json)!;

        summary.Copyright.Should().Be("Public Domain");
    }

    [Fact]
    public void BibleVersionSummary_Copyright_DefaultsToNull()
    {
        var summary = new BibleVersionSummary();
        summary.Copyright.Should().BeNull();
    }
}

public sealed class BookCanonTests
{
    [Theory]
    [InlineData("old_testament", BookCanon.OldTestament)]
    [InlineData("new_testament", BookCanon.NewTestament)]
    [InlineData("deuterocanon", BookCanon.Deuterocanon)]
    public void BookCanon_DeserializesKnownValues_Correctly(string wireValue, BookCanon expected)
    {
        var json = $$"""{"canon":"{{wireValue}}"}""";
        var book = JsonSerializer.Deserialize<IndexBook>(json)!;

        book.Canon.Should().Be(expected);
    }

    [Fact]
    public void BookCanon_DeserializesUnrecognizedValue_AsUnknown()
    {
        const string json = """{"canon":"some_future_canon"}""";
        var book = JsonSerializer.Deserialize<IndexBook>(json)!;

        book.Canon.Should().Be(BookCanon.Unknown);
    }
}

public sealed class BibleIndexTests
{
    [Fact]
    public void BibleIndex_DeserializesFromJson_Correctly()
    {
        const string json = """
            {
              "text_direction": "ltr",
              "books": [
                {
                  "id": "GEN", "title": "Genesis", "full_title": "The Book of Genesis",
                  "abbreviation": "Gen.", "canon": "old_testament",
                  "chapters": [
                    {
                      "id": 1, "passage_id": "GEN.1", "title": 1,
                      "verses": [
                        { "id": 1, "passage_id": "GEN.1.1", "title": 1 },
                        { "id": 2, "passage_id": "GEN.1.2", "title": 2 }
                      ]
                    }
                  ],
                  "intro": { "id": "INTRO", "passage_id": "GEN.INTRO", "title": "Intro" }
                }
              ]
            }
            """;

        var index = JsonSerializer.Deserialize<BibleIndex>(json)!;

        index.TextDirection.Should().Be("ltr");
        index.Books.Should().HaveCount(1);

        var book = index.Books[0];
        book.Usfm.Should().Be("GEN");
        book.Title.Should().Be("Genesis");
        book.FullTitle.Should().Be("The Book of Genesis");
        book.Abbreviation.Should().Be("Gen.");
        book.Canon.Should().Be(BookCanon.OldTestament);
        book.Chapters.Should().HaveCount(1);
        book.Intro.Should().NotBeNull();
        book.Intro!.Id.Should().Be("INTRO");
        book.Intro.Title.Should().Be("Intro");

        var chapter = book.Chapters[0];
        chapter.Number.Should().Be(1);
        chapter.Usfm.Should().Be("GEN.1");
        chapter.Title.Should().Be("1");
        chapter.Verses.Should().HaveCount(2);
        chapter.Verses[0].Number.Should().Be(1);
        chapter.Verses[0].Usfm.Should().Be("GEN.1.1");
        chapter.Verses[0].Title.Should().Be("1");
    }

    [Fact]
    public void BibleIndex_Intro_IsNull_WhenNotProvided()
    {
        const string json = """
            {
              "text_direction": "ltr",
              "books": [
                { "id": "EXO", "title": "Exodus", "full_title": "The Book of Exodus",
                  "abbreviation": "Exo.", "canon": "old_testament", "chapters": [] }
              ]
            }
            """;

        var index = JsonSerializer.Deserialize<BibleIndex>(json)!;

        index.Books[0].Intro.Should().BeNull();
    }

    [Fact]
    public void BibleIndex_ChapterAndVerseTitle_AcceptsStringOrNumber()
    {
        const string numericJson = """{"id":1,"passage_id":"GEN.1","title":1,"verses":[]}""";
        const string stringJson = """{"id":1,"passage_id":"GEN.1","title":"1","verses":[]}""";

        JsonSerializer.Deserialize<IndexChapter>(numericJson)!.Title.Should().Be("1");
        JsonSerializer.Deserialize<IndexChapter>(stringJson)!.Title.Should().Be("1");
    }

    [Fact]
    public void BibleIndex_Books_DefaultsToEmpty()
    {
        var index = new BibleIndex();
        index.Books.Should().BeEmpty();
    }
}
