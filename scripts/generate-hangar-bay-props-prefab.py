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
            ("Workbench apron", (0.0, 0.78, 0.35), (2.2, 0.06, 0.18), "cube"),
            ("Workbench vise", (0.85, 0.98, 0.15), (0.35, 0.28, 0.35), "cube"),
            ("Workbench vise jaw", (0.85, 1.05, 0.28), (0.28, 0.12, 0.12), "cube"),
            ("Workbench leg L", (-1.0, 0.4, 0.0), (0.12, 0.8, 0.8), "cube"),
            ("Workbench leg R", (1.0, 0.4, 0.0), (0.12, 0.8, 0.8), "cube"),
            ("Workbench brace", (0.0, 0.35, 0.0), (1.9, 0.08, 0.08), "cube"),
            ("Workbench drawer", (0.0, 0.55, 0.2), (1.6, 0.25, 0.55), "cube"),
            ("Workbench handle", (0.0, 0.55, 0.48), (0.35, 0.06, 0.06), "cube"),
            ("Shelf frame", (-2.2, 1.1, -0.1), (0.9, 1.8, 0.45), "cube"),
            ("Shelf board low", (-2.2, 0.45, -0.1), (0.85, 0.08, 0.4), "cube"),
            ("Shelf board mid", (-2.2, 1.0, -0.1), (0.85, 0.08, 0.4), "cube"),
            ("Shelf board top", (-2.2, 1.55, -0.1), (0.85, 0.08, 0.4), "cube"),
            ("Shelf upright L", (-2.55, 1.1, -0.1), (0.08, 1.8, 0.08), "cube"),
            ("Shelf upright R", (-1.85, 1.1, -0.1), (0.08, 1.8, 0.08), "cube"),
            ("Shelf bin A", (-2.35, 1.15, -0.05), (0.28, 0.22, 0.28), "cube"),
            ("Shelf bin B", (-2.05, 1.15, -0.05), (0.28, 0.22, 0.28), "cube"),
            ("Shelf can", (-2.2, 1.7, -0.05), (0.18, 0.28, 0.18), "cylinder"),
            ("Tool cart body", (1.8, 0.55, 0.6), (0.9, 0.7, 0.7), "cube"),
            ("Tool cart drawer", (1.8, 0.7, 0.6), (0.85, 0.2, 0.65), "cube"),
            ("Tool cart drawer 2", (1.8, 0.45, 0.6), (0.85, 0.18, 0.65), "cube"),
            ("Tool cart handle", (1.8, 1.0, 0.3), (0.7, 0.08, 0.08), "cube"),
            ("Tool cart wheel FL", (1.5, 0.12, 0.85), (0.16, 0.16, 0.12), "cylinder"),
            ("Tool cart wheel FR", (2.1, 0.12, 0.85), (0.16, 0.16, 0.12), "cylinder"),
            ("Tool cart wheel RL", (1.5, 0.12, 0.35), (0.16, 0.16, 0.12), "cylinder"),
            ("Tool cart wheel RR", (2.1, 0.12, 0.35), (0.16, 0.16, 0.12), "cylinder"),
            ("Oil drum", (-1.6, 0.55, 0.9), (0.55, 1.1, 0.55), "cylinder"),
            ("Oil drum rim", (-1.6, 1.1, 0.9), (0.58, 0.06, 0.58), "cylinder"),
            ("Oil drum bung", (-1.6, 1.15, 0.9), (0.12, 0.08, 0.12), "cylinder"),
            ("Oil drum B", (-0.9, 0.45, 1.1), (0.45, 0.9, 0.45), "cylinder"),
            ("Crate stack", (2.3, 0.45, -0.5), (0.7, 0.9, 0.55), "cube"),
            ("Crate strap", (2.3, 0.55, -0.5), (0.72, 0.06, 0.57), "cube"),
            ("Crate top", (2.3, 0.95, -0.5), (0.65, 0.2, 0.5), "cube"),
            ("Tire rack", (-0.2, 0.7, -1.0), (1.4, 1.2, 0.35), "cube"),
            ("Tire A", (-0.55, 0.55, -1.0), (0.45, 0.45, 0.2), "cylinder"),
            ("Tire B", (0.15, 0.55, -1.0), (0.45, 0.45, 0.2), "cylinder"),
            ("Tire C", (-0.2, 1.05, -1.0), (0.45, 0.45, 0.2), "cylinder"),
            ("Fire extinguisher", (0.9, 0.55, -0.9), (0.22, 0.7, 0.22), "cylinder"),
            ("Extinguisher hose", (0.95, 0.85, -0.78), (0.08, 0.08, 0.25), "cube"),
            ("Extinguisher sign", (0.9, 1.05, -0.9), (0.28, 0.18, 0.04), "cube"),
            ("Parts bin", (0.4, 0.35, 0.7), (0.55, 0.4, 0.4), "cube"),
            ("Parts bin lid", (0.4, 0.58, 0.7), (0.58, 0.06, 0.42), "cube"),
            ("Floor stain", (0.0, 0.02, 0.4), (1.8, 0.02, 1.2), "cube"),
            ("Jack stand", (-0.6, 0.35, -0.3), (0.35, 0.55, 0.35), "cube"),
            ("Jack pad", (-0.6, 0.65, -0.3), (0.4, 0.08, 0.4), "cube"),
            ("Pegboard", (-2.2, 1.6, 0.25), (0.85, 0.9, 0.06), "cube"),
            ("Peg tool A", (-2.35, 1.7, 0.3), (0.08, 0.35, 0.08), "cube"),
            ("Peg tool B", (-2.05, 1.55, 0.3), (0.08, 0.28, 0.08), "cube"),
        ],
        base=(0.45, 0.42, 0.38),
        accent=(0.85, 0.55, 0.18),
        step=(0.25, 0.25, 0.28),
    )

if __name__ == "__main__":
    main()
