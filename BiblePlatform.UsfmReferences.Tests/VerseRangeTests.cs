using FluentAssertions;
using Xunit;

namespace BiblePlatform.UsfmReferences.Tests;

public sealed class VerseRangeTests
{
    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new VerseRange(1, 3);
        var b = new VerseRange(1, 3);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Theory]
    [InlineData(1, 5, 2, 5, true)]   // lower Start sorts first
    [InlineData(2, 5, 1, 5, false)]
    [InlineData(2, 1, 2, 5, true)]   // same Start, lower End sorts first
    [InlineData(2, 5, 2, 1, false)]
    public void CompareTo_OrdersByStartThenEnd(
        int leftStart, int leftEnd, int rightStart, int rightEnd, bool leftIsLess)
    {
        var left = new VerseRange(leftStart, leftEnd);
        var right = new VerseRange(rightStart, rightEnd);

        (left.CompareTo(right) < 0).Should().Be(leftIsLess);
    }

    [Fact]
    public void CompareTo_IsZero_ForEqualRanges()
    {
        new VerseRange(4, 4).CompareTo(new VerseRange(4, 4)).Should().Be(0);
    }

    [Fact]
    public void Sort_OrdersRangesByStartThenEnd()
    {
        var ranges = new List<VerseRange>
        {
            new(10, 10),
            new(1, 3),
            new(1, 1),
        };

        ranges.Sort();

        ranges.Should().Equal(new VerseRange(1, 1), new VerseRange(1, 3), new VerseRange(10, 10));
    }
}
