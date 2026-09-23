#!/usr/bin/env bash
set -euo pipefail

# Fast, Unity-free check for Domain/Simulation: runs the same
# EditMode NUnit tests via `dotnet test` against scripts/dotnet-harness/,
# which compiles those assemblies straight from the Unity project. Presentation
# is not covered (it needs UnityEngine) and neither is a real Unity compile, so
# this is a supplementary pre-check — scripts/test-unity.sh remains the
# source of truth before merging.

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not found on PATH." >&2
  echo "Install the .NET 8 SDK (e.g. 'apt-get install dotnet-sdk-8.0' or https://dotnet.microsoft.com/download) and retry." >&2
  exit 1
fi

cd "$root/scripts/dotnet-harness"

# Harness.csproj excludes UnityEngine-importing EditMode tests by name. A new one that is
# not listed there fails the whole compile, which hides every other test result — that has
# already happened twice (MapLabelLayout/GroundSeparation, then AirsideFramePacing). Name the
# offending files instead of leaving a bare CS0246 to be interpreted.
unlisted=()
for test in "$root"/game/Airside/Assets/Airside/Tests/EditMode/*.cs; do
  grep -qE '^using Unity(Engine|Editor)' "$test" || continue
  grep -q "EditMode/$(basename "$test")" Harness.csproj || unlisted+=("$(basename "$test")")
done

if [ ${#unlisted[@]} -gt 0 ]; then
  echo "These EditMode tests import UnityEngine/UnityEditor but are not excluded in Harness.csproj:" >&2
  printf '  %s\n' "${unlisted[@]}" >&2
  echo "Add them to the Exclude list (they belong to scripts/test-unity.sh) and retry." >&2
  exit 1
fi

dotnet test
