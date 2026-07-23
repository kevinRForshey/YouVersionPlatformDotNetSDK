using System.Net;

using Platform.API.Exceptions;
using Platform.API.Models;

namespace Platform.API.Clients;

/// <summary>
/// Projects a <see cref="BibleIndex"/> into the flattened <see cref="Book"/>/<see cref="Chapter"/>/
/// <see cref="Verse"/> shapes. Shared by <see cref="BibleClient"/> and <see cref="CachingBibleClient"/>
/// so both project the same (possibly cached) index consistently.
/// </summary>
internal static class BibleIndexMapper
{
    internal static IndexBook FindBook(BibleIndex index, int versionId, string bookUsfm)
        => index.Books.FirstOrDefault(b => string.Equals(b.Usfm, bookUsfm, StringComparison.OrdinalIgnoreCase))
            ?? throw new BibleApiException(
                HttpStatusCode.NotFound,
                $"Book '{bookUsfm}' was not found in Bible version {versionId}.");

    internal static IndexChapter FindChapter(IndexBook book, int versionId, string bookUsfm, int chapterNumber)
        => book.Chapters.FirstOrDefault(c => c.Number == chapterNumber)
            ?? throw new BibleApiException(
                HttpStatusCode.NotFound,
                $"Chapter {chapterNumber} was not found in book '{bookUsfm}' of Bible version {versionId}.");

    internal static IReadOnlyList<Book> BuildBooks(BibleIndex index)
        => index.Books
            .Select(b => new Book { Usfm = b.Usfm, Human = b.Title, ChapterCount = b.Chapters.Count })
            .ToList()
            .AsReadOnly();

    internal static IReadOnlyList<Chapter> BuildChapters(IndexBook book)
        => book.Chapters
            .Select(c => new Chapter { Usfm = c.Usfm, Human = $"{book.Title} {c.Number}", VerseCount = c.Verses.Count })
            .ToList()
            .AsReadOnly();

    internal static IReadOnlyList<Verse> BuildVerses(IndexBook book, IndexChapter chapter)
        => chapter.Verses
            .Select(v => new Verse { Usfm = v.Usfm, Human = $"{book.Title} {chapter.Number}:{v.Number}", Text = string.Empty })
            .ToList()
            .AsReadOnly();
}
