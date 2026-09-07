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
            ("ARFF roof eave F", (0.0, 2.95, -2.95), (7.4, 0.12, 0.2), "cube"),
            ("ARFF roof eave B", (0.0, 2.95, 2.95), (7.4, 0.12, 0.2), "cube"),
            ("ARFF door L", (-1.4, 1.2, -2.8), (2.4, 2.2, 0.12), "cube"),
            ("ARFF door R", (1.4, 1.2, -2.8), (2.4, 2.2, 0.12), "cube"),
            ("ARFF door stripe L", (-1.4, 1.2, -2.85), (2.2, 0.18, 0.06), "cube"),
            ("ARFF door stripe R", (1.4, 1.2, -2.85), (2.2, 0.18, 0.06), "cube"),
            ("ARFF door handle L", (-0.45, 1.15, -2.88), (0.12, 0.35, 0.08), "cube"),
            ("ARFF door handle R", (0.45, 1.15, -2.88), (0.12, 0.35, 0.08), "cube"),
            ("ARFF door rail", (0.0, 2.35, -2.82), (5.0, 0.08, 0.1), "cube"),
            ("ARFF window L", (-2.8, 1.8, 0.0), (0.08, 0.9, 1.4), "cube"),
            ("ARFF window R", (2.8, 1.8, 0.0), (0.08, 0.9, 1.4), "cube"),
            ("ARFF window mullion L", (-2.82, 1.8, 0.0), (0.04, 0.95, 0.08), "cube"),
            ("ARFF window mullion R", (2.82, 1.8, 0.0), (0.04, 0.95, 0.08), "cube"),
            ("ARFF vent", (0.0, 2.6, 2.6), (1.2, 0.35, 0.25), "cube"),
            ("ARFF vent grille", (0.0, 2.6, 2.72), (1.0, 0.28, 0.06), "cube"),
            ("ARFF sign face", (0.0, 2.6, -2.85), (2.2, 0.45, 0.08), "cube"),
            ("ARFF sign glyph", (0.0, 2.6, -2.9), (1.4, 0.22, 0.04), "cube"),
            ("ARFF corner L", (-3.4, 1.4, -2.6), (0.25, 2.8, 0.25), "cube"),
            ("ARFF corner R", (3.4, 1.4, -2.6), (0.25, 2.8, 0.25), "cube"),
            ("ARFF corner BL", (-3.4, 1.4, 2.6), (0.25, 2.8, 0.25), "cube"),
            ("ARFF corner BR", (3.4, 1.4, 2.6), (0.25, 2.8, 0.25), "cube"),
            ("ARFF gutter L", (-3.55, 2.85, 0.0), (0.12, 0.12, 5.6), "cube"),
            ("ARFF gutter R", (3.55, 2.85, 0.0), (0.12, 0.12, 5.6), "cube"),
            ("ARFF downpipe L", (-3.55, 1.4, 2.5), (0.1, 2.6, 0.1), "cube"),
            ("ARFF downpipe R", (3.55, 1.4, 2.5), (0.1, 2.6, 0.1), "cube"),
            ("ARFF side light L", (-3.5, 2.4, -1.2), (0.12, 0.12, 0.12), "cube"),
            ("ARFF side light R", (3.5, 2.4, -1.2), (0.12, 0.12, 0.12), "cube"),
            ("ARFF hose reel", (-2.6, 0.9, 2.2), (0.7, 0.7, 0.45), "cylinder"),
            ("ARFF gear rack", (2.4, 0.85, 2.1), (1.4, 1.2, 0.35), "cube"),
            ("ARFF apron pad", (0.0, 0.02, -3.5), (9.0, 0.06, 4.0), "cube"),
            ("ARFF apron strip", (0.0, 0.04, -3.5), (8.4, 0.03, 0.18), "cube"),
            ("ARFF kerb L", (-4.4, 0.12, -3.5), (0.2, 0.2, 3.8), "cube"),
            ("ARFF kerb R", (4.4, 0.12, -3.5), (0.2, 0.2, 3.8), "cube"),
            ("ARFF fascia", (0.0, 2.75, -2.78), (6.8, 0.22, 0.12), "cube"),
            ("ARFF door track L", (-2.6, 2.4, -2.84), (0.12, 0.12, 0.18), "cube"),
            ("ARFF door track R", (2.6, 2.4, -2.84), (0.12, 0.12, 0.18), "cube"),
            ("ARFF office porch", (-2.6, 0.15, -3.2), (1.8, 0.12, 1.0), "cube"),
            ("ARFF office door", (-2.6, 1.0, -3.55), (0.9, 1.7, 0.1), "cube"),
            ("ARFF office window", (-2.6, 1.7, -3.58), (0.7, 0.55, 0.06), "cube"),
            ("ARFF roof vent stack", (1.6, 3.45, 0.8), (0.25, 0.55, 0.25), "cylinder"),
            ("ARFF antenna", (2.4, 3.7, 1.5), (0.06, 1.1, 0.06), "cube"),
            ("ARFF flood arm", (0.0, 3.35, -2.4), (0.12, 0.12, 1.2), "cube"),
            ("ARFF flood head", (0.0, 3.25, -3.0), (0.45, 0.2, 0.35), "cube"),
            ("ARFF hose nozzle", (-2.6, 0.55, 2.55), (0.2, 0.2, 0.45), "cube"),
            ("ARFF gear locker", (2.4, 0.7, 1.4), (1.0, 1.1, 0.55), "cube"),
            ("ARFF pad chevron", (0.0, 0.05, -4.6), (2.2, 0.03, 0.35), "cube"),
            ("ARFF bay line L", (-2.2, 0.05, -3.5), (0.12, 0.03, 3.2), "cube"),
            ("ARFF bay line R", (2.2, 0.05, -3.5), (0.12, 0.03, 3.2), "cube"),
        ],
        base=(0.72, 0.22, 0.18),
        accent=(0.95, 0.85, 0.2),
        step=(0.35, 0.36, 0.38),
    )


if __name__ == "__main__":
    main()
