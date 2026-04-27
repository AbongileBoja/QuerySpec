using System;

namespace QuerySpec.Core.Advanced;

/// <summary>
/// Advanced filter operators for enterprise query scenarios.
/// </summary>
public enum FilterOperator
{
    // COMPARISON
    /// <summary>Equal comparison.</summary>
    Equal = 0,
    /// <summary>Not equal comparison.</summary>
    NotEqual = 1,
    /// <summary>Greater than comparison.</summary>
    GreaterThan = 2,
    /// <summary>Greater than or equal comparison.</summary>
    GreaterThanOrEqual = 3,
    /// <summary>Less than comparison.</summary>
    LessThan = 4,
    /// <summary>Less than or equal comparison.</summary>
    LessThanOrEqual = 5,

    // STRING
    /// <summary>Contains substring.</summary>
    Contains = 10,
    /// <summary>Does not contain substring.</summary>
    NotContains = 11,
    /// <summary>Starts with substring.</summary>
    StartsWith = 12,
    /// <summary>Ends with substring.</summary>
    EndsWith = 13,
    /// <summary>String match with case sensitivity.</summary>
    StringMatchCase = 14,
    /// <summary>String match ignoring case.</summary>
    StringMatchIgnoreCase = 15,
    /// <summary>Regular expression match.</summary>
    Regex = 16,

    // COLLECTIONS
    /// <summary>Value is in collection.</summary>
    In = 20,
    /// <summary>Value is not in collection.</summary>
    NotIn = 21,
    /// <summary>Any element matches condition.</summary>
    AnyMatch = 22,
    /// <summary>All elements match condition.</summary>
    AllMatch = 23,
    /// <summary>Count is greater than value.</summary>
    CountGreaterThan = 24,
    /// <summary>Count is less than value.</summary>
    CountLessThan = 25,

    // RANGES
    /// <summary>Value is between two values (inclusive).</summary>
    Between = 30,
    /// <summary>Value is not between two values.</summary>
    NotBetween = 31,

    // NULL
    /// <summary>Value is null.</summary>
    IsNull = 40,
    /// <summary>Value is not null.</summary>
    IsNotNull = 41,

    // ADVANCED
    /// <summary>Value is empty.</summary>
    IsEmpty = 50,
    /// <summary>Value is not empty.</summary>
    IsNotEmpty = 51,
    /// <summary>
    /// Contains substring case-insensitive. Snake-case spelling retained for source compatibility
    /// with 2.x consumers; prefer <see cref="ContainsCaseInsensitive"/>. Will be removed in 3.0.
    /// </summary>
    [Obsolete("Use ContainsCaseInsensitive. Will be removed in 3.0.", error: false)]
    Contains_CaseInsensitive = 52,
    /// <summary>Contains substring case-insensitive.</summary>
    ContainsCaseInsensitive = 52,
    /// <summary>Full-text search.</summary>
    FullTextSearch = 60,
    /// <summary>Geographic distance filter.</summary>
    GeoDistance = 70,
    /// <summary>Geographic within radius filter.</summary>
    GeoWithin = 71,

    // TEMPORAL
    /// <summary>Date is within range.</summary>
    DateInRange = 80,
    /// <summary>Date is after specified date.</summary>
    DateAfter = 81,
    /// <summary>Date is before specified date.</summary>
    DateBefore = 82,
    /// <summary>Date equals specified date.</summary>
    DateEquals = 83,
    /// <summary>Relative date filter.</summary>
    RelativeDate = 84,

    // BITWISE
    /// <summary>Bitwise AND operation.</summary>
    BitwiseAnd = 90,
    /// <summary>Bitwise OR operation.</summary>
    BitwiseOr = 91,

    // CUSTOM
    /// <summary>Custom operator.</summary>
    Custom = 999
}

/// <summary>
/// Logical operators for combining filters.
/// </summary>
public enum LogicalOperator
{
    /// <summary>Logical AND.</summary>
    And = 0,
    /// <summary>Logical OR.</summary>
    Or = 1,
    /// <summary>Logical XOR.</summary>
    Xor = 2
}

/// <summary>
/// Aggregation operators for analytical queries.
/// </summary>
public enum AggregationOperator
{
    /// <summary>Count aggregation.</summary>
    Count = 0,
    /// <summary>Sum aggregation.</summary>
    Sum = 1,
    /// <summary>Average aggregation.</summary>
    Average = 2,
    /// <summary>Minimum aggregation.</summary>
    Min = 3,
    /// <summary>Maximum aggregation.</summary>
    Max = 4,
    /// <summary>Distinct aggregation.</summary>
    Distinct = 5,
    /// <summary>Group by aggregation.</summary>
    GroupBy = 6,
    /// <summary>Standard deviation aggregation.</summary>
    StdDev = 7,
    /// <summary>Variance aggregation.</summary>
    Variance = 8,
    /// <summary>Percentile aggregation.</summary>
    Percentile = 9
}
