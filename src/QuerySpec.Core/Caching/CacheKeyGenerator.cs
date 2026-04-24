using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Cache key generator with SHA256 hashing for long keys.
/// </summary>
public class CacheKeyGenerator
{
    private const int MaxKeyLength = 256;

    /// <summary>Private constructor to prevent instantiation.</summary>
    private CacheKeyGenerator() { }

    /// <summary>
    /// Generates a cache key from prefix and components. Hashes long keys (&gt;256 chars)
    /// into a deterministic SHA-256 digest so they fit within Redis / Memcached key limits.
    /// Uses <see cref="SHA256.HashData(byte[])"/> which is allocation-lean compared with
    /// creating a new <see cref="SHA256"/> instance per call.
    /// </summary>
    public static string GenerateKey(string prefix, params object[] components)
    {
        // Build the key with a single StringBuilder pass to avoid the intermediate
        // List + LINQ chain + string.Join that the original implementation produced.
        var sb = new StringBuilder(prefix.Length + 64);
        sb.Append(prefix);
        foreach (var c in components)
        {
            if (c is null) continue;
            var s = c.ToString();
            if (string.IsNullOrEmpty(s)) continue;
            sb.Append(':').Append(s);
        }

        var key = sb.ToString();
        if (key.Length <= MaxKeyLength)
            return key;

        // Allocation-minimizing hash path: HashData is static and avoids instantiating
        // a new SHA256 per call. The base64 output is ~44 chars for a 32-byte digest.
        Span<byte> digest = stackalloc byte[32];
        var inputByteCount = Encoding.UTF8.GetByteCount(key);
        if (inputByteCount <= 1024)
        {
            Span<byte> stackBuf = stackalloc byte[1024];
            var written = Encoding.UTF8.GetBytes(key, stackBuf);
            SHA256.HashData(stackBuf[..written], digest);
        }
        else
        {
            var heapBuf = Encoding.UTF8.GetBytes(key);
            SHA256.HashData(heapBuf, digest);
        }
        return string.Concat(prefix, ":", Convert.ToBase64String(digest));
    }

    /// <summary>Generates a query cache key.</summary>
    public static string GenerateQueryCacheKey(string tenantId, string userId, string queryHash, string sortHash, int page)
    {
        return GenerateKey("query", tenantId, userId, queryHash, sortHash, page);
    }

    /// <summary>Generates a security policy cache key.</summary>
    public static string GenerateSecurityPolicyCacheKey(string resourceType)
    {
        return GenerateKey("policy", resourceType);
    }

    /// <summary>Generates a permission cache key.</summary>
    public static string GeneratePermissionCacheKey(string userId, string resourceType, string fieldName)
    {
        return GenerateKey("permission", userId, resourceType, fieldName);
    }
}
