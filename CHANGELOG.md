# Changelog

All notable changes to QuerySpec are documented here. Generated from Conventional Commits by `standard-version`.

The format is based on [Keep a Changelog](https://keepachangelog.com/) and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

* **core:** `FilterOperator.ContainsCaseInsensitive` ships alongside the existing snake-case `Contains_CaseInsensitive`. Both members share the underlying value `52` so binary callers passing the integer continue to work; only the symbolic name has changed.
* **auditing:** `IComplianceExporter.GenerateGdprExportAsync` (with and without `CancellationToken`) ships alongside the existing `GenerateGDPRExportAsync` overloads. Default-interface-method delegation lets either name be called against any 2.x implementation.
* **core:** `GeoCoordinate` — immutable `readonly record struct` with `double` lat/long, validated construction (`[-90, 90]` / `[-180, 180]`, no `NaN`), Haversine `DistanceTo`, ISO 6709 `ToString` / `Parse` / `TryParse` round-trip, and an `explicit` operator from `GeoLocation`. `GeoLocation.ToGeoCoordinate()` is the recommended migration helper. Closes site 3 of [#84](https://github.com/AbongileBoja/queryspec/issues/84).
* **core:** `FilterSpec` — immutable `sealed record` with `init` accessors, `IReadOnlyList<FilterSpec>` children, structural value equality across the entire tree, `Validate()` and `ComputeStableHash()` semantically matching the legacy `AdvancedFilterExpression`, and lossless round-trip via `FilterSpec.FromMutable(AdvancedFilterExpression)` / `spec.ToMutable()`. Closes site 4 of [#84](https://github.com/AbongileBoja/queryspec/issues/84).
* **core:** `CacheResult<T>` (`readonly struct`) and `ICacheStore` — supersede `ICacheProvider.GetAsync<T>` / `SetAsync<T>`, lifting the `where T : class` constraint so value types compose directly. `TryGetAsync<T>` returns `CacheResult<T>` whose `HasValue` flag distinguishes a hit on `default` from a miss without a heap allocation. Closes site 5 of [#84](https://github.com/AbongileBoja/queryspec/issues/84).
* **core:** `MemoryCacheProvider`, `DistributedCacheProvider`, and `MultiLevelCache` now implement both `ICacheProvider` and `ICacheStore`. The legacy interface methods stay functional through 3.x; the value-type-aware `ICacheStore` methods are independent implementations (no delegation) so value-type handling stays clean.
* **efcore:** `QuerySpecExpressionTranslator.ApplyFilter<T>(IQueryable<T>, FilterSpec?)` overload. Internally projects the spec via `ToMutable()` and routes through the existing predicate-builder, so the deprecation window introduces no new code path.
* **di:** `CachingBuilder.UseMemoryCache` / `UseDistributedRedis` / `UseMultiLevel` now register a single provider instance against both `ICacheProvider` (legacy) and `ICacheStore` (new). Consumers may inject either contract through 3.x.
* **docs:** Per-diagnostic reference pages under `docs/diagnostics/`: `QSPEC0001.md`, `QSPEC0002.md`, `QSPEC0003.md`. Each `[Obsolete]` attribute uses these as its `UrlFormat` target so IDE quick-info links resolve directly.
* **samples:** `samples/Migration/{GeoCoordinateMigration,FilterSpecMigration,CacheStoreMigration}` — three console programs showing the old usage (with the matching `#pragma warning disable QSPEC####`) followed by the new equivalent, including value-type caching and structural equality.
* **core:** `QuerySpec.Analyzers` — standalone Roslyn analyzer + code-fix package shipping `QSPEC0001` (`GeoLocation.Latitude`/`.Longitude` → `GeoCoordinate`), `QSPEC0002` (`AdvancedFilterExpression` → `FilterSpec`), and `QSPEC0003` (`ICacheProvider.GetAsync`/`SetAsync` → `ICacheStore.TryGetAsync`/`SetValueAsync`) diagnostics with one-click code fixes and Fix-All-In-Document/Project/Solution support. Targets `netstandard2.0` per Roslyn host requirements; consumed by adding `<PackageReference Include="QuerySpec.Analyzers" />` alongside the runtime packages. The analyzers suppress themselves when the target member already carries `[Obsolete(DiagnosticId = "QSPEC####")]` so consumers see exactly one warning per call site. Closes [#151](https://github.com/AbongileBoja/QuerySpec/issues/151).

### Deprecations

* **core:** `FilterOperator.Contains_CaseInsensitive` is marked `[Obsolete(error: false)]` in favour of `FilterOperator.ContainsCaseInsensitive`. Snake-case spelling will be **removed in 3.0**. Both members share the same numeric value so binary callers are unaffected; switch tables and source references should migrate to the PascalCase name. Refs [#84](https://github.com/AbongileBoja/QuerySpec/issues/84).
* **auditing:** `IComplianceExporter.GenerateGDPRExportAsync(string, string, Stream)` and `IComplianceExporter.GenerateGDPRExportAsync(string, string, Stream, CancellationToken)` are marked `[Obsolete(error: false)]` in favour of `GenerateGdprExportAsync`. The all-caps acronym violates the .NET naming guideline that acronyms three or more characters long are PascalCase. Will be **removed in 3.0**. The new overloads delegate to the obsolete ones via default-interface-method, so existing implementations continue to satisfy the interface unchanged. Refs [#84](https://github.com/AbongileBoja/QuerySpec/issues/84).
* **security:** `IEncryptionProvider.RotateKeyAsync()` and `IEncryptionProvider.RotateKeyAsync(CancellationToken)` are marked `[Obsolete(error: true)]` and will be **removed in 3.0**. Every shipping implementation already throws `NotSupportedException` because the provider does not own the persisted ciphertexts. Implement key rotation at the storage layer instead (Azure Key Vault, AWS KMS, etc.): decrypt with the old provider, re-encrypt with the new provider. Closes [#73](https://github.com/AbongileBoja/QuerySpec/issues/73).
* **di:** Eleven DI-builder methods that have only ever thrown `NotImplementedException` are marked `[Obsolete(error: true)]` and will be **removed in 3.0**. No implementation is planned. Affected: `AuditingBuilder.LogAllQueries`, `AuditingBuilder.TrackChanges`, `AuditingBuilder.EnableEncryption`, `AuditingBuilder.UseDatabase(string)`, `AuditingBuilder.RetentionDays(int)`, `CachingBuilder.EnableCompressionForLarge(int)`, `MonitoringBuilder.EnableOpenTelemetry`, `MonitoringBuilder.EnableHealthChecks`, `PerformanceBuilder.EnableQueryCaching`, `PerformanceBuilder.OptimizeExpressions`, `SecurityBuilder.RotateKeysEvery(int)`. Each obsolete message names the recommended replacement (composing your own `IAuditLogger`, calling `Services.AddOpenTelemetry()` / `Services.AddHealthChecks()` directly on the builder's `Services` property, decorating `ICacheProvider`, or implementing key rotation at the storage layer). Closes [#77](https://github.com/AbongileBoja/QuerySpec/issues/77); 3.0 removal tracked in [#139](https://github.com/AbongileBoja/QuerySpec/issues/139).

### Deprecations (3.x → 4.0)

| Diagnostic ID | Member                                                                           | Replacement                                                              | Severity    |
| ------------- | -------------------------------------------------------------------------------- | ------------------------------------------------------------------------ | ----------- |
| `QSPEC0001`   | `GeoLocation.Latitude` / `.Longitude` (decimal)                                  | `GeoCoordinate` (double, immutable, validated)                           | warning     |
| `QSPEC0002`   | `AdvancedFilterExpression` (mutable POCO)                                        | `FilterSpec` (record with `init` accessors)                              | warning     |
| `QSPEC0003`   | `ICacheProvider.GetAsync<T>` / `SetAsync<T>` (`where T : class`)                 | `ICacheStore.TryGetAsync<T>` / `SetValueAsync<T>` + `CacheResult<T>`     | warning     |

Each `[Obsolete]` attribute carries a `DiagnosticId` and a `UrlFormat` pointing at `docs/diagnostics/QSPEC####.md`, the canonical Microsoft pattern (mirrors `SYSLIB####`). Removal is scheduled for 4.0; tracking issue links below.

### Migration guide

#### Site 3 — `GeoLocation` → `GeoCoordinate` (QSPEC0001)

```csharp
// Before
var loc = new GeoLocation(40.7128m, -74.0060m);
var d = loc.DistanceTo(other);

// After
var loc = new GeoCoordinate(40.7128, -74.0060);
var d = loc.DistanceTo(other);

// Migration helper - allocation-free.
GeoCoordinate migrated = legacyGeoLocation.ToGeoCoordinate();
```

#### Site 4 — `AdvancedFilterExpression` → `FilterSpec` (QSPEC0002)

```csharp
// Before
var f = new AdvancedFilterExpression { Field = "Status", Operator = FilterOperator.Equal, Value = "Active" };

// After
var f = new FilterSpec { Field = "Status", Operator = FilterOperator.Equal, Value = "Active" };
var query = QuerySpecExpressionTranslator.ApplyFilter(source, f);

// Round-trip.
FilterSpec spec = FilterSpec.FromMutable(legacy);
AdvancedFilterExpression legacy = spec.ToMutable();
```

#### Site 5 — `ICacheProvider` → `ICacheStore` (QSPEC0003)

```csharp
// Before
await cache.SetAsync("key", value);
var v = await cache.GetAsync<MyDto>("key"); // null on miss; reference types only

// After
await store.SetValueAsync("key", value);              // value types work directly
var hit = await store.TryGetAsync<int>("key");
if (hit.HasValue) Console.WriteLine(hit.Value);       // unambiguous miss-vs-default
```

A Roslyn analyzer + code-fix package shipping these migrations as one-keystroke fixes is tracked as a follow-up.

#### Issue #84 site map

| Site | Old (3.x) | New (3.x additive) | 4.0 final |
|---|---|---|---|
| 1 | `FilterOperator.Contains_CaseInsensitive` (obsolete in 2.x) | `FilterOperator.ContainsCaseInsensitive` (shipped) | snake-case removed |
| 2 | `IComplianceExporter.GenerateGDPRExportAsync(...)` (obsolete in 2.x) | `IComplianceExporter.GenerateGdprExportAsync(...)` (shipped) | `GDPR` overloads removed |
| 3 | `GeoLocation.Latitude` / `.Longitude` — `[Obsolete]` QSPEC0001 | `GeoCoordinate` (shipped this release) | `GeoLocation` removed in 4.0 |
| 4 | `AdvancedFilterExpression` — `[Obsolete]` QSPEC0002 | `FilterSpec` (shipped this release) | `AdvancedFilterExpression` removed in 4.0 |
| 5 | `ICacheProvider.GetAsync<T>` / `SetAsync<T>` — `[Obsolete]` QSPEC0003 | `ICacheStore` + `CacheResult<T>` (shipped this release) | constraints removed, contract reshaped in 4.0 |

### [3.0.1-rc1](https://github.com/AbongileBoja/QuerySpec/compare/v3.0.0...v3.0.1-rc1) (2026-04-26)


### Features

* **release:** add per-package NuGet icons ([e96a769](https://github.com/AbongileBoja/QuerySpec/commit/e96a7697aba8219fe60f3565a7399a82c58c0dff)), closes [#49](https://github.com/AbongileBoja/QuerySpec/issues/49)


### Build System

* **release:** bump PackageValidation baseline to 3.0.0 ([80d1528](https://github.com/AbongileBoja/QuerySpec/commit/80d1528ba58436b4ebe43e61452f7c99313752cf)), closes [#72](https://github.com/AbongileBoja/QuerySpec/issues/72)
* **release:** trim PackageTags and lead with queryspec brand ([133be12](https://github.com/AbongileBoja/QuerySpec/commit/133be12aa072d227d7a5e7db058c568a22fd1d43)), closes [#51](https://github.com/AbongileBoja/QuerySpec/issues/51)


### Documentation

* **release:** rewrite v2.0.0 and v3.0.0 BREAKING CHANGES as prose ([#120](https://github.com/AbongileBoja/QuerySpec/issues/120)) ([f469e22](https://github.com/AbongileBoja/QuerySpec/commit/f469e22f117df55f6a00b6306bb7197370a6da30)), closes [#61](https://github.com/AbongileBoja/QuerySpec/issues/61)


### CI

* **release:** benchmark smoke gate before pack ([10346d3](https://github.com/AbongileBoja/QuerySpec/commit/10346d3a2c5931e752d2fff7df7e7d2d496e9034)), closes [#57](https://github.com/AbongileBoja/QuerySpec/issues/57)
* **release:** publish to NuGet.org via OIDC trusted publishing ([802eafc](https://github.com/AbongileBoja/QuerySpec/commit/802eafc1b165e80c35eec5aba0e1d155319a30e3)), closes [#59](https://github.com/AbongileBoja/QuerySpec/issues/59)
* **release:** validate package contents and metadata before publish ([#121](https://github.com/AbongileBoja/QuerySpec/issues/121)) ([067df9b](https://github.com/AbongileBoja/QuerySpec/commit/067df9b09c9ed309aa3b9de94fb198e88b2c69ac)), closes [#64](https://github.com/AbongileBoja/QuerySpec/issues/64)

## [3.0.0](https://github.com/AbongileBoja/QuerySpec/compare/v2.0.1...v3.0.0) (2026-04-26)


### ⚠ BREAKING CHANGES

* **core:** `ICacheProvider` async members return `ValueTask` instead of `Task`. Every method on the interface (`GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync`, `FlushAsync`, `GetStatsAsync`) is affected. Callers that simply `await` continue to work unchanged; code that captures the returned task explicitly must call `.AsTask()` to convert. Custom `ICacheProvider` implementations must update their return types. ([#115](https://github.com/AbongileBoja/QuerySpec/pull/115))
* **core:** `AssemblyVersion` bumped from `2.x.x.x` to `3.0.0.0`. Update any strong-name binding redirects or `[assembly: AssemblyVersion]`-pinned references.

### Features

* **core:** CancellationToken-accepting overloads across the async public surface ([#107](https://github.com/AbongileBoja/QuerySpec/issues/107)) ([6d88a17](https://github.com/AbongileBoja/QuerySpec/commit/6d88a1756890888db8285deefb052e63289a73d1)), closes [#68](https://github.com/AbongileBoja/QuerySpec/issues/68) [#47](https://github.com/AbongileBoja/QuerySpec/issues/47)
* **core:** ICacheProvider returns ValueTask, AssemblyVersion -> 3.0.0.0 ([#115](https://github.com/AbongileBoja/QuerySpec/issues/115)) ([fb28a5b](https://github.com/AbongileBoja/QuerySpec/commit/fb28a5b3f9f1c511e62c85823dddb91657003993)), closes [#4](https://github.com/AbongileBoja/QuerySpec/issues/4) [#90](https://github.com/AbongileBoja/QuerySpec/issues/90)
* **di:** expose Services getter on every QuerySpec DI builder ([#106](https://github.com/AbongileBoja/QuerySpec/issues/106)) ([857a10b](https://github.com/AbongileBoja/QuerySpec/commit/857a10bf3c4d5960abcda352be20c6e8c1aae653)), closes [#69](https://github.com/AbongileBoja/QuerySpec/issues/69)


### Bug Fixes

* **auditing:** InMemoryAuditLogger implements IDisposable to release ReaderWriterLockSlim ([#102](https://github.com/AbongileBoja/QuerySpec/issues/102)) ([3457e1e](https://github.com/AbongileBoja/QuerySpec/commit/3457e1ee4664d90b8adfdd1967bf91863306d1df)), closes [#100](https://github.com/AbongileBoja/QuerySpec/issues/100) [#52](https://github.com/AbongileBoja/QuerySpec/issues/52)
* **di:** WithAuditing uses TryAddSingleton so caller-supplied IAuditLogger wins ([#100](https://github.com/AbongileBoja/QuerySpec/issues/100)) ([3cad22f](https://github.com/AbongileBoja/QuerySpec/commit/3cad22fc6f973e91bf8de7aa303a7d080d92f512)), closes [#60](https://github.com/AbongileBoja/QuerySpec/issues/60)
* **release:** set PackageValidationBaselineVersion=2.0.1 with AssemblyVersion pinning ([#105](https://github.com/AbongileBoja/QuerySpec/issues/105)) ([2efe5e9](https://github.com/AbongileBoja/QuerySpec/commit/2efe5e9c38d1d9d0cb1e7b4d55b2870431deebbd)), closes [#102](https://github.com/AbongileBoja/QuerySpec/issues/102) [#104](https://github.com/AbongileBoja/QuerySpec/issues/104) [#70](https://github.com/AbongileBoja/QuerySpec/issues/70) [#55](https://github.com/AbongileBoja/QuerySpec/issues/55)
* **resilience:** add ConfigureAwait(false) to library awaits in resilience and compliance paths ([#101](https://github.com/AbongileBoja/QuerySpec/issues/101)) ([f59d7e5](https://github.com/AbongileBoja/QuerySpec/commit/f59d7e504e49bcb9373d960c343433e9c8203a45)), closes [#46](https://github.com/AbongileBoja/QuerySpec/issues/46)
* **resilience:** BulkheadPolicy implements IDisposable to release SemaphoreSlim ([#104](https://github.com/AbongileBoja/QuerySpec/issues/104)) ([1b7c6f7](https://github.com/AbongileBoja/QuerySpec/commit/1b7c6f7d4d4c028e872ed4526810e280911e9bd6)), closes [#102](https://github.com/AbongileBoja/QuerySpec/issues/102) [#103](https://github.com/AbongileBoja/QuerySpec/issues/103)


### Performance

* **efcore:** cache Enumerable.Contains open generic + closed instantiations in BuildIn ([#108](https://github.com/AbongileBoja/QuerySpec/issues/108)) ([beae14b](https://github.com/AbongileBoja/QuerySpec/commit/beae14b82e283d4a6e998a7308ca038870007659)), closes [#86](https://github.com/AbongileBoja/QuerySpec/issues/86)
* **monitoring:** bound stack walk in N1DetectionEngine.RecordQuery ([#109](https://github.com/AbongileBoja/QuerySpec/issues/109)) ([c842dc0](https://github.com/AbongileBoja/QuerySpec/commit/c842dc0be1c64a82af0f38ca63a91578816f671e)), closes [#88](https://github.com/AbongileBoja/QuerySpec/issues/88)


### Tests

* **efcore:** add SQLite-backed translator fixture and document StringHelper translation gap ([#111](https://github.com/AbongileBoja/QuerySpec/issues/111)) ([8bb2705](https://github.com/AbongileBoja/QuerySpec/commit/8bb2705e167483284564cc6b9105acc12948555e)), closes [#71](https://github.com/AbongileBoja/QuerySpec/issues/71) [#71](https://github.com/AbongileBoja/QuerySpec/issues/71)
* **efcore:** cover translator branches at 0% from EFCore tests ([#110](https://github.com/AbongileBoja/QuerySpec/issues/110)) ([0e9d9f0](https://github.com/AbongileBoja/QuerySpec/commit/0e9d9f0aef2147db448be7c80d9b1db86192c5af)), closes [#69](https://github.com/AbongileBoja/QuerySpec/issues/69) [#78](https://github.com/AbongileBoja/QuerySpec/issues/78) [#69](https://github.com/AbongileBoja/QuerySpec/issues/69)
* **resilience:** synchronise BulkheadPolicy tests via ManualResetEventSlim ([#114](https://github.com/AbongileBoja/QuerySpec/issues/114)) ([6527e32](https://github.com/AbongileBoja/QuerySpec/commit/6527e32b5ebe90c689857aed0f7f931e2f6b7693)), closes [#74](https://github.com/AbongileBoja/QuerySpec/issues/74) [#112](https://github.com/AbongileBoja/QuerySpec/issues/112) [#107](https://github.com/AbongileBoja/QuerySpec/issues/107)

### [2.0.1](https://github.com/AbongileBoja/QuerySpec/compare/v2.0.0...v2.0.1) (2026-04-26)


### Bug Fixes

* **auditing:** materialise read snapshots in InMemoryAuditLogger under the read lock ([#96](https://github.com/AbongileBoja/QuerySpec/issues/96)) ([8219192](https://github.com/AbongileBoja/QuerySpec/commit/82191923cbc9813331fc4f9513eb09f0dae841f9)), closes [#54](https://github.com/AbongileBoja/QuerySpec/issues/54)
* **auditing:** refuse partial PurgeOldLogsAsync to preserve audit-chain integrity ([#94](https://github.com/AbongileBoja/QuerySpec/issues/94)) ([e72fd22](https://github.com/AbongileBoja/QuerySpec/commit/e72fd22668a65757a9d89353c2877ebf596122f9)), closes [#2](https://github.com/AbongileBoja/QuerySpec/issues/2)
* **ci:** pin reproducibility job actions to SHAs ([#98](https://github.com/AbongileBoja/QuerySpec/issues/98)) ([050ccc4](https://github.com/AbongileBoja/QuerySpec/commit/050ccc469f0e7d40b191ddb25732a752cd580033)), closes [#6](https://github.com/AbongileBoja/QuerySpec/issues/6)
* **monitoring:** bound MetricsCollector, return zero-report on empty, aggregate outside lock ([#97](https://github.com/AbongileBoja/QuerySpec/issues/97)) ([59385c5](https://github.com/AbongileBoja/QuerySpec/commit/59385c56cb93d39c11c8377dc70015b14400c4ba)), closes [#50](https://github.com/AbongileBoja/QuerySpec/issues/50) [#81](https://github.com/AbongileBoja/QuerySpec/issues/81) [#89](https://github.com/AbongileBoja/QuerySpec/issues/89)
* **release:** replace stale Directory.Build.props version with 0.0.0-local sentinel ([c67b7e3](https://github.com/AbongileBoja/QuerySpec/commit/c67b7e37432ad12e35c64d73218742de534a66ff))
* **security:** bound RegexHelper.RegexCache to prevent heap-DoS over user patterns ([#95](https://github.com/AbongileBoja/QuerySpec/issues/95)) ([d0e18ef](https://github.com/AbongileBoja/QuerySpec/commit/d0e18efc6438919c8cefe8ea5f580cdd43f81e07)), closes [#3](https://github.com/AbongileBoja/QuerySpec/issues/3)
* **security:** default RLSPolicy.FilterGenerator to DenyAll instead of AllowAll ([#93](https://github.com/AbongileBoja/QuerySpec/issues/93)) ([710b37b](https://github.com/AbongileBoja/QuerySpec/commit/710b37bccd9652666b36b6069db80ed64369cedf)), closes [#56](https://github.com/AbongileBoja/QuerySpec/issues/56)
* **security:** make AdvancedFilterExpression.MaskResult/EncryptValue fail Validate when true ([#92](https://github.com/AbongileBoja/QuerySpec/issues/92)) ([7ba4834](https://github.com/AbongileBoja/QuerySpec/commit/7ba4834fb1bbb32aa405c4be5e6ab1ae8d71b453)), closes [#1](https://github.com/AbongileBoja/QuerySpec/issues/1)
* **security:** promote IsPii(string, object?) Obsolete from warning to error ([#99](https://github.com/AbongileBoja/QuerySpec/issues/99)) ([29c06ca](https://github.com/AbongileBoja/QuerySpec/commit/29c06ca6359068977ec1effb806e0b0b0105c6a5)), closes [#73](https://github.com/AbongileBoja/QuerySpec/issues/73)

## [2.0.0](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.8...v2.0.0) (2026-04-25)


### ⚠ BREAKING CHANGES

* **security:** nested public types in `QuerySpec.Core.Security` and `QuerySpec.Core.Resilience` are now top-level. Drop the `HostEngine.` qualifier from usages; existing `using` directives stay valid since the namespace is unchanged.
* **di:** `PluginBuilder`, `QuerySpecBuilder.WithPlugins`, `MonitoringBuilder.EnableDashboard`, and `MonitoringBuilder.EnablePrometheus` are removed. Other no-op builder stubs now throw `NotImplementedException` and carry `[Obsolete]`; `ApplyAggregation` throws on non-null aggregation where it previously returned the input unchanged. Remove the calls or replace with concrete implementations.
* **security:** `AesEncryptionProvider.RotateKeyAsync` now throws `NotSupportedException`; the class is marked `[Obsolete]`. New ciphertexts should use `AesGcmEncryptionProvider`. Existing ciphertexts encrypted under `AesEncryptionProvider` must be re-encrypted under the new provider — there is no automatic upgrade path.
* **auditing:** `AuditLogEntry` properties (other than `Id` and `ErrorMessage`) are now `init`-only; `Hash` and `PreviousHash` setters are `private`. `ComputeHash()` is `[Obsolete]`. Move post-construction mutations into the object initializer or build a fresh entry, and call `Seal(previousHash)` (owned by `IAuditLogger`) instead of `ComputeHash()`.
* **security:** `HashMask` now requires a hash key at construction or registration. Output length is no longer 8 characters, and old SHA-256-truncated values are not reversibly migratable. Pass a hash key to the constructor, re-mask persisted values, and rotate any join keys built on the old algorithm.
* **security:** the strong-name public key has been rotated. Update any `[InternalsVisibleTo]` pins, binding redirects, or GAC-resolved references to the new token.
* **security:** `RowLevelSecurityEngine` fails closed by default — a missing policy denies access instead of permitting it. Either pass `RLSDefaultBehavior.AllowAll` to the engine constructor or call `RegisterUnrestricted<T>(resourceType)` per resource that should bypass RLS.

### Features

* **auditing:** link audit log hash chain and seal entries ([2d34e69](https://github.com/AbongileBoja/QuerySpec/commit/2d34e692b894181077619cedf02273f15be1c47e))
* **security:** add aes-gcm authenticated encryption provider ([59e2f46](https://github.com/AbongileBoja/QuerySpec/commit/59e2f46b6766de101a930c5e3cd64110d8436c7d)), closes [#27](https://github.com/AbongileBoja/QuerySpec/issues/27) [#27](https://github.com/AbongileBoja/QuerySpec/issues/27)
* **security:** add MigratingEncryptionProvider with prefix-tag dispatch ([#37](https://github.com/AbongileBoja/QuerySpec/issues/37)) ([deab402](https://github.com/AbongileBoja/QuerySpec/commit/deab402e540694fa9b258f0474ada90bd1360109)), closes [#27](https://github.com/AbongileBoja/QuerySpec/issues/27)
* **security:** keyed hash masking with per-tenant separation ([38a0d05](https://github.com/AbongileBoja/QuerySpec/commit/38a0d05203bdc0438f8b832273c1d584b98ae238)), closes [#24](https://github.com/AbongileBoja/QuerySpec/issues/24)
* **security:** row-level security engine fails closed by default ([d6a0c84](https://github.com/AbongileBoja/QuerySpec/commit/d6a0c844b6a9ff8244acb60a1b374ee395c7adc6)), closes [#10](https://github.com/AbongileBoja/QuerySpec/issues/10)
* **security:** typed SetPredicate<T> helper on RLSPolicy ([f23a353](https://github.com/AbongileBoja/QuerySpec/commit/f23a353bd9f60669a0232934d1f4a9d802980f3c))


### Bug Fixes

* **ci:** default to delay-sign so CI builds without the private key succeed ([5001c96](https://github.com/AbongileBoja/QuerySpec/commit/5001c96bcf76e8b4abe4a0ea11dde2bc89b65277))
* **ci:** mark xunit.v3 test projects as OutputType=Exe so CodeQL build doesn't trip ([#44](https://github.com/AbongileBoja/QuerySpec/issues/44)) ([1fbc653](https://github.com/AbongileBoja/QuerySpec/commit/1fbc653f1583bdc223668b59029fbec2edf5c0ae))
* **ci:** per-run trx filenames so parallel test runners don't race on a shared file ([#45](https://github.com/AbongileBoja/QuerySpec/issues/45)) ([dec2e4f](https://github.com/AbongileBoja/QuerySpec/commit/dec2e4fc926b1a274d6fb8ea9d496fff44e02620)), closes [#44](https://github.com/AbongileBoja/QuerySpec/issues/44)
* **di:** null-check IServiceCollection in builder constructors ([7b4f7b6](https://github.com/AbongileBoja/QuerySpec/commit/7b4f7b6a02a9bcc23ac2d850e8fd35565cba7b68)), closes [#29](https://github.com/AbongileBoja/QuerySpec/issues/29)
* **resilience:** add standard exception constructors (CA1032) ([cc402ec](https://github.com/AbongileBoja/QuerySpec/commit/cc402ec9b140396bc26f6a7f46bbc6000004260d))
* **security:** explicit IPiiClassifier replaces name-heuristic for PII decisions ([#36](https://github.com/AbongileBoja/QuerySpec/issues/36)) ([7fb0a5b](https://github.com/AbongileBoja/QuerySpec/commit/7fb0a5b33999431f7a5461fd62a88ce611557ced)), closes [#24](https://github.com/AbongileBoja/QuerySpec/issues/24)
* **security:** guard dynamic permission evaluator against null and empty subject ([986a84c](https://github.com/AbongileBoja/QuerySpec/commit/986a84c7a16efb8307c8a41b6366f4958f95d49a))
* **security:** seal DataMaskingEngine ([#43](https://github.com/AbongileBoja/QuerySpec/issues/43)) ([fb98538](https://github.com/AbongileBoja/QuerySpec/commit/fb98538601615799bed9185d2870f659553c02b9)), closes [#38](https://github.com/AbongileBoja/QuerySpec/issues/38)


* **security:** rotate strong-name keypair after public exposure ([b68305f](https://github.com/AbongileBoja/QuerySpec/commit/b68305f5371edb2fb05513e0cee5c69b76865329)), closes [#8](https://github.com/AbongileBoja/QuerySpec/issues/8) [#9](https://github.com/AbongileBoja/QuerySpec/issues/9) [#18](https://github.com/AbongileBoja/QuerySpec/issues/18)


### CI

* **ci:** pin actions to commit SHAs and lock workflow permissions ([efa25be](https://github.com/AbongileBoja/QuerySpec/commit/efa25be95e7616678bdeedbadd509d7b1ba1bc3a))
* **release:** gate release on tag reachable from main or develop ([9771531](https://github.com/AbongileBoja/QuerySpec/commit/97715311b28d058a96423fd21d80089478a4bba4))


### Refactoring

* **di:** triage every no-op builder stub ([c688627](https://github.com/AbongileBoja/QuerySpec/commit/c6886273ee9e73438acd67f5c1286d6f2b048d27))
* **security:** extract nested public types to top-level ([3b29d59](https://github.com/AbongileBoja/QuerySpec/commit/3b29d597a051e1c6f8ced9c13fd989872a2dac89)), closes [#7](https://github.com/AbongileBoja/QuerySpec/issues/7)

### [1.0.8](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.7...v1.0.8) (2026-04-25)


### Bug Fixes

* **ci:** fix devcontainer permission errors on dotnet first-run sentinel ([2ebf8a1](https://github.com/AbongileBoja/QuerySpec/commit/2ebf8a12b7f76b61fabb237fcec7be6699460e68))

### [1.0.7](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.6...v1.0.7) (2026-04-25)


### Build System

* add reproducible devcontainer with multi-tfm dotnet, node, and tooling ([275ab96](https://github.com/AbongileBoja/QuerySpec/commit/275ab9600de1331da4a8cae652fc806bcf4facf1))

### [1.0.6](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.5...v1.0.6) (2026-04-25)


### CI

* mirror packages to github packages and strip workflow comments ([9358836](https://github.com/AbongileBoja/QuerySpec/commit/9358836872d35ae5affa9d15539447a77f4dd7df))

### [1.0.5](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.4...v1.0.5) (2026-04-24)


### CI

* add nuget release pipeline triggered on version tags ([f012887](https://github.com/AbongileBoja/QuerySpec/commit/f012887bf6d858823f503f42be1485baa764ad6e))

### [1.0.4](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.3...v1.0.4) (2026-04-24)


### Bug Fixes

* **ci:** collect coverage from Core.Tests and fix summary parser regex ([a2da606](https://github.com/AbongileBoja/QuerySpec/commit/a2da606b7bf0898aa690913e11f78b902d8ddb9f))

### [1.0.3](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.2...v1.0.3) (2026-04-24)

### [1.0.2](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.1...v1.0.2) (2026-04-24)


### Bug Fixes

* **ci:** use --severity warn (dropped 'warning' in dotnet format 10.x) ([3f20d5c](https://github.com/AbongileBoja/QuerySpec/commit/3f20d5cff54d8c7496840228f75d3a14a16e1ab0))

### [1.0.1](https://github.com/AbongileBoja/QuerySpec/compare/v1.0.0...v1.0.1) (2026-04-24)


### Bug Fixes

* **deps:** downgrade [@commitlint](https://github.com/commitlint) to 19 for standard-version compatibility ([29ca743](https://github.com/AbongileBoja/QuerySpec/commit/29ca7439ea3094156121881023ffb1864c62c729))


### Documentation

* add sample projects ([d80f143](https://github.com/AbongileBoja/QuerySpec/commit/d80f143d49ef4aca2691bf03e4eea7796853da23))

## 1.0.0 (2026-04-24)


### Build System

* **release:** add standard-version for changelog and versioning ([3d847f1](https://github.com/AbongileBoja/QuerySpec/commit/3d847f16ac015f56588cfd53f3d78899b47cf36d))
