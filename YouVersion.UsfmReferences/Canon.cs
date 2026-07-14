namespace YouVersion.UsfmReferences;

/// <summary>
/// The canonical category a book belongs to.
/// </summary>
/// <remarks>
/// The original Python library represented this as the strings "ot", "nt", and "ap".
/// This port models it as a type-safe enum; use <see cref="CanonExtensions.ToCode"/>
/// when the lowercase string code is required (e.g. for serialization or wire
/// compatibility with the Python implementation).
/// </remarks>
public enum Canon
{
    /// <summary>Old Testament ("ot").</summary>
    OldTestament,

    /// <summary>New Testament ("nt").</summary>
    NewTestament,

    /// <summary>Apocrypha / deuterocanonical ("ap"). Also the fallback for unknown books.</summary>
    Apocrypha,
}

/// <summary>
/// Conversions between <see cref="Canon"/> and its lowercase string code.
/// </summary>
public static class CanonExtensions
{
    /// <summary>
    /// Returns the lowercase string code for a canon: "ot", "nt", or "ap".
    /// </summary>
    public static string ToCode(this Canon canon) => canon switch
    {
        Canon.OldTestament => "ot",
        Canon.NewTestament => "nt",
        Canon.Apocrypha => "ap",
        _ => "ap",
    };
}
