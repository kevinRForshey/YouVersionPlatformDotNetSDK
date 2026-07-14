namespace YouVersion.UsfmReferences;

/// <summary>
/// The single source of truth for canonical USFM book data: canon classification,
/// English-name lookups, and numbered-book stems. Mirrors <c>books.py</c>.
/// </summary>
/// <remarks>
/// This type holds <em>data</em> only. The algorithm that resolves a free-text book
/// name into a USFM code (key normalization, leading-ordinal handling) lives in
/// <see cref="UsfmReferenceService"/>.
/// </remarks>
public static class BookCatalog
{
    // Ordered (code, canon) pairs. Order is preserved so that Books / OldTestamentBooks /
    // NewTestamentBooks match the insertion order of BOOK_CANON in the Python source.
    private static readonly (string Code, Canon Canon)[] CanonData =
    [
        ("GEN", Canon.OldTestament), ("EXO", Canon.OldTestament), ("LEV", Canon.OldTestament),
        ("NUM", Canon.OldTestament), ("DEU", Canon.OldTestament), ("JOS", Canon.OldTestament),
        ("JDG", Canon.OldTestament), ("RUT", Canon.OldTestament), ("1SA", Canon.OldTestament),
        ("2SA", Canon.OldTestament), ("1KI", Canon.OldTestament), ("2KI", Canon.OldTestament),
        ("1CH", Canon.OldTestament), ("2CH", Canon.OldTestament), ("EZR", Canon.OldTestament),
        ("NEH", Canon.OldTestament), ("EST", Canon.OldTestament), ("JOB", Canon.OldTestament),
        ("PSA", Canon.OldTestament), ("PRO", Canon.OldTestament), ("ECC", Canon.OldTestament),
        ("SNG", Canon.OldTestament), ("ISA", Canon.OldTestament), ("JER", Canon.OldTestament),
        ("LAM", Canon.OldTestament), ("EZK", Canon.OldTestament), ("DAN", Canon.OldTestament),
        ("HOS", Canon.OldTestament), ("JOL", Canon.OldTestament), ("AMO", Canon.OldTestament),
        ("OBA", Canon.OldTestament), ("JON", Canon.OldTestament), ("MIC", Canon.OldTestament),
        ("NAM", Canon.OldTestament), ("HAB", Canon.OldTestament), ("ZEP", Canon.OldTestament),
        ("HAG", Canon.OldTestament), ("ZEC", Canon.OldTestament), ("MAL", Canon.OldTestament),
        ("MAT", Canon.NewTestament), ("MRK", Canon.NewTestament), ("LUK", Canon.NewTestament),
        ("JHN", Canon.NewTestament), ("ACT", Canon.NewTestament), ("ROM", Canon.NewTestament),
        ("1CO", Canon.NewTestament), ("2CO", Canon.NewTestament), ("GAL", Canon.NewTestament),
        ("EPH", Canon.NewTestament), ("PHP", Canon.NewTestament), ("COL", Canon.NewTestament),
        ("1TH", Canon.NewTestament), ("2TH", Canon.NewTestament), ("1TI", Canon.NewTestament),
        ("2TI", Canon.NewTestament), ("TIT", Canon.NewTestament), ("PHM", Canon.NewTestament),
        ("HEB", Canon.NewTestament), ("JAS", Canon.NewTestament), ("1PE", Canon.NewTestament),
        ("2PE", Canon.NewTestament), ("1JN", Canon.NewTestament), ("2JN", Canon.NewTestament),
        ("3JN", Canon.NewTestament), ("JUD", Canon.NewTestament), ("REV", Canon.NewTestament),
        ("TOB", Canon.Apocrypha), ("JDT", Canon.Apocrypha), ("ESG", Canon.Apocrypha),
        ("WIS", Canon.Apocrypha), ("SIR", Canon.Apocrypha), ("BAR", Canon.Apocrypha),
        ("LJE", Canon.Apocrypha), ("S3Y", Canon.Apocrypha), ("SUS", Canon.Apocrypha),
        ("BEL", Canon.Apocrypha), ("1MA", Canon.Apocrypha), ("2MA", Canon.Apocrypha),
        ("3MA", Canon.Apocrypha), ("4MA", Canon.Apocrypha), ("1ES", Canon.Apocrypha),
        ("2ES", Canon.Apocrypha), ("MAN", Canon.Apocrypha), ("PS2", Canon.Apocrypha),
        ("ODA", Canon.Apocrypha), ("PSS", Canon.Apocrypha), ("3ES", Canon.Apocrypha),
        ("EZA", Canon.Apocrypha), ("5EZ", Canon.Apocrypha), ("6EZ", Canon.Apocrypha),
        ("DAG", Canon.Apocrypha), ("PS3", Canon.Apocrypha), ("2BA", Canon.Apocrypha),
        ("LBA", Canon.Apocrypha), ("JUB", Canon.Apocrypha), ("ENO", Canon.Apocrypha),
        ("1MQ", Canon.Apocrypha), ("2MQ", Canon.Apocrypha), ("3MQ", Canon.Apocrypha),
        ("REP", Canon.Apocrypha), ("4BA", Canon.Apocrypha), ("LAO", Canon.Apocrypha),
        ("LKA", Canon.NewTestament), // luke-acts combo
    ];

    private static readonly Dictionary<string, Canon> CanonLookup =
        CanonData.ToDictionary(p => p.Code, p => p.Canon, StringComparer.Ordinal);

    /// <summary>All USFM book codes, in canonical order.</summary>
    public static IReadOnlyList<string> Books { get; } =
        CanonData.Select(p => p.Code).ToArray();

    /// <summary>New Testament USFM book codes, in canonical order.</summary>
    public static IReadOnlyList<string> NewTestamentBooks { get; } =
        CanonData.Where(p => p.Canon == Canon.NewTestament).Select(p => p.Code).ToArray();

    /// <summary>Old Testament USFM book codes, in canonical order.</summary>
    public static IReadOnlyList<string> OldTestamentBooks { get; } =
        CanonData.Where(p => p.Canon == Canon.OldTestament).Select(p => p.Code).ToArray();

    /// <summary>
    /// Returns the canon for a USFM book code, or <see cref="Canon.Apocrypha"/> for an
    /// unknown code (mirroring the Python <c>BOOK_CANON.get(book, "ap")</c> fallback).
    /// </summary>
    public static Canon GetCanon(string book) =>
        CanonLookup.TryGetValue(book, out var canon) ? canon : Canon.Apocrypha;

    /// <summary>Returns <c>true</c> if the exact (case-sensitive) USFM code is recognized.</summary>
    public static bool IsKnownBook(string book) => CanonLookup.ContainsKey(book);

    // English book names and common abbreviations, mapped to USFM codes. Keys are
    // already normalized: lowercased with all non-alphanumeric characters stripped.
    // Numbered books are NOT pre-expanded here; the leading ordinal is peeled by the
    // resolver and the remaining stem is looked up in NumberedBooks.
    internal static readonly IReadOnlyDictionary<string, string> BookNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["genesis"] = "GEN", ["gen"] = "GEN", ["gn"] = "GEN",
            ["exodus"] = "EXO", ["exod"] = "EXO", ["exo"] = "EXO", ["ex"] = "EXO",
            ["leviticus"] = "LEV", ["lev"] = "LEV", ["lv"] = "LEV",
            ["numbers"] = "NUM", ["num"] = "NUM", ["nm"] = "NUM",
            ["deuteronomy"] = "DEU", ["deut"] = "DEU", ["deu"] = "DEU", ["dt"] = "DEU",
            ["joshua"] = "JOS", ["josh"] = "JOS", ["jos"] = "JOS", ["jsh"] = "JOS",
            ["judges"] = "JDG", ["judg"] = "JDG", ["jdg"] = "JDG", ["jdgs"] = "JDG",
            ["ruth"] = "RUT", ["rut"] = "RUT", ["rth"] = "RUT",
            ["ezra"] = "EZR", ["ezr"] = "EZR",
            ["nehemiah"] = "NEH", ["neh"] = "NEH",
            ["esther"] = "EST", ["esth"] = "EST", ["est"] = "EST",
            ["job"] = "JOB", ["jb"] = "JOB",
            ["psalms"] = "PSA", ["psalm"] = "PSA", ["psa"] = "PSA", ["pss"] = "PSA",
            ["psm"] = "PSA", ["ps"] = "PSA",
            ["proverbs"] = "PRO", ["prov"] = "PRO", ["pro"] = "PRO", ["prv"] = "PRO",
            ["ecclesiastes"] = "ECC", ["eccles"] = "ECC", ["eccl"] = "ECC", ["ecc"] = "ECC",
            ["qoh"] = "ECC", ["qoheleth"] = "ECC",
            ["songofsongs"] = "SNG", ["songofsolomon"] = "SNG", ["song"] = "SNG", ["sng"] = "SNG",
            ["sos"] = "SNG", ["canticles"] = "SNG", ["cant"] = "SNG",
            ["isaiah"] = "ISA", ["isa"] = "ISA",
            ["jeremiah"] = "JER", ["jer"] = "JER",
            ["lamentations"] = "LAM", ["lam"] = "LAM",
            ["ezekiel"] = "EZK", ["ezek"] = "EZK", ["ezk"] = "EZK", ["eze"] = "EZK",
            ["daniel"] = "DAN", ["dan"] = "DAN", ["dn"] = "DAN",
            ["hosea"] = "HOS", ["hos"] = "HOS",
            ["joel"] = "JOL", ["jol"] = "JOL", ["jl"] = "JOL",
            ["amos"] = "AMO", ["amo"] = "AMO",
            ["obadiah"] = "OBA", ["obad"] = "OBA", ["oba"] = "OBA", ["ob"] = "OBA",
            ["jonah"] = "JON", ["jon"] = "JON", ["jnh"] = "JON",
            ["micah"] = "MIC", ["mic"] = "MIC",
            ["nahum"] = "NAM", ["nah"] = "NAM", ["nam"] = "NAM",
            ["habakkuk"] = "HAB", ["hab"] = "HAB", ["hb"] = "HAB",
            ["zephaniah"] = "ZEP", ["zeph"] = "ZEP", ["zep"] = "ZEP",
            ["haggai"] = "HAG", ["hag"] = "HAG", ["hg"] = "HAG",
            ["zechariah"] = "ZEC", ["zech"] = "ZEC", ["zec"] = "ZEC",
            ["malachi"] = "MAL", ["mal"] = "MAL",
            ["matthew"] = "MAT", ["matt"] = "MAT", ["mat"] = "MAT", ["mt"] = "MAT",
            ["mark"] = "MRK", ["mrk"] = "MRK", ["mar"] = "MRK", ["mk"] = "MRK",
            ["luke"] = "LUK", ["luk"] = "LUK", ["lk"] = "LUK",
            ["john"] = "JHN", ["jhn"] = "JHN", ["jn"] = "JHN",
            ["acts"] = "ACT", ["act"] = "ACT",
            ["romans"] = "ROM", ["rom"] = "ROM",
            ["galatians"] = "GAL", ["gal"] = "GAL",
            ["ephesians"] = "EPH", ["eph"] = "EPH",
            ["philippians"] = "PHP", ["phil"] = "PHP", ["php"] = "PHP",
            ["colossians"] = "COL", ["col"] = "COL",
            ["titus"] = "TIT", ["tit"] = "TIT",
            ["philemon"] = "PHM", ["philem"] = "PHM", ["phlm"] = "PHM", ["phm"] = "PHM",
            ["hebrews"] = "HEB", ["heb"] = "HEB",
            ["james"] = "JAS", ["jas"] = "JAS",
            ["jude"] = "JUD", ["jud"] = "JUD",
            ["revelation"] = "REV", ["rev"] = "REV", ["rv"] = "REV",
            ["apocalypse"] = "REV", ["apoc"] = "REV",
            ["tobit"] = "TOB", ["tob"] = "TOB", ["tb"] = "TOB",
            ["judith"] = "JDT", ["jdt"] = "JDT", ["jdth"] = "JDT",
            ["wisdomofsolomon"] = "WIS", ["wisdom"] = "WIS", ["wis"] = "WIS", ["wissol"] = "WIS",
            ["sirach"] = "SIR", ["ecclesiasticus"] = "SIR", ["sir"] = "SIR", ["bensira"] = "SIR",
            ["baruch"] = "BAR", ["bar"] = "BAR",
            ["letterofjeremiah"] = "LJE", ["letjer"] = "LJE", ["lje"] = "LJE",
            ["epistleofjeremiah"] = "LJE",
            ["songofthethreeyoungmen"] = "S3Y", ["songofthree"] = "S3Y",
            ["prayerofazariah"] = "S3Y", ["praz"] = "S3Y", ["s3y"] = "S3Y",
            ["susanna"] = "SUS", ["sus"] = "SUS",
            ["belandthedragon"] = "BEL", ["bel"] = "BEL",
            ["prayerofmanasseh"] = "MAN", ["prman"] = "MAN",
        };

    // Numbered books: each stem (full name + abbreviations) stored once, mapped to
    // {ordinal: USFM}. The resolver peels a leading ordinal (1/2/3, I/II/III, or
    // First/Second/Third) to an int, then looks the stem up here. Out-of-range or
    // malformed ordinals find no entry and yield null.
    internal static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> NumberedBooks =
        new Dictionary<string, IReadOnlyDictionary<int, string>>(StringComparer.Ordinal)
        {
            ["samuel"] = Two("1SA", "2SA"), ["sam"] = Two("1SA", "2SA"),
            ["sa"] = Two("1SA", "2SA"), ["sm"] = Two("1SA", "2SA"),
            ["kings"] = Two("1KI", "2KI"), ["kgs"] = Two("1KI", "2KI"),
            ["kg"] = Two("1KI", "2KI"), ["ki"] = Two("1KI", "2KI"),
            ["chronicles"] = Two("1CH", "2CH"), ["chron"] = Two("1CH", "2CH"),
            ["chr"] = Two("1CH", "2CH"), ["ch"] = Two("1CH", "2CH"),
            ["corinthians"] = Two("1CO", "2CO"), ["cor"] = Two("1CO", "2CO"),
            ["co"] = Two("1CO", "2CO"),
            ["thessalonians"] = Two("1TH", "2TH"), ["thess"] = Two("1TH", "2TH"),
            ["thes"] = Two("1TH", "2TH"), ["th"] = Two("1TH", "2TH"),
            ["timothy"] = Two("1TI", "2TI"), ["tim"] = Two("1TI", "2TI"),
            ["ti"] = Two("1TI", "2TI"),
            ["peter"] = Two("1PE", "2PE"), ["pet"] = Two("1PE", "2PE"),
            ["pe"] = Two("1PE", "2PE"),
            ["john"] = Three("1JN", "2JN", "3JN"), ["jn"] = Three("1JN", "2JN", "3JN"),
            ["jhn"] = Three("1JN", "2JN", "3JN"), ["jo"] = Three("1JN", "2JN", "3JN"),
            ["maccabees"] = Two("1MA", "2MA"), ["macc"] = Two("1MA", "2MA"),
            ["mac"] = Two("1MA", "2MA"), ["ma"] = Two("1MA", "2MA"),
            ["esdras"] = Two("1ES", "2ES"), ["esd"] = Two("1ES", "2ES"),
            ["es"] = Two("1ES", "2ES"),
        };

    internal static readonly IReadOnlyDictionary<string, int> OrdinalWords =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["first"] = 1, ["second"] = 2, ["third"] = 3,
        };

    internal static readonly IReadOnlyDictionary<string, int> RomanOrdinals =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["i"] = 1, ["ii"] = 2, ["iii"] = 3,
        };

    private static IReadOnlyDictionary<int, string> Two(string first, string second) =>
        new Dictionary<int, string> { [1] = first, [2] = second };

    private static IReadOnlyDictionary<int, string> Three(string first, string second, string third) =>
        new Dictionary<int, string> { [1] = first, [2] = second, [3] = third };
}
