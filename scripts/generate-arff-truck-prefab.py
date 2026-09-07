#!/usr/bin/env python3
"""Emit mdl_arff_truck_v01 Resources prefab (decision 0025 item 1)."""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    _NS["emit_prefab"](
        "mdl_arff_truck_v01",
        "48596a7b8c9d0e1f2021222324252637",
        [
            ("ARFF chassis", (0.0, 0.55, 0.0), (1.8, 0.55, 4.2), "cube"),
            ("ARFF cab", (0.0, 1.35, 1.2), (1.7, 1.0, 1.6), "cube"),
            ("ARFF cab glass", (0.0, 1.45, 1.85), (1.5, 0.55, 0.12), "cube"),
            ("ARFF tank", (0.0, 1.4, -0.9), (1.55, 1.1, 2.4), "cube"),
            ("ARFF turret", (0.0, 2.15, -0.4), (0.55, 0.45, 0.7), "cube"),
            ("ARFF monitor", (0.0, 2.35, 0.15), (0.18, 0.18, 1.1), "cube"),
            ("ARFF lightbar", (0.0, 1.95, 1.2), (1.4, 0.12, 0.25), "cube"),
            ("ARFF bumper", (0.0, 0.45, 2.2), (1.9, 0.35, 0.25), "cube"),
            ("ARFF wheel FL", (-0.95, 0.35, 1.3), (0.35, 0.7, 0.7), "cylinder"),
            ("ARFF wheel FR", (0.95, 0.35, 1.3), (0.35, 0.7, 0.7), "cylinder"),
            ("ARFF wheel RL", (-0.95, 0.35, -1.3), (0.35, 0.7, 0.7), "cylinder"),
            ("ARFF wheel RR", (0.95, 0.35, -1.3), (0.35, 0.7, 0.7), "cylinder"),
            ("ARFF stripe", (0.0, 0.85, 0.0), (1.85, 0.18, 3.6), "cube"),
        ],
        base=(0.78, 0.18, 0.14),
        accent=(0.95, 0.85, 0.2),
        step=(0.15, 0.15, 0.16),
    )


if __name__ == "__main__":
    main()
