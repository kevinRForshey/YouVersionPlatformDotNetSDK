namespace YouVersion.UsfmReferences;

/// <summary>
/// An inclusive range of verse numbers within a single chapter.
/// </summary>
/// <remarks>
/// Replaces the Python <c>tuple[int, int]</c>. Being a <c>readonly record struct</c>,
/// it has value equality for free, which the <see cref="Reference"/> equality contract
/// relies on. A single verse is represented by a range where <see cref="Start"/> equals
/// <see cref="End"/>.
/// </remarks>
/// <param name="Start">The first verse in the range (inclusive).</param>
/// <param name="End">The last verse in the range (inclusive).</param>
public readonly record struct VerseRange(int Start, int End) : IComparable<VerseRange>
{
    /// <summary>
    /// Orders ranges by <see cref="Start"/>, then <see cref="End"/>, matching the
    /// lexicographic tuple ordering the Python implementation relied on when sorting.
    /// </summary>
    public int CompareTo(VerseRange other)
    {
        int byStart = Start.CompareTo(other.Start);
        return byStart != 0 ? byStart : End.CompareTo(other.End);
    }
}
