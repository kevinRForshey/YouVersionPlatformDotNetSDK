using BenchmarkDotNet.Attributes;
using Platform.API.Clients;
using Platform.API.Models;

namespace Platform.API.Benchmarks;

/// <summary>
/// Measures the cost of projecting a cached <see cref="BibleIndex"/> into the flattened
/// <see cref="Book"/>/<see cref="Chapter"/>/<see cref="Verse"/> shapes — the in-memory work
/// <see cref="CachingBibleClient"/> redoes on every call even when the index itself is a cache hit.
/// </summary>
[MemoryDiagnoser]
public class BibleIndexMapperBenchmarks
{
    private BibleIndex _index = null!;
    private IndexBook _firstBook = null!;
    private IndexChapter _firstChapter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _index = SampleBibleIndex.Create();
        _firstBook = _index.Books[0];
        _firstChapter = _firstBook.Chapters[0];
    }

    [Benchmark]
    public IReadOnlyList<Book> BuildBooks() => BibleIndexMapper.BuildBooks(_index);

    [Benchmark]
    public IReadOnlyList<Chapter> BuildChapters() => BibleIndexMapper.BuildChapters(_firstBook);

    [Benchmark]
    public IReadOnlyList<Verse> BuildVerses() => BibleIndexMapper.BuildVerses(_firstBook, _firstChapter);
}
