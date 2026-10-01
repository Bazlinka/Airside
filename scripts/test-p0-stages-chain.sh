#!/usr/bin/env bash
# Headless guard: one-paste Stage A→B→C wrapper keeps the documented ONLY lists.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
stages="$root/scripts/run-post-audit-p0-stages.sh"

test -x "$stages" || { echo "missing executable $stages" >&2; exit 1; }

grep -Fq 'AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter' "$stages" || {
  echo "Stage A ONLY list missing/changed in $stages" >&2
  exit 1
}
grep -Fq 'AIRSIDE_P0_ONLY=follow-jet-takeoff' "$stages" || {
  echo "Stage B ONLY list missing/changed in $stages" >&2
  exit 1
}
grep -Fq 'AIRSIDE_P0_ONLY=follow-hangar-tow,follow-boarding-tape,follow-human-ops-close,follow-jet-day,follow-jet-close,follow-storm-landing' "$stages" || {
  echo "Stage C ONLY list missing/changed in $stages" >&2
  exit 1
}
grep -Fq 'AIRSIDE_P0_SKIP_BUILD=1' "$stages" || {
  echo "Stage B/C must skip rebuild" >&2
  exit 1
}
grep -Fq 'AIRSIDE_P0_SKIP_PULL=1' "$stages" || {
  echo "Stage B/C must skip pull" >&2
  exit 1
}
# Must call the remaining runner, not invent a parallel path.
grep -Fq 'run-post-audit-p0-remaining.sh' "$stages" || {
  echo "stages wrapper must call run-post-audit-p0-remaining.sh" >&2
  exit 1
}
grep -Fq 'caffeinate -d -i' "$stages" || {
  echo "stages wrapper must caffeinate the full A→B→C run" >&2
  exit 1
}
grep -Fq 'AIRSIDE_P0_CAFFEINATED' "$stages" || {
  echo "stages wrapper must guard against nested caffeinate re-exec" >&2
  exit 1
}
grep -Fq 'display notification' "$stages" || {
  echo "stages wrapper should notify when A→B→C finishes" >&2
  exit 1
}

# Finder double-click wrapper must stay executable and call the stages runner.
command_wrapper="$root/scripts/run-post-audit-p0-stages.command"
test -x "$command_wrapper" || { echo "missing executable $command_wrapper" >&2; exit 1; }
grep -Fq 'run-post-audit-p0-stages.sh' "$command_wrapper" || {
  echo ".command wrapper must exec run-post-audit-p0-stages.sh" >&2
  exit 1
}
grep -Fq 'Code/Airside' "$command_wrapper" || {
  echo ".command wrapper must target ~/Code/Airside" >&2
  exit 1
}
grep -Fq 'p0-checkout-mac-tip.sh' "$command_wrapper" || {
  echo ".command wrapper must checkout via p0-checkout-mac-tip.sh" >&2
  exit 1
}

# Short Stage A Finder path (night-sky + freighter) for when ~45 min is too much.
stage_a_wrapper="$root/scripts/run-post-audit-p0-stage-a.command"
test -x "$stage_a_wrapper" || { echo "missing executable $stage_a_wrapper" >&2; exit 1; }
grep -Fq 'run-post-audit-p0-remaining.sh' "$stage_a_wrapper" || {
  echo "Stage A .command must call run-post-audit-p0-remaining.sh" >&2
  exit 1
}
grep -Fq 'overview-night-sky-traffic,follow-freighter' "$stage_a_wrapper" || {
  echo "Stage A .command must set Stage A ONLY list" >&2
  exit 1
}
grep -Fq 'Code/Airside' "$stage_a_wrapper" || {
  echo "Stage A .command must target ~/Code/Airside" >&2
  exit 1
}
grep -Fq 'p0-checkout-mac-tip.sh' "$stage_a_wrapper" || {
  echo "Stage A .command must checkout via p0-checkout-mac-tip.sh" >&2
  exit 1
}

# Stage A .command calls remaining.sh directly — remaining must caffeinate so a
# cold Unity build cannot let the display sleep before the first soak.
remaining="$root/scripts/run-post-audit-p0-remaining.sh"
grep -Fq 'caffeinate -d -i' "$remaining" || {
  echo "remaining runner must caffeinate pull/build/captures for Stage A" >&2
  exit 1
}
grep -Fq 'AIRSIDE_P0_CAFFEINATED' "$remaining" || {
  echo "remaining runner must guard against nested caffeinate re-exec" >&2
  exit 1
}
grep -Fq 'display notification' "$remaining" || {
  echo "remaining runner must notify when Stage A/B/C PNGs are ready" >&2
  exit 1
}

checkout="$root/scripts/p0-checkout-mac-tip.sh"
test -x "$checkout" || { echo "missing executable $checkout" >&2; exit 1; }
grep -Fq 'AIRSIDE_P0_BRANCH' "$checkout" || {
  echo "checkout helper must honour AIRSIDE_P0_BRANCH" >&2
  exit 1
}
grep -Fq 'cursor/p0-freighter-pick-lock-709e' "$checkout" || {
  echo "checkout helper must default to freighter-pick tip branch" >&2
  exit 1
}
grep -Fq 'origin main' "$checkout" || {
  echo "checkout helper must fall back to main after tip merge" >&2
  exit 1
}

echo "P0 stages chain lock passed"
