using System.Text.Json;
using Platform.API.Models;

namespace Platform.API.Benchmarks;

/// <summary>
/// Builds a synthetic <see cref="BibleIndex"/> roughly the size of a real Bible (66 books,
/// ~20 chapters/book, ~25 verses/chapter -> ~33,000 verses), so mapping and caching
/// benchmarks operate on realistically sized payloads rather than a handful of rows.
/// </summary>
internal static class SampleBibleIndex
{
    private const int BookCount = 66;
    private const int ChaptersPerBook = 20;
    private const int VersesPerChapter = 25;

    public static BibleIndex Create()
    {
        var books = new List<IndexBook>(BookCount);
        for (var b = 1; b <= BookCount; b++)
        {
            var chapters = new List<IndexChapter>(ChaptersPerBook);
            for (var c = 1; c <= ChaptersPerBook; c++)
            {
                var verses = new List<IndexVerse>(VersesPerChapter);
                for (var v = 1; v <= VersesPerChapter; v++)
                {
                    verses.Add(new IndexVerse
                    {
                        Number = v,
                        Usfm = $"BK{b:D2}.{c}.{v}",
                        Title = v.ToString()
                    });
                }

                chapters.Add(new IndexChapter
                {
                    Number = c,
                    Usfm = $"BK{b:D2}.{c}",
                    Title = c.ToString(),
                    Verses = verses
                });
            }

            books.Add(new IndexBook
            {
                Usfm = $"BK{b:D2}",
                Title = $"Book {b}",
                FullTitle = $"The Book of Book {b}",
                Abbreviation = $"Bk{b}.",
                Canon = BookCanon.OldTestament,
                Chapters = chapters
            });
        }

        return new BibleIndex { TextDirection = "ltr", Books = books };
    }

    public static string CreateJson() => JsonSerializer.Serialize(Create());
}
