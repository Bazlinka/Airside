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
            ("Hydrant pad", (0.0, 0.02, 0.0), (0.75, 0.04, 0.75), "cube"),
            ("Hydrant barrel", (0.0, 0.55, 0.0), (0.4, 0.7, 0.4), "cube"),
            ("Hydrant barrel rib", (0.0, 0.55, 0.0), (0.44, 0.08, 0.44), "cube"),
            ("Hydrant cap", (0.0, 0.95, 0.0), (0.45, 0.18, 0.45), "cube"),
            ("Hydrant stem", (0.0, 1.1, 0.0), (0.12, 0.2, 0.12), "cube"),
            ("Hydrant nut", (0.0, 1.22, 0.0), (0.16, 0.08, 0.16), "cube"),
            ("Hydrant outlet L", (-0.28, 0.55, 0.0), (0.2, 0.18, 0.18), "cube"),
            ("Hydrant outlet R", (0.28, 0.55, 0.0), (0.2, 0.18, 0.18), "cube"),
            ("Hydrant outlet F", (0.0, 0.55, 0.28), (0.18, 0.18, 0.2), "cube"),
            ("Hydrant chain", (0.18, 0.85, 0.18), (0.08, 0.25, 0.08), "cube"),
            ("Hydrant stripe", (0.0, 0.55, 0.0), (0.42, 0.12, 0.42), "cube"),
            ("Hydrant stripe 2", (0.0, 0.75, 0.0), (0.42, 0.08, 0.42), "cube"),
            ("Hydrant label", (0.0, 0.4, 0.22), (0.28, 0.12, 0.04), "cube"),
            ("Hydrant bolt A", (-0.22, 0.08, -0.22), (0.08, 0.06, 0.08), "cube"),
            ("Hydrant bolt B", (0.22, 0.08, -0.22), (0.08, 0.06, 0.08), "cube"),
            ("Hydrant bolt C", (-0.22, 0.08, 0.22), (0.08, 0.06, 0.08), "cube"),
            ("Hydrant bolt D", (0.22, 0.08, 0.22), (0.08, 0.06, 0.08), "cube"),
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
            ("Cabinet mullion", (0.0, 0.75, 0.21), (0.04, 0.7, 0.03), "cube"),
            ("Cabinet handle", (0.18, 0.7, 0.22), (0.06, 0.18, 0.06), "cube"),
            ("Cabinet hinge T", (-0.22, 1.1, 0.18), (0.06, 0.08, 0.08), "cube"),
            ("Cabinet hinge B", (-0.22, 0.35, 0.18), (0.06, 0.08, 0.08), "cube"),
            ("Cabinet stripe", (0.0, 1.15, 0.2), (0.5, 0.1, 0.05), "cube"),
            ("Cabinet stripe 2", (0.0, 0.3, 0.2), (0.5, 0.08, 0.05), "cube"),
            ("Cabinet base", (0.0, 0.08, 0.0), (0.6, 0.12, 0.4), "cube"),
            ("Cabinet roof", (0.0, 1.35, 0.0), (0.62, 0.08, 0.42), "cube"),
            ("Cabinet extinguisher", (0.0, 0.7, 0.0), (0.22, 0.7, 0.22), "cylinder"),
            ("Cabinet hose", (0.12, 0.95, 0.08), (0.08, 0.08, 0.25), "cube"),
            ("Cabinet latch", (0.2, 0.85, 0.22), (0.05, 0.1, 0.05), "cube"),
            ("Cabinet sign", (0.0, 1.25, 0.2), (0.35, 0.12, 0.03), "cube"),
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
            ("Bin lid hinge", (0.0, 0.88, -0.28), (0.7, 0.06, 0.08), "cube"),
            ("Bin stripe", (0.0, 0.55, 0.28), (0.65, 0.12, 0.04), "cube"),
            ("Bin stripe 2", (0.0, 0.35, 0.28), (0.65, 0.08, 0.04), "cube"),
            ("Bin handle", (0.0, 0.95, -0.1), (0.35, 0.08, 0.08), "cube"),
            ("Bin grip L", (-0.32, 0.7, 0.0), (0.06, 0.35, 0.08), "cube"),
            ("Bin grip R", (0.32, 0.7, 0.0), (0.06, 0.35, 0.08), "cube"),
            ("Bin wheel L", (-0.28, 0.12, -0.2), (0.12, 0.12, 0.12), "cube"),
            ("Bin wheel R", (0.28, 0.12, -0.2), (0.12, 0.12, 0.12), "cube"),
            ("Bin axle", (0.0, 0.12, -0.2), (0.55, 0.05, 0.05), "cube"),
            ("Bin label", (0.0, 0.65, 0.29), (0.4, 0.18, 0.03), "cube"),
            ("Bin rim", (0.0, 0.82, 0.0), (0.72, 0.05, 0.57), "cube"),
            ("Bin foot L", (-0.28, 0.05, 0.2), (0.12, 0.08, 0.12), "cube"),
            ("Bin foot R", (0.28, 0.05, 0.2), (0.12, 0.08, 0.12), "cube"),
        ],
        base=(0.95, 0.75, 0.15),
        accent=(0.2, 0.22, 0.25),
        step=(0.15, 0.15, 0.16),
    )


if __name__ == "__main__":
    main()
