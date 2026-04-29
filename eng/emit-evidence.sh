#!/usr/bin/env bash
# emit-evidence.sh — emit a structured JSON record of a CI gate's outcome.
# Used by .github/workflows/ci.yml and .github/workflows/release.yml to
# produce machine-readable release-health artifacts alongside the human
# logs. No external dependencies beyond bash + jq (preinstalled on the
# ubuntu-latest runner).
#
# Each invocation writes one file: $OUTPUT_DIR/evidence-<gate>[-<matrix>].json
# matching the per-gate evidence shape in QuerySpec's evidence schema.
#
# Usage:
#   eng/emit-evidence.sh \
#     --gate <name> \
#     --verdict <pass|fail|skipped> \
#     --output-dir <dir> \
#     [--matrix-slot <slug>] \
#     [--blocking <true|false>] \
#     [--summary-json <inline-json> | --summary-file <path>] \
#     [--details-artifact <name>] \
#     [--notes <text>]
#
# Reads from environment (set by GitHub Actions):
#   GITHUB_SHA, GITHUB_REF, GITHUB_RUN_ID, GITHUB_SERVER_URL, GITHUB_REPOSITORY
#   STARTED_AT (optional ISO-8601; defaults to now-1s)
#
# Exit codes: 0 success, 2 invalid args, 3 missing env, 4 invalid summary JSON.

set -euo pipefail

GATE=""
VERDICT=""
OUTPUT_DIR=""
MATRIX_SLOT=""
BLOCKING="true"
SUMMARY_JSON=""
SUMMARY_FILE=""
DETAILS_ARTIFACT=""
NOTES=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --gate)              GATE="$2"; shift 2 ;;
    --verdict)           VERDICT="$2"; shift 2 ;;
    --output-dir)        OUTPUT_DIR="$2"; shift 2 ;;
    --matrix-slot)       MATRIX_SLOT="$2"; shift 2 ;;
    --blocking)          BLOCKING="$2"; shift 2 ;;
    --summary-json)      SUMMARY_JSON="$2"; shift 2 ;;
    --summary-file)      SUMMARY_FILE="$2"; shift 2 ;;
    --details-artifact)  DETAILS_ARTIFACT="$2"; shift 2 ;;
    --notes)             NOTES="$2"; shift 2 ;;
    -h|--help)           grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

for v in GATE VERDICT OUTPUT_DIR; do
  if [[ -z "${!v}" ]]; then echo "missing --${v,,}" >&2; exit 2; fi
done

case "$GATE" in
  build|test|format|codeql|vulnerability-scan|commitlint|reproducibility|dependency-review|pack|package-validation|strong-name-verify|benchmark-smoke|no-suppression) ;;
  *) echo "invalid --gate: $GATE" >&2; exit 2 ;;
esac
case "$VERDICT" in
  pass|fail|skipped) ;;
  *) echo "invalid --verdict: $VERDICT" >&2; exit 2 ;;
esac
case "$BLOCKING" in
  true|false) ;;
  *) echo "invalid --blocking: $BLOCKING" >&2; exit 2 ;;
esac

for v in GITHUB_SHA GITHUB_REF GITHUB_RUN_ID; do
  if [[ -z "${!v:-}" ]]; then echo "missing env: $v (run inside GitHub Actions)" >&2; exit 3; fi
done
SERVER_URL="${GITHUB_SERVER_URL:-https://github.com}"
REPO="${GITHUB_REPOSITORY:-unknown/unknown}"
RUN_URL="$SERVER_URL/$REPO/actions/runs/$GITHUB_RUN_ID"

if [[ -n "$SUMMARY_JSON" && -n "$SUMMARY_FILE" ]]; then
  echo "use either --summary-json or --summary-file, not both" >&2; exit 2
fi
if [[ -n "$SUMMARY_FILE" ]]; then
  [[ -f "$SUMMARY_FILE" ]] || { echo "summary file not found: $SUMMARY_FILE" >&2; exit 2; }
  SUMMARY_JSON="$(cat "$SUMMARY_FILE")"
fi
if [[ -z "$SUMMARY_JSON" ]]; then SUMMARY_JSON="{}"; fi

if ! echo "$SUMMARY_JSON" | jq -e . >/dev/null 2>&1; then
  echo "invalid --summary-json (must be parseable JSON)" >&2
  echo "got: $SUMMARY_JSON" >&2
  exit 4
fi

NOW="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
STARTED_AT_VAL="${STARTED_AT:-$(date -u -d '1 second ago' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || echo "$NOW")}"

mkdir -p "$OUTPUT_DIR"

slot_suffix=""
if [[ -n "$MATRIX_SLOT" ]]; then
  slot_clean="$(echo "$MATRIX_SLOT" | tr -cs 'a-zA-Z0-9._-' '-' | sed 's/^-//;s/-$//')"
  slot_suffix="-$slot_clean"
fi
OUT_FILE="$OUTPUT_DIR/evidence-${GATE}${slot_suffix}.json"

jq -n \
  --arg gate "$GATE" \
  --arg matrix "$MATRIX_SLOT" \
  --arg sha "$GITHUB_SHA" \
  --arg ref "$GITHUB_REF" \
  --arg run_id "$GITHUB_RUN_ID" \
  --arg run_url "$RUN_URL" \
  --arg started "$STARTED_AT_VAL" \
  --arg completed "$NOW" \
  --arg verdict "$VERDICT" \
  --argjson blocking "$BLOCKING" \
  --argjson summary "$SUMMARY_JSON" \
  --arg details "$DETAILS_ARTIFACT" \
  --arg notes "$NOTES" \
  '
  {
    schema_version: "1",
    kind: "per-gate-evidence",
    gate: $gate,
    sha: $sha,
    ref: $ref,
    workflow_run_id: $run_id,
    workflow_run_url: $run_url,
    started_at: $started,
    completed_at: $completed,
    verdict: $verdict,
    blocking: $blocking,
    summary: $summary
  }
  + (if $matrix == "" then {} else { matrix_slot: $matrix } end)
  + (if $details == "" then {} else { details_artifact: $details } end)
  + (if $notes == "" then {} else { notes: $notes } end)
  ' > "$OUT_FILE"

echo "[emit-evidence] wrote $OUT_FILE"
echo "[emit-evidence] gate=$GATE verdict=$VERDICT blocking=$BLOCKING sha=${GITHUB_SHA:0:8}"
