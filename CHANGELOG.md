# Changelog

All notable changes to QuerySpec are documented here. Generated from Conventional Commits by `standard-version`.

The format is based on [Keep a Changelog](https://keepachangelog.com/) and this project adheres to [Semantic Versioning](https://semver.org/).

## [3.0.0](https://github.com/AbongileBoja/QuerySpec/compare/v2.0.1...v3.0.0) (2026-04-26)


### ⚠ BREAKING CHANGES

* **core:** ICacheProvider returns ValueTask, AssemblyVersion -> 3.0.0.0 (#115)

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

* **security:** `!` marker for the changelog.

329/329 Core tests pass on net8/net9/net10.
* **security:** every type listed above moved from
`HostEngine.NestedType` to top-level `NestedType` in the same
namespace. Consumers must remove the `HostEngine.` qualifier — the
namespace stays the same, so a `using QuerySpec.Core.Security;` (or
`QuerySpec.Core.Resilience;`) keeps everything in scope.
* **di:** PluginBuilder is removed. QuerySpecBuilder.WithPlugins
is removed. MonitoringBuilder.EnableDashboard and
MonitoringBuilder.EnablePrometheus are removed. Every other no-op
listed above now throws NotImplementedException when called and
carries [Obsolete]. ApplyAggregation throws on non-null aggregation
where it previously returned the input unchanged.
* **security:** AesEncryptionProvider.RotateKeyAsync now throws
NotSupportedException instead of returning a no-op success. The
class is also marked [Obsolete] — callers should migrate to
AesGcmEncryptionProvider for new ciphertexts. Existing ciphertexts
encrypted under AesEncryptionProvider must be re-encrypted with the
* **auditing:** AuditLogEntry properties (other than Id and
ErrorMessage) are now init-only. Hash and PreviousHash setters are
private. Code that mutated entry properties after creation must move
the mutation into the object initializer or build a fresh entry.
ComputeHash() is obsolete; callers should use Seal(previousHash)
which an IAuditLogger implementation owns.
* **security:** callers using HashMask must now supply a hash key
to the constructor or registration will throw. Existing 8-character
mask outputs cannot be migrated automatically — old SHA-256-truncated
values are not reversible. Document the change in the rotation
release notes; consumers persisting masked values should re-mask
new outputs and rotate any join keys built on the old algorithm.

Adds 14 new tests covering: short-key rejection, missing-key
registration failure, full output length, key-determinism,
cross-key separation, cross-tenant separation, same-tenant
determinism, null and empty tenantId equivalence, and key-clone
isolation. Final count: 224 Core tests on net8/9/10 (was 210).
* **security:** the strong-name public key token has changed. Any
consumer that pinned [InternalsVisibleTo] on the old token, used
binding redirects against it, or resolved QuerySpec assemblies via the
GAC must update their references against the new token.
* **security:** code that relied on the implicit fail-open default
(missing policy returning AllowAll) must now either pass
RLSDefaultBehavior.AllowAll to the constructor or call
RegisterUnrestricted<T>(resourceType) per resource that should
bypass RLS.

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
