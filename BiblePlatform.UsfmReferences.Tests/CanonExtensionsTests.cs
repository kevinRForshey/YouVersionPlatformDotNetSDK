using FluentAssertions;
using Xunit;

namespace BiblePlatform.UsfmReferences.Tests;

public sealed class CanonExtensionsTests
{
    [Theory]
    [InlineData(Canon.OldTestament, "ot")]
    [InlineData(Canon.NewTestament, "nt")]
    [InlineData(Canon.Apocrypha, "ap")]
    public void ToCode_ReturnsExpectedLowercaseCode(Canon canon, string expected)
    {
        canon.ToCode().Should().Be(expected);
    }

    [Fact]
    public void ToCode_FallsBackToApocrypha_ForUndefinedEnumValue()
    {
        ((Canon)99).ToCode().Should().Be("ap");
    }
}
