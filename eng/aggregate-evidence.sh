#!/usr/bin/env bash
# aggregate-evidence.sh — combine per-gate evidence files into a single
# evidence-index.json describing the overall release-health verdict for
# this commit SHA.
#
# Run after all CI gates have emitted their per-gate evidence files into
# a shared directory (typically by downloading every "evidence-*" artifact
# from the workflow run into one folder). Matrix gates are folded into
# the index as arrays of summaries, one entry per matrix slot.
#
# Usage:
#   eng/aggregate-evidence.sh \
#     --input-dir <dir-containing-evidence-*.json> \
#     --output    <path-to-evidence-index.json> \
#     --workflow  <ci|release>
#
# Reads from environment (set by GitHub Actions):
#   GITHUB_SHA, GITHUB_REF, GITHUB_RUN_ID, GITHUB_SERVER_URL, GITHUB_REPOSITORY
#
# Exit codes:
#   0  success, overall_verdict=pass
#   1  success but overall_verdict=fail (use to fail the aggregator step)
#   2  invalid args / missing env / no evidence found

set -euo pipefail

INPUT_DIR=""
OUTPUT=""
WORKFLOW=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --input-dir) INPUT_DIR="$2"; shift 2 ;;
    --output)    OUTPUT="$2"; shift 2 ;;
    --workflow)  WORKFLOW="$2"; shift 2 ;;
    -h|--help)   grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

for v in INPUT_DIR OUTPUT WORKFLOW; do
  if [[ -z "${!v}" ]]; then echo "missing --${v,,}" >&2; exit 2; fi
done
case "$WORKFLOW" in ci|release) ;; *) echo "invalid --workflow: $WORKFLOW" >&2; exit 2 ;; esac
[[ -d "$INPUT_DIR" ]] || { echo "input dir not found: $INPUT_DIR" >&2; exit 2; }

for v in GITHUB_SHA GITHUB_REF GITHUB_RUN_ID; do
  if [[ -z "${!v:-}" ]]; then echo "missing env: $v" >&2; exit 2; fi
done
SERVER_URL="${GITHUB_SERVER_URL:-https://github.com}"
REPO="${GITHUB_REPOSITORY:-unknown/unknown}"
RUN_URL="$SERVER_URL/$REPO/actions/runs/$GITHUB_RUN_ID"
NOW="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

mapfile -t FILES < <(find "$INPUT_DIR" -type f -name 'evidence-*.json' | sort)
if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "no evidence files found under $INPUT_DIR" >&2; exit 2
fi

echo "[aggregate-evidence] folding ${#FILES[@]} per-gate evidence file(s)"

TMP_GATES="$(mktemp)"; trap 'rm -f "$TMP_GATES" "$TMP_FAILURES"' EXIT
TMP_FAILURES="$(mktemp)"
echo '{}' > "$TMP_GATES"
echo '[]' > "$TMP_FAILURES"

for f in "${FILES[@]}"; do
  if ! jq -e '.kind == "per-gate-evidence"' "$f" >/dev/null 2>&1; then
    echo "[aggregate-evidence] skipping non-per-gate file: $f" >&2
    continue
  fi

  gate=$(jq -r '.gate' "$f")
  matrix=$(jq -r '.matrix_slot // empty' "$f")
  verdict=$(jq -r '.verdict' "$f")
  blocking=$(jq -r '.blocking // true' "$f")
  artifact_name="evidence-${gate}"
  [[ -n "$matrix" ]] && artifact_name="${artifact_name}-${matrix}"

  summary_payload=$(jq '.summary' "$f")
  if [[ -n "$matrix" ]]; then
    summary_payload=$(jq --arg slot "$matrix" --argjson s "$summary_payload" \
                        -n '{($slot): $s}')
  fi

  jq --arg gate "$gate" \
     --argjson summary "$summary_payload" \
     --arg verdict "$verdict" \
     --argjson blocking "$blocking" \
     --arg artifact "$artifact_name" \
     '
     .[$gate] = (
       (.[$gate] // {verdict: "pass", blocking: $blocking, summary: null, artifact: ""})
       | .blocking = (.blocking and $blocking)
       | .verdict = (
           if $verdict == "fail" then "fail"
           elif .verdict == "fail" then "fail"
           elif $verdict == "skipped" and .verdict == "pass" then "skipped"
           elif $verdict == "pass" and .verdict == "skipped" then "pass"
           else .verdict
         end
         )
       | .summary = (
           if (.summary | type) == "object" and ((.summary | keys | length) > 0) then
             (.summary * $summary)
           elif (.summary | type) == "array" then
             (.summary + [$summary])
           else
             $summary
           end
         )
       | .artifact = (if .artifact == "" then $artifact else (.artifact + "," + $artifact) end)
     )
     ' "$TMP_GATES" > "${TMP_GATES}.new" && mv "${TMP_GATES}.new" "$TMP_GATES"

  if [[ "$verdict" != "pass" && "$blocking" == "true" ]]; then
    jq --arg gate "$gate" \
       --arg matrix "$matrix" \
       --arg verdict "$verdict" \
       '. + [{gate: $gate, verdict: $verdict} + (if $matrix == "" then {} else {matrix_slot: $matrix} end)]' \
       "$TMP_FAILURES" > "${TMP_FAILURES}.new" && mv "${TMP_FAILURES}.new" "$TMP_FAILURES"
  fi
done

OVERALL="pass"
if [[ "$(jq 'length' "$TMP_FAILURES")" -gt 0 ]]; then OVERALL="fail"; fi

mkdir -p "$(dirname "$OUTPUT")"
jq -n \
  --arg sha "$GITHUB_SHA" \
  --arg ref "$GITHUB_REF" \
  --arg workflow "$WORKFLOW" \
  --arg run_id "$GITHUB_RUN_ID" \
  --arg run_url "$RUN_URL" \
  --arg completed "$NOW" \
  --argjson gates "$(cat "$TMP_GATES")" \
  --arg overall "$OVERALL" \
  --argjson failures "$(cat "$TMP_FAILURES")" \
  '
  {
    schema_version: "1",
    kind: "evidence-index",
    sha: $sha,
    ref: $ref,
    workflow_name: $workflow,
    workflow_run_id: $run_id,
    workflow_run_url: $run_url,
    completed_at: $completed,
    gates: $gates,
    overall_verdict: $overall,
    blocking_failures: $failures
  }
  ' > "$OUTPUT"

echo "[aggregate-evidence] wrote $OUTPUT"
echo "[aggregate-evidence] overall_verdict=$OVERALL  blocking_failures=$(jq 'length' "$TMP_FAILURES")"

if [[ "$OVERALL" == "fail" ]]; then exit 1; fi
exit 0
