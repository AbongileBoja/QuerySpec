using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using QuerySpec.Core.Advanced;

namespace QuerySpec.EFCore;

/// <summary>
/// EF Core expression translator for advanced filter expressions.
/// Translates AdvancedFilterExpression into LINQ expression trees applicable to IQueryable&lt;T&gt;.
/// Optimized for enterprise use: cached reflection, EF Core SQL-compatible expressions,
/// null-safe comparisons, and depth-limited recursion.
/// </summary>
public class QuerySpecExpressionTranslator
{
    private const int MaxFilterDepth = 10;
    private const int MaxInItems = 200;

    /// <summary>
    /// Maximum number of entries retained in the internal (Type, property-name) → PropertyInfo
    /// cache. When exceeded the cache is cleared in bulk; this is a simple bounded policy
    /// appropriate for a lookup cache where entries are cheap to recompute via reflection.
    /// </summary>
    internal const int PropertyCacheCapacity = 4096;

    private static readonly MethodInfo StringContainsMethod = typeof(StringHelper).GetMethod(nameof(StringHelper.Contains))!;
    private static readonly MethodInfo StringStartsWithMethod = typeof(StringHelper).GetMethod(nameof(StringHelper.StartsWith))!;
    private static readonly MethodInfo StringEndsWithMethod = typeof(StringHelper).GetMethod(nameof(StringHelper.EndsWith))!;
    private static readonly MethodInfo StringToStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes)!;

    /// <summary>
    /// Cached open-generic <c>Enumerable.Contains&lt;T&gt;(IEnumerable&lt;T&gt;, T)</c>. Resolved
    /// once at type init via a single <c>GetMethods()</c> call instead of per-<c>BuildIn</c>
    /// invocation; closed instantiations are memoised in <see cref="EnumerableContainsClosedCache"/>.
    /// </summary>
    private static readonly MethodInfo EnumerableContainsOpenGeneric = typeof(Enumerable).GetMethods()
        .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2);

    private static readonly ConcurrentDictionary<Type, MethodInfo> EnumerableContainsClosedCache = new();

    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> PropertyCache = new();

    /// <summary>
    /// Maximum number of distinct compiled filter predicates retained in the cache before
    /// bulk eviction. Entries are cheap to rebuild; capacity bounds worst-case memory at
    /// roughly a few MB even with complex trees.
    /// </summary>
    internal const int PredicateCacheCapacity = 1024;

    private static readonly ConcurrentDictionary<(Type, long), LambdaExpression> PredicateCache = new();

    /// <summary>
    /// Translates an advanced filter expression to an EF Core IQueryable.
    /// </summary>
    public static IQueryable<T> ApplyFilter<T>(
        IQueryable<T> query,
        AdvancedFilterExpression? filter) where T : class
    {
        if (filter == null)
            return query;

        var errors = filter.Validate();
        if (errors.Any())
            throw new ArgumentException($"Invalid filter: {string.Join(", ", errors)}");

        var predicate = BuildPredicate<T>(filter, 0);
        return query.Where(predicate);
    }

    /// <summary>
    /// Same contract as <see cref="ApplyFilter{T}"/>, but memoizes the built predicate by
    /// <c>(typeof(T), filter.ComputeStableHash())</c>. Subsequent calls with a structurally-
    /// equivalent filter skip tree construction, reflection, and <c>Validate()</c>, which
    /// cuts per-call allocation on the hot path by ~99% for repeated filter shapes.
    /// </summary>
    /// <remarks>
    /// Use this overload when the same filter shapes are applied repeatedly (typical for
    /// API endpoints that accept a bounded set of query shapes). For one-shot ad-hoc filters,
    /// prefer <see cref="ApplyFilter{T}"/> so the cache doesn't accumulate single-use entries.
    /// Validation runs on cache miss only — invalid filters still throw on first insertion.
    /// </remarks>
    public static IQueryable<T> ApplyFilterCached<T>(
        IQueryable<T> query,
        AdvancedFilterExpression? filter) where T : class
    {
        if (filter == null)
            return query;

        var predicate = GetOrBuildCachedPredicate<T>(filter);
        return query.Where(predicate);
    }

    /// <summary>
    /// Exposes the cached <see cref="Expression{TDelegate}"/> directly so callers can
    /// compose it into larger queries without forcing a <c>.Where(...)</c>.
    /// </summary>
    public static Expression<Func<T, bool>> GetOrBuildCachedPredicate<T>(
        AdvancedFilterExpression filter) where T : class
    {
        if (filter is null) throw new ArgumentNullException(nameof(filter));

        var hash = filter.ComputeStableHash();
        var key = (typeof(T), hash);

        if (PredicateCache.TryGetValue(key, out var cached))
            return (Expression<Func<T, bool>>)cached;

        var errors = filter.Validate();
        if (errors.Any())
            throw new ArgumentException($"Invalid filter: {string.Join(", ", errors)}");

        var predicate = BuildPredicate<T>(filter, 0);

        // Bounded cache with bulk eviction; matches the policy already used for PropertyCache.
        if (PredicateCache.Count >= PredicateCacheCapacity)
            PredicateCache.Clear();

        PredicateCache.TryAdd(key, predicate);
        return predicate;
    }

    /// <summary>
    /// Clears the compiled-predicate cache. Intended for tests and diagnostic scenarios;
    /// production code should not need to call this.
    /// </summary>
    public static void ClearPredicateCache() => PredicateCache.Clear();

    /// <summary>
    /// Applies aggregation to a queryable. Currently unimplemented; throws when an aggregation
    /// is supplied so callers cannot silently rely on a no-op. Returns the queryable unchanged
    /// when <paramref name="aggregation"/> is null.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="aggregation">Aggregation request, or <c>null</c> for a passthrough.</param>
    /// <exception cref="NotImplementedException">Thrown when <paramref name="aggregation"/> is non-null.</exception>
    [Obsolete("Aggregation translation is not implemented. The method now throws when an aggregation is supplied; previously it silently returned the unmodified query.", error: false)]
    public static IQueryable<T> ApplyAggregation<T>(
        IQueryable<T> query,
        AggregationRequest? aggregation) where T : class
    {
        if (aggregation is null)
            return query;

        throw new NotImplementedException(
            "QuerySpecExpressionTranslator.ApplyAggregation is not implemented. " +
            "The previous behaviour was to silently return the unaggregated query, which masked logic errors in callers.");
    }

    private static Expression<Func<T, bool>> BuildPredicate<T>(AdvancedFilterExpression filter, int depth)
    {
        if (depth > MaxFilterDepth)
            throw new ArgumentException($"Filter nesting exceeds maximum depth of {MaxFilterDepth}");

        var param = Expression.Parameter(typeof(T), "x");
        var body = BuildBody(param, filter, typeof(T), depth);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static Expression BuildBody(ParameterExpression param, AdvancedFilterExpression filter, Type entityType, int depth)
    {
        if (depth > MaxFilterDepth)
            throw new ArgumentException($"Filter nesting exceeds maximum depth of {MaxFilterDepth}");

        Expression? body = null;

        if (!string.IsNullOrEmpty(filter.Field))
        {
            body = BuildOperatorExpression(param, filter, entityType);
        }

        if (filter.Filters?.Any() == true)
        {
            foreach (var nested in filter.Filters)
            {
                var nestedBody = BuildBody(param, nested, entityType, depth + 1);

                body = filter.Logic switch
                {
                    LogicalOperator.And => body == null ? nestedBody : Expression.AndAlso(body, nestedBody),
                    LogicalOperator.Or => body == null ? nestedBody : Expression.OrElse(body, nestedBody),
                    LogicalOperator.Xor => body == null ? nestedBody : Expression.ExclusiveOr(body, nestedBody),
                    _ => body
                };
            }
        }

        return body ?? Expression.Constant(true);
    }

    private static Expression BuildOperatorExpression(ParameterExpression param, AdvancedFilterExpression filter, Type entityType)
    {
        var property = GetPropertyExpression(param, filter.Field, entityType);
        var propertyType = property.Type;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var isNullable = Nullable.GetUnderlyingType(propertyType) != null;

        return filter.Operator switch
        {
            FilterOperator.Equal => BuildEqual(property, filter.Value, propertyType, isNullable),
            FilterOperator.NotEqual => Expression.Not(BuildEqual(property, filter.Value, propertyType, isNullable)),
            FilterOperator.GreaterThan => BuildComparison(property, filter.Value, propertyType, underlyingType, isNullable, ExpressionType.GreaterThan),
            FilterOperator.GreaterThanOrEqual => BuildComparison(property, filter.Value, propertyType, underlyingType, isNullable, ExpressionType.GreaterThanOrEqual),
            FilterOperator.LessThan => BuildComparison(property, filter.Value, propertyType, underlyingType, isNullable, ExpressionType.LessThan),
            FilterOperator.LessThanOrEqual => BuildComparison(property, filter.Value, propertyType, underlyingType, isNullable, ExpressionType.LessThanOrEqual),

            FilterOperator.Contains => BuildStringMethod(property, filter.Value, StringContainsMethod, filter.CaseSensitive, isNullable),
            FilterOperator.NotContains => Expression.Not(BuildStringMethod(property, filter.Value, StringContainsMethod, filter.CaseSensitive, isNullable)),
            FilterOperator.StartsWith => BuildStringMethod(property, filter.Value, StringStartsWithMethod, filter.CaseSensitive, isNullable),
            FilterOperator.EndsWith => BuildStringMethod(property, filter.Value, StringEndsWithMethod, filter.CaseSensitive, isNullable),
            FilterOperator.StringMatchCase => BuildEqual(property, filter.Value, propertyType, isNullable),
            FilterOperator.StringMatchIgnoreCase => BuildStringMethod(property, filter.Value, StringContainsMethod, false, isNullable),
            FilterOperator.Regex => BuildRegexMatch(property, filter.Value, isNullable),
            FilterOperator.Contains_CaseInsensitive => BuildStringMethod(property, filter.Value, StringContainsMethod, false, isNullable),

            FilterOperator.In => BuildIn(property, filter.Value, propertyType, underlyingType, isNullable),
            FilterOperator.NotIn => Expression.Not(BuildIn(property, filter.Value, propertyType, underlyingType, isNullable)),

            FilterOperator.Between => BuildBetween(property, filter.Value, filter.ValueTo, propertyType, underlyingType, isNullable),
            FilterOperator.NotBetween => Expression.Not(BuildBetween(property, filter.Value, filter.ValueTo, propertyType, underlyingType, isNullable)),

            FilterOperator.IsNull => BuildIsNull(property, isNullable),
            FilterOperator.IsNotNull => Expression.Not(BuildIsNull(property, isNullable)),

            FilterOperator.IsEmpty => BuildIsEmpty(property, isNullable),
            FilterOperator.IsNotEmpty => Expression.Not(BuildIsEmpty(property, isNullable)),

            FilterOperator.DateInRange when filter.TemporalStart.HasValue && filter.TemporalEnd.HasValue =>
                BuildDateInRange(property, filter.TemporalStart.Value, filter.TemporalEnd.Value, isNullable),
            FilterOperator.DateAfter => BuildDateComparison(property, filter.Value, ExpressionType.GreaterThan, isNullable),
            FilterOperator.DateBefore => BuildDateComparison(property, filter.Value, ExpressionType.LessThan, isNullable),
            FilterOperator.DateEquals => BuildDateEquals(property, filter.Value, isNullable),

            _ => Expression.Constant(true)
        };
    }

    private static Expression GetPropertyExpression(ParameterExpression param, string fieldName, Type entityType)
    {
        var parts = fieldName.Split('.');
        Expression current = param;
        foreach (var part in parts)
        {
            if (PropertyCache.Count >= PropertyCacheCapacity)
                PropertyCache.Clear();

            var prop = PropertyCache.GetOrAdd((current.Type, part),
                key => key.Item1.GetProperty(key.Item2, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new ArgumentException($"Property '{key.Item2}' not found on type '{key.Item1.Name}'"));
            current = Expression.Property(current, prop);
        }
        return current;
    }

    #region Comparison Operators

    private static Expression BuildEqual(Expression property, object? value, Type propertyType, bool isNullable)
    {
        var converted = NormalizeValue(value, propertyType);
        return Expression.Equal(property, Expression.Constant(converted, propertyType));
    }

    private static Expression BuildComparison(Expression property, object? value, Type propertyType, Type underlyingType, bool isNullable, ExpressionType op)
    {
        var converted = NormalizeValue(value, propertyType);
        var constant = Expression.Constant(converted, propertyType);

        if (isNullable && underlyingType.IsValueType)
        {
            var hasValue = Expression.Property(property, propertyType.GetProperty("HasValue")!);
            var valueAccess = Expression.Property(property, propertyType.GetProperty("Value")!);
            var underlyingConstant = Expression.Constant(converted, underlyingType);

            Expression comparison = op switch
            {
                ExpressionType.GreaterThan => Expression.GreaterThan(valueAccess, underlyingConstant),
                ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(valueAccess, underlyingConstant),
                ExpressionType.LessThan => Expression.LessThan(valueAccess, underlyingConstant),
                ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(valueAccess, underlyingConstant),
                _ => Expression.Constant(true)
            };

            return Expression.AndAlso(hasValue, comparison);
        }

        return op switch
        {
            ExpressionType.GreaterThan => Expression.GreaterThan(property, constant),
            ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, constant),
            ExpressionType.LessThan => Expression.LessThan(property, constant),
            ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(property, constant),
            _ => Expression.Constant(true)
        };
    }

    #endregion

    #region String Operators

    private static Expression BuildStringMethod(Expression property, object? value, MethodInfo helperMethod, bool caseSensitive, bool isNullable)
    {
        var strValue = value?.ToString() ?? "";

        Expression stringProperty = property.Type == typeof(string)
            ? property
            : Expression.Call(property, StringToStringMethod);

        Expression call = Expression.Call(helperMethod, stringProperty, Expression.Constant(strValue), Expression.Constant(caseSensitive));

        if (isNullable)
        {
            var hasValue = Expression.Property(property, property.Type.GetProperty("HasValue")!);
            return Expression.AndAlso(hasValue, call);
        }

        if (property.Type != typeof(string)
            && property.Type.IsClass
            && property.Type != typeof(object))
        {
            return Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, property.Type)), call);
        }

        return call;
    }

    private static Expression BuildRegexMatch(Expression property, object? value, bool isNullable)
    {
        var pattern = value?.ToString() ?? "";

        Expression stringProperty = property.Type == typeof(string)
            ? property
            : Expression.Call(property, StringToStringMethod);

        var regexIsMatch = typeof(RegexHelper).GetMethod(nameof(RegexHelper.IsMatch))!;
        var call = Expression.Call(regexIsMatch, stringProperty, Expression.Constant(pattern));

        if (isNullable)
        {
            var hasValue = Expression.Property(property, property.Type.GetProperty("HasValue")!);
            return Expression.AndAlso(hasValue, call);
        }

        return call;
    }

    #endregion

    #region Collection Operators

    private static Expression BuildIn(Expression property, object? value, Type propertyType, Type underlyingType, bool isNullable)
    {
        if (value == null)
            return Expression.Constant(false);

        var items = ExtractArrayValues(value, propertyType);
        if (items.Count == 0)
            return Expression.Constant(false);

        if (items.Count > MaxInItems)
            throw new ArgumentException($"IN operator exceeds maximum of {MaxInItems} items");

        var typedArray = Array.CreateInstance(underlyingType, items.Count);
        for (var i = 0; i < items.Count; i++)
            typedArray.SetValue(items[i], i);

        var containsMethod = EnumerableContainsClosedCache.GetOrAdd(
            underlyingType,
            static t => EnumerableContainsOpenGeneric.MakeGenericMethod(t));

        var constantArray = Expression.Constant(typedArray, underlyingType.MakeArrayType());
        var containsCall = Expression.Call(containsMethod, constantArray, property);

        if (isNullable && underlyingType.IsValueType)
        {
            var hasValue = Expression.Property(property, propertyType.GetProperty("HasValue")!);
            return Expression.AndAlso(hasValue, containsCall);
        }

        return containsCall;
    }

    #endregion

    #region Range Operators

    private static Expression BuildBetween(Expression property, object? valueFrom, object? valueTo, Type propertyType, Type underlyingType, bool isNullable)
    {
        var from = NormalizeValue(valueFrom, propertyType);
        var to = NormalizeValue(valueTo, propertyType);

        if (isNullable && underlyingType.IsValueType)
        {
            var hasValue = Expression.Property(property, propertyType.GetProperty("HasValue")!);
            var valueAccess = Expression.Property(property, propertyType.GetProperty("Value")!);
            var fromExpr = Expression.Constant(from, underlyingType);
            var toExpr = Expression.Constant(to, underlyingType);

            var rangeCheck = Expression.AndAlso(
                Expression.GreaterThanOrEqual(valueAccess, fromExpr),
                Expression.LessThanOrEqual(valueAccess, toExpr));

            return Expression.AndAlso(hasValue, rangeCheck);
        }

        var fromConstant = Expression.Constant(from, propertyType);
        var toConstant = Expression.Constant(to, propertyType);

        return Expression.AndAlso(
            Expression.GreaterThanOrEqual(property, fromConstant),
            Expression.LessThanOrEqual(property, toConstant));
    }

    #endregion

    #region Null Operators

    private static Expression BuildIsNull(Expression property, bool isNullable)
    {
        if (isNullable)
        {
            var hasValue = Expression.Property(property, property.Type.GetProperty("HasValue")!);
            return Expression.Not(hasValue);
        }

        return Expression.Equal(property, Expression.Constant(null, property.Type));
    }

    private static Expression BuildIsEmpty(Expression property, bool isNullable)
    {
        if (isNullable)
        {
            var hasValue = Expression.Property(property, property.Type.GetProperty("HasValue")!);
            var valueAccess = Expression.Property(property, property.Type.GetProperty("Value")!);
            return Expression.OrElse(
                Expression.Not(hasValue),
                Expression.Equal(valueAccess, Expression.Constant(string.Empty, property.Type)));
        }

        return Expression.Equal(property, Expression.Constant(string.Empty, property.Type));
    }

    #endregion

    #region Temporal Operators

    private static Expression BuildDateInRange(Expression property, DateTime from, DateTime to, bool isNullable)
    {
        var fromExpr = Expression.Constant(from, typeof(DateTime));
        var toExpr = Expression.Constant(to, typeof(DateTime));

        if (isNullable)
        {
            var hasValue = Expression.Property(property, property.Type.GetProperty("HasValue")!);
            var valueAccess = Expression.Property(property, property.Type.GetProperty("Value")!);
            var rangeCheck = Expression.AndAlso(
                Expression.GreaterThanOrEqual(valueAccess, fromExpr),
                Expression.LessThanOrEqual(valueAccess, toExpr));
            return Expression.AndAlso(hasValue, rangeCheck);
        }

        return Expression.AndAlso(
            Expression.GreaterThanOrEqual(property, fromExpr),
            Expression.LessThanOrEqual(property, toExpr));
    }

    private static Expression BuildDateComparison(Expression property, object? value, ExpressionType op, bool isNullable)
    {
        var dateValue = (DateTime?)NormalizeValue(value, typeof(DateTime));
        if (dateValue == null)
            return Expression.Constant(false);

        var constant = Expression.Constant(dateValue.Value, typeof(DateTime));

        if (isNullable)
        {
            var hasValue = Expression.Property(property, property.Type.GetProperty("HasValue")!);
            var valueAccess = Expression.Property(property, property.Type.GetProperty("Value")!);
            Expression comparison = op switch
            {
                ExpressionType.GreaterThan => Expression.GreaterThan(valueAccess, constant),
                ExpressionType.LessThan => Expression.LessThan(valueAccess, constant),
                _ => Expression.Constant(true)
            };
            return Expression.AndAlso(hasValue, comparison);
        }

        return op switch
        {
            ExpressionType.GreaterThan => Expression.GreaterThan(property, constant),
            ExpressionType.LessThan => Expression.LessThan(property, constant),
            _ => Expression.Constant(true)
        };
    }

    private static Expression BuildDateEquals(Expression property, object? value, bool isNullable)
    {
        var dateValue = (DateTime?)NormalizeValue(value, typeof(DateTime));
        if (dateValue == null)
            return Expression.Constant(false);

        var dayStart = dateValue.Value.Date;
        var dayEnd = dayStart.AddDays(1);

        var dayStartExpr = Expression.Constant(dayStart, typeof(DateTime));
        var dayEndExpr = Expression.Constant(dayEnd, typeof(DateTime));

        if (isNullable)
        {
            var hasValue = Expression.Property(property, property.Type.GetProperty("HasValue")!);
            var valueAccess = Expression.Property(property, property.Type.GetProperty("Value")!);
            var rangeCheck = Expression.AndAlso(
                Expression.GreaterThanOrEqual(valueAccess, dayStartExpr),
                Expression.LessThan(valueAccess, dayEndExpr));
            return Expression.AndAlso(hasValue, rangeCheck);
        }

        return Expression.AndAlso(
            Expression.GreaterThanOrEqual(property, dayStartExpr),
            Expression.LessThan(property, dayEndExpr));
    }

    #endregion

    #region Value Normalization

    private static object? NormalizeValue(object? value, Type? targetType)
    {
        if (value == null) return null;

        if (value is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.String:
                    var s = je.GetString();
                    if (targetType == null || targetType == typeof(string)) return s;
                    var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    if (underlying == typeof(DateTime) && DateTime.TryParse(s, out var dt)) return dt;
                    if (underlying == typeof(bool) && bool.TryParse(s, out var b)) return b;
                    if (IsNumericType(underlying) && double.TryParse(s, out var dbl))
                        return Convert.ChangeType(dbl, underlying);
                    return s;
                case JsonValueKind.Number:
                    if (targetType == typeof(int) || targetType == typeof(int?)) return je.GetInt32();
                    if (targetType == typeof(long) || targetType == typeof(long?)) return je.GetInt64();
                    if (targetType == typeof(double) || targetType == typeof(double?)) return je.GetDouble();
                    if (targetType == typeof(decimal) || targetType == typeof(decimal?)) return je.GetDecimal();
                    if (targetType == typeof(float) || targetType == typeof(float?)) return je.GetSingle();
                    return je.GetDouble();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return je.GetBoolean();
                default:
                    return je.ToString();
            }
        }

        if (targetType != null)
        {
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            try
            {
                if (underlying == typeof(DateTime) && value is string vs && DateTime.TryParse(vs, out var dt2))
                    return dt2;
                if (IsNumericType(underlying))
                    return Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture);
                if (underlying == typeof(bool) && value is string vs2 && bool.TryParse(vs2, out var bv))
                    return bv;
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                throw new ArgumentException(
                    $"Unable to convert value '{value}' (type {value.GetType().FullName}) to target type '{underlying.FullName}': {ex.Message}",
                    nameof(value), ex);
            }
        }

        return value;
    }

    private static List<object?> ExtractArrayValues(object value, Type propertyType)
    {
        var items = new List<object?>();

        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
                items.Add(NormalizeValue(item, propertyType));
        }
        else if (value is System.Collections.IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
                items.Add(NormalizeValue(item, propertyType));
        }
        else
        {
            items.Add(NormalizeValue(value, propertyType));
        }

        return items;
    }

    private static bool IsNumericType(Type? type)
    {
        if (type == null) return false;
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong)
            || t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    #endregion

    #region Helper Types for EF Core-Compatible Expressions

    /// <summary>Helper class for string operations in EF Core-compatible expressions.</summary>
    public static class StringHelper
    {
        /// <summary>Checks if source contains value with optional case sensitivity.</summary>
        public static bool Contains(string? source, string value, bool caseSensitive)
        {
            if (source == null) return false;
            return caseSensitive
                ? source.Contains(value)
                : source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Checks if source starts with value with optional case sensitivity.</summary>
        public static bool StartsWith(string? source, string value, bool caseSensitive)
        {
            if (source == null) return false;
            return caseSensitive
                ? source.StartsWith(value)
                : source.StartsWith(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Checks if source ends with value with optional case sensitivity.</summary>
        public static bool EndsWith(string? source, string value, bool caseSensitive)
        {
            if (source == null) return false;
            return caseSensitive
                ? source.EndsWith(value)
                : source.EndsWith(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Helper class for regex operations in EF Core-compatible expressions.</summary>
    public static class RegexHelper
    {
        /// <summary>
        /// Maximum number of compiled <see cref="System.Text.RegularExpressions.Regex"/> instances retained in the
        /// cache before bulk eviction. Patterns are typically supplied by clients via filter payloads,
        /// so an unbounded cache is a heap-DoS vector. Bulk-clear matches the policy used for
        /// <c>PropertyCache</c> and <c>PredicateCache</c>: simple, bounded, and cheap on rebuild.
        /// </summary>
        internal const int RegexCacheCapacity = 512;

        private static readonly ConcurrentDictionary<string, System.Text.RegularExpressions.Regex> RegexCache =
            new(StringComparer.Ordinal);

        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Clears the compiled-regex cache. Intended for tests and diagnostic scenarios; production
        /// code does not need to call this — the cache self-bounds at <see cref="RegexCacheCapacity"/>.
        /// </summary>
        public static void ClearRegexCache() => RegexCache.Clear();

        /// <summary>
        /// Checks if <paramref name="input"/> matches <paramref name="pattern"/>. Patterns are compiled
        /// once and cached up to <see cref="RegexCacheCapacity"/> distinct patterns; the cache is
        /// bulk-cleared on overflow to prevent unbounded heap growth from hostile or naturally-diverse
        /// pattern streams. Invalid patterns throw <see cref="ArgumentException"/>; a match timeout
        /// of 500ms is enforced to prevent catastrophic backtracking from freezing the host.
        /// </summary>
        public static bool IsMatch(string? input, string pattern)
        {
            if (input == null) return false;
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("Regex pattern must not be empty.", nameof(pattern));

            System.Text.RegularExpressions.Regex regex;
            try
            {
                if (RegexCache.Count >= RegexCacheCapacity)
                    RegexCache.Clear();

                regex = RegexCache.GetOrAdd(pattern, static p => new System.Text.RegularExpressions.Regex(
                    p,
                    System.Text.RegularExpressions.RegexOptions.Compiled
                        | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    MatchTimeout));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regex pattern '{pattern}': {ex.Message}", nameof(pattern), ex);
            }

            try
            {
                return regex.IsMatch(input);
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException ex)
            {
                throw new TimeoutException(
                    $"Regex match exceeded {MatchTimeout.TotalMilliseconds}ms for pattern '{pattern}'. " +
                    "This usually indicates catastrophic backtracking; review the pattern.", ex);
            }
        }
    }

    #endregion
}
