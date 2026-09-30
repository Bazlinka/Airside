#!/usr/bin/env bash
# Headless guard: Stage A/B/C AIRSIDE_P0_ONLY lists select the intended shots
# (and skip the rest) without launching the Unity player.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
remaining="$root/scripts/review-post-audit-p0-remaining.sh"

tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT
mkdir -p "$tmpdir/bin"
ran="$tmpdir/ran.txt"
: >"$ran"

cat >"$tmpdir/bin/capture-game.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
out=""
follow=""
declare -a outs=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --out) out="$2"; outs+=("$2"); shift 2 ;;
    --shot)
      spec="$2"
      path="${spec%%:*}"
      outs+=("$path")
      shift 2
      ;;
    --follow) follow="$2"; shift 2 ;;
    --delay|--timeout|--minutes) shift 2 ;;
    --print-plan) shift ;;
    --) shift; break ;;
    *) shift ;;
  esac
done
if [[ ${#outs[@]} -eq 0 && -n "$out" ]]; then
  outs+=("$out")
fi
for path in "${outs[@]}"; do
  base="$(basename "${path%.png}")"
  echo "${base}|follow=${follow:-}" >>"${AIRSIDE_P0_RAN_FILE:?}"
  mkdir -p "$(dirname "$path")"
  printf 'PNG' >"$path"
done
EOF
chmod +x "$tmpdir/bin/capture-game.sh"

# Prepend stub ahead of real scripts/ on PATH via wrapping: remaining calls
# bash "$root/scripts/capture-game.sh" with an absolute path, so PATH won't work.
# Patch by running under a fake root symlink layout instead.
fake_root="$tmpdir/root"
mkdir -p "$fake_root/scripts"
cp "$remaining" "$fake_root/scripts/review-post-audit-p0-remaining.sh"
# Absolute capture path inside the copied script points at $root — rewrite to stub.
sed -i "s|bash \"\$root/scripts/capture-game.sh\"|bash \"$tmpdir/bin/capture-game.sh\"|" \
  "$fake_root/scripts/review-post-audit-p0-remaining.sh"

run_only() {
  local label="$1" only="$2" expect="$3"
  : >"$ran"
  AIRSIDE_P0_ONLY="$only" \
  AIRSIDE_P0_OUT="$tmpdir/shots-$label" \
  AIRSIDE_P0_RAN_FILE="$ran" \
    bash "$fake_root/scripts/review-post-audit-p0-remaining.sh" >/dev/null
  got="$(cut -d'|' -f1 "$ran" | paste -sd, -)"
  if [[ "$got" != "$expect" ]]; then
    echo "ONLY filter mismatch for $label" >&2
    echo "  only=$only" >&2
    echo "  expect=$expect" >&2
    echo "  got=$got" >&2
    cat "$ran" >&2
    exit 1
  fi
  echo "ok $label → $got"
}

run_only stage-a \
  'overview-night-sky-traffic,follow-freighter' \
  'overview-night-sky-traffic,follow-freighter'

run_only stage-b \
  'follow-jet-takeoff' \
  'follow-jet-takeoff'

run_only stage-c \
  'follow-hangar-tow,follow-boarding-tape,follow-human-ops-close,follow-jet-day,follow-jet-close,follow-storm-landing' \
  'follow-hangar-tow,follow-jet-day,follow-jet-close,follow-storm-landing,follow-boarding-tape,follow-human-ops-close'

# Follow tokens for takeoff / landing stages
: >"$ran"
AIRSIDE_P0_ONLY='follow-jet-takeoff,follow-jet-day' \
AIRSIDE_P0_OUT="$tmpdir/shots-follow" \
AIRSIDE_P0_RAN_FILE="$ran" \
  bash "$fake_root/scripts/review-post-audit-p0-remaining.sh" >/dev/null
grep -qx 'follow-jet-takeoff|follow=auto-takeoff' "$ran" || {
  echo "expected auto-takeoff follow token" >&2
  cat "$ran" >&2
  exit 1
}
grep -qx 'follow-jet-day|follow=auto-landing' "$ran" || {
  echo "expected auto-landing follow token" >&2
  cat "$ran" >&2
  exit 1
}
echo "ok follow tokens auto-takeoff / auto-landing"

echo "P0 remaining ONLY filter passed"
