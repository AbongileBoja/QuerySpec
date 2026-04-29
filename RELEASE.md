# Releasing QuerySpec to NuGet

Four packages ship together at the same version:

- [`QuerySpec.Core`](https://www.nuget.org/packages/QuerySpec.Core)
- [`QuerySpec.EFCore`](https://www.nuget.org/packages/QuerySpec.EFCore)
- [`QuerySpec.DependencyInjection`](https://www.nuget.org/packages/QuerySpec.DependencyInjection)
- [`QuerySpec.Analyzers`](https://www.nuget.org/packages/QuerySpec.Analyzers)

## One-time setup

### 1. Create a scoped NuGet API key

1. Sign in to [nuget.org](https://www.nuget.org/) as the package owner.
2. Go to **Account settings → API keys → Create**.
3. Scope:
   - **Key name:** `queryspec-github-actions`
   - **Package owner:** your account
   - **Scopes:** `Push new packages and package versions`
   - **Packages:** `QuerySpec.Core`, `QuerySpec.EFCore`, `QuerySpec.DependencyInjection`, `QuerySpec.Analyzers`
     *(If the packages don't yet exist, use the `Glob pattern` field with `QuerySpec.*`.)*
   - **Expiration:** 365 days (rotate annually).
4. **Copy the key once** — nuget.org won't show it again.

### 2. Store the key as a GitHub secret

```bash
gh secret set NUGET_API_KEY --body "<paste-the-key-here>" --repo AbongileBoja/QuerySpec
```

Or via UI: **Settings → Secrets and variables → Actions → New repository secret**,
name `NUGET_API_KEY`.

### 3. Protect the release environment

**Settings → Environments → New environment** named `nuget-release`. Under it:

- **Required reviewers:** add yourself (and anyone else who should approve pushes).
- **Wait timer:** 0–5 minutes if you want a brief window to cancel.
- **Deployment branches:** `main` and `develop` only.

This gates every NuGet push on a manual approval click, so a rogue tag can't silently
publish.

### 4. Rotate-annually checklist

- Regenerate the NuGet API key before it expires.
- Re-run `gh secret set NUGET_API_KEY ...` with the new key.
- Delete the old key on nuget.org.

## Cutting a release

All commands run from the repo root on `develop`.

```bash
# 1. Sanity: everything pushed, working tree clean, tests green
git status
dotnet test QuerySpec.sln -c Release

# 1a. When cutting a major: promote analyzer rules from Unshipped.md to Shipped.md.
#     Move every row under "### New Rules" / "### Removed Rules" / etc. from
#     src/QuerySpec.Analyzers/AnalyzerReleases.Unshipped.md into a new
#     "## Release X.0.0" section in
#     src/QuerySpec.Analyzers/AnalyzerReleases.Shipped.md, leaving Unshipped
#     header-only. The Microsoft.CodeAnalysis.Analyzers release-tracking gate
#     fails the analyzer build until this is done.

# 2. Use standard-version to bump + changelog + tag atomically.
#    Pick the right semver bump type:
npm run release                    # auto: derives from commit types since last tag
npm run release -- --release-as patch     # 1.0.4 → 1.0.5
npm run release -- --release-as minor     # 1.0.4 → 1.1.0
npm run release -- --release-as major     # 1.0.4 → 2.0.0
npm run release -- --release-as 1.2.3-rc.1   # explicit pre-release

# 3. Push the commit + tag. The 'release.yml' workflow fires on the v*.*.* tag.
git push --follow-tags origin develop
```

### What happens after `git push --follow-tags`

1. **`ci.yml`** runs (commit push path) — build/test/format/CodeQL/dep-review all must pass.
2. **`release.yml`** runs (tag push path) on the matching `vX.Y.Z` tag:
   - Builds `QuerySpec.sln` with `-p:Version=<tag>` so the tag is the source of truth
     (not `Directory.Build.props`).
   - Runs the full test suite across net8/net9/net10.
   - Packs all four shipping `src/` projects → `artifacts/*.nupkg` + `*.snupkg`.
   - Validates that exactly 4 nupkg + 3 snupkg exist and filenames embed the version.
     (QuerySpec.Analyzers ships with `DevelopmentDependency=true` and embedded PDB symbols; no `.snupkg`.)
3. **Approval gate** — GitHub pauses and requests approval on the `nuget-release`
   environment. Review, then click **Approve and deploy**.
4. **Publish** — `dotnet nuget push` uploads each `.nupkg` to nuget.org. Adjacent
   `.snupkg` files auto-upload to the NuGet symbol server.
5. **GitHub Release** — a release is created for the tag with auto-generated notes
   and the `.nupkg`/`.snupkg` files attached.

## Pre-releases

For release candidates, betas, or alphas:

```bash
npm run release -- --release-as 1.1.0-rc.1
git push --follow-tags origin develop
```

NuGet treats any version with a `-suffix` as a pre-release; it won't show up as the
latest stable on the package page. Consumers opt in with:

```bash
dotnet add package QuerySpec.Core --prerelease
```

## Hotfixing an already-published release

NuGet does **not** allow overwriting a pushed version — you can only publish a new
one. Workflow:

```bash
# From main (or develop if main is behind)
git checkout -b hotfix/fix-rls-regression
# fix + test
git commit -m "fix(security): patch RLS regression"
git push -u origin hotfix/fix-rls-regression
gh pr create --base main --fill

# After merge:
git checkout main && git pull --ff-only
npm run release -- --release-as patch
git push --follow-tags origin main
git checkout develop && git merge --no-ff main && git push
```

## Verifying build provenance

Every published `.nupkg` carries a Sigstore-signed SLSA build provenance attestation
generated by `actions/attest-build-provenance` during the release workflow. The
attestation cryptographically binds the package to the commit, workflow, and
repository that produced it.

To verify a downloaded package:

```bash
gh attestation verify <package>.nupkg --owner AbongileBoja
```

A successful verification confirms the package was built by `release.yml` running on
this repository at a specific commit, with no intermediate tampering. Verification
requires no third-party services and no offline trust roots — GitHub publishes the
Sigstore transparency log and the attestation alongside the release.

## Unlisting a broken release

If a published version contains something harmful, don't delete — **unlist** it.
On nuget.org → package page → **Manage Package → Listing**, uncheck the version.
It stays resolvable for existing lockfiles but won't appear in `dotnet add package` or
package-search results.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Workflow says `NUGET_API_KEY secret is not set` | Set it: `gh secret set NUGET_API_KEY --body "<key>"` |
| `dotnet nuget push` returns `409 Conflict` | That version was already pushed. Bump the version and re-tag. `--skip-duplicate` hides this from failing the job. |
| `Tag '...' is not a valid SemVer version` | Tag must match `v[0-9]+.[0-9]+.[0-9]+(-suffix)?`. Rename with `git tag -d` + re-create. |
| Published but `SourceLink` doesn't jump to source | The pack step needs a full-depth checkout (`fetch-depth: 0`). Already configured. |
| Symbols (.snupkg) don't show up | nuget.org validation takes ~10–30 min for symbol publishing. |
