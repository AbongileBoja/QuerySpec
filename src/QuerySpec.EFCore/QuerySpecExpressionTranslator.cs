using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using QuerySpec.Core.Advanced;
using QuerySpec.Core.Diagnostics;

namespace QuerySpec.EFCore;

/// <summary>
/// EF Core expression translator for advanced filter expressions.
/// Translates <see cref="FilterSpec"/> into LINQ expression trees applicable to <see cref="IQueryable{T}"/>.
/// Optimized for enterprise use: cached reflection, EF Core SQL-compatible expressions,
/// null-safe comparisons, and depth-limited recursion.
/// </summary>
public static class QuerySpecExpressionTranslator
{
    private const int MaxFilterDepth = 10;
    private const int MaxInItems = 200;

    internal const string BuildInRequiresDynamicCodeMessage =
        "QuerySpec filter translation builds an Enumerable.Contains<T> call via MethodInfo.MakeGenericMethod / Array.CreateInstance for In/NotIn operators. These APIs emit IL at runtime and are not supported under Native AOT. Avoid In/NotIn in AOT-published applications, or pre-translate to a Contains-call against a strongly-typed array at the call site.";

    internal const string TranslateRequiresUnreferencedCodeMessage =
        "QuerySpec filter translation resolves entity properties by name via reflection (Type.GetProperty), navigates nested property paths whose intermediate types are not statically annotated, and reads Nullable<T>.HasValue/Value via reflected PropertyInfo. Under PublishTrimmed the trimmer cannot prove these members are preserved for arbitrary entity types, so callers must either annotate T with [DynamicallyAccessedMembers(PublicProperties)] for the entire entity graph or opt out of trimming for the call site.";

    /// <summary>
    /// Maximum number of entries retained in the internal (Type, property-name) → PropertyInfo
    /// cache before generation-based LRU eviction drops the bottom 25% by last-access epoch.
    /// </summary>
    internal const int PropertyCacheCapacity = 4096;

    private static readonly GenerationCache<(Type, string), PropertyInfo> PropertyCache
        = new(PropertyCacheCapacity);

    private static readonly MethodInfo StringToStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes)!;

    private static readonly MethodInfo StringContainsMethod =
        typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

    private static readonly MethodInfo StringStartsWithMethod =
        typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;

    private static readonly MethodInfo StringEndsWithMethod =
        typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;

    private static readonly MethodInfo StringToLowerMethod =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;


    /// <summary>
    /// Cached open-generic <c>Enumerable.Contains&lt;T&gt;(IEnumerable&lt;T&gt;, T)</c>. Resolved
    /// once at type init via a single <c>GetMethods()</c> call instead of per-<c>BuildIn</c>
    /// invocation; closed instantiations are memoised in <see cref="EnumerableContainsClosedCache"/>.
    /// </summary>
    private static readonly MethodInfo EnumerableContainsOpenGeneric = typeof(Enumerable).GetMethods()
        .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2);

    private static readonly ConcurrentDictionary<Type, MethodInfo> EnumerableContainsClosedCache = new();


    /// <summary>
    /// Maximum number of distinct closed <c>Nullable&lt;T&gt;</c> types whose
    /// <c>HasValue</c> / <c>Value</c> <see cref="PropertyInfo"/> pairs are cached before
    /// generation-based LRU eviction drops the bottom 25% by last-access epoch.
    /// </summary>
    internal const int NullablePropertyInfoCacheCapacity = 4096;

    private static readonly GenerationCache<Type, (PropertyInfo HasValue, PropertyInfo Value)> NullablePropertyInfoCache
        = new(NullablePropertyInfoCacheCapacity);

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static (PropertyInfo HasValue, PropertyInfo Value) GetNullablePropertyInfos(Type nullableType)
        => NullablePropertyInfoCache.GetOrAdd(nullableType, static t =>
            (t.GetProperty("HasValue")!, t.GetProperty("Value")!));

    /// <summary>
    /// Maximum number of distinct compiled filter predicates retained per entity-type partition
    /// before generation-based LRU eviction drops the bottom 25% by last-access epoch.
    /// Each entity type has its own isolated partition; one hot entity type cannot evict
    /// predicates compiled for another type.
    /// </summary>
    internal const int PredicateCacheCapacity = 1024;

    private static readonly ConcurrentBag<Action> PredicateCacheClearActions = new();

    private static class PerTypePredicateCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> where T : class
    {
        internal static readonly GenerationCache<long, LambdaExpression> Cache = CreateAndRegister();

        private static GenerationCache<long, LambdaExpression> CreateAndRegister()
        {
            var cache = new GenerationCache<long, LambdaExpression>(PredicateCacheCapacity);
            PredicateCacheClearActions.Add(cache.Clear);
            return cache;
        }
    }

    /// <summary>
    /// Translates an immutable <see cref="FilterSpec"/> to an EF Core <see cref="IQueryable{T}"/>.
    /// </summary>
    /// <typeparam name="T">Entity type the queryable produces. Must be a reference type so it can compose with EF Core entity-framework constraints.</typeparam>
    /// <param name="query">Source queryable to compose the filter onto.</param>
    /// <param name="filter">Filter specification to apply, or <c>null</c> for a passthrough.</param>
    /// <returns>A new <see cref="IQueryable{T}"/> with the filter appended via <see cref="Queryable.Where{TSource}(IQueryable{TSource}, System.Linq.Expressions.Expression{Func{TSource, bool}})"/>; the input <paramref name="query"/> when <paramref name="filter"/> is null.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filter"/> fails <see cref="FilterSpec.Validate"/> or its nesting depth exceeds the configured maximum.</exception>
    [RequiresDynamicCode(BuildInRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    public static IQueryable<T> ApplyFilter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        IQueryable<T> query,
        FilterSpec? filter) where T : class
    {
        if (filter is null)
            return query;

        var entityType = typeof(T).Name;
        var buildEnabled = QuerySpecMetrics.FilterBuildDuration.Enabled;
        var sw = buildEnabled ? Stopwatch.StartNew() : null;

        var errors = filter.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid filter: {string.Join(", ", errors)}");

        var predicate = BuildPredicate<T>(filter, 0);

        sw?.Stop();
        if (buildEnabled)
            QuerySpecMetrics.FilterBuildDuration.Record(sw!.Elapsed.TotalMilliseconds);
        if (QuerySpecMetrics.FilterApplications.Enabled)
            QuerySpecMetrics.FilterApplications.Add(1,
                new KeyValuePair<string, object?>("cached", false),
                new KeyValuePair<string, object?>("entity_type", entityType));

        return query.Where(predicate);
    }

    /// <summary>
    /// Same contract as <see cref="ApplyFilter{T}(IQueryable{T}, FilterSpec?)"/>, but memoizes
    /// the built predicate by <c>(typeof(T), filter.ComputeStableHash())</c>. Subsequent calls
    /// with a structurally-equivalent filter skip tree construction, reflection, and
    /// <c>Validate()</c>, which cuts per-call allocation on the hot path by ~99% for repeated
    /// filter shapes.
    /// </summary>
    /// <remarks>
    /// Use this overload when the same filter shapes are applied repeatedly (typical for
    /// API endpoints that accept a bounded set of query shapes). For one-shot ad-hoc filters,
    /// prefer <see cref="ApplyFilter{T}(IQueryable{T}, FilterSpec?)"/> so the cache doesn't
    /// accumulate single-use entries. Validation runs on cache miss only — invalid filters
    /// still throw on first insertion.
    /// </remarks>
    /// <typeparam name="T">Entity type the queryable produces.</typeparam>
    /// <param name="query">Source queryable to compose the filter onto.</param>
    /// <param name="filter">Filter specification to apply, or <c>null</c> for a passthrough.</param>
    /// <returns>A new <see cref="IQueryable{T}"/> with the cached predicate appended; the input <paramref name="query"/> when <paramref name="filter"/> is null.</returns>
    /// <exception cref="ArgumentException">Thrown on cache miss when <paramref name="filter"/> fails <see cref="FilterSpec.Validate"/> or its nesting depth exceeds the configured maximum.</exception>
    [RequiresDynamicCode(BuildInRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    public static IQueryable<T> ApplyFilterCached<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        IQueryable<T> query,
        FilterSpec? filter) where T : class
    {
        if (filter is null)
            return query;

        var predicate = GetOrBuildCachedPredicate<T>(filter);

        if (QuerySpecMetrics.FilterApplications.Enabled)
            QuerySpecMetrics.FilterApplications.Add(1,
                new KeyValuePair<string, object?>("cached", true),
                new KeyValuePair<string, object?>("entity_type", typeof(T).Name));

        return query.Where(predicate);
    }

    /// <summary>
    /// Exposes the cached <see cref="Expression{TDelegate}"/> directly so callers can
    /// compose it into larger queries without forcing a <c>.Where(...)</c>.
    /// </summary>
    /// <typeparam name="T">Entity type the predicate applies to.</typeparam>
    /// <param name="filter">Filter specification to compile or fetch from the cache. Must not be null.</param>
    /// <returns>The compiled predicate, retrieved from cache when available.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown on cache miss when <paramref name="filter"/> fails <see cref="FilterSpec.Validate"/> or its nesting depth exceeds the configured maximum.</exception>
    [RequiresDynamicCode(BuildInRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    public static Expression<Func<T, bool>> GetOrBuildCachedPredicate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        FilterSpec filter) where T : class
    {
        ArgumentNullException.ThrowIfNull(filter);

        var hash = filter.ComputeStableHash();
        var cache = PerTypePredicateCache<T>.Cache;

        if (cache.TryGetValue(hash, out var cached))
        {
            if (QuerySpecMetrics.CacheHits.Enabled)
                QuerySpecMetrics.CacheHits.Add(1,
                    new KeyValuePair<string, object?>("cache_name", "predicate"));
            return (Expression<Func<T, bool>>)cached;
        }

        if (QuerySpecMetrics.CacheMisses.Enabled)
            QuerySpecMetrics.CacheMisses.Add(1,
                new KeyValuePair<string, object?>("cache_name", "predicate"));

        var buildEnabled = QuerySpecMetrics.FilterBuildDuration.Enabled;
        var sw = buildEnabled ? Stopwatch.StartNew() : null;

        var errors = filter.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid filter: {string.Join(", ", errors)}");

        var predicate = BuildPredicate<T>(filter, 0);
        cache.Set(hash, predicate);

        sw?.Stop();
        if (buildEnabled)
            QuerySpecMetrics.FilterBuildDuration.Record(sw!.Elapsed.TotalMilliseconds);

        return predicate;
    }

    /// <summary>
    /// Clears the compiled-predicate cache for all entity types. Intended for tests and
    /// diagnostic scenarios; production code should not need to call this.
    /// </summary>
    public static void ClearPredicateCache()
    {
        foreach (var clear in PredicateCacheClearActions)
            clear();
    }

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

    [RequiresDynamicCode(BuildInRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression<Func<T, bool>> BuildPredicate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(FilterSpec filter, int depth)
    {
        if (depth > MaxFilterDepth)
            throw new ArgumentException($"Filter nesting exceeds maximum depth of {MaxFilterDepth}");

        var param = Expression.Parameter(typeof(T), "x");
        var body = BuildBody(param, filter, typeof(T), depth);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    [RequiresDynamicCode(BuildInRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression BuildBody(ParameterExpression param, FilterSpec filter, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type entityType, int depth)
    {
        if (depth > MaxFilterDepth)
            throw new ArgumentException($"Filter nesting exceeds maximum depth of {MaxFilterDepth}");

        Expression? body = null;

        if (!string.IsNullOrEmpty(filter.Field))
        {
            body = BuildOperatorExpression(param, filter, entityType);
        }

        if (filter.Filters.Count > 0)
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

    [RequiresDynamicCode(BuildInRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression BuildOperatorExpression(ParameterExpression param, FilterSpec filter, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type entityType)
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

            FilterOperator.Contains => BuildStringPredicate(property, filter.Value, StringPredicate.Contains, filter.CaseSensitive, isNullable),
            FilterOperator.NotContains => Expression.Not(BuildStringPredicate(property, filter.Value, StringPredicate.Contains, filter.CaseSensitive, isNullable)),
            FilterOperator.StartsWith => BuildStringPredicate(property, filter.Value, StringPredicate.StartsWith, filter.CaseSensitive, isNullable),
            FilterOperator.EndsWith => BuildStringPredicate(property, filter.Value, StringPredicate.EndsWith, filter.CaseSensitive, isNullable),
            FilterOperator.StringMatchCase => BuildEqual(property, filter.Value, propertyType, isNullable),
            FilterOperator.StringMatchIgnoreCase => BuildStringPredicate(property, filter.Value, StringPredicate.Contains, false, isNullable),
            FilterOperator.Regex => BuildRegexMatch(property, filter.Value, isNullable),
            FilterOperator.ContainsCaseInsensitive => BuildStringPredicate(property, filter.Value, StringPredicate.Contains, false, isNullable),

            FilterOperator.In => BuildIn(property, filter.Value, propertyType, underlyingType, isNullable),
            FilterOperator.NotIn => BuildIn(property, filter.Value, propertyType, underlyingType, isNullable, negate: true),

            FilterOperator.Between => BuildBetween(property, filter.Value, filter.ValueTo, propertyType, underlyingType, isNullable),
            FilterOperator.NotBetween => Expression.Not(BuildBetween(property, filter.Value, filter.ValueTo, propertyType, underlyingType, isNullable)),

            FilterOperator.IsNull => BuildIsNull(property, isNullable),
            FilterOperator.IsNotNull => Expression.Not(BuildIsNull(property, isNullable)),

            FilterOperator.IsEmpty => BuildIsEmpty(property, isNullable),
            FilterOperator.IsNotEmpty => Expression.Not(BuildIsEmpty(property, isNullable)),

            FilterOperator.DateInRange when filter.TemporalStart.HasValue && filter.TemporalEnd.HasValue =>
                BuildDateInRange(property, filter.TemporalStart.Value, filter.TemporalEnd.Value, isNullable),
            FilterOperator.DateInRange =>
                throw new NotSupportedException($"FilterOperator 'DateInRange' requires both TemporalStart and TemporalEnd to be set."),
            FilterOperator.DateAfter => BuildDateComparison(property, filter.Value, ExpressionType.GreaterThan, isNullable),
            FilterOperator.DateBefore => BuildDateComparison(property, filter.Value, ExpressionType.LessThan, isNullable),
            FilterOperator.DateEquals => BuildDateEquals(property, filter.Value, isNullable),

            var op => throw new NotSupportedException($"FilterOperator '{op}' is not implemented in the EF Core translator.")
        };
    }

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression GetPropertyExpression(ParameterExpression param, string fieldName, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type entityType)
    {
        var parts = fieldName.Split('.');
        Expression current = param;
        foreach (var part in parts)
        {
            var prop = PropertyCache.GetOrAdd((current.Type, part),
                static key => key.Item1.GetProperty(key.Item2, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new ArgumentException($"Property '{key.Item2}' not found on type '{key.Item1.Name}'"));
            current = Expression.Property(current, prop);
        }
        return current;
    }

    #region Comparison Operators

    private static BinaryExpression BuildEqual(Expression property, object? value, Type propertyType, bool isNullable)
    {
        var converted = NormalizeValue(value, propertyType);
        return Expression.Equal(property, Expression.Constant(converted, propertyType));
    }

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression BuildComparison(Expression property, object? value, Type propertyType, Type underlyingType, bool isNullable, ExpressionType op)
    {
        var converted = NormalizeValue(value, propertyType);
        var constant = Expression.Constant(converted, propertyType);

        if (isNullable && underlyingType.IsValueType)
        {
            var (hvProp, valProp) = GetNullablePropertyInfos(propertyType);
            var hasValue = Expression.Property(property, hvProp);
            var valueAccess = Expression.Property(property, valProp);
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

    private enum StringPredicate { Contains, StartsWith, EndsWith }

    #region String Operators

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression BuildStringPredicate(Expression property, object? value, StringPredicate predicate, bool caseSensitive, bool isNullable)
    {
        var strValue = value?.ToString() ?? "";

        Expression stringProperty = property.Type == typeof(string)
            ? property
            : Expression.Call(property, StringToStringMethod);

        var method = predicate switch
        {
            StringPredicate.Contains => StringContainsMethod,
            StringPredicate.StartsWith => StringStartsWithMethod,
            _ => StringEndsWithMethod,
        };

        Expression callTarget = caseSensitive
            ? stringProperty
            : Expression.Call(stringProperty, StringToLowerMethod);

        var callValue = caseSensitive
            ? Expression.Constant(strValue)
            : Expression.Constant(strValue.ToLowerInvariant());

        Expression call = Expression.Call(callTarget, method, callValue);

        if (isNullable)
        {
            var (hvProp, _) = GetNullablePropertyInfos(property.Type);
            var hasValue = Expression.Property(property, hvProp);
            return Expression.AndAlso(hasValue, call);
        }

        if (property.Type == typeof(string))
        {
            return Expression.AndAlso(
                Expression.NotEqual(property, Expression.Constant(null, typeof(string))),
                call);
        }

        if (property.Type.IsClass && property.Type != typeof(object))
        {
            return Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, property.Type)), call);
        }

        return call;
    }

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
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
            var (hvProp, _) = GetNullablePropertyInfos(property.Type);
            var hasValue = Expression.Property(property, hvProp);
            return Expression.AndAlso(hasValue, call);
        }

        return call;
    }

    #endregion

    #region Collection Operators

    [RequiresDynamicCode(BuildInRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression BuildIn(Expression property, object? value, Type propertyType, Type underlyingType, bool isNullable, bool negate = false)
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

        if (isNullable && underlyingType.IsValueType)
        {
            var (hvProp, valProp) = GetNullablePropertyInfos(propertyType);
            var hasValue = Expression.Property(property, hvProp);
            var valueAccess = Expression.Property(property, valProp);
            Expression containsExpr = Expression.Call(containsMethod, constantArray, valueAccess);
            if (negate)
                containsExpr = Expression.Not(containsExpr);
            return Expression.AndAlso(hasValue, containsExpr);
        }

        Expression call = Expression.Call(containsMethod, constantArray, property);
        return negate ? Expression.Not(call) : call;
    }

    #endregion

    #region Range Operators

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static BinaryExpression BuildBetween(Expression property, object? valueFrom, object? valueTo, Type propertyType, Type underlyingType, bool isNullable)
    {
        var from = NormalizeValue(valueFrom, propertyType);
        var to = NormalizeValue(valueTo, propertyType);

        if (isNullable && underlyingType.IsValueType)
        {
            var (hvProp, valProp) = GetNullablePropertyInfos(propertyType);
            var hasValue = Expression.Property(property, hvProp);
            var valueAccess = Expression.Property(property, valProp);
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

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression BuildIsNull(Expression property, bool isNullable)
    {
        if (isNullable)
        {
            var (hvProp, _) = GetNullablePropertyInfos(property.Type);
            var hasValue = Expression.Property(property, hvProp);
            return Expression.Not(hasValue);
        }

        if (property.Type.IsValueType)
            return Expression.Constant(false);

        return Expression.Equal(property, Expression.Constant(null, property.Type));
    }

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static BinaryExpression BuildIsEmpty(Expression property, bool isNullable)
    {
        if (isNullable)
        {
            var (hvProp, valProp) = GetNullablePropertyInfos(property.Type);
            var hasValue = Expression.Property(property, hvProp);
            var valueAccess = Expression.Property(property, valProp);
            return Expression.OrElse(
                Expression.Not(hasValue),
                Expression.Equal(valueAccess, Expression.Constant(string.Empty, property.Type)));
        }

        return Expression.Equal(property, Expression.Constant(string.Empty, property.Type));
    }

    #endregion

    #region Temporal Operators

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static BinaryExpression BuildDateInRange(Expression property, DateTime from, DateTime to, bool isNullable)
    {
        var fromExpr = Expression.Constant(from, typeof(DateTime));
        var toExpr = Expression.Constant(to, typeof(DateTime));

        if (isNullable)
        {
            var (hvProp, valProp) = GetNullablePropertyInfos(property.Type);
            var hasValue = Expression.Property(property, hvProp);
            var valueAccess = Expression.Property(property, valProp);
            var rangeCheck = Expression.AndAlso(
                Expression.GreaterThanOrEqual(valueAccess, fromExpr),
                Expression.LessThanOrEqual(valueAccess, toExpr));
            return Expression.AndAlso(hasValue, rangeCheck);
        }

        return Expression.AndAlso(
            Expression.GreaterThanOrEqual(property, fromExpr),
            Expression.LessThanOrEqual(property, toExpr));
    }

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
    private static Expression BuildDateComparison(Expression property, object? value, ExpressionType op, bool isNullable)
    {
        var dateValue = (DateTime?)NormalizeValue(value, typeof(DateTime));
        if (dateValue == null)
            return Expression.Constant(false);

        var constant = Expression.Constant(dateValue.Value, typeof(DateTime));

        if (isNullable)
        {
            var (hvProp, valProp) = GetNullablePropertyInfos(property.Type);
            var hasValue = Expression.Property(property, hvProp);
            var valueAccess = Expression.Property(property, valProp);
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

    [RequiresUnreferencedCode(TranslateRequiresUnreferencedCodeMessage)]
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
            var (hvProp, valProp) = GetNullablePropertyInfos(property.Type);
            var hasValue = Expression.Property(property, hvProp);
            var valueAccess = Expression.Property(property, valProp);
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
                        return Convert.ChangeType(dbl, underlying, System.Globalization.CultureInfo.InvariantCulture);
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

    /// <summary>Helper class for regex operations in EF Core-compatible expressions.</summary>
    public static class RegexHelper
    {
        /// <summary>
        /// Maximum number of compiled <see cref="System.Text.RegularExpressions.Regex"/> instances retained in the
        /// cache. Patterns are typically supplied by clients via filter payloads, so an unbounded cache
        /// is a heap-DoS vector. Generation-based eviction drops the bottom 25% by last-access epoch
        /// when capacity is exceeded, preserving hot patterns and preventing thundering-herd re-compilation.
        /// </summary>
        internal const int RegexCacheCapacity = 512;

        private static readonly GenerationCache<string, System.Text.RegularExpressions.Regex> RegexCache =
            new(RegexCacheCapacity);

        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Clears the compiled-regex cache. Intended for tests and diagnostic scenarios; production
        /// code does not need to call this — the cache self-bounds at <see cref="RegexCacheCapacity"/>
        /// via generation-based eviction.
        /// </summary>
        public static void ClearRegexCache() => RegexCache.Clear();

        /// <summary>
        /// Checks if <paramref name="input"/> matches <paramref name="pattern"/>. Patterns are compiled
        /// once and cached up to <see cref="RegexCacheCapacity"/> distinct patterns; the cache is
        /// bulk-cleared on overflow to prevent unbounded heap growth from hostile or naturally-diverse
        /// pattern streams. Invalid patterns throw <see cref="ArgumentException"/>; a match timeout
        /// of 500ms is enforced to prevent catastrophic backtracking from freezing the host.
        /// </summary>
        /// <param name="input">String to test against the pattern; <see langword="null"/> returns <see langword="false"/>.</param>
        /// <param name="pattern">Regex pattern to compile and match. Must not be empty.</param>
        /// <returns><see langword="true"/> when the pattern matches; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is empty or invalid.</exception>
        /// <exception cref="TimeoutException">Thrown when matching exceeds the 500ms safety budget.</exception>
        public static bool IsMatch(string? input, string pattern)
        {
            if (input == null) return false;
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("Regex pattern must not be empty.", nameof(pattern));

            System.Text.RegularExpressions.Regex regex;
            try
            {
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
