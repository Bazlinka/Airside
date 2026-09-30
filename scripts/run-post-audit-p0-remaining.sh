#!/usr/bin/env bash
# One-command Mac path for remaining P0 stills after #490 (ADR 0205).
# Pulls the tip that has review helpers, builds the player, runs
# scripts/review-post-audit-p0-remaining.sh, and copies PNGs into the dated
# RESULTS folder for verdict fill-in. Does not invent keep/fix/revert.
#
# Usage (Darwin, awake display):
#   scripts/run-post-audit-p0-remaining.sh
#   AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter scripts/run-post-audit-p0-remaining.sh
#   AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_ONLY=follow-jet-takeoff scripts/run-post-audit-p0-remaining.sh
#     (Stage B after Stage A — reuse the player just built; still pulls unless SKIP_PULL=1)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cd "$root"

uname_s="$(uname -s)"
if [ "$uname_s" != "Darwin" ]; then
  echo "This script must run on Darwin (got $uname_s). Use Bailey's MacBook Pro." >&2
  exit 1
fi

branch="$(git branch --show-current)"
case "$branch" in
  main|cursor/p0-auto-landing-follow-709e) ;;
  *)
    echo "Run from main or cursor/p0-auto-landing-follow-709e (got $branch)." >&2
    exit 1
    ;;
esac
# Stage A stamps RESULTS / copies PNGs, so Stage B must tolerate those dirty paths.
# Any other dirty path still blocks (do not mix unrelated edits into a capture run).
git status --porcelain | bash "$root/scripts/p0-remaining-check-dirty.sh"

if [ "${AIRSIDE_P0_SKIP_PULL:-}" = "1" ]; then
  echo "==> Skipping git pull (AIRSIDE_P0_SKIP_PULL=1)"
else
  git fetch origin "$branch"
  git pull --ff-only origin "$branch"
fi

app="${AIRSIDE_APP:-$root/work/builds/Airside.app}/Contents/MacOS/Airside"
if [ "${AIRSIDE_P0_SKIP_BUILD:-}" = "1" ]; then
  if [ ! -x "$app" ]; then
    echo "AIRSIDE_P0_SKIP_BUILD=1 but no player at $app — run a full build first." >&2
    exit 1
  fi
  echo "==> Skipping Mac build (AIRSIDE_P0_SKIP_BUILD=1); using $app"
else
  echo "==> Building Mac player (need multi-shot review + auto-landing/takeoff helpers)"
  bash "$root/scripts/build-mac.sh"
fi

# Stage C landing/boarding batches need ReviewShotSchedule in the player. An older
# build only honours the first -airsideReviewShot and then quits — capture-game
# would wait out the full timeout for the missing PNGs. Fail-closed review aborts
# must also be present or SKIP_BUILD can write blind overview stills that look OK.
if [ -x "$app" ]; then
  if ! strings "$app" 2>/dev/null | grep -Fq 'ReviewShotSchedule'; then
    echo "Player at $app lacks multi-shot review (ReviewShotSchedule). Rebuild without SKIP_BUILD." >&2
    exit 1
  fi
  echo "==> Player has multi-shot review (ReviewShotSchedule)"
  if ! strings "$app" 2>/dev/null | grep -Fq 'freighter refit never applied'; then
    echo "Player at $app lacks review-flag fail-closed. Rebuild without SKIP_BUILD." >&2
    exit 1
  fi
  if ! strings "$app" 2>/dev/null | grep -Fq 'follow never started before delay'; then
    echo "Player at $app lacks auto-follow fail-closed. Rebuild without SKIP_BUILD." >&2
    exit 1
  fi
  echo "==> Player has review fail-closed aborts"
fi

stamp="$(date +%Y%m%d)"
capture_out="${AIRSIDE_P0_OUT:-$root/work/captures/post-audit-p0-remaining-$stamp}"
docs_dir="$root/docs/testing/post-audit-p0-2026-09-30"
mkdir -p "$capture_out" "$docs_dir"

echo "==> Remaining P0 captures (display must stay awake; ~45+ min for full A→B→C)"
AIRSIDE_P0_OUT="$capture_out" bash "$root/scripts/review-post-audit-p0-remaining.sh"

echo "==> Copying new PNGs into $docs_dir (overwrites prior stills of the same name)"
copied=0
shopt -s nullglob
for png in "$capture_out"/*.png; do
  cp -f "$png" "$docs_dir/$(basename "$png")"
  copied=$((copied + 1))
done
shopt -u nullglob

results="$docs_dir/RESULTS.md"
if [ ! -f "$results" ]; then
  echo "Missing $results — cannot stamp inventory." >&2
  exit 1
fi

# Drop a prior auto-inventory block so re-runs stay idempotent.
if grep -q '^<!-- AIRSIDE_P0_REMAINING_INVENTORY_BEGIN -->$' "$results"; then
  tmp="$(mktemp)"
  awk '
    /^<!-- AIRSIDE_P0_REMAINING_INVENTORY_BEGIN -->$/ { skip=1; next }
    /^<!-- AIRSIDE_P0_REMAINING_INVENTORY_END -->$/ { skip=0; next }
    !skip { print }
  ' "$results" > "$tmp"
  mv "$tmp" "$results"
fi

follow_fail=0
{
  echo
  echo "<!-- AIRSIDE_P0_REMAINING_INVENTORY_BEGIN -->"
  echo "## Remaining capture inventory (auto — not a verdict)"
  echo
  echo "- Captured at: \`$(date -u +%Y-%m-%dT%H:%MZ)\`"
  echo "- Branch tip: \`$(git -C "$root" rev-parse --short HEAD)\`"
  echo "- Host: \`$uname_s\` / \`$(hostname 2>/dev/null || echo unknown)\`"
  echo "- Shots directory: \`$capture_out\`"
  echo "- AIRSIDE_P0_ONLY: \`${AIRSIDE_P0_ONLY:-<all remaining>}\`"
  echo "- PNGs copied into docs folder: \`$copied\`"
  echo
  echo "| File | PNG bytes | Log |"
  echo "|---|---:|---|"
  shopt -s nullglob
  for png in "$capture_out"/*.png; do
    base="$(basename "$png" .png)"
    bytes="$(wc -c < "$png" | tr -d ' ')"
    logf="$capture_out/$base.log"
    if [ -f "$logf" ]; then
      # Prefer grep — ripgrep is often missing on CI / fresh Mac agents.
      if grep -Eq 'Shader error|NullReferenceException|InvalidOperationException|IndexOutOfRangeException|\[Airside soak\] STALL|review shot aborted' "$logf"; then
        log_status="errors"
      else
        log_status="clean"
      fi
      # Review stills must log a successful apply/follow (fail closed vs blind overview).
      # Multi-shot batches share capture-game's first --shot logFile, so scan siblings too.
      case "$base" in
        follow-jet-takeoff)
          if ! grep -Eq '\[Airside soak\] following auto-takeoff ' "$logf"; then
            log_status="errors"
            follow_fail=1
          fi
          ;;
        follow-jet-day|follow-jet-close|follow-storm-landing)
          found_follow=0
          for alt in follow-jet-day follow-jet-close follow-storm-landing; do
            if [ -f "$capture_out/$alt.log" ] \
              && grep -Eq '\[Airside soak\] following auto-landing ' "$capture_out/$alt.log"; then
              found_follow=1
              break
            fi
          done
          if [ "$found_follow" -eq 0 ]; then
            log_status="errors"
            follow_fail=1
          fi
          ;;
        follow-freighter)
          if ! grep -Eq '\[Airside soak\] review freighter ' "$logf"; then
            log_status="errors"
            follow_fail=1
          fi
          ;;
        follow-hangar-tow)
          if ! grep -Eq '\[Airside soak\] review hangar check ' "$logf"; then
            log_status="errors"
            follow_fail=1
          fi
          ;;
        follow-boarding-tape|follow-human-ops-close)
          found_board=0
          for alt in follow-boarding-tape follow-human-ops-close; do
            if [ -f "$capture_out/$alt.log" ] \
              && grep -Eq '\[Airside soak\] review boarding ' "$capture_out/$alt.log"; then
              found_board=1
              break
            fi
          done
          if [ "$found_board" -eq 0 ]; then
            log_status="errors"
            follow_fail=1
          fi
          ;;
      esac
    else
      log_status="missing"
    fi
    echo "| \`$base.png\` | $bytes | $log_status |"
  done
  shopt -u nullglob
  echo
  echo "PNG presence / log clean is **not** keep. Fill Verdict columns by eye/ear."
  echo "<!-- AIRSIDE_P0_REMAINING_INVENTORY_END -->"
} >> "$results"

if [ "$follow_fail" -ne 0 ]; then
  echo "Auto-landing/auto-takeoff still(s) missing 'following auto-*' log line — treat as failed capture." >&2
  exit 1
fi

echo
echo "Copied $copied PNG(s) into $docs_dir"
echo "Stamped capture inventory (not verdicts) into:"
echo "  $results"
echo "Fill Verdict columns for remaining + manual rows — do not invent."
echo "Then update GAME.md and push via the protected-main PR workflow."
echo
echo "Optional subset next time: AIRSIDE_P0_ONLY=shot,shot $0"
