namespace QuerySpec.Core.Security;

/// <summary>
/// Sensitivity categories used by <see cref="IPiiClassifier"/> implementations.
/// Categories carry an intent — callers map them onto a <see cref="MaskingStrategy"/>.
/// </summary>
public enum PiiCategory
{
    /// <summary>The field is not personally identifiable.</summary>
    None = 0,

    /// <summary>Direct identifier — name, government id, account number. Mask aggressively.</summary>
    DirectIdentifier,

    /// <summary>Contact information — email, phone, address. Mask but keep correlation token if needed.</summary>
    Contact,

    /// <summary>Financial identifier — credit card, bank account, IBAN.</summary>
    Financial,

    /// <summary>Health information — medical record id, diagnosis code.</summary>
    Health,

    /// <summary>Location data — precise coordinates, IP address.</summary>
    Location,

    /// <summary>Sensitive but not otherwise categorized — credentials, tokens, secrets.</summary>
    Sensitive
}
