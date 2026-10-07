#!/usr/bin/env python3
"""Run only the headless tests that matter for what you changed (seconds, not the full 3-minute suite).

  python3 scripts/test-quick.py --changed            tests that reference any C# file changed vs origin/main, plus your uncommitted work
  python3 scripts/test-quick.py FlightWorld Camera   tests whose full name contains any of these words
  python3 scripts/test-quick.py --changed --list     show what it would run, run nothing

How `--changed` decides: a changed test file runs itself; a changed source file runs every test file in the headless harness whose text
mentions its class name (word match). It is a fast pre-check, not a replacement: run `scripts/test-domain.sh` (everything, plus the Unity
NUnit compile check) before you push, and `scripts/test-unity.sh` on a Mac for anything that touches UnityEngine.
Needs the .NET 8 SDK (`bash scripts/bootstrap-dotnet.sh`).
"""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HARNESS = ROOT / "scripts/dotnet-harness"
TESTS = "game/Airside/Assets/Airside/Tests/EditMode/"
CODE = "game/Airside/Assets/Airside/"


def git(*args: str) -> list[str]:
    out = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True).stdout
    return [line for line in out.splitlines() if line.strip()]


def changed_files(base: str) -> list[str]:
    files = set(git("diff", "--name-only", f"{base}...HEAD")) | set(git("diff", "--name-only", "HEAD")) | set(git("ls-files", "-o", "--exclude-standard"))
    return sorted(files)


def harness_tests() -> dict[str, str]:
    """Test file stem -> its text, for every test the headless harness compiles."""
    props = (HARNESS / "Harness.Generated.props").read_text()
    result = {}
    for rel in re.findall(r'Include="\.\./\.\./(game/Airside/Assets/Airside/Tests/EditMode/[^"]+\.cs)"', props):
        path = ROOT / rel
        if path.exists():
            result[path.stem] = path.read_text(encoding="utf-8", errors="replace")
    return result


def classes_for(files: list[str], tests: dict[str, str]) -> tuple[list[str], list[str]]:
    chosen, notes = set(), []
    for f in files:
        if not f.endswith(".cs") or not f.startswith(CODE):
            continue
        stem = Path(f).stem
        if f.startswith(TESTS):
            if stem in tests:
                chosen.add(stem)
            else:
                notes.append(f"{stem}: a Unity-only test (not in the headless harness) — run scripts/test-unity.sh on a Mac")
            continue
        word = re.compile(r"\b" + re.escape(stem.split(".")[0]) + r"\b")
        hits = sorted(name for name, text in tests.items() if word.search(text))
        if hits:
            chosen.update(hits)
        else:
            notes.append(f"{stem}: no headless test mentions it")
    return sorted(chosen), notes


def main(argv: list[str]) -> int:
    if not argv or argv[0] in {"-h", "--help"}:
        print(__doc__)
        return 0
    list_only = "--list" in argv
    base = "origin/main"
    if "--base" in argv:
        base = argv[argv.index("--base") + 1]
    words = [a for a in argv if not a.startswith("--") and a != base]
    names: list[str] = []
    if "--changed" in argv:
        files = changed_files(base)
        names, notes = classes_for(files, harness_tests())
        print(f"{len(files)} changed file(s) vs {base} and the working tree.")
        for n in notes:
            print("  note:", n)
    names += words
    if not names:
        print("Nothing to run: no headless test references the changed code. Run scripts/test-domain.sh to be sure.")
        return 0
    print(f"Running {len(names)} test class(es): {', '.join(names)}")
    if list_only:
        return 0
    flt = "|".join(f"FullyQualifiedName~{n}" for n in names)
    return subprocess.run(["dotnet", "test", "--nologo", "-v", "q", "--filter", flt], cwd=HARNESS).returncode


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
