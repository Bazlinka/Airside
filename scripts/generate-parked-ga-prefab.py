#!/usr/bin/env python3
"""Emit mdl_parked_ga_v01 Resources prefab (decision 0025 item 1)."""
from __future__ import annotations
from pathlib import Path
_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)

def main() -> None:
    # ~40 parts: canopy glass, struts, spinner, gear scissors, tapered silhouette.
    _NS["emit_prefab"](
        "mdl_parked_ga_v01",
        "3748596a7b8c9d0e1f20212223242526",
        [
            ("GA fuselage", (0.0, 0.0, 0.0), (0.55, 0.55, 2.4), "cube"),
            ("GA nose", (0.0, 0.0, 1.15), (0.42, 0.42, 0.55), "cube"),
            ("GA cowling", (0.0, 0.02, 1.35), (0.48, 0.48, 0.35), "cylinder"),
            ("GA cabin windows", (0.0, 0.18, 0.35), (0.5, 0.28, 0.95), "cube"),
            ("GA canopy glass", (0.0, 0.32, 0.45), (0.42, 0.22, 0.7), "cube"),
            ("GA canopy frame", (0.0, 0.32, 0.45), (0.46, 0.04, 0.72), "cube"),
            ("GA wing", (0.0, 0.08, 0.15), (3.2, 0.08, 0.7), "cube"),
            ("GA wing tip L", (-1.55, 0.08, 0.15), (0.2, 0.07, 0.55), "cube"),
            ("GA wing tip R", (1.55, 0.08, 0.15), (0.2, 0.07, 0.55), "cube"),
            ("GA wing root", (0.0, 0.06, 0.15), (0.7, 0.12, 0.75), "cube"),
            ("GA wing strut L", (-0.9, -0.12, 0.15), (0.06, 0.42, 0.06), "cube"),
            ("GA wing strut R", (0.9, -0.12, 0.15), (0.06, 0.42, 0.06), "cube"),
            ("GA flap L", (-0.9, 0.05, -0.15), (1.0, 0.05, 0.22), "cube"),
            ("GA flap R", (0.9, 0.05, -0.15), (1.0, 0.05, 0.22), "cube"),
            ("GA aileron L", (-1.35, 0.05, 0.05), (0.55, 0.04, 0.2), "cube"),
            ("GA aileron R", (1.35, 0.05, 0.05), (0.55, 0.04, 0.2), "cube"),
            ("GA tail", (0.0, 0.55, -1.0), (0.1, 0.9, 0.55), "cube"),
            ("GA rudder", (0.0, 0.55, -1.2), (0.06, 0.7, 0.25), "cube"),
            ("GA tailplane", (0.0, 0.4, -1.05), (1.4, 0.06, 0.4), "cube"),
            ("GA elevator", (0.0, 0.4, -1.2), (1.2, 0.04, 0.18), "cube"),
            ("GA spinner", (0.0, 0.0, 1.55), (0.22, 0.22, 0.28), "cylinder"),
            ("GA prop blade A", (0.0, 0.0, 1.48), (0.06, 0.95, 0.1), "cube"),
            ("GA prop blade B", (0.0, 0.0, 1.48), (0.95, 0.06, 0.1), "cube"),
            ("GA prop hub", (0.0, 0.0, 1.42), (0.14, 0.14, 0.14), "cylinder"),
            ("GA gear nose", (0.0, -0.35, 0.85), (0.08, 0.35, 0.08), "cube"),
            ("GA gear nose wheel", (0.0, -0.52, 0.85), (0.12, 0.12, 0.08), "cylinder"),
            ("GA gear L", (-0.5, -0.35, -0.15), (0.08, 0.35, 0.08), "cube"),
            ("GA gear R", (0.5, -0.35, -0.15), (0.08, 0.35, 0.08), "cube"),
            ("GA gear scissor L", (-0.5, -0.2, -0.05), (0.05, 0.28, 0.2), "cube"),
            ("GA gear scissor R", (0.5, -0.2, -0.05), (0.05, 0.28, 0.2), "cube"),
            ("GA gear wheel L", (-0.5, -0.52, -0.15), (0.14, 0.14, 0.1), "cylinder"),
            ("GA gear wheel R", (0.5, -0.52, -0.15), (0.14, 0.14, 0.1), "cylinder"),
            ("GA stripe", (0.0, 0.05, 0.1), (0.58, 0.08, 1.6), "cube"),
            ("GA stripe upper", (0.0, 0.22, 0.05), (0.52, 0.05, 1.3), "cube"),
            ("GA antenna", (0.0, 0.55, 0.1), (0.04, 0.35, 0.04), "cube"),
            ("GA pitot", (0.15, 0.05, 0.95), (0.04, 0.04, 0.25), "cube"),
            ("GA exhaust", (-0.2, -0.15, 1.05), (0.08, 0.08, 0.3), "cylinder"),
            ("GA fairing L", (-0.35, -0.15, -0.15), (0.2, 0.18, 0.35), "cube"),
            ("GA fairing R", (0.35, -0.15, -0.15), (0.2, 0.18, 0.35), "cube"),
            ("GA door", (0.28, 0.05, 0.35), (0.04, 0.4, 0.55), "cube"),
        ],
        base=(0.9, 0.91, 0.93),
        accent=(0.85, 0.55, 0.2),
        step=(0.2, 0.2, 0.22),
    )

if __name__ == "__main__":
    main()
