#!/usr/bin/env bash
# Headless guard: AIRSIDE_CAPTURE_RESUME=1 skips already-written PNGs so a mid-batch
# timeout can retry only the missing shots without wiping good stills.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cap="$root/scripts/capture-game.sh"
tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT

good="$tmpdir/good.png"
missing="$tmpdir/missing.png"
printf 'PNG' > "$good"

plan="$(AIRSIDE_CAPTURE_RESUME=1 bash "$cap" \
  --shot "$good:100" --shot "$missing:200" --print-plan)"
echo "plan $plan"
[[ "$plan" == *"delay=200"* ]] || { echo "resume plan must size soak to remaining max delay 200: $plan" >&2; exit 1; }
[[ "$plan" == *"shots=1"* ]] || { echo "resume plan must count only missing shots: $plan" >&2; exit 1; }
[[ "$plan" == *"resume_skipped=1"* ]] || { echo "resume plan must report resume_skipped=1: $plan" >&2; exit 1; }
[[ -s "$good" ]] || { echo "resume must not wipe existing good.png before launch" >&2; exit 1; }

# Without resume, both shots remain in the plan.
plan="$(bash "$cap" --shot "$good:100" --shot "$missing:200" --print-plan)"
echo "fresh $plan"
[[ "$plan" == *"delay=200"* ]] || { echo "fresh plan delay: $plan" >&2; exit 1; }
[[ "$plan" == *"shots=2"* ]] || { echo "fresh plan must keep both shots: $plan" >&2; exit 1; }

# All present → print-plan reports zero remaining shots (caller would short-circuit).
printf 'PNG' > "$missing"
plan="$(AIRSIDE_CAPTURE_RESUME=1 bash "$cap" \
  --shot "$good:100" --shot "$missing:200" --print-plan)"
echo "done $plan"
[[ "$plan" == *"shots=0"* ]] || { echo "all-present resume plan must be shots=0: $plan" >&2; exit 1; }
[[ "$plan" == *"resume_skipped=2"* ]] || { echo "all-present must skip 2: $plan" >&2; exit 1; }

grep -Fq 'AIRSIDE_CAPTURE_RESUME' "$root/scripts/review-post-audit-p0-remaining.sh" || {
  echo "remaining.sh must enable AIRSIDE_CAPTURE_RESUME for Stage C retries" >&2
  exit 1
}
echo "capture-game resume checks passed"
