namespace DataFlow.Dataverse.Caching;

using Microsoft.Extensions.Caching.Memory;

public class SafeMemoryCacheEntryOptions : MemoryCacheEntryOptions
{
    public ExpirationMode ExpirationMode { get; set; } = ExpirationMode.LazyExpiration;

    public TimeSpan ImmediateExpirationTimespan { get; set; }
}
