#!/usr/bin/env python3
"""Check Unity asset metadata and packaged-runtime art without modifying either tree."""

from __future__ import annotations

import hashlib
import re
import sys
from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "game/Airside/Assets"
ART_SOURCE = ASSETS / "Airside/Art"
ART_RUNTIME = ASSETS / "StreamingAssets/Airside/Art"
RUNTIME_SUFFIXES = {".gltf", ".bin", ".png", ".jpg", ".jpeg"}
NUMBERED_COPY = re.compile(r" \d+$")
GUID = re.compile(r"^guid:\s*([0-9a-fA-F]+)\s*$", re.MULTILINE)


def relative(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def digest(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def runtime_files(root: Path) -> dict[Path, Path]:
    files: dict[Path, Path] = {}
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in RUNTIME_SUFFIXES:
            continue
        if NUMBERED_COPY.search(path.stem):
            continue
        files[path.relative_to(root)] = path
    return files


def main() -> int:
    problems: list[str] = []

    missing_meta = []
    for path in ASSETS.rglob("*"):
        if path == ASSETS or path.name == ".DS_Store" or path.suffix == ".meta":
            continue
        if not Path(f"{path}.meta").is_file():
            missing_meta.append(relative(path))
    if missing_meta:
        problems.append("Assets missing .meta files:\n  " + "\n  ".join(missing_meta))

    orphan_meta = []
    guid_paths: defaultdict[str, list[str]] = defaultdict(list)
    unreadable_meta = []
    for meta in ASSETS.rglob("*.meta"):
        target = Path(str(meta)[:-5])
        if not target.exists():
            orphan_meta.append(relative(meta))
        text = meta.read_text(encoding="utf-8", errors="replace")
        match = GUID.search(text)
        if match:
            guid_paths[match.group(1).lower()].append(relative(meta))
        else:
            unreadable_meta.append(relative(meta))
    if orphan_meta:
        problems.append("Orphan .meta files:\n  " + "\n  ".join(orphan_meta))
    if unreadable_meta:
        problems.append(".meta files without a GUID:\n  " + "\n  ".join(unreadable_meta))

    duplicate_guids = {guid: paths for guid, paths in guid_paths.items() if len(paths) > 1}
    if duplicate_guids:
        lines = [f"{guid}: {', '.join(paths)}" for guid, paths in sorted(duplicate_guids.items())]
        problems.append("Duplicate Unity GUIDs:\n  " + "\n  ".join(lines))

    source = runtime_files(ART_SOURCE)
    runtime = runtime_files(ART_RUNTIME)
    missing_runtime = sorted(set(source) - set(runtime))
    unexpected_runtime = sorted(set(runtime) - set(source))
    mismatched = sorted(rel for rel in set(source) & set(runtime) if digest(source[rel]) != digest(runtime[rel]))
    if missing_runtime:
        problems.append("Runtime art missing from StreamingAssets:\n  " + "\n  ".join(map(str, missing_runtime)))
    if unexpected_runtime:
        problems.append("Stale runtime art in StreamingAssets:\n  " + "\n  ".join(map(str, unexpected_runtime)))
    if mismatched:
        problems.append("Runtime art differs from its source:\n  " + "\n  ".join(map(str, mismatched)))

    material_dir = ASSETS / "Resources/Airside/Characters/Materials"
    material_count = len(list(material_dir.glob("*.mat"))) if material_dir.is_dir() else 0
    if material_count == 0:
        problems.append("Character materials are absent; Unity will regenerate unstable external materials.")

    if problems:
        print("Unity asset audit FAILED", file=sys.stderr)
        for problem in problems:
            print(f"\n{problem}", file=sys.stderr)
        return 1

    print(
        "Unity asset audit passed: "
        f"{len(guid_paths)} unique GUIDs, {len(source)} mirrored runtime art files, "
        f"{material_count} committed character materials."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
