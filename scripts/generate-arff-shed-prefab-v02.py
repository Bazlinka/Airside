#!/usr/bin/env python3
"""Emit mdl_arff_shed_v02 Resources prefab — open bay regional ARFF shed."""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    # Open bay: doors retracted / parked to the sides (not closed across the mouth).
    _NS["emit_prefab"](
        "mdl_arff_shed_v02",
        "d4e5f60718293a4b5c6d7e8f901a2b3c",
        [
            ("ARFF shed body", (0.0, 1.4, 0.5), (7.0, 2.8, 4.5), "cube"),
            ("ARFF shed wall L", (-3.4, 1.4, -0.4), (0.2, 2.8, 5.2), "cube"),
            ("ARFF shed wall R", (3.4, 1.4, -0.4), (0.2, 2.8, 5.2), "cube"),
            ("ARFF shed wall B", (0.0, 1.4, 2.6), (7.0, 2.8, 0.2), "cube"),
            ("ARFF roof", (0.0, 3.0, 0.0), (7.6, 0.35, 6.0), "cube"),
            ("ARFF roof ridge", (0.0, 3.25, 0.0), (7.8, 0.18, 0.4), "cube"),
            ("ARFF roof eave F", (0.0, 2.95, -2.95), (7.4, 0.12, 0.2), "cube"),
            ("ARFF roof eave B", (0.0, 2.95, 2.95), (7.4, 0.12, 0.2), "cube"),
            # Doors open / stacked to the sides of the bay mouth.
            ("ARFF door L open", (-4.2, 1.2, -2.2), (0.12, 2.2, 2.4), "cube"),
            ("ARFF door R open", (4.2, 1.2, -2.2), (0.12, 2.2, 2.4), "cube"),
            ("ARFF door stripe L", (-4.28, 1.2, -2.2), (0.06, 0.18, 2.2), "cube"),
            ("ARFF door stripe R", (4.28, 1.2, -2.2), (0.06, 0.18, 2.2), "cube"),
            ("ARFF door rail", (0.0, 2.45, -2.82), (5.2, 0.08, 0.1), "cube"),
            ("ARFF door track L", (-2.6, 2.4, -2.84), (0.12, 0.12, 0.18), "cube"),
            ("ARFF door track R", (2.6, 2.4, -2.84), (0.12, 0.12, 0.18), "cube"),
            ("ARFF bay opening lintel", (0.0, 2.5, -2.7), (5.0, 0.2, 0.25), "cube"),
            ("ARFF window L", (-2.8, 1.8, 0.0), (0.08, 0.9, 1.4), "cube"),
            ("ARFF window R", (2.8, 1.8, 0.0), (0.08, 0.9, 1.4), "cube"),
            ("ARFF window mullion L", (-2.82, 1.8, 0.0), (0.04, 0.95, 0.08), "cube"),
            ("ARFF window mullion R", (2.82, 1.8, 0.0), (0.04, 0.95, 0.08), "cube"),
            ("ARFF vent", (0.0, 2.6, 2.6), (1.2, 0.35, 0.25), "cube"),
            ("ARFF vent grille", (0.0, 2.6, 2.72), (1.0, 0.28, 0.06), "cube"),
            ("ARFF sign face", (0.0, 2.7, -2.9), (2.2, 0.45, 0.08), "cube"),
            ("ARFF sign glyph", (0.0, 2.7, -2.95), (1.4, 0.22, 0.04), "cube"),
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
            ("ARFF hose reel", (-2.6, 0.9, 2.0), (0.7, 0.7, 0.45), "cylinder"),
            ("ARFF hose nozzle", (-2.6, 0.55, 2.35), (0.2, 0.2, 0.45), "cube"),
            ("ARFF gear rack", (2.4, 0.85, 2.0), (1.4, 1.2, 0.35), "cube"),
            ("ARFF gear locker", (2.4, 0.7, 1.3), (1.0, 1.1, 0.55), "cube"),
            ("ARFF apron pad", (0.0, 0.02, -3.5), (9.0, 0.06, 4.0), "cube"),
            ("ARFF apron strip", (0.0, 0.04, -3.5), (8.4, 0.03, 0.18), "cube"),
            ("ARFF kerb L", (-4.4, 0.12, -3.5), (0.2, 0.2, 3.8), "cube"),
            ("ARFF kerb R", (4.4, 0.12, -3.5), (0.2, 0.2, 3.8), "cube"),
            ("ARFF fascia", (0.0, 2.75, -2.78), (6.8, 0.22, 0.12), "cube"),
            ("ARFF office porch", (-2.6, 0.15, -3.2), (1.8, 0.12, 1.0), "cube"),
            ("ARFF office door", (-2.6, 1.0, -3.55), (0.9, 1.7, 0.1), "cube"),
            ("ARFF office window", (-2.6, 1.7, -3.58), (0.7, 0.55, 0.06), "cube"),
            ("ARFF roof vent stack", (1.6, 3.45, 0.8), (0.25, 0.55, 0.25), "cylinder"),
            ("ARFF antenna", (2.4, 3.7, 1.5), (0.06, 1.1, 0.06), "cube"),
            ("ARFF flood arm", (0.0, 3.35, -2.4), (0.12, 0.12, 1.2), "cube"),
            ("ARFF flood head", (0.0, 3.25, -3.0), (0.45, 0.2, 0.35), "cube"),
            ("ARFF pad chevron", (0.0, 0.05, -4.6), (2.2, 0.03, 0.35), "cube"),
            ("ARFF bay line L", (-2.2, 0.05, -3.5), (0.12, 0.03, 3.2), "cube"),
            ("ARFF bay line R", (2.2, 0.05, -3.5), (0.12, 0.03, 3.2), "cube"),
            ("ARFF white band L", (-3.45, 1.6, -0.4), (0.06, 0.35, 5.0), "cube"),
            ("ARFF white band R", (3.45, 1.6, -0.4), (0.06, 0.35, 5.0), "cube"),
            ("ARFF white fascia stripe", (0.0, 2.85, -2.82), (6.4, 0.1, 0.08), "cube"),
        ],
        base=(0.72, 0.22, 0.18),
        accent=(0.95, 0.92, 0.9),
        step=(0.35, 0.36, 0.38),
    )
    print("Wrote mdl_arff_shed_v02 Resources prefab")


if __name__ == "__main__":
    main()
