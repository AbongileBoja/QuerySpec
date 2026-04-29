using System;
using Microsoft.Extensions.Logging;

namespace QuerySpec.Core.Caching;

internal static partial class DistributedCacheProviderLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Distributed cache GET failed for key {Key}; treating as miss.")]
    internal static partial void GetFailed(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Corrupt cache entry for key {Key}; evicting.")]
    internal static partial void CorruptEntryEvicting(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to evict corrupt cache entry for key {Key}.")]
    internal static partial void EvictFailed(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Distributed cache SET failed for key {Key}.")]
    internal static partial void SetFailed(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Distributed cache REMOVE failed for key {Key}.")]
    internal static partial void RemoveFailed(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Distributed cache EXISTS check failed for key {Key}.")]
    internal static partial void ExistsFailed(ILogger logger, Exception exception, string key);
}
