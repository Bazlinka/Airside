#!/usr/bin/env python3
"""Emit Resources prefab for Batch F1 AIR-001 v05 turboprop.

Pipeline-proof Cube/Cylinder hierarchy with motion part names so
airside-prefab/mdl_regional_turboprop_01_v05 resolves. ArtPresentationLoader
yields to StreamingAssets glTF until Mac FBX bake replaces builtin meshes.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

_SPEC = importlib.util.spec_from_file_location(
    "authored_prefabs",
    Path(__file__).resolve().parents[1] / "scripts/generate-authored-resources-prefabs.py",
)
_mod = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_mod)


def main() -> None:
    zcyl = (90.0, 0.0, 0.0)
    # Six blades at 60° — named for NestCrossPropellerBlades / SpinPropellers.
    prop_parts = []
    for side, x in (("L", -2.45), ("R", 2.45)):
        prop_parts.append((f"Propeller {side}", (x, 0.95, 2.32), (0.08, 2.4, 0.14), "cube"))
        prop_parts.append((f"PropBlade {side}", (x, 0.95, 2.32), (2.4, 0.08, 0.14), "cube"))
        prop_parts.append((f"PropBlade {side}2", (x, 0.95, 2.32), (1.7, 0.08, 0.14), "cube", (0.0, 0.0, 60.0)))
        prop_parts.append((f"PropBlade {side}3", (x, 0.95, 2.32), (1.7, 0.08, 0.14), "cube", (0.0, 0.0, 120.0)))
        prop_parts.append((f"PropBlade {side}4", (x, 0.95, 2.32), (2.4, 0.08, 0.14), "cube", (0.0, 0.0, 180.0)))
        prop_parts.append((f"PropBlade {side}5", (x, 0.95, 2.32), (1.7, 0.08, 0.14), "cube", (0.0, 0.0, 240.0)))
        prop_parts.append((f"Spinner {side}", (x, 0.95, 2.5), (0.32, 0.38, 0.32), "cylinder", zcyl))

    _mod.emit_prefab(
        "mdl_regional_turboprop_01_v05",
        "4df038a815b5a4c71b79d16c9196b2b7",
        [
            ("Nose", (0.0, 1.08, 5.0), (0.5, 1.0, 0.5), "cylinder", zcyl),
            ("Cockpit", (0.0, 1.35, 3.7), (0.95, 1.1, 0.95), "cylinder", zcyl),
            ("Windscreen C", (0.0, 1.62, 4.05), (0.55, 0.38, 0.04), "cube", (-28.0, 0.0, 0.0)),
            ("Fuselage", (0.0, 1.18, 0.5), (1.4, 7.5, 1.4), "cylinder", zcyl),
            ("Belly fairing", (0.0, 0.5, 0.15), (0.8, 4.2, 0.55), "cylinder", zcyl),
            ("Cabin window band", (0.0, 1.4, 0.35), (1.5, 0.1, 4.4), "cube"),
            ("Livery stripe", (0.0, 1.02, 0.4), (1.56, 0.12, 6.0), "cube"),
            ("Wing L", (-4.0, 1.22, 0.35), (6.6, 0.16, 1.6), "cube"),
            ("Wing R", (4.0, 1.22, 0.35), (6.6, 0.16, 1.6), "cube"),
            ("Engine L", (-2.45, 0.95, 1.05), (0.75, 2.1, 0.75), "cylinder", zcyl),
            ("Engine R", (2.45, 0.95, 1.05), (0.75, 2.1, 0.75), "cylinder", zcyl),
            *prop_parts,
            ("Tail", (0.0, 2.55, -3.75), (0.11, 2.35, 1.55), "cube"),
            ("Tailplane", (0.0, 1.85, -3.95), (3.6, 0.09, 1.05), "cube"),
            ("Rudder", (0.0, 2.55, -4.45), (0.08, 1.7, 0.48), "cube"),
            ("Gear nose", (0.0, 0.4, 3.2), (0.11, 0.75, 0.3), "cube"),
            ("Gear L", (-1.2, 0.34, -0.3), (0.11, 0.75, 0.4), "cube"),
            ("Gear R", (1.2, 0.34, -0.3), (0.11, 0.75, 0.4), "cube"),
            ("Gear door nose", (0.0, 0.58, 3.2), (0.52, 0.045, 0.68), "cube"),
            ("Gear door L", (-1.2, 0.58, -0.3), (0.62, 0.045, 0.82), "cube"),
            ("Gear door R", (1.2, 0.58, -0.3), (0.62, 0.045, 0.82), "cube"),
            ("Tire nose", (0.0, 0.12, 3.2), (0.22, 0.22, 0.22), "cylinder", (0.0, 0.0, 90.0)),
            ("Tire L", (-1.2, 0.12, -0.3), (0.26, 0.26, 0.26), "cylinder", (0.0, 0.0, 90.0)),
            ("Tire R", (1.2, 0.12, -0.3), (0.26, 0.26, 0.26), "cylinder", (0.0, 0.0, 90.0)),
            ("CabinDoor", (-0.74, 1.12, 2.2), (0.06, 1.05, 1.2), "cube"),
            ("Cargo door", (0.74, 1.02, -1.5), (0.06, 0.95, 1.55), "cube"),
            ("NavLight L", (-7.55, 1.25, 0.45), (0.09, 0.09, 0.09), "cube"),
            ("NavLight R", (7.55, 1.25, 0.45), (0.09, 0.09, 0.09), "cube"),
            ("Beacon", (0.0, 3.05, -3.4), (0.1, 0.1, 0.1), "cube"),
            ("LandingLight L", (-2.6, 1.05, 2.15), (0.15, 0.09, 0.09), "cube"),
            ("LandingLight R", (2.6, 1.05, 2.15), (0.15, 0.09, 0.09), "cube"),
            ("TaxiLight", (0.0, 0.58, 3.6), (0.13, 0.08, 0.09), "cube"),
        ],
        base=(0.93, 0.95, 0.97),
        accent=(0.15, 0.38, 0.55),
        step=(0.25, 0.25, 0.28),
    )


if __name__ == "__main__":
    main()
