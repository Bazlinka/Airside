#!/usr/bin/env bash
set -euo pipefail

# Fast, Unity-free check for Domain/Simulation/Persistence: runs the same
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
dotnet test
