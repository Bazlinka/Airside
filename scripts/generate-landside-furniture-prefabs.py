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
            ("Trolley mesh L", (-0.38, 0.5, 0.0), (0.04, 0.45, 0.4), "cube"),
            ("Trolley mesh R", (0.38, 0.5, 0.0), (0.04, 0.45, 0.4), "cube"),
            ("Trolley mesh front", (0.0, 0.5, 0.22), (0.72, 0.45, 0.04), "cube"),
            ("Trolley handle", (0.0, 0.85, -0.35), (0.7, 0.08, 0.08), "cube"),
            ("Trolley handle grip", (0.0, 0.85, -0.42), (0.55, 0.1, 0.1), "cylinder"),
            ("Trolley upright L", (-0.32, 0.55, -0.28), (0.06, 0.7, 0.06), "cube"),
            ("Trolley upright R", (0.32, 0.55, -0.28), (0.06, 0.7, 0.06), "cube"),
            ("Trolley brace", (0.0, 0.65, -0.28), (0.6, 0.05, 0.05), "cube"),
            ("Trolley caster FL", (-0.32, 0.08, 0.18), (0.1, 0.08, 0.1), "cube"),
            ("Trolley caster FR", (0.32, 0.08, 0.18), (0.1, 0.08, 0.1), "cube"),
            ("Trolley caster RL", (-0.32, 0.08, -0.18), (0.1, 0.08, 0.1), "cube"),
            ("Trolley caster RR", (0.32, 0.08, -0.18), (0.1, 0.08, 0.1), "cube"),
            ("Trolley wheel FL", (-0.32, 0.1, 0.18), (0.12, 0.12, 0.12), "cube"),
            ("Trolley wheel FR", (0.32, 0.1, 0.18), (0.12, 0.12, 0.12), "cube"),
            ("Trolley wheel RL", (-0.32, 0.1, -0.18), (0.12, 0.12, 0.12), "cube"),
            ("Trolley wheel RR", (0.32, 0.1, -0.18), (0.12, 0.12, 0.12), "cube"),
            ("Trolley stripe", (0.0, 0.55, 0.22), (0.75, 0.12, 0.04), "cube"),
            ("Trolley bag A", (-0.15, 0.55, 0.0), (0.28, 0.35, 0.22), "cube"),
            ("Trolley bag B", (0.18, 0.5, -0.05), (0.25, 0.3, 0.2), "cube"),
            ("Trolley nest", (0.0, 0.78, -0.1), (0.35, 0.12, 0.25), "cube"),
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
            ("Bench seat slat A", (0.0, 0.42, 0.12), (2.1, 0.04, 0.12), "cube"),
            ("Bench seat slat B", (0.0, 0.42, 0.0), (2.1, 0.04, 0.12), "cube"),
            ("Bench seat slat C", (0.0, 0.42, -0.12), (2.1, 0.04, 0.12), "cube"),
            ("Bench back", (0.0, 0.7, -0.22), (2.2, 0.55, 0.1), "cube"),
            ("Bench back slat A", (0.0, 0.85, -0.18), (2.1, 0.12, 0.04), "cube"),
            ("Bench back slat B", (0.0, 0.65, -0.18), (2.1, 0.12, 0.04), "cube"),
            ("Bench back slat C", (0.0, 0.5, -0.18), (2.1, 0.12, 0.04), "cube"),
            ("Bench leg L", (-0.9, 0.18, 0.0), (0.12, 0.35, 0.45), "cube"),
            ("Bench leg R", (0.9, 0.18, 0.0), (0.12, 0.35, 0.45), "cube"),
            ("Bench leg mid", (0.0, 0.18, 0.0), (0.1, 0.3, 0.4), "cube"),
            ("Bench arm L", (-1.05, 0.55, 0.0), (0.1, 0.35, 0.5), "cube"),
            ("Bench arm R", (1.05, 0.55, 0.0), (0.1, 0.35, 0.5), "cube"),
            ("Bench arm cap L", (-1.05, 0.75, 0.0), (0.14, 0.08, 0.52), "cube"),
            ("Bench arm cap R", (1.05, 0.75, 0.0), (0.14, 0.08, 0.52), "cube"),
            ("Bench foot L", (-0.9, 0.04, 0.0), (0.22, 0.06, 0.55), "cube"),
            ("Bench foot R", (0.9, 0.04, 0.0), (0.22, 0.06, 0.55), "cube"),
            ("Bench bolt A", (-0.7, 0.3, 0.2), (0.06, 0.06, 0.06), "cube"),
            ("Bench bolt B", (0.7, 0.3, 0.2), (0.06, 0.06, 0.06), "cube"),
            ("Bench stripe", (0.0, 0.42, 0.22), (2.0, 0.04, 0.06), "cube"),
            ("Bench end L", (-1.15, 0.45, 0.0), (0.08, 0.55, 0.5), "cube"),
            ("Bench end R", (1.15, 0.45, 0.0), (0.08, 0.55, 0.5), "cube"),
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
            ("Boat gunwale", (0.0, 0.28, 0.05), (1.48, 0.08, 3.9), "cube"),
            ("Boat bow", (0.0, 0.12, 2.05), (1.05, 0.4, 0.7), "cube"),
            ("Boat transom", (0.0, 0.18, -2.05), (1.25, 0.45, 0.18), "cube"),
            ("Boat roof", (0.0, 0.45, -0.4), (1.1, 0.7, 1.6), "cube"),
            ("Boat window", (0.0, 0.55, 0.15), (1.0, 0.35, 0.08), "cube"),
            ("Boat mast", (0.0, 1.4, 0.2), (0.1, 2.2, 0.1), "cube"),
            ("Boat boom", (0.0, 0.95, -0.35), (0.08, 0.08, 1.6), "cube"),
            ("Boat cabin door", (0.45, 0.4, -0.55), (0.08, 0.45, 0.35), "cube"),
            ("Boat rail L", (-0.72, 0.42, 0.2), (0.05, 0.08, 3.2), "cube"),
            ("Boat rail R", (0.72, 0.42, 0.2), (0.05, 0.08, 3.2), "cube"),
            ("Boat outboard", (0.0, 0.05, -2.35), (0.35, 0.45, 0.55), "cube"),
            ("Boat stripe", (0.0, 0.15, 0.0), (1.42, 0.08, 3.6), "cube"),
            ("Boat cleat L", (-0.55, 0.38, 1.4), (0.12, 0.08, 0.18), "cube"),
            ("Boat cleat R", (0.55, 0.38, 1.4), (0.12, 0.08, 0.18), "cube"),
            ("Boat cabin roof lip", (0.0, 0.82, -0.4), (1.15, 0.06, 1.65), "cube"),
        ],
        base=(0.85, 0.88, 0.9),
        accent=(0.2, 0.45, 0.65),
        step=(0.75, 0.75, 0.72),
    )


if __name__ == "__main__":
    main()
