#!/usr/bin/env bash
# compare-bench.sh — compare a BenchmarkDotNet JSON run against committed baselines.
#
# Usage:
#   compare-bench.sh --run-dir <path> --baseline-dir <path> [--threshold-pct <n>] [--alloc-threshold <n>]
#
# Exit codes:
#   0  all benchmarks within threshold
#   1  one or more regressions detected (fail CI)
#   2  usage error

set -euo pipefail

THRESHOLD_PCT=5
ALLOC_THRESHOLD=100
RUN_DIR=""
BASELINE_DIR=""

usage() {
    echo "Usage: $0 --run-dir <path> --baseline-dir <path> [--threshold-pct <n>] [--alloc-threshold <n>]" >&2
    exit 2
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --run-dir)        RUN_DIR="$2";        shift 2 ;;
        --baseline-dir)   BASELINE_DIR="$2";   shift 2 ;;
        --threshold-pct)  THRESHOLD_PCT="$2";  shift 2 ;;
        --alloc-threshold) ALLOC_THRESHOLD="$2"; shift 2 ;;
        *) usage ;;
    esac
done

[[ -z "$RUN_DIR" || -z "$BASELINE_DIR" ]] && usage

if ! command -v jq &>/dev/null; then
    echo "ERROR: jq is required but not found on PATH." >&2
    exit 2
fi

if [[ ! -d "$RUN_DIR" ]]; then
    echo "ERROR: run directory not found: $RUN_DIR" >&2
    exit 2
fi

if [[ ! -d "$BASELINE_DIR" ]]; then
    echo "ERROR: baseline directory not found: $BASELINE_DIR" >&2
    exit 2
fi

regressions=0
compared=0

for baseline_file in "$BASELINE_DIR"/*.json; do
    [[ -f "$baseline_file" ]] || continue
    class_name=$(basename "$baseline_file" .json)

    run_file=$(find "$RUN_DIR" -name "*${class_name}*-report-full.json" 2>/dev/null | head -1)
    if [[ -z "$run_file" ]]; then
        run_file=$(find "$RUN_DIR" -name "*${class_name}*.json" 2>/dev/null | grep -v "github\|markdown\|html\|csv" | head -1)
    fi

    if [[ -z "$run_file" ]]; then
        echo "WARNING: no run JSON found for class '$class_name' (skipping)" >&2
        continue
    fi

    baseline_count=$(jq '.Benchmarks | length' "$baseline_file" 2>/dev/null || echo 0)
    run_count=$(jq '.Benchmarks | length' "$run_file" 2>/dev/null || echo 0)

    if [[ "$baseline_count" -eq 0 || "$run_count" -eq 0 ]]; then
        echo "WARNING: empty benchmark list in '$class_name' (skipping)" >&2
        continue
    fi

    while IFS= read -r bench_name; do
        baseline_mean=$(jq -r --arg n "$bench_name" \
            '.Benchmarks[] | select(.FullName == $n) | .Statistics.Mean' \
            "$baseline_file" 2>/dev/null || echo "")
        run_mean=$(jq -r --arg n "$bench_name" \
            '.Benchmarks[] | select(.FullName == $n) | .Statistics.Mean' \
            "$run_file" 2>/dev/null || echo "")

        baseline_alloc=$(jq -r --arg n "$bench_name" \
            '.Benchmarks[] | select(.FullName == $n) | .Memory.BytesAllocatedPerOperation' \
            "$baseline_file" 2>/dev/null || echo "0")
        run_alloc=$(jq -r --arg n "$bench_name" \
            '.Benchmarks[] | select(.FullName == $n) | .Memory.BytesAllocatedPerOperation' \
            "$run_file" 2>/dev/null || echo "0")

        [[ -z "$baseline_mean" || "$baseline_mean" == "null" ]] && continue
        [[ -z "$run_mean" || "$run_mean" == "null" ]] && continue

        compared=$((compared + 1))

        pct_delta=$(awk -v b="$baseline_mean" -v r="$run_mean" \
            'BEGIN { if (b > 0) printf "%.2f", ((r - b) / b) * 100; else print "0" }')

        is_regression=$(awk -v d="$pct_delta" -v t="$THRESHOLD_PCT" \
            'BEGIN { print (d + 0 > t + 0) ? "1" : "0" }')

        alloc_delta=0
        if [[ "$baseline_alloc" != "null" && "$run_alloc" != "null" && \
              -n "$baseline_alloc" && -n "$run_alloc" ]]; then
            alloc_delta=$(awk -v b="${baseline_alloc:-0}" -v r="${run_alloc:-0}" \
                'BEGIN { printf "%d", r - b }')
        fi

        alloc_regression=$(awk -v d="$alloc_delta" -v t="$ALLOC_THRESHOLD" \
            'BEGIN { print (d + 0 > t + 0) ? "1" : "0" }')

        if [[ "$is_regression" == "1" || "$alloc_regression" == "1" ]]; then
            regressions=$((regressions + 1))
            baseline_ns=$(awk -v n="$baseline_mean" 'BEGIN { printf "%.1f", n }')
            run_ns=$(awk -v n="$run_mean" 'BEGIN { printf "%.1f", n }')
            echo "REGRESSION DETECTED: $bench_name"
            echo "  baseline mean : ${baseline_ns} ns/op"
            echo "  current mean  : ${run_ns} ns/op"
            echo "  mean delta    : +${pct_delta}% (threshold: ${THRESHOLD_PCT}%)"
            if [[ "$alloc_regression" == "1" ]]; then
                echo "  alloc delta   : +${alloc_delta} B/op (threshold: ${ALLOC_THRESHOLD} B/op)"
            fi
        else
            short_name=$(echo "$bench_name" | sed 's/.*\.//')
            echo "  OK  ${short_name}: +${pct_delta}% mean, +${alloc_delta} B alloc"
        fi
    done < <(jq -r '.Benchmarks[].FullName' "$baseline_file" 2>/dev/null | tr -d '\r')
done

echo ""
echo "Compared $compared benchmark(s). Regressions: $regressions."

if [[ "$regressions" -gt 0 ]]; then
    echo "::error::Perf regression gate FAILED — $regressions benchmark(s) exceeded threshold (mean >+${THRESHOLD_PCT}% or alloc >+${ALLOC_THRESHOLD} B/op)."
    exit 1
fi

echo "Perf regression gate PASSED."
exit 0
