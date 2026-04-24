using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QuerySpec.Core.Security;

/// <summary>
/// PII (Personally Identifiable Information) data masking engine.
/// Supports multiple masking strategies for different data types.
/// </summary>
public class DataMaskingEngine
{
    /// <summary>
    /// Masking strategies for different sensitivity levels.
    /// </summary>
    public enum MaskingStrategy
    {
        /// <summary>Full mask: ****</summary>
        FullMask,
        /// <summary>Partial mask: Show first 2, mask rest: "JO****"</summary>
        PartialMask,
        /// <summary>Last four only: "****5678"</summary>
        LastFourOnly,
        /// <summary>Email mask: "j****@example.com"</summary>
        EmailMask,
        /// <summary>Hash mask: Replace with hash</summary>
        HashMask
    }

    private readonly Dictionary<string, MaskingStrategy> _fieldMasks = new();
    private readonly Dictionary<string, Regex> _piiPatterns = new();

    /// <summary>Initializes a new instance of the DataMaskingEngine.</summary>
    public DataMaskingEngine()
    {
        InitializeDefaultPiiPatterns();
    }

    /// <summary>
    /// Initializes default PII detection patterns.
    /// </summary>
    private void InitializeDefaultPiiPatterns()
    {
        _piiPatterns["Email"] = new Regex(@"^[^\@]+@[^\@]+$");
        _piiPatterns["SSN"] = new Regex(@"^\d{3}-\d{2}-\d{4}$");
        _piiPatterns["Phone"] = new Regex(@"^\+?1?\d{9,15}$");
        _piiPatterns["CreditCard"] = new Regex(@"^\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}$");
    }

    /// <summary>
    /// Registers a masking strategy for a specific field.
    /// </summary>
    public void RegisterFieldMask(string fieldName, MaskingStrategy strategy)
    {
        _fieldMasks[fieldName] = strategy;
    }

    /// <summary>
    /// Masks a field value based on registered strategy.
    /// </summary>
    public string Mask(string fieldName, object? value)
    {
        if (value == null) return "null";

        var strValue = value.ToString() ?? "";

        if (!_fieldMasks.TryGetValue(fieldName, out var strategy))
            return strValue;

        return strategy switch
        {
            MaskingStrategy.FullMask => MaskFull(strValue),
            MaskingStrategy.PartialMask => MaskPartial(strValue),
            MaskingStrategy.LastFourOnly => MaskLastFour(strValue),
            MaskingStrategy.EmailMask => MaskEmail(strValue),
            MaskingStrategy.HashMask => MaskHash(strValue),
            _ => strValue
        };
    }

    private string MaskFull(string value) => new string('*', value.Length);
    private string MaskPartial(string value) => value.Length <= 2 ? MaskFull(value) : value[..2] + new string('*', value.Length - 2);
    private string MaskLastFour(string value) => value.Length <= 4 ? MaskFull(value) : new string('*', value.Length - 4) + value[^4..];

    private string MaskEmail(string value)
    {
        var parts = value.Split('@');
        if (parts.Length != 2) return MaskFull(value);
        return parts[0][..Math.Min(1, parts[0].Length)] + new string('*', Math.Max(0, parts[0].Length - 1)) + "@" + parts[1];
    }

    private string MaskHash(string value)
    {
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(hash)[..8];
        }
    }

    /// <summary>
    /// Detects if a field contains PII based on patterns.
    /// </summary>
    public bool IsPii(string fieldName, object? value)
    {
        if (value == null) return false;

        var strValue = value.ToString() ?? "";

        foreach (var (pattern, regex) in _piiPatterns)
        {
            if (fieldName.Contains(pattern, StringComparison.OrdinalIgnoreCase) && regex.IsMatch(strValue))
                return true;
        }

        return false;
    }
}
