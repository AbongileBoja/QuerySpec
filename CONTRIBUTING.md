# Contributing to QuerySpec

## Branching strategy

QuerySpec uses a simplified Gitflow:

| Branch            | Purpose                                           | Base       | Merges into        |
|-------------------|---------------------------------------------------|------------|--------------------|
| `main`            | Production / released code. Tagged, immutable.    | —          | (release only)     |
| `develop`         | Integration branch for the next release.          | `main`     | `main` via release |
| `feat/<slug>`     | A new feature.                                    | `develop`  | `develop`          |
| `fix/<slug>`      | A non-urgent bug fix.                             | `develop`  | `develop`          |
| `hotfix/<slug>`   | Urgent production fix.                            | `main`     | `main` + `develop` |
| `release/x.y.z`   | Release stabilisation (version bumps, CHANGELOG). | `develop`  | `main` + `develop` |
| `chore/<slug>`    | Tooling, deps, CI changes.                        | `develop`  | `develop`          |
| `docs/<slug>`     | Documentation-only.                               | `develop`  | `develop`          |

**Never push directly to `main` or `develop`.** All changes land via pull request with
a green CI run.

### Typical flow

```bash
# Start from a clean develop
git checkout develop
git pull --ff-only origin develop

# Cut a topic branch (names are lower-case, hyphen-separated)
git checkout -b feat/add-redis-cache-warmup

# Commit using the Conventional Commits format (see below)
git add .
git commit -m "feat(caching): add Redis cache warm-up on startup"

# Push and open a PR targeting develop
git push -u origin feat/add-redis-cache-warmup
gh pr create --base develop --fill
```

### Releasing

```bash
git checkout develop
git pull --ff-only
git checkout -b release/1.1.0
# bump <Version> in Directory.Build.props, update CHANGELOG.md
git commit -m "release(release): 1.1.0"
git push -u origin release/1.1.0
gh pr create --base main --fill
# after merge to main, tag and back-merge:
git checkout main && git pull --ff-only
git tag v1.1.0 && git push origin v1.1.0
git checkout develop && git merge --no-ff main && git push
```

### Hotfixes

```bash
git checkout main
git pull --ff-only
git checkout -b hotfix/fix-rls-injection-regression
# fix + test
git commit -m "fix(security): patch RLS injection regression"
git push -u origin hotfix/fix-rls-injection-regression
gh pr create --base main --fill
# after merge, back-merge into develop
git checkout develop && git merge --no-ff main && git push
```

## Commit messages

This repository enforces [Conventional Commits](https://www.conventionalcommits.org/) via
[commitlint](https://commitlint.js.org/). The rules live in [.commitlintrc.json](.commitlintrc.json).

### Format

```
<type>(<scope>): <subject>

[optional body]

[optional footer]
```

### Allowed types

`feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`

### Allowed scopes

`core`, `efcore`, `di`, `caching`, `resilience`, `security`, `auditing`, `monitoring`,
`advanced`, `benchmarks`, `tests`, `ci`, `deps`, `docs`, `release`

### Examples

```
feat(caching): add TTL-based cache invalidation strategy
fix(resilience): prevent RateLimiter token surge on clock adjustment
perf(efcore): cache PropertyInfo lookups per (Type, name) pair
docs(readme): document breaking RLS API change
test(security): add injection payloads to RLS policy tests
chore(deps): bump Meziantou.Analyzer to 2.0.180
```

### Rules that will reject your commit

- Subject must not be empty and must not end in `.`
- Subject must not be Title-Cased, PascalCased, or UPPER-CASED
- Type and scope must be lower-case
- Header (first line) cannot exceed 100 characters
- Body and footer lines cannot exceed 120 characters

## Local setup

```bash
# One-time: installs commitlint + husky locally
npm install

# After that, every `git commit` runs commitlint automatically via the
# .husky/commit-msg hook. To lint the most recent commit manually:
npm run commitlint:last
```

If you don't have Node installed, you can still commit — CI will enforce the rules on
your PR. Local validation is strongly recommended to avoid round-trips.

## Bypassing locally (discouraged)

`git commit --no-verify` skips the hook. CI will still reject non-conforming commits on
PR, so there is no production path that avoids the linter.

## Public-API approval

QuerySpec uses [PublicApiGenerator](https://github.com/PublicApiGenerator/PublicApiGenerator) and [Verify](https://github.com/VerifyTests/Verify) to snapshot the public surface of every shipping assembly. The baselines live in `tests/QuerySpec.PublicApi.Tests/ApprovedApi/`.

### What triggers approval failure

Any change to the public surface of `QuerySpec.Core`, `QuerySpec.EFCore`, `QuerySpec.DependencyInjection`, or `QuerySpec.Analyzers` will cause `dotnet test` on `QuerySpec.PublicApi.Tests` to fail with a diff between the committed `.verified.txt` and the newly produced `.received.txt`.

### Updating the baselines

When an intentional public-API change is made, accept the new snapshot:

```bash
# From the repo root — run the approval tests once to produce .received.txt files
dotnet test tests/QuerySpec.PublicApi.Tests/ --framework net10.0

# Review every diff, then promote received → verified
for f in tests/QuerySpec.PublicApi.Tests/ApprovedApi/*.received.txt; do
  cp "$f" "${f/DotNet10_0.received/verified}"
done

# Stage the updated baselines alongside the API change
git add tests/QuerySpec.PublicApi.Tests/ApprovedApi/
```

Both a `feat:` (or `fix:`) commit for the API change and the baseline update belong in the same PR so reviewers see the intended diff.

### Double-gate

1. `Microsoft.CodeAnalysis.PublicApiAnalyzers` (RS0016/RS0017) rejects undeclared API additions at **build time** via `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`.
2. The approval tests in `QuerySpec.PublicApi.Tests` catch **any surface change** (added, removed, or modified members) at **test time**.

Both gates must pass for CI to be green.

## No warning suppression

Every compiler and analyzer warning in QuerySpec is a signal. Suppressing that signal is never the right default — it hides bugs, confuses future contributors, and creates noise in code scanning tools.

### The rule

Do not introduce any of the following in a PR:

- `<NoWarn>` in `.csproj` or `.props` files
- `<WarningsNotAsErrors>` in `.csproj` or `.props` files
- `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` (downgrading the repo-wide true)
- `[UnconditionalSuppressMessage(...)]` attribute
- `[SuppressMessage(...)]` attribute
- `#pragma warning disable <code>`
- `continue-on-error: true` in a workflow step

CI enforces this on every PR via `eng/no-suppression-check.sh`. The check is diff-based: it compares the PR head against the merge-base with the target branch, so pre-existing suppressions in `develop` are not retriggered.

### Why this matters

Signal preservation keeps the analyzer baseline honest. A suppressed CA1062 today becomes a NullReferenceException in production six months later. Suppressions also confuse supply-chain scanners (CodeQL, Dependabot) that rely on the analyzer results being unfiltered.

### How to fix a warning instead of suppressing it

For the most common cases:

- **CA1062 (validate public params):** replace `if (x == null) throw ...` with `ArgumentNullException.ThrowIfNull(x);`.
- **MA0004 / CA2007 (ConfigureAwait):** add `.ConfigureAwait(false)` to every `await` in library code.
- **CA1822 (can be static):** add `static` to the member, or extract to a helper if the member is on a public interface.
- **RS0026/RS0027 (optional params on overloads):** declare an explicit overload rather than `= default` on an overloaded parameter.

When the fix requires a refactor that is out of scope for the current PR, open a tracked issue describing the warning, link it from the PR description, and land the fix in a subsequent PR. The warning will remain active in CI and continue to show in logs — which is intentional.

### The escape hatch

If a warning genuinely cannot be fixed without a breaking change slated for a future major version, document the carve-out in a GitHub issue before merging. The issue must explain: the warning code and why it fires, why the fix requires a breaking change, and the target version for the fix. This is the only path that allows a suppression past CI — and it requires human review of the issue before the PR can merge.

## Coverage ratchet

CI enforces minimum line and branch coverage on every push and PR. Thresholds are defined in `.github/workflows/ci.yml`:

| Metric | Current floor | Target |
|---|---|---|
| Line coverage | 91% | ≥ 90% |
| Branch coverage | 81% | ≥ 80% |
| Method coverage | collected, not gated | — |

Rules:
- Thresholds only ever move forward — never backward.
- When a coverage gap is filled, open a PR that raises the floor by the coverage gained (typically 1–5 pp) and link to the issue that closed the gap.
- A PR that drops coverage below the floor fails CI.
- Method coverage is reported in the CI log but not gated until line ≥ 90% and branch ≥ 80% are consistently held.
