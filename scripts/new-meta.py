#!/usr/bin/env python3
"""Create the Unity .meta file for new assets: a fresh GUID on an importer cloned from an existing asset of the same kind.

Every file and folder under game/Airside/Assets needs a committed .meta with a unique GUID (scripts/audit-unity-assets.py checks).
Unity writes them when the editor opens the project; cloud tools have no editor, so this makes them the same way each time.

  python3 scripts/new-meta.py game/Airside/Assets/Airside/Presentation/NewThing.cs [more paths...]

Skips paths that already have a .meta. The template is the nearest existing .meta for the same extension (same folder first, then
anywhere under Assets), so importer settings match the neighbours. A folder gets the standard folder meta. Run
`python3 scripts/audit-unity-assets.py` afterwards. For runtime art, also mirror it into StreamingAssets
(`scripts/sync-art-streaming-assets.sh`) and meta the copy too.
"""
from __future__ import annotations

import re
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "game/Airside/Assets"

FOLDER = "fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def template_for(path: Path) -> str | None:
    ext = path.suffix
    siblings = [p for p in path.parent.glob(f"*{ext}.meta") if p.name != path.name + ".meta"]
    if not siblings:
        scope = ASSETS / "StreamingAssets" if "StreamingAssets" in path.parts else ASSETS
        siblings = list(scope.rglob(f"*{ext}.meta"))[:1] or list(ASSETS.rglob(f"*{ext}.meta"))[:1]
    return siblings[0].read_text() if siblings else None


def main(argv: list[str]) -> int:
    if not argv or argv[0] in {"-h", "--help"}:
        print(__doc__)
        return 0
    status = 0
    for arg in argv:
        path = (ROOT / arg).resolve() if not Path(arg).is_absolute() else Path(arg)
        if not path.exists():
            print(f"missing: {arg}", file=sys.stderr)
            status = 1
            continue
        if ASSETS not in path.parents:
            print(f"not under {ASSETS.relative_to(ROOT)}: {arg}", file=sys.stderr)
            status = 1
            continue
        meta = path.parent / (path.name + ".meta")
        if meta.exists():
            print(f"already has a meta: {arg}")
            continue
        guid = uuid.uuid4().hex
        if path.is_dir():
            meta.write_text(FOLDER.format(guid=guid))
        else:
            text = template_for(path)
            if text is None:
                print(f"no example .meta for {path.suffix} to clone: {arg} (let Unity create this one)", file=sys.stderr)
                status = 1
                continue
            made = re.sub(r"^guid: \w+", f"guid: {guid}", text, count=1, flags=re.M)
            if made == text:
                print(f"template for {arg} has no guid line", file=sys.stderr)
                status = 1
                continue
            meta.write_text(made)
        print(f"wrote {meta.relative_to(ROOT)}  guid {guid}")
    return status


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
