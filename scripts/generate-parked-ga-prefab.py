#!/usr/bin/env python3
"""Emit mdl_parked_ga_v01 Resources prefab (decision 0025 item 1)."""
from __future__ import annotations
from pathlib import Path
_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)

def main() -> None:
    _NS["emit_prefab"](
        "mdl_parked_ga_v01",
        "3748596a7b8c9d0e1f20212223242526",
        [
            ("GA fuselage", (0.0, 0.0, 0.0), (0.55, 0.55, 2.4), "cube"),
            ("GA cabin windows", (0.0, 0.12, 0.35), (0.48, 0.22, 0.9), "cube"),
            ("GA wing", (0.0, 0.05, 0.2), (3.2, 0.08, 0.7), "cube"),
            ("GA tail", (0.0, 0.55, -1.0), (0.1, 0.9, 0.55), "cube"),
            ("GA tailplane", (0.0, 0.45, -1.05), (1.4, 0.06, 0.4), "cube"),
            ("GA prop", (0.0, 0.0, 1.25), (0.06, 0.9, 0.12), "cube"),
            ("GA gear nose", (0.0, -0.35, 0.85), (0.1, 0.35, 0.18), "cube"),
            ("GA gear L", (-0.45, -0.35, -0.15), (0.1, 0.35, 0.2), "cube"),
            ("GA gear R", (0.45, -0.35, -0.15), (0.1, 0.35, 0.2), "cube"),
            ("GA stripe", (0.0, 0.05, 0.1), (0.58, 0.08, 1.6), "cube"),
        ],
        base=(0.9, 0.91, 0.93),
        accent=(0.85, 0.55, 0.2),
        step=(0.2, 0.2, 0.22),
    )

if __name__ == "__main__":
    main()
