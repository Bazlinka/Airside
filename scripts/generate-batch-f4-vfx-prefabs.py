#!/usr/bin/env python3
"""Emit Resources + Art/VFX prefabs for Batch F4 VFX-001…004.

Pipeline-proof sphere hierarchies mirroring the current procedural builders in
AirsidePrototype (touchdown smoke, engine heat, rain drops, wet puddle proxy).
Presentation only — simulation timing unchanged.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "authored_prefabs", REPO / "scripts/generate-authored-resources-prefabs.py"
)
_mod = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_mod)

RESOURCES = REPO / "game/Airside/Assets/Resources/Airside/Prefabs"
ART_VFX = REPO / "game/Airside/Assets/Airside/Art/VFX"
RESOURCES.mkdir(parents=True, exist_ok=True)
ART_VFX.mkdir(parents=True, exist_ok=True)


def emit(name: str, guid: str, parts, *, base, accent, step) -> None:
    _mod.OUT_DIR = RESOURCES
    _mod.emit_prefab(name, guid, parts, base=base, accent=accent, step=step)
    # Mirror under Art/VFX for the exact packet path (same YAML).
    src = RESOURCES / f"{name}.prefab"
    dst = ART_VFX / f"{name}.prefab"
    dst.write_text(src.read_text(encoding="utf-8"), encoding="utf-8")
    # Distinct GUID for Art/VFX copy so Unity does not collide with Resources.
    art_guid = guid[:-1] + ("a" if guid[-1] != "a" else "b")
    _mod.write_meta(dst.with_suffix(".prefab.meta"), art_guid)
    print(f"  mirrored {dst.relative_to(REPO)}")


def main() -> None:
    emit(
        "vfx_touchdown_smoke_v01",
        "b0c1d2e3f4a55678901234567890c100",
        [
            ("Smoke L", (-0.75, 0.12, 0.0), (1.1, 0.35, 1.1), "cube"),
            ("Smoke R", (0.75, 0.12, 0.0), (1.1, 0.35, 1.1), "cube"),
            ("Smoke L2", (-0.75, 0.12, -0.15), (1.0, 0.3, 1.0), "cube"),
            ("Smoke R2", (0.75, 0.12, -0.15), (1.0, 0.3, 1.0), "cube"),
        ],
        base=(0.85, 0.85, 0.88),
        accent=(0.78, 0.78, 0.82),
        step=(0.7, 0.7, 0.74),
    )
    emit(
        "vfx_engine_heat_v01",
        "b0c1d2e3f4a55678901234567890c101",
        [
            ("EngineHeat L", (-1.2, 0.4, -1.8), (0.35, 0.35, 0.7), "cube"),
            ("EngineHeat R", (1.2, 0.4, -1.8), (0.35, 0.35, 0.7), "cube"),
        ],
        base=(0.95, 0.75, 0.45),
        accent=(0.9, 0.55, 0.25),
        step=(0.8, 0.4, 0.2),
    )
    emit(
        "vfx_rain_airfield_v01",
        "b0c1d2e3f4a55678901234567890c102",
        [
            ("Drop 0", (0.0, 2.0, 0.0), (0.04, 0.55, 0.04), "cube"),
            ("Drop 1", (2.0, 3.0, 1.0), (0.04, 0.55, 0.04), "cube"),
            ("Drop 2", (-2.0, 2.5, -1.5), (0.04, 0.55, 0.04), "cube"),
            ("Drop 3", (1.5, 4.0, -2.0), (0.04, 0.55, 0.04), "cube"),
            ("Drop 4", (-1.0, 3.5, 2.5), (0.04, 0.55, 0.04), "cube"),
            ("Drop 5", (3.0, 2.2, 0.5), (0.04, 0.55, 0.04), "cube"),
        ],
        base=(0.7, 0.78, 0.88),
        accent=(0.65, 0.72, 0.82),
        step=(0.55, 0.62, 0.72),
    )
    emit(
        "vfx_wet_surface_response_v01",
        "b0c1d2e3f4a55678901234567890c103",
        [
            ("Puddle A", (0.0, 0.02, 0.0), (2.4, 0.04, 1.6), "cube"),
            ("Puddle B", (3.0, 0.02, 1.2), (1.8, 0.04, 1.2), "cube"),
            ("Sheen", (-2.5, 0.015, -1.0), (2.0, 0.03, 2.0), "cube"),
        ],
        base=(0.35, 0.4, 0.45),
        accent=(0.45, 0.5, 0.55),
        step=(0.28, 0.32, 0.36),
    )
    print("Wrote Batch F4 VFX prefabs")


if __name__ == "__main__":
    main()
