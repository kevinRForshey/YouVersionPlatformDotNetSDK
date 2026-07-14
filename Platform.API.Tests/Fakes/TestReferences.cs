using YouVersion.UsfmReferences;

namespace Platform.API.Tests.Fakes;

/// <summary>
/// Reusable USFM reference fixtures for testing.
/// Eliminates hardcoded USFM string literals scattered throughout tests.
/// </summary>
internal static class TestReferences
{
    /// <summary>John 3:16 single verse.</summary>
    public static Reference John316 => Reference.FromString("JHN.3.16");
    
    /// <summary>Genesis 1 whole chapter.</summary>
    public static Reference Genesis1 => Reference.FromString("GEN.1");
    
    /// <summary>John 3:16-17 verse range.</summary>
    public static Reference John316To17 => Reference.FromString("JHN.3.16-17");
}

