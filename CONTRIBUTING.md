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
