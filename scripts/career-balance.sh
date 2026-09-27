#!/usr/bin/env bash
set -euo pipefail

# Career balance report (ADR 0125): plays whole careers headlessly with the CareerBot for
# play style × seed and writes docs/testing/career-balance-<date>/report.md (plus
# runs.json and daily.csv). Deterministic: the same seeds give the same report.
#
#   scripts/career-balance.sh [out-dir] [seeds=5] [max-open-hours=180] [all] [style|all]

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not found on PATH (install the .NET 8 SDK)." >&2
  exit 1
fi

out="${1:-$root/docs/testing/career-balance-$(date +%F)}"
shift || true
dotnet run -c Release --project "$root/scripts/career-sim/CareerSim.csproj" -- "$out" "$@"
