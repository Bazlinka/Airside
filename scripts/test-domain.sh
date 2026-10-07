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

# Which Presentation files and tests compile without UnityEngine is derived by scripts/update-harness.py
# (Harness.Generated.props). If you added a Presentation file or a test, regenerate it first:
#   python3 scripts/update-harness.py
python3 "$root/scripts/update-harness.py" --check

# Compile-only pass against Unity's NUnit 3.5 (no implicit usings), then the real run on NUnit 3.14 (ADR 0252).
bash "$root/scripts/check-unity-nunit.sh"
dotnet test
