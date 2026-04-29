# Changelog

All notable changes to QuerySpec are documented here. Generated from Conventional Commits by `standard-version`.

The format is based on [Keep a Changelog](https://keepachangelog.com/) and this project adheres to [Semantic Versioning](https://semver.org/).

### [4.1.2](https://github.com/AbongileBoja/QuerySpec/compare/v4.1.1...v4.1.2) (2026-04-29)


### Bug Fixes

* **release:** test SourceLink against .snupkg when symbols are external ([#250](https://github.com/AbongileBoja/QuerySpec/issues/250)) ([8a348a7](https://github.com/AbongileBoja/QuerySpec/commit/8a348a7a9db3c42a0d27215a190c7ea723cdcf4e))
* **release:** walk back postbump baseline through unpublished tags ([#252](https://github.com/AbongileBoja/QuerySpec/issues/252)) ([0dd3d32](https://github.com/AbongileBoja/QuerySpec/commit/0dd3d32df0bc6f2e49651e6310cddf5408965c08)), closes [#251](https://github.com/AbongileBoja/QuerySpec/issues/251)

### [4.1.1](https://github.com/AbongileBoja/QuerySpec/compare/v4.1.0...v4.1.1) (2026-04-29)


### Bug Fixes

* **release:** generate SBOM for QuerySpec.Analyzers package ([#249](https://github.com/AbongileBoja/QuerySpec/issues/249)) ([17a54f2](https://github.com/AbongileBoja/QuerySpec/commit/17a54f2331895b97d13f54e65f6011396c1f9cb9)), closes [#153](https://github.com/AbongileBoja/QuerySpec/issues/153) [#248](https://github.com/AbongileBoja/QuerySpec/issues/248)

## [4.1.0](https://github.com/AbongileBoja/QuerySpec/compare/v4.0.0...v4.1.0) (2026-04-29)


### Features

* **ci:** fail CI on CodeQL high-severity alerts ([#244](https://github.com/AbongileBoja/QuerySpec/issues/244)) ([b50d2f3](https://github.com/AbongileBoja/QuerySpec/commit/b50d2f3bd34357745dfb4981fb963daf55c44a8f)), closes [#200](https://github.com/AbongileBoja/QuerySpec/issues/200)
* **ci:** ratchet coverage gate with line+branch enforcement ([77f8120](https://github.com/AbongileBoja/QuerySpec/commit/77f81208c5bf0f6d464fd93594cd403e34a65deb)), closes [#177](https://github.com/AbongileBoja/QuerySpec/issues/177)
* **core:** declare IsTrimmable/IsAotCompatible and annotate reflection-using surface ([#243](https://github.com/AbongileBoja/QuerySpec/issues/243)) ([560493d](https://github.com/AbongileBoja/QuerySpec/commit/560493d061e1a59e6d2828129c612f6e649e0bb6)), closes [#170](https://github.com/AbongileBoja/QuerySpec/issues/170)
* **release:** enable strict ApiCompat baseline validation and prune unused suppressions ([#229](https://github.com/AbongileBoja/QuerySpec/issues/229)) ([e4b453c](https://github.com/AbongileBoja/QuerySpec/commit/e4b453cd5bd86f98e7c811e56b0a3e6662f61a9a)), closes [#167](https://github.com/AbongileBoja/QuerySpec/issues/167)
* **release:** polish NuGet package metadata to first-party standard ([#206](https://github.com/AbongileBoja/QuerySpec/issues/206)) ([5cd8bbb](https://github.com/AbongileBoja/QuerySpec/commit/5cd8bbb7d2227d2cd0f55fb79d8f1de01b7e6d55)), closes [#186](https://github.com/AbongileBoja/QuerySpec/issues/186) [#205](https://github.com/AbongileBoja/QuerySpec/issues/205)
* **release:** verify SBOM in nupkg and broaden reproducibility to all shipped assemblies ([ea14962](https://github.com/AbongileBoja/QuerySpec/commit/ea14962c0762d031246f107ba2b7bbf1749bd21e)), closes [#187](https://github.com/AbongileBoja/QuerySpec/issues/187)
* **security:** add gitleaks secret-scanning workflow ([#220](https://github.com/AbongileBoja/QuerySpec/issues/220)) ([db8e876](https://github.com/AbongileBoja/QuerySpec/commit/db8e876c6a6e4853b511a5fbfd1489cb6ad5c614)), closes [#165](https://github.com/AbongileBoja/QuerySpec/issues/165)
* **security:** add step-security/harden-runner in audit mode ([#215](https://github.com/AbongileBoja/QuerySpec/issues/215)) ([35c96a6](https://github.com/AbongileBoja/QuerySpec/commit/35c96a6ee94ce1bb89616b874e006e551bc23800)), closes [#164](https://github.com/AbongileBoja/QuerySpec/issues/164)
* **security:** emit SLSA build provenance for released NuGet packages ([#204](https://github.com/AbongileBoja/QuerySpec/issues/204)) ([e9e93ee](https://github.com/AbongileBoja/QuerySpec/commit/e9e93ee4743d1c7a8a5f18a0e608d84df85b83a8)), closes [#163](https://github.com/AbongileBoja/QuerySpec/issues/163)
* **tests:** add public-API approval baselines via PublicApiGenerator + Verify ([#245](https://github.com/AbongileBoja/QuerySpec/issues/245)) ([454fa23](https://github.com/AbongileBoja/QuerySpec/commit/454fa23db832e09a7be195ad9384a1d154cc4260)), closes [#182](https://github.com/AbongileBoja/QuerySpec/issues/182)
* **tests:** add SQL Server and PostgreSQL provider matrix via Testcontainers ([#247](https://github.com/AbongileBoja/QuerySpec/issues/247)) ([bc93698](https://github.com/AbongileBoja/QuerySpec/commit/bc93698884941a029b543580a5b8a0e91edf1aad)), closes [#178](https://github.com/AbongileBoja/QuerySpec/issues/178)


### Bug Fixes

* **benchmarks:** guard against vacuous gate pass and exclude flaky SimpleEqual ([7211698](https://github.com/AbongileBoja/QuerySpec/commit/7211698493cf0670bb8046de7438297f35b0da1e)), closes [#179](https://github.com/AbongileBoja/QuerySpec/issues/179) [#179](https://github.com/AbongileBoja/QuerySpec/issues/179) [#198](https://github.com/AbongileBoja/QuerySpec/issues/198) [#199](https://github.com/AbongileBoja/QuerySpec/issues/199) [#197](https://github.com/AbongileBoja/QuerySpec/issues/197) [#198](https://github.com/AbongileBoja/QuerySpec/issues/198) [#199](https://github.com/AbongileBoja/QuerySpec/issues/199)
* **ci:** exempt dependabot from body/footer line-length rules ([#241](https://github.com/AbongileBoja/QuerySpec/issues/241)) ([1f10ae1](https://github.com/AbongileBoja/QuerySpec/commit/1f10ae11b4c41f77b39be68e5936feae5d13a50a)), closes [#211](https://github.com/AbongileBoja/QuerySpec/issues/211) [#214](https://github.com/AbongileBoja/QuerySpec/issues/214) [#212](https://github.com/AbongileBoja/QuerySpec/issues/212) [#209](https://github.com/AbongileBoja/QuerySpec/issues/209) [#240](https://github.com/AbongileBoja/QuerySpec/issues/240)
* **ci:** parse real CodeQL alert counts in evidence gate ([90cf713](https://github.com/AbongileBoja/QuerySpec/commit/90cf7132612bb744ccfcbcf5ec983c6ecae75b4e)), closes [#200](https://github.com/AbongileBoja/QuerySpec/issues/200) [#189](https://github.com/AbongileBoja/QuerySpec/issues/189)
* **ci:** remove git push from mutation jobs; reports via artifacts ([#232](https://github.com/AbongileBoja/QuerySpec/issues/232)) ([fd2f23a](https://github.com/AbongileBoja/QuerySpec/commit/fd2f23a285f009ff166eff0f4d060a7040b52026)), closes [#190](https://github.com/AbongileBoja/QuerySpec/issues/190)
* **ci:** switch dependabot to chore(deps) prefix and add deps-dev scope ([#239](https://github.com/AbongileBoja/QuerySpec/issues/239)) ([b97e3d8](https://github.com/AbongileBoja/QuerySpec/commit/b97e3d8dbb9c16208c8668a9fbc341d7111b8851))
* **release:** enforce PublicAPI tracking and populate Shipped.txt for v4.0.0 surface ([46cba49](https://github.com/AbongileBoja/QuerySpec/commit/46cba4932c7391f67da64ad55f5a8681cb0e595b)), closes [#166](https://github.com/AbongileBoja/QuerySpec/issues/166)
* **security:** use fixed-time compare for audit chain-link verification ([#233](https://github.com/AbongileBoja/QuerySpec/issues/233)) ([6e15761](https://github.com/AbongileBoja/QuerySpec/commit/6e157611b9a3da84f62b5f829eaf2e9647fa2f70)), closes [#188](https://github.com/AbongileBoja/QuerySpec/issues/188)


### Documentation

* **security:** add SECURITY.md vulnerability-disclosure policy ([#207](https://github.com/AbongileBoja/QuerySpec/issues/207)) ([afdff2e](https://github.com/AbongileBoja/QuerySpec/commit/afdff2e3f4cee71059a5a6f52b5e009a7f4103a1)), closes [#161](https://github.com/AbongileBoja/QuerySpec/issues/161)

## [4.0.0](https://github.com/AbongileBoja/QuerySpec/compare/v3.0.1-rc1...v4.0.0) (2026-04-27)


### ⚠ BREAKING CHANGES

* **security:** remove IEncryptionProvider.RotateKeyAsync overloads (#157)
* **di:** remove 11 throwing DI-builder stub methods (#156)
* **efcore:** convert QuerySpecExpressionTranslator to static class (#155)
* **core:** remove 3.x deprecations GeoLocation/AdvancedFilterExpression/ICacheProvider (#154)

### Features

* **analyzers:** ship QuerySpec.Analyzers package with QSPEC0001/2/3 diagnostics and code fixes ([#153](https://github.com/AbongileBoja/QuerySpec/issues/153)) ([c9032a8](https://github.com/AbongileBoja/QuerySpec/commit/c9032a8f25dc59b6a91750337770e09c90b328a6)), closes [#151](https://github.com/AbongileBoja/QuerySpec/issues/151) [#151](https://github.com/AbongileBoja/QuerySpec/issues/151)
* **core:** Microsoft-grade additive replacements + PublicAPI tracking ([#84](https://github.com/AbongileBoja/QuerySpec/issues/84)) ([#152](https://github.com/AbongileBoja/QuerySpec/issues/152)) ([773353e](https://github.com/AbongileBoja/QuerySpec/commit/773353ed53f5e11dd406334493d369c6d2b306b6)), closes [#151](https://github.com/AbongileBoja/QuerySpec/issues/151) [#150](https://github.com/AbongileBoja/QuerySpec/issues/150)
* **core:** remove 3.x deprecations GeoLocation/AdvancedFilterExpression/ICacheProvider ([#154](https://github.com/AbongileBoja/QuerySpec/issues/154)) ([90d6bee](https://github.com/AbongileBoja/QuerySpec/commit/90d6bee3256c6358ba6f2ba0f215aeb651e854d3)), closes [#150](https://github.com/AbongileBoja/QuerySpec/issues/150) [#84](https://github.com/AbongileBoja/QuerySpec/issues/84)
* **di:** remove 11 throwing DI-builder stub methods ([#156](https://github.com/AbongileBoja/QuerySpec/issues/156)) ([ed3be34](https://github.com/AbongileBoja/QuerySpec/commit/ed3be34faf2364e27ac726e8896145a5ec3fddbb)), closes [#77](https://github.com/AbongileBoja/QuerySpec/issues/77) [#139](https://github.com/AbongileBoja/QuerySpec/issues/139) [#77](https://github.com/AbongileBoja/QuerySpec/issues/77)
* **efcore:** convert QuerySpecExpressionTranslator to static class ([#155](https://github.com/AbongileBoja/QuerySpec/issues/155)) ([032dec4](https://github.com/AbongileBoja/QuerySpec/commit/032dec4523629c7daf4fa1296f1e4d697410bab4)), closes [#79](https://github.com/AbongileBoja/QuerySpec/issues/79) [#141](https://github.com/AbongileBoja/QuerySpec/issues/141) [#79](https://github.com/AbongileBoja/QuerySpec/issues/79)
* **security:** remove IEncryptionProvider.RotateKeyAsync overloads ([#157](https://github.com/AbongileBoja/QuerySpec/issues/157)) ([1d2e35c](https://github.com/AbongileBoja/QuerySpec/commit/1d2e35cc8e5bc740530eead6dfd222c577bee043)), closes [#138](https://github.com/AbongileBoja/QuerySpec/issues/138) [#73](https://github.com/AbongileBoja/QuerySpec/issues/73)


### Bug Fixes

* **auditing:** stream GDPR JSON export and propagate cancellation ([#62](https://github.com/AbongileBoja/QuerySpec/issues/62)) ([#126](https://github.com/AbongileBoja/QuerySpec/issues/126)) ([4f19193](https://github.com/AbongileBoja/QuerySpec/commit/4f19193a3fe8c20e3d8838766d8ba5a1f9759de3))
* **efcore:** translate string operators via EF-recognized methods ([#113](https://github.com/AbongileBoja/QuerySpec/issues/113)) ([#124](https://github.com/AbongileBoja/QuerySpec/issues/124)) ([dfa8fe5](https://github.com/AbongileBoja/QuerySpec/commit/dfa8fe5652f333ec4651a99d6b2805545b68eec2))
* **resilience:** inject TimeProvider into RateLimiter and replace wall-clock test ([#143](https://github.com/AbongileBoja/QuerySpec/issues/143)) ([#146](https://github.com/AbongileBoja/QuerySpec/issues/146)) ([04f1877](https://github.com/AbongileBoja/QuerySpec/commit/04f187758d3ec5216552b4cdc41745df31d12efd))
* **tests:** replace wall-clock timing with FakeTimeProvider and sync primitives ([#74](https://github.com/AbongileBoja/QuerySpec/issues/74)) ([#125](https://github.com/AbongileBoja/QuerySpec/issues/125)) ([b2d74ed](https://github.com/AbongileBoja/QuerySpec/commit/b2d74edd0be0a04dca8bf05dc1fa7e10e687e3ed))


### CI

* emit per-gate JSON evidence and aggregate release-health index ([7bcb4aa](https://github.com/AbongileBoja/QuerySpec/commit/7bcb4aa9aaf3b64687a2f4f7e126f8ad24cdeae2))


### Performance

* **core:** eliminate params object[] alloc in CacheKeyGenerator hot path ([#58](https://github.com/AbongileBoja/QuerySpec/issues/58)) ([9f88a21](https://github.com/AbongileBoja/QuerySpec/commit/9f88a21a5b1f08722400e230a4ff4b4d68245169))
* **efcore:** cache Nullable<T> HasValue/Value PropertyInfo in translator ([#87](https://github.com/AbongileBoja/QuerySpec/issues/87)) ([#127](https://github.com/AbongileBoja/QuerySpec/issues/127)) ([534eb7e](https://github.com/AbongileBoja/QuerySpec/commit/534eb7e6015d8074cdb6ceda26a34d48b59a31c2))


### Documentation

* **core:** complete <param>/<returns>/<exception>/<typeparam> on public surface ([#136](https://github.com/AbongileBoja/QuerySpec/issues/136)) ([332a2f5](https://github.com/AbongileBoja/QuerySpec/commit/332a2f540a4bdbe128e7764b0649384e0b960c2f)), closes [#3](https://github.com/AbongileBoja/QuerySpec/issues/3)
* **efcore:** mark QuerySpecExpressionTranslator for 3.0 static-class conversion ([#79](https://github.com/AbongileBoja/QuerySpec/issues/79)) ([#142](https://github.com/AbongileBoja/QuerySpec/issues/142)) ([571cc0a](https://github.com/AbongileBoja/QuerySpec/commit/571cc0a083cdf1023419233ed87178a6d648b015))
* **release:** document supported TFM policy and netstandard2.0 stance ([#66](https://github.com/AbongileBoja/QuerySpec/issues/66)) ([43bdc24](https://github.com/AbongileBoja/QuerySpec/commit/43bdc2456e40e531507a31bfb606408742077fb0))


### Tests

* **auditing:** cover InMemoryAuditLogger query and report methods ([#80](https://github.com/AbongileBoja/QuerySpec/issues/80)) ([#131](https://github.com/AbongileBoja/QuerySpec/issues/131)) ([8fddcb1](https://github.com/AbongileBoja/QuerySpec/commit/8fddcb17c5be53777db5d4d85824595b1e093dd9))
* **efcore:** add property-based tests and replace silent catch-all with NotSupportedException ([#78](https://github.com/AbongileBoja/QuerySpec/issues/78)) ([#135](https://github.com/AbongileBoja/QuerySpec/issues/135)) ([69a3112](https://github.com/AbongileBoja/QuerySpec/commit/69a3112e59ddbaa4b41ccb28c0dc6175fb210311))
* **monitoring:** cover N1DetectionEngine eviction/truncation paths and HealthStatus contract ([#83](https://github.com/AbongileBoja/QuerySpec/issues/83)) ([#132](https://github.com/AbongileBoja/QuerySpec/issues/132)) ([59d9ff9](https://github.com/AbongileBoja/QuerySpec/commit/59d9ff9910692bb7fcc876784fe46e6b97fcd1e5))
* **security:** configure Stryker.NET mutation testing scoped to security paths ([#76](https://github.com/AbongileBoja/QuerySpec/issues/76)) ([#134](https://github.com/AbongileBoja/QuerySpec/issues/134)) ([2caee91](https://github.com/AbongileBoja/QuerySpec/commit/2caee91395d19d7c1e6ad81099345636c9b7f99a)), closes [#130](https://github.com/AbongileBoja/QuerySpec/issues/130)
* **security:** cover large-AAD heap path in MigratingAuthenticatedEncryptionProvider ([#85](https://github.com/AbongileBoja/QuerySpec/issues/85)) ([#130](https://github.com/AbongileBoja/QuerySpec/issues/130)) ([3dec4e3](https://github.com/AbongileBoja/QuerySpec/commit/3dec4e3df594e9a3136e38a2d61c3c47a8751044))
* **tests:** full-library Stryker mutation gating with per-PR incremental + weekly sweep ([#76](https://github.com/AbongileBoja/QuerySpec/issues/76)) ([9ebff94](https://github.com/AbongileBoja/QuerySpec/commit/9ebff94a45a1d8208a2c9527b10fcbf68c2eae01))

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
