#!/usr/bin/env python3
"""Emit apron safety Resources prefabs (decision 0025 items 1+3)."""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    _NS["emit_prefab"](
        "mdl_fire_hydrant_v01",
        "9d0e1f202122232425263748596a7b8c",
        [
            ("Hydrant base", (0.0, 0.12, 0.0), (0.55, 0.2, 0.55), "cube"),
            ("Hydrant barrel", (0.0, 0.55, 0.0), (0.4, 0.7, 0.4), "cube"),
            ("Hydrant cap", (0.0, 0.95, 0.0), (0.45, 0.18, 0.45), "cube"),
            ("Hydrant outlet L", (-0.28, 0.55, 0.0), (0.2, 0.18, 0.18), "cube"),
            ("Hydrant outlet R", (0.28, 0.55, 0.0), (0.2, 0.18, 0.18), "cube"),
            ("Hydrant stripe", (0.0, 0.55, 0.0), (0.42, 0.12, 0.42), "cube"),
        ],
        base=(0.78, 0.18, 0.14),
        accent=(0.95, 0.85, 0.2),
        step=(0.35, 0.36, 0.38),
    )

    _NS["emit_prefab"](
        "mdl_extinguisher_cabinet_v01",
        "0e1f202122232425263748596a7b8c9d",
        [
            ("Cabinet body", (0.0, 0.7, 0.0), (0.55, 1.2, 0.35), "cube"),
            ("Cabinet door", (0.0, 0.7, 0.18), (0.48, 1.05, 0.06), "cube"),
            ("Cabinet glass", (0.0, 0.75, 0.2), (0.35, 0.7, 0.04), "cube"),
            ("Cabinet handle", (0.18, 0.7, 0.22), (0.06, 0.18, 0.06), "cube"),
            ("Cabinet stripe", (0.0, 1.15, 0.2), (0.5, 0.1, 0.05), "cube"),
            ("Cabinet base", (0.0, 0.08, 0.0), (0.6, 0.12, 0.4), "cube"),
        ],
        base=(0.82, 0.2, 0.16),
        accent=(0.95, 0.85, 0.2),
        step=(0.25, 0.26, 0.28),
    )

    _NS["emit_prefab"](
        "mdl_fod_bin_v01",
        "1f202122232425263748596a7b8c9d0e",
        [
            ("Bin body", (0.0, 0.45, 0.0), (0.7, 0.75, 0.55), "cube"),
            ("Bin lid", (0.0, 0.88, 0.0), (0.75, 0.1, 0.6), "cube"),
            ("Bin stripe", (0.0, 0.55, 0.28), (0.65, 0.12, 0.04), "cube"),
            ("Bin handle", (0.0, 0.95, -0.1), (0.35, 0.08, 0.08), "cube"),
            ("Bin wheel L", (-0.28, 0.12, -0.2), (0.12, 0.12, 0.12), "cube"),
            ("Bin wheel R", (0.28, 0.12, -0.2), (0.12, 0.12, 0.12), "cube"),
        ],
        base=(0.95, 0.75, 0.15),
        accent=(0.2, 0.22, 0.25),
        step=(0.15, 0.15, 0.16),
    )


if __name__ == "__main__":
    main()
