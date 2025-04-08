namespace DataFlow.Dataverse.Caching;

public enum ExpirationMode
{
    /// <summary>
    /// The default eviction mode where items are evicted the next time they are accessed.
    /// </summary>
    LazyExpiration,

    /// <summary>
    /// Uses a timer to force eviction of the cache item after the expiration time has elapsed.
    /// </summary>
    ImmediateEviction,
}
