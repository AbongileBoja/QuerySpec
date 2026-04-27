using System;
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
    /// On .NET 9+, the <c>params ReadOnlySpan&lt;object?&gt;</c> overload avoids the
    /// <c>object[]</c> heap allocation at the call site for inline argument lists.
    /// Value-type arguments still box individually; use the strongly-typed overloads on
    /// high-frequency call sites to eliminate that residual boxing.
    /// </summary>
    /// <param name="prefix">Namespace prefix appended at the head of the key (e.g. <c>"query"</c>, <c>"policy"</c>).</param>
    /// <param name="components">Ordered components joined by <c>:</c> after the prefix. Null and empty entries are skipped.</param>
    /// <returns>The composed key, or its base64-encoded SHA-256 digest when the composed length exceeds 256 characters.</returns>
#if NET9_0_OR_GREATER
    public static string GenerateKey(string prefix, params ReadOnlySpan<object?> components)
#else
    public static string GenerateKey(string prefix, params object?[] components)
#endif
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

        return FinaliseKey(prefix, sb);
    }

    /// <summary>
    /// Strongly-typed overload for query cache keys. Avoids boxing of <paramref name="page"/>
    /// on all TFMs by appending the integer directly without going through <c>object?</c>.
    /// This is the high-frequency path and accounts for the majority of <c>GenerateKey</c>
    /// call volume under normal application load.
    /// </summary>
    /// <param name="tenantId">Tenant scope; appended only when non-empty.</param>
    /// <param name="userId">User scope; appended only when non-empty.</param>
    /// <param name="queryHash">Stable hash of the query expression.</param>
    /// <param name="sortHash">Stable hash of the sort specification.</param>
    /// <param name="page">Zero-based page index. Always appended.</param>
    /// <returns>The composed query cache key, or its base64-encoded SHA-256 digest when the composed length exceeds 256 characters.</returns>
    public static string GenerateQueryCacheKey(string tenantId, string userId, string queryHash, string sortHash, int page)
    {
        var sb = new StringBuilder(64);
        sb.Append("query");
        AppendIfNonEmpty(sb, tenantId);
        AppendIfNonEmpty(sb, userId);
        AppendIfNonEmpty(sb, queryHash);
        AppendIfNonEmpty(sb, sortHash);
        sb.Append(':').Append(page);
        return FinaliseKey("query", sb);
    }

    /// <summary>Generates a security policy cache key.</summary>
    /// <param name="resourceType">Resource type identifier the policy applies to.</param>
    /// <returns>The cache key under the <c>policy</c> namespace.</returns>
    public static string GenerateSecurityPolicyCacheKey(string resourceType)
    {
        return GenerateKey("policy", resourceType);
    }

    /// <summary>Generates a permission cache key.</summary>
    /// <param name="userId">User the permission is being evaluated for.</param>
    /// <param name="resourceType">Resource type the permission targets.</param>
    /// <param name="fieldName">Field the permission protects.</param>
    /// <returns>The cache key under the <c>permission</c> namespace.</returns>
    public static string GeneratePermissionCacheKey(string userId, string resourceType, string fieldName)
    {
        return GenerateKey("permission", userId, resourceType, fieldName);
    }

    private static void AppendIfNonEmpty(StringBuilder sb, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            sb.Append(':').Append(value);
    }

    private static string FinaliseKey(string prefix, StringBuilder sb)
    {
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
}
