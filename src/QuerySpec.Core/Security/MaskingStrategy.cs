namespace QuerySpec.Core.Security;

/// <summary>
/// Masking strategies applied by <see cref="DataMaskingEngine"/> for different sensitivity levels.
/// </summary>
public enum MaskingStrategy
{
    /// <summary>Replace every character with <c>*</c>.</summary>
    FullMask,
    /// <summary>Show first 2 characters; mask the rest. Example: <c>"JO****"</c>.</summary>
    PartialMask,
    /// <summary>Mask all but the last four characters. Example: <c>"****5678"</c>.</summary>
    LastFourOnly,
    /// <summary>Mask the local part of an email; leave the domain visible. Example: <c>"j****@example.com"</c>.</summary>
    EmailMask,
    /// <summary>Replace with a tenant-keyed HMAC of the value. Requires a hash key on <see cref="DataMaskingEngine"/>.</summary>
    HashMask
}
