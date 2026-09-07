#!/usr/bin/env python3
"""Emit mdl_hangar_bay_props_v01 Resources prefab (decision 0025 item 1+3)."""
from __future__ import annotations
from pathlib import Path
_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)

def main() -> None:
    _NS["emit_prefab"](
        "mdl_hangar_bay_props_v01",
        "48596a7b8c9d0e1f2021222324252627",
        [
            ("Workbench top", (0.0, 0.85, 0.0), (2.4, 0.12, 0.9), "cube"),
            ("Workbench leg L", (-1.0, 0.4, 0.0), (0.12, 0.8, 0.8), "cube"),
            ("Workbench leg R", (1.0, 0.4, 0.0), (0.12, 0.8, 0.8), "cube"),
            ("Shelf frame", (-2.2, 1.1, -0.1), (0.9, 1.8, 0.45), "cube"),
            ("Shelf board mid", (-2.2, 1.0, -0.1), (0.85, 0.08, 0.4), "cube"),
            ("Shelf board top", (-2.2, 1.55, -0.1), (0.85, 0.08, 0.4), "cube"),
            ("Tool cart body", (1.8, 0.55, 0.6), (0.9, 0.7, 0.7), "cube"),
            ("Tool cart drawer", (1.8, 0.7, 0.6), (0.85, 0.2, 0.65), "cube"),
            ("Oil drum", (-1.6, 0.55, 0.9), (0.55, 1.1, 0.55), "cylinder"),
            ("Crate stack", (2.3, 0.45, -0.5), (0.7, 0.9, 0.55), "cube"),
        ],
        base=(0.45, 0.42, 0.38),
        accent=(0.85, 0.55, 0.18),
        step=(0.25, 0.25, 0.28),
    )

if __name__ == "__main__":
    main()
