#!/usr/bin/env bash
set -euo pipefail

# Compile-only check that the EditMode tests (and the Presentation files in the headless harness) build against the NUnit that
# Unity actually ships (3.5, bundled by Unity Test Framework 1.6) and without .NET's implicit usings. It catches the breaks the
# normal harness cannot: Is.AnyOf and other newer NUnit APIs, or a missing `using System;` (ADR 0252). It runs no tests.
# Run it before `dotnet test`: the next restore switches the harness back to NUnit 3.14, which can run on .NET 8.

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not found on PATH." >&2
  exit 1
fi

cd "$root/scripts/dotnet-harness"
python3 "$root/scripts/update-harness.py" --check
dotnet build --nologo -v q -p:UnityNUnit=true
echo "Tests compile against Unity's NUnit 3.5 without implicit usings."
