#!/usr/bin/env python3
"""Emit mdl_arff_shed_v01 Resources prefab (decision 0025 items 1+3)."""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    _NS["emit_prefab"](
        "mdl_arff_shed_v01",
        "202122232425263748596a7b8c9d0e1f",
        [
            ("ARFF shed body", (0.0, 1.4, 0.0), (7.0, 2.8, 5.5), "cube"),
            ("ARFF roof", (0.0, 3.0, 0.0), (7.6, 0.35, 6.0), "cube"),
            ("ARFF roof ridge", (0.0, 3.25, 0.0), (7.8, 0.18, 0.4), "cube"),
            ("ARFF door L", (-1.4, 1.2, -2.8), (2.4, 2.2, 0.12), "cube"),
            ("ARFF door R", (1.4, 1.2, -2.8), (2.4, 2.2, 0.12), "cube"),
            ("ARFF door stripe L", (-1.4, 1.2, -2.85), (2.2, 0.18, 0.06), "cube"),
            ("ARFF door stripe R", (1.4, 1.2, -2.85), (2.2, 0.18, 0.06), "cube"),
            ("ARFF window L", (-2.8, 1.8, 0.0), (0.08, 0.9, 1.4), "cube"),
            ("ARFF window R", (2.8, 1.8, 0.0), (0.08, 0.9, 1.4), "cube"),
            ("ARFF vent", (0.0, 2.6, 2.6), (1.2, 0.35, 0.25), "cube"),
            ("ARFF sign face", (0.0, 2.6, -2.85), (2.2, 0.45, 0.08), "cube"),
            ("ARFF corner L", (-3.4, 1.4, -2.6), (0.25, 2.8, 0.25), "cube"),
            ("ARFF corner R", (3.4, 1.4, -2.6), (0.25, 2.8, 0.25), "cube"),
            ("ARFF apron pad", (0.0, 0.02, -3.5), (9.0, 0.06, 4.0), "cube"),
        ],
        base=(0.72, 0.22, 0.18),
        accent=(0.95, 0.85, 0.2),
        step=(0.35, 0.36, 0.38),
    )


if __name__ == "__main__":
    main()
