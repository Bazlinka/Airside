#!/usr/bin/env python3
"""Emit landside furniture Resources prefabs (decision 0025 items 1+3)."""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    _NS["emit_prefab"](
        "mdl_luggage_trolley_v01",
        "6a7b8c9d0e1f20212223242526374859",
        [
            ("Trolley base", (0.0, 0.12, 0.0), (0.85, 0.08, 0.5), "cube"),
            ("Trolley basket", (0.0, 0.45, 0.0), (0.8, 0.55, 0.45), "cube"),
            ("Trolley handle", (0.0, 0.85, -0.35), (0.7, 0.08, 0.08), "cube"),
            ("Trolley upright L", (-0.32, 0.55, -0.28), (0.06, 0.7, 0.06), "cube"),
            ("Trolley upright R", (0.32, 0.55, -0.28), (0.06, 0.7, 0.06), "cube"),
            ("Trolley wheel FL", (-0.32, 0.1, 0.18), (0.12, 0.12, 0.12), "cube"),
            ("Trolley wheel FR", (0.32, 0.1, 0.18), (0.12, 0.12, 0.12), "cube"),
            ("Trolley wheel RL", (-0.32, 0.1, -0.18), (0.12, 0.12, 0.12), "cube"),
            ("Trolley wheel RR", (0.32, 0.1, -0.18), (0.12, 0.12, 0.12), "cube"),
            ("Trolley stripe", (0.0, 0.55, 0.22), (0.75, 0.12, 0.04), "cube"),
        ],
        base=(0.7, 0.72, 0.75),
        accent=(0.95, 0.75, 0.2),
        step=(0.18, 0.18, 0.2),
    )

    _NS["emit_prefab"](
        "mdl_landside_bench_v01",
        "7b8c9d0e1f202122232425263748596a",
        [
            ("Bench seat", (0.0, 0.35, 0.0), (2.2, 0.12, 0.55), "cube"),
            ("Bench back", (0.0, 0.7, -0.22), (2.2, 0.55, 0.1), "cube"),
            ("Bench leg L", (-0.9, 0.18, 0.0), (0.12, 0.35, 0.45), "cube"),
            ("Bench leg R", (0.9, 0.18, 0.0), (0.12, 0.35, 0.45), "cube"),
            ("Bench arm L", (-1.05, 0.55, 0.0), (0.1, 0.35, 0.5), "cube"),
            ("Bench arm R", (1.05, 0.55, 0.0), (0.1, 0.35, 0.5), "cube"),
            ("Bench stripe", (0.0, 0.42, 0.22), (2.0, 0.04, 0.06), "cube"),
        ],
        base=(0.45, 0.32, 0.18),
        accent=(0.55, 0.4, 0.22),
        step=(0.28, 0.22, 0.14),
    )

    _NS["emit_prefab"](
        "mdl_coast_boat_v01",
        "8c9d0e1f202122232425263748596a7b",
        [
            ("Boat hull", (0.0, 0.0, 0.0), (1.4, 0.55, 4.2), "cube"),
            ("Boat roof", (0.0, 0.45, -0.4), (1.1, 0.7, 1.6), "cube"),
            ("Boat window", (0.0, 0.55, 0.15), (1.0, 0.35, 0.08), "cube"),
            ("Boat mast", (0.0, 1.4, 0.2), (0.1, 2.2, 0.1), "cube"),
            ("Boat stripe", (0.0, 0.15, 0.0), (1.42, 0.08, 3.6), "cube"),
            ("Boat bow", (0.0, 0.1, 2.0), (1.0, 0.35, 0.55), "cube"),
        ],
        base=(0.85, 0.88, 0.9),
        accent=(0.2, 0.45, 0.65),
        step=(0.75, 0.75, 0.72),
    )


if __name__ == "__main__":
    main()
