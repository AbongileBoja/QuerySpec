# Changelog

All notable changes to QuerySpec are documented here. Generated from Conventional Commits by `standard-version`.

The format is based on [Keep a Changelog](https://keepachangelog.com/) and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Security

- Strong-name keypair rotation in progress (issues #8, #9, #18). The original `QuerySpec.snk` was committed to a public repository and is treated as burned. The repository now delay-signs locally with a public-only `QuerySpec.public.snk` and full-signs in CI from the `STRONG_NAME_KEY_BASE64` GitHub Actions secret bound to the `release-signing` environment.
- Row-Level Security engine now fails closed by default (issue #10). `RowLevelSecurityEngine` throws `InvalidOperationException` when a resource has no registered policy. New `RLSDefaultBehavior` enum and `RegisterUnrestricted<T>(...)` provide explicit opt-ins.

### Breaking (planned for the rotation release)

- Strong-name **public key token will change** after rotation. Consumers that pinned `[InternalsVisibleTo]` on the old token, used binding redirects, or resolved QuerySpec assemblies via the GAC must update.
- RLS engine fail-open default removed. Code that previously relied on missing-policy returning `AllowAll` must either pass `RLSDefaultBehavior.AllowAll` to the constructor or call `RegisterUnrestricted<T>(resourceType)` per resource.

### Changed

- `Directory.Build.props` is the single source of truth for assembly signing; per-project `<SignAssembly>` overrides removed.
- `.github/workflows/release.yml` rewritten: minimum permissions, environment-gated signing, decode-on-demand → use → scrub flow, signed-assembly verification before pack.

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
