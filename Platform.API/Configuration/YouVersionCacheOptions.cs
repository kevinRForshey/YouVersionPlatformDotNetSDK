namespace Platform.API.Configuration;

/// <summary>
/// Configuration options for the YouVersion SDK caching layer (HybridCache).
/// </summary>
public sealed class YouVersionCacheOptions
{
    /// <summary>The configuration section name to bind from appsettings.json.</summary>
    public const string SectionName = "YouVersionCache";

    /// <summary>
    /// Optional Redis connection string. When set, HybridCache uses Redis as an L2 distributed cache.
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Maximum number of entries held in the L1 in-process memory cache.
    /// When <see langword="null"/>, no size limit is applied.
    /// </summary>
    public int? MaxL1Entries { get; set; }

    /// <summary>
    /// Cache lifetime for Bible version lists and version metadata. Default: 24 hours.
    /// </summary>
    public TimeSpan VersionsTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Cache lifetime for book lists. Default: 24 hours.
    /// </summary>
    public TimeSpan BooksTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Cache lifetime for passage content. Default: 7 days.
    /// </summary>
    public TimeSpan PassageTtl { get; set; } = TimeSpan.FromDays(7);
}
