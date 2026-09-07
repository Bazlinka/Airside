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
            ("ARFF chassis rail L", (-0.85, 0.7, 0.0), (0.08, 0.18, 3.8), "cube"),
            ("ARFF chassis rail R", (0.85, 0.7, 0.0), (0.08, 0.18, 3.8), "cube"),
            ("ARFF cab", (0.0, 1.35, 1.2), (1.7, 1.0, 1.6), "cube"),
            ("ARFF cab glass", (0.0, 1.45, 1.85), (1.5, 0.55, 0.12), "cube"),
            ("ARFF cab side glass L", (-0.88, 1.45, 1.2), (0.06, 0.45, 0.9), "cube"),
            ("ARFF cab side glass R", (0.88, 1.45, 1.2), (0.06, 0.45, 0.9), "cube"),
            ("ARFF mirror L", (-0.95, 1.55, 1.9), (0.12, 0.18, 0.08), "cube"),
            ("ARFF mirror R", (0.95, 1.55, 1.9), (0.12, 0.18, 0.08), "cube"),
            ("ARFF tank", (0.0, 1.4, -0.9), (1.55, 1.1, 2.4), "cube"),
            ("ARFF tank band", (0.0, 1.55, -0.9), (1.62, 0.12, 2.2), "cube"),
            ("ARFF tank hatch", (0.0, 2.0, -0.5), (0.45, 0.12, 0.45), "cube"),
            ("ARFF turret", (0.0, 2.15, -0.4), (0.55, 0.45, 0.7), "cube"),
            ("ARFF monitor", (0.0, 2.35, 0.15), (0.18, 0.18, 1.1), "cube"),
            ("ARFF monitor nozzle", (0.0, 2.35, 0.75), (0.14, 0.14, 0.25), "cube"),
            ("ARFF lightbar", (0.0, 1.95, 1.2), (1.4, 0.12, 0.25), "cube"),
            ("ARFF lightbar lens", (0.0, 1.98, 1.2), (1.2, 0.08, 0.18), "cube"),
            ("ARFF bumper", (0.0, 0.45, 2.2), (1.9, 0.35, 0.25), "cube"),
            ("ARFF bumper plate", (0.0, 0.55, 2.32), (1.6, 0.22, 0.08), "cube"),
            ("ARFF grille", (0.0, 1.05, 2.0), (1.2, 0.45, 0.1), "cube"),
            ("ARFF headlight L", (-0.7, 0.85, 2.05), (0.22, 0.16, 0.1), "cube"),
            ("ARFF headlight R", (0.7, 0.85, 2.05), (0.22, 0.16, 0.1), "cube"),
            ("ARFF taillight L", (-0.7, 0.9, -2.05), (0.2, 0.14, 0.08), "cube"),
            ("ARFF taillight R", (0.7, 0.9, -2.05), (0.2, 0.14, 0.08), "cube"),
            ("ARFF step L", (-0.95, 0.55, 1.4), (0.25, 0.12, 0.45), "cube"),
            ("ARFF step R", (0.95, 0.55, 1.4), (0.25, 0.12, 0.45), "cube"),
            ("ARFF hose reel", (0.0, 1.15, -2.0), (0.7, 0.55, 0.45), "cylinder"),
            ("ARFF wheel FL", (-0.95, 0.35, 1.3), (0.35, 0.7, 0.7), "cylinder"),
            ("ARFF wheel FR", (0.95, 0.35, 1.3), (0.35, 0.7, 0.7), "cylinder"),
            ("ARFF wheel RL", (-0.95, 0.35, -1.3), (0.35, 0.7, 0.7), "cylinder"),
            ("ARFF wheel RR", (0.95, 0.35, -1.3), (0.35, 0.7, 0.7), "cylinder"),
            ("ARFF rim FL", (-0.95, 0.35, 1.3), (0.18, 0.45, 0.45), "cylinder"),
            ("ARFF rim FR", (0.95, 0.35, 1.3), (0.18, 0.45, 0.45), "cylinder"),
            ("ARFF rim RL", (-0.95, 0.35, -1.3), (0.18, 0.45, 0.45), "cylinder"),
            ("ARFF rim RR", (0.95, 0.35, -1.3), (0.18, 0.45, 0.45), "cylinder"),
            ("ARFF stripe", (0.0, 0.85, 0.0), (1.85, 0.18, 3.6), "cube"),
            ("ARFF stripe upper", (0.0, 1.75, -0.4), (1.6, 0.1, 2.0), "cube"),
        ],
        base=(0.78, 0.18, 0.14),
        accent=(0.95, 0.85, 0.2),
        step=(0.15, 0.15, 0.16),
    )


if __name__ == "__main__":
    main()
