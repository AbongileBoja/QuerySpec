#!/usr/bin/env bash
# no-suppression-check.sh — diff-based check that a PR introduces no new warning suppressions.
#
# Compares PR head against the merge-base with the target branch. Pre-existing
# suppressions in the base are not flagged; only additions in the PR are checked.
#
# Usage:
#   eng/no-suppression-check.sh [base-ref]
#
#   base-ref  Git ref to diff against. Defaults to origin/develop.
#             On CI, pass origin/${{ github.event.pull_request.base.ref }}.
#
# Forbidden patterns (matched on '+' lines of the unified diff):
#   <NoWarn>                        — silences specific diagnostics in MSBuild
#   <WarningsNotAsErrors>           — demotes specific warnings from error treatment
#   <TreatWarningsAsErrors>false    — downgrades the global treat-as-errors flag
#   [UnconditionalSuppressMessage   — unconditional suppression attribute
#   [SuppressMessage                — conditional suppression attribute
#   #pragma warning disable         — in-source warning suppression
#   continue-on-error: true         — silences failure in GitHub Actions workflow steps
#
# Exit codes: 0 clean, 1 forbidden patterns found, 2 no base ref available (non-fatal on push).

set -euo pipefail

base_ref="${1:-origin/develop}"

if git rev-parse --verify "$base_ref" >/dev/null 2>&1; then
  range="${base_ref}...HEAD"
else
  echo "no-suppression check: base ref '${base_ref}' not found; falling back to HEAD~1..HEAD" >&2
  if git rev-parse --verify HEAD~1 >/dev/null 2>&1; then
    range="HEAD~1..HEAD"
  else
    echo "no-suppression check: only one commit in history; nothing to diff. Skipping." >&2
    exit 0
  fi
fi

added="$(git diff --unified=0 \
    "$range" -- '*.cs' '*.csproj' '*.props' '*.targets' '*.yml' '*.yaml' \
  | grep -vE '^\+\+\+' \
  | grep -E '^\+' \
  | grep -E '<NoWarn>|<WarningsNotAsErrors>|<TreatWarningsAsErrors>false|UnconditionalSuppressMessage|\[SuppressMessage|#pragma warning disable|continue-on-error: true' \
  || true)"

if [[ -n "$added" ]]; then
  echo "::error::This PR introduces forbidden warning-suppression patterns:" >&2
  echo "$added" >&2
  echo "" >&2
  echo "Per the project's no-suppression rule, fix the warning at its root cause." >&2
  echo "If a warning genuinely cannot be fixed without a breaking refactor," >&2
  echo "document the carve-out in an issue before merging. See CONTRIBUTING.md." >&2
  exit 1
fi

echo "no-suppression check: clean (no new <NoWarn>, SuppressMessage, pragma, WarningsNotAsErrors, or continue-on-error)."
