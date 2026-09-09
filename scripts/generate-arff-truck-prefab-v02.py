#!/usr/bin/env python3
"""Emit mdl_arff_truck_v02 Resources prefab — denser red/white rescue appliance."""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    _NS["emit_prefab"](
        "mdl_arff_truck_v02",
        "c3d4e5f60718293a4b5c6d7e8f901a2b",
        [
            ("ARFF chassis", (0.0, 0.55, 0.0), (1.8, 0.55, 4.2), "cube"),
            ("ARFF chassis rail L", (-0.85, 0.7, 0.0), (0.08, 0.18, 3.8), "cube"),
            ("ARFF chassis rail R", (0.85, 0.7, 0.0), (0.08, 0.18, 3.8), "cube"),
            ("ARFF chassis cross", (0.0, 0.68, -0.4), (1.7, 0.08, 0.1), "cube"),
            ("ARFF cab", (0.0, 1.35, 1.2), (1.7, 1.0, 1.6), "cube"),
            ("ARFF cab roof panel", (0.0, 1.92, 1.2), (1.55, 0.08, 1.4), "cube"),
            ("ARFF cab glass", (0.0, 1.45, 1.85), (1.5, 0.55, 0.12), "cube"),
            ("ARFF cab side glass L", (-0.88, 1.45, 1.2), (0.06, 0.45, 0.9), "cube"),
            ("ARFF cab side glass R", (0.88, 1.45, 1.2), (0.06, 0.45, 0.9), "cube"),
            ("ARFF mirror L", (-0.95, 1.55, 1.9), (0.12, 0.18, 0.08), "cube"),
            ("ARFF mirror R", (0.95, 1.55, 1.9), (0.12, 0.18, 0.08), "cube"),
            ("ARFF door L", (-0.9, 1.25, 1.55), (0.08, 0.85, 0.7), "cube"),
            ("ARFF door R", (0.9, 1.25, 1.55), (0.08, 0.85, 0.7), "cube"),
            ("ARFF door panel L", (-0.93, 1.3, 1.55), (0.04, 0.7, 0.55), "cube"),
            ("ARFF door panel R", (0.93, 1.3, 1.55), (0.04, 0.7, 0.55), "cube"),
            ("ARFF door handle L", (-0.95, 1.2, 1.7), (0.06, 0.18, 0.08), "cube"),
            ("ARFF door handle R", (0.95, 1.2, 1.7), (0.06, 0.18, 0.08), "cube"),
            ("ARFF tank", (0.0, 1.4, -0.9), (1.55, 1.1, 2.4), "cube"),
            ("ARFF tank band", (0.0, 1.55, -0.9), (1.62, 0.12, 2.2), "cube"),
            ("ARFF tank hatch", (0.0, 2.0, -0.5), (0.45, 0.12, 0.45), "cube"),
            ("ARFF foam tank", (0.0, 1.55, -1.6), (1.1, 0.45, 0.7), "cube"),
            ("ARFF foam tank band", (0.0, 1.7, -1.6), (1.15, 0.08, 0.55), "cube"),
            ("ARFF turret", (0.0, 2.15, -0.4), (0.55, 0.45, 0.7), "cube"),
            ("ARFF monitor", (0.0, 2.35, 0.15), (0.18, 0.18, 1.1), "cube"),
            ("ARFF monitor nozzle", (0.0, 2.35, 0.75), (0.14, 0.14, 0.25), "cube"),
            # Blink code looks for "ARFF lightbar" / "ARFF beacon" substrings.
            ("ARFF lightbar", (0.0, 1.98, 1.2), (1.45, 0.14, 0.28), "cube"),
            ("ARFF lightbar lens", (0.0, 2.02, 1.2), (1.25, 0.08, 0.2), "cube"),
            ("ARFF lightbar rear", (0.0, 2.05, -0.2), (1.1, 0.1, 0.22), "cube"),
            ("ARFF beacon", (0.0, 2.4, -0.9), (0.22, 0.22, 0.22), "cylinder"),
            ("ARFF bumper", (0.0, 0.45, 2.2), (1.9, 0.35, 0.25), "cube"),
            ("ARFF bumper plate", (0.0, 0.55, 2.32), (1.6, 0.22, 0.08), "cube"),
            ("ARFF bumper chevron L", (-0.45, 0.55, 2.36), (0.55, 0.16, 0.05), "cube"),
            ("ARFF bumper chevron R", (0.45, 0.55, 2.36), (0.55, 0.16, 0.05), "cube"),
            ("ARFF grille", (0.0, 1.05, 2.0), (1.2, 0.45, 0.1), "cube"),
            ("ARFF grille bar A", (0.0, 1.18, 2.04), (1.05, 0.04, 0.04), "cube"),
            ("ARFF grille bar B", (0.0, 1.08, 2.04), (1.05, 0.04, 0.04), "cube"),
            ("ARFF grille bar C", (0.0, 0.98, 2.04), (1.05, 0.04, 0.04), "cube"),
            ("ARFF grille bar D", (0.0, 0.88, 2.04), (1.05, 0.04, 0.04), "cube"),
            ("ARFF grille mesh L", (-0.35, 1.05, 2.05), (0.04, 0.38, 0.03), "cube"),
            ("ARFF grille mesh R", (0.35, 1.05, 2.05), (0.04, 0.38, 0.03), "cube"),
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
            ("ARFF wiper", (0.0, 1.7, 1.9), (0.7, 0.04, 0.04), "cube"),
            ("ARFF roof rack", (0.0, 2.05, -0.9), (1.2, 0.08, 1.6), "cube"),
            ("ARFF side locker L", (-0.95, 1.25, -0.6), (0.2, 0.7, 1.2), "cube"),
            ("ARFF side locker R", (0.95, 1.25, -0.6), (0.2, 0.7, 1.2), "cube"),
            ("ARFF side locker door L", (-1.02, 1.25, -0.6), (0.04, 0.55, 1.0), "cube"),
            ("ARFF side locker door R", (1.02, 1.25, -0.6), (0.04, 0.55, 1.0), "cube"),
            ("ARFF locker latch L", (-1.05, 1.25, -0.2), (0.04, 0.1, 0.08), "cube"),
            ("ARFF locker latch R", (1.05, 1.25, -0.2), (0.04, 0.1, 0.08), "cube"),
            ("ARFF pump panel", (0.55, 1.15, -2.15), (0.45, 0.55, 0.12), "cube"),
            ("ARFF rear step", (0.0, 0.45, -2.15), (1.0, 0.15, 0.35), "cube"),
            ("ARFF mudflap L", (-0.85, 0.35, -1.75), (0.08, 0.35, 0.25), "cube"),
            ("ARFF mudflap R", (0.85, 0.35, -1.75), (0.08, 0.35, 0.25), "cube"),
            ("ARFF number plate", (0.0, 0.55, 2.38), (0.55, 0.14, 0.04), "cube"),
        ],
        base=(0.78, 0.18, 0.14),
        accent=(0.95, 0.92, 0.9),
        step=(0.15, 0.15, 0.16),
    )
    print("Wrote mdl_arff_truck_v02 Resources prefab")


if __name__ == "__main__":
    main()
