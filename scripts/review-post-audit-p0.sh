#!/usr/bin/env bash
# Packaged Mac capture matrix for the post-audit P0 playtest
# (docs/plans/post-audit-improvement-plan.md, docs/testing/post-audit-p0-playtest.md).
#
# Uses ADR 0200 named bookmarks (`-airsideReviewView`) where they match, plus
# extra P0-only shots (far zoom, freight, fire station, weather, follow).
#
# Requires a built player (scripts/build-mac.sh) and a Mac with the display awake.
# Writes PNGs + Player logs under work/captures/post-audit-p0-<date>/ and a RESULTS.md
# stub for Bailey to fill keep / fix / revert.
#
# Usage:
#   scripts/review-post-audit-p0.sh
#   AIRSIDE_APP=/path/to/Airside.app scripts/review-post-audit-p0.sh
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stamp="$(date +%Y%m%d)"
shots="${AIRSIDE_P0_OUT:-$root/work/captures/post-audit-p0-$stamp}"
mkdir -p "$shots"

common=(
  -airsideSoakHeartbeatSeconds 10
  -screen-fullscreen 0
  -screen-width 1600
  -screen-height 900
)

capture() {
  local name="$1"; shift
  echo "==> $name"
  bash "$root/scripts/capture-game.sh" \
    --out "$shots/$name.png" \
    --delay "${CAPTURE_DELAY:-28}" \
    --timeout "${CAPTURE_TIMEOUT:-100}" \
    ${FOLLOW:+--follow "$FOLLOW"} \
    -- "$@" "${common[@]}"
  FOLLOW=""
}

# --- Overview + far zoom / land cover (ADR 0185, 0190, 0191) ---
capture overview-day-clear \
  -airsideReviewView overview \
  -airsideReviewWeather clear -airsideReviewTime 12:00

capture overview-far-land-cover \
  -airsideReviewView overview \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideOverviewDistance 28000 -airsideOverviewPitch 35

capture overview-night-sky-traffic \
  -airsideReviewView overview \
  -airsideReviewWeather clear -airsideReviewTime 23:30 \
  -airsideOverviewDistance 3200 -airsideOverviewPitch 28

# --- Terminal / hangar bookmarks (ADR 0185–0188, 0197) ---
capture terminal-airside-day \
  -airsideReviewView terminal-airside \
  -airsideReviewWeather clear -airsideReviewTime 12:00

capture terminal-airside-night \
  -airsideReviewView terminal-airside \
  -airsideReviewWeather clear -airsideReviewTime 23:30

capture terminal-kerb-day \
  -airsideReviewView terminal-kerb \
  -airsideReviewWeather clear -airsideReviewTime 12:00

capture hangar-row-day \
  -airsideReviewView hangar-row \
  -airsideReviewWeather clear -airsideReviewTime 12:00

capture suburb-edge-day \
  -airsideReviewView suburb-edge \
  -airsideReviewWeather clear -airsideReviewTime 12:00

capture coast-day \
  -airsideReviewView coast \
  -airsideReviewWeather clear -airsideReviewTime 12:00

# --- Freight sheds / fire station detail (ADR 0199) — not in Phase 0 table ---
capture freight-qantas-day \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideOverviewCenterX 740 -airsideOverviewCenterZ 340 \
  -airsideOverviewDistance 260 -airsideOverviewPitch 28 -airsideOverviewYaw 200

capture fire-station-night \
  -airsideReviewWeather clear -airsideReviewTime 23:30 \
  -airsideOverviewCenterX 130 -airsideOverviewCenterZ 220 \
  -airsideOverviewDistance 220 -airsideOverviewPitch 30 -airsideOverviewYaw 160

# --- Weather depth sample (ADR 0193); full matrix remains scripts/review-weather.sh ---
capture weather-storm-overview \
  -airsideReviewView overview \
  -airsideReviewWeather storm -airsideReviewTime 12:00

capture weather-fog-overview \
  -airsideReviewView overview \
  -airsideReviewWeather fog -airsideReviewTime 12:00

# --- Follow a soak aircraft (tyres / audio / boarding tape when present) ---
FOLLOW=VH-PAX
capture follow-jet-day \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

FOLLOW=VH-PAX
capture follow-jet-close \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.35

# Log sweep — same spirit as review-weather.sh
if command -v rg >/dev/null 2>&1; then
  if rg -n 'Shader error|NullReferenceException|InvalidOperationException|IndexOutOfRangeException|\[Airside soak\] STALL' "$shots"/*.log 2>/dev/null; then
    echo "P0 review found an error; inspect the logs above." >&2
    exit 1
  fi
fi

results="$shots/RESULTS.md"
docs_stamp="$root/docs/testing/post-audit-p0-$stamp"
mkdir -p "$docs_stamp"
if [ ! -f "$results" ]; then
  cp "$root/docs/testing/post-audit-p0-playtest.md" "$results"
  if command -v sed >/dev/null 2>&1; then
    sed -i.bak "s|work/captures/post-audit-p0-<date>|$shots|" "$results" 2>/dev/null \
      || sed -i '' "s|work/captures/post-audit-p0-<date>|$shots|" "$results"
    rm -f "$results.bak"
  fi
  {
    echo
    echo "## Capture run"
    echo
    echo "- Date stamp: \`$stamp\`"
    echo "- Shots directory: \`$shots\`"
    echo "- Branch tip: \`$(git -C "$root" rev-parse --short HEAD 2>/dev/null || echo unknown)\`"
    echo "- Code-only evidence (do not treat as visual keep): \`docs/testing/post-audit-p0-2026-09-30/CODE_EVIDENCE.md\`"
    echo
    echo "Copy this file to \`$docs_stamp/RESULTS.md\` after filling verdicts, then push"
    echo "branch \`cursor/post-audit-p0-results-709e\`."
  } >> "$results"
fi
cp "$results" "$docs_stamp/RESULTS.md"

echo "P0 captures and logs: $shots"
echo "Fill keep/fix/revert in: $results"
echo "Docs copy (for git): $docs_stamp/RESULTS.md"
echo
echo "Still manual (not automated by this script):"
echo "  - Listen to aircraft audio at overview + follow (ADR 0192/0196)"
echo "  - Refit a freighter and check cargo livery + tyre pivot (ADR 0194)"
echo "  - Arrival already on final during a storm lands (ADR 0190) — code covered; still see it"
echo "  - Hangar tow / boarding tape during a live check (ADR 0186–0188, 0196)"
echo "  - Follow-camera feel while turning / climbing (ADR 0189)"
