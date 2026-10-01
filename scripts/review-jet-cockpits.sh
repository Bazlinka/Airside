#!/usr/bin/env bash
# Native geometry stills for all ten jets; this does not prove a packaged flight.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity="${AIRSIDE_UNITY:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
out="${1:-$root/work/jet-cockpit-review}"
mkdir -p "$out"
run_dir="$(mktemp -d "$out/run-XXXXXX")"
"$unity" -batchmode -quit -projectPath "$root/game/Airside" \
  -executeMethod CockpitAppearanceReview.RunAllJets \
  -cockpitReviewOutput "$run_dir" -logFile "$run_dir/native-review.log"
python3 - "$run_dir" <<'PY'
import pathlib, sys
folder = pathlib.Path(sys.argv[1])
types = ['B738','B38M','A320','A21N','E190','A223','A359','A339','B789','B78X']
shots = ['forward','left','panel','right','overhead','layout','bank','footwell','left-down','right-down']
missing = [f'{t}_{s}.png' for t in types for s in shots if not (folder / f'{t}_{s}.png').is_file()]
if missing: sys.exit('Missing cockpit renders: ' + ', '.join(missing))
print('All 100 type/angle captures produced in ' + str(folder) + '; inspect them before accepting the visuals.')
PY
