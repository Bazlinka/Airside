#!/usr/bin/env python3
"""Emit mdl_arff_shed_v02 Resources prefab — pale-grey open-bay regional ARFF shed.

Hollow U-shell (walls L/R/B + pitched roof only). No solid body cube filling the
bay. Matches Bailey-approved ARFF fidelity board: pale corrugated grey, yellow
bollards at bay mouth, bay fluorescents, dark interior void, no sign glyph text.
"""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    # Open bay: hollow U-shell — side + back walls and roof only (no body fill).
    _NS["emit_prefab"](
        "mdl_arff_shed_v02",
        "d4e5f60718293a4b5c6d7e8f901a2b3c",
        [
            # Structural shell (corrugated pale grey via base).
            ("ARFF shed wall L", (-3.4, 1.4, -0.2), (0.22, 2.8, 5.4), "cube"),
            ("ARFF shed wall R", (3.4, 1.4, -0.2), (0.22, 2.8, 5.4), "cube"),
            ("ARFF shed wall B", (0.0, 1.4, 2.55), (7.0, 2.8, 0.22), "cube"),
            # Corrugation ribs (read as pale-grey cladding, not red).
            ("ARFF cladding rib L1", (-3.52, 1.4, -1.4), (0.06, 2.6, 0.08), "cube"),
            ("ARFF cladding rib L2", (-3.52, 1.4, 0.2), (0.06, 2.6, 0.08), "cube"),
            ("ARFF cladding rib L3", (-3.52, 1.4, 1.6), (0.06, 2.6, 0.08), "cube"),
            ("ARFF cladding rib R1", (3.52, 1.4, -1.4), (0.06, 2.6, 0.08), "cube"),
            ("ARFF cladding rib R2", (3.52, 1.4, 0.2), (0.06, 2.6, 0.08), "cube"),
            ("ARFF cladding rib R3", (3.52, 1.4, 1.6), (0.06, 2.6, 0.08), "cube"),
            ("ARFF cladding rib B1", (-1.8, 1.4, 2.68), (0.08, 2.6, 0.06), "cube"),
            ("ARFF cladding rib B2", (0.0, 1.4, 2.68), (0.08, 2.6, 0.06), "cube"),
            ("ARFF cladding rib B3", (1.8, 1.4, 2.68), (0.08, 2.6, 0.06), "cube"),
            # Dual-pitch roof read (flat planes + ridge — emit_prefab has no rotation).
            ("ARFF roof", (0.0, 3.05, 0.0), (7.6, 0.28, 6.0), "cube"),
            ("ARFF roof ridge", (0.0, 3.28, 0.0), (7.8, 0.16, 0.35), "cube"),
            ("ARFF roof eave F", (0.0, 2.92, -2.95), (7.4, 0.1, 0.18), "cube"),
            ("ARFF roof eave B", (0.0, 2.92, 2.95), (7.4, 0.1, 0.18), "cube"),
            # Doors retracted / stacked to the sides of the bay mouth.
            ("ARFF door L open", (-4.15, 1.2, -2.15), (0.12, 2.2, 2.3), "cube"),
            ("ARFF door R open", (4.15, 1.2, -2.15), (0.12, 2.2, 2.3), "cube"),
            ("ARFF door rail", (0.0, 2.45, -2.82), (5.2, 0.08, 0.1), "cube"),
            ("ARFF door track L", (-2.6, 2.4, -2.84), (0.12, 0.12, 0.18), "cube"),
            ("ARFF door track R", (2.6, 2.4, -2.84), (0.12, 0.12, 0.18), "cube"),
            ("ARFF bay opening lintel", (0.0, 2.55, -2.72), (5.2, 0.18, 0.22), "cube"),
            # Dark interior void — recessed so the bay mouth stays open.
            # Dark via stepColor (darker grey on shed); "pad" selects step branch.
            ("ARFF bay void pad", (0.0, 1.35, 0.35), (6.5, 2.5, 4.2), "cube"),
            # Bay fluorescents (housing meshes; Point light stays in prototype).
            ("ARFF bay fluorescent L", (-1.4, 2.72, -0.6), (1.8, 0.08, 0.14), "cube"),
            ("ARFF bay fluorescent R", (1.4, 2.72, -0.6), (1.8, 0.08, 0.14), "cube"),
            ("ARFF bay fluorescent mid", (0.0, 2.72, 1.0), (2.2, 0.08, 0.14), "cube"),
            # Yellow bollards at bay mouth (binder hardcodes Safety Yellow on "bollard").
            ("ARFF bay bollard L", (-2.35, 0.35, -2.95), (0.16, 0.7, 0.16), "cylinder"),
            ("ARFF bay bollard R", (2.35, 0.35, -2.95), (0.16, 0.7, 0.16), "cylinder"),
            ("ARFF bay bollard cap L", (-2.35, 0.72, -2.95), (0.18, 0.08, 0.18), "cylinder"),
            ("ARFF bay bollard cap R", (2.35, 0.72, -2.95), (0.18, 0.08, 0.18), "cylinder"),
            ("ARFF window L", (-3.42, 1.8, 0.2), (0.08, 0.9, 1.3), "cube"),
            ("ARFF window R", (3.42, 1.8, 0.2), (0.08, 0.9, 1.3), "cube"),
            ("ARFF window mullion L", (-3.44, 1.8, 0.2), (0.04, 0.95, 0.08), "cube"),
            ("ARFF window mullion R", (3.44, 1.8, 0.2), (0.04, 0.95, 0.08), "cube"),
            ("ARFF vent", (0.0, 2.55, 2.55), (1.1, 0.32, 0.22), "cube"),
            ("ARFF vent grille", (0.0, 2.55, 2.68), (0.9, 0.24, 0.05), "cube"),
            # No sign face / glyph text blocks — board forbids readable markings.
            ("ARFF corner L", (-3.4, 1.4, -2.6), (0.22, 2.8, 0.22), "cube"),
            ("ARFF corner R", (3.4, 1.4, -2.6), (0.22, 2.8, 0.22), "cube"),
            ("ARFF corner BL", (-3.4, 1.4, 2.55), (0.22, 2.8, 0.22), "cube"),
            ("ARFF corner BR", (3.4, 1.4, 2.55), (0.22, 2.8, 0.22), "cube"),
            ("ARFF gutter L", (-3.55, 2.88, 0.0), (0.1, 0.1, 5.6), "cube"),
            ("ARFF gutter R", (3.55, 2.88, 0.0), (0.1, 0.1, 5.6), "cube"),
            ("ARFF downpipe L", (-3.55, 1.4, 2.45), (0.09, 2.6, 0.09), "cube"),
            ("ARFF downpipe R", (3.55, 1.4, 2.45), (0.09, 2.6, 0.09), "cube"),
            ("ARFF side light L", (-3.5, 2.4, -1.2), (0.12, 0.12, 0.12), "cube"),
            ("ARFF side light R", (3.5, 2.4, -1.2), (0.12, 0.12, 0.12), "cube"),
            ("ARFF hose reel", (-2.5, 0.85, 1.9), (0.65, 0.65, 0.4), "cylinder"),
            ("ARFF hose nozzle", (-2.5, 0.5, 2.25), (0.18, 0.18, 0.4), "cube"),
            ("ARFF gear rack", (2.35, 0.8, 1.9), (1.3, 1.1, 0.32), "cube"),
            ("ARFF gear locker", (2.35, 0.65, 1.25), (0.95, 1.0, 0.5), "cube"),
            ("ARFF apron pad", (0.0, 0.02, -3.5), (9.0, 0.06, 4.0), "cube"),
            ("ARFF apron strip", (0.0, 0.04, -3.5), (8.4, 0.03, 0.18), "cube"),
            ("ARFF kerb L", (-4.4, 0.12, -3.5), (0.2, 0.2, 3.8), "cube"),
            ("ARFF kerb R", (4.4, 0.12, -3.5), (0.2, 0.2, 3.8), "cube"),
            ("ARFF fascia", (0.0, 2.78, -2.78), (6.8, 0.2, 0.12), "cube"),
            ("ARFF office porch", (-2.6, 0.15, -3.15), (1.7, 0.1, 0.9), "cube"),
            ("ARFF office door", (-2.6, 1.0, -3.5), (0.85, 1.65, 0.1), "cube"),
            ("ARFF office window", (-2.6, 1.7, -3.55), (0.65, 0.5, 0.06), "cube"),
            ("ARFF roof vent stack", (1.6, 3.4, 0.8), (0.22, 0.5, 0.22), "cylinder"),
            ("ARFF antenna", (2.4, 3.65, 1.5), (0.05, 1.0, 0.05), "cube"),
            ("ARFF flood arm", (0.0, 3.3, -2.35), (0.1, 0.1, 1.1), "cube"),
            ("ARFF flood head", (0.0, 3.2, -2.95), (0.4, 0.18, 0.32), "cube"),
            ("ARFF bay line L", (-2.2, 0.05, -3.5), (0.1, 0.03, 3.0), "cube"),
            ("ARFF bay line R", (2.2, 0.05, -3.5), (0.1, 0.03, 3.0), "cube"),
            ("ARFF white band L", (-3.45, 1.55, -0.2), (0.05, 0.28, 5.0), "cube"),
            ("ARFF white band R", (3.45, 1.55, -0.2), (0.05, 0.28, 5.0), "cube"),
            ("ARFF white fascia stripe", (0.0, 2.88, -2.82), (6.2, 0.08, 0.06), "cube"),
        ],
        base=(0.72, 0.74, 0.76),
        accent=(0.95, 0.94, 0.92),
        step=(0.22, 0.23, 0.25),
    )
    print("Wrote mdl_arff_shed_v02 Resources prefab")


if __name__ == "__main__":
    main()
