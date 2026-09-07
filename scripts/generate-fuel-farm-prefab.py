#!/usr/bin/env python3
"""Emit mdl_fuel_farm_v01 Resources prefab (decision 0025 item 1)."""

from __future__ import annotations

from pathlib import Path

# Reuse the YAML emitter from the apron GSE generator.
_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    _NS["emit_prefab"](
        "mdl_fuel_farm_v01",
        "263748596a7b8c9d0e1f202122232425",
        [
            ("Fuel pad", (0.0, 0.02, 0.0), (8.0, 0.08, 6.0), "cube"),
            ("Fuel bund", (0.0, 0.25, 0.0), (7.2, 0.35, 5.2), "cube"),
            ("Fuel bund wall N", (0.0, 0.55, 2.55), (7.0, 0.7, 0.18), "cube"),
            ("Fuel bund wall S", (0.0, 0.55, -2.55), (7.0, 0.7, 0.18), "cube"),
            ("Fuel bund wall E", (3.5, 0.55, 0.0), (0.18, 0.7, 5.0), "cube"),
            ("Fuel bund wall W", (-3.5, 0.55, 0.0), (0.18, 0.7, 5.0), "cube"),
            ("Fuel tank A", (-1.5, 1.1, 0.5), (2.2, 2.2, 2.2), "cylinder"),
            ("Fuel tank B", (1.8, 1.1, 0.5), (2.2, 2.2, 2.2), "cylinder"),
            ("Fuel tank A dome", (-1.5, 2.25, 0.5), (1.9, 0.35, 1.9), "cylinder"),
            ("Fuel tank B dome", (1.8, 2.25, 0.5), (1.9, 0.35, 1.9), "cylinder"),
            ("Fuel tank A vent", (-1.5, 2.55, 0.5), (0.18, 0.35, 0.18), "cylinder"),
            ("Fuel tank B vent", (1.8, 2.55, 0.5), (0.18, 0.35, 0.18), "cylinder"),
            ("Fuel accent band A", (-1.5, 1.5, 0.5), (2.35, 0.18, 2.35), "cube"),
            ("Fuel accent band B", (1.8, 1.5, 0.5), (2.35, 0.18, 2.35), "cube"),
            ("Fuel ladder A", (-2.55, 1.2, 0.5), (0.12, 2.0, 0.35), "cube"),
            ("Fuel ladder B", (2.85, 1.2, 0.5), (0.12, 2.0, 0.35), "cube"),
            ("Fuel pump", (0.0, 0.7, -2.4), (1.2, 1.2, 0.8), "cube"),
            ("Fuel pump panel", (0.0, 0.85, -2.75), (0.9, 0.7, 0.08), "cube"),
            ("Fuel pump hose", (0.35, 0.55, -2.9), (0.12, 0.12, 0.7), "cube"),
            ("Fuel pipe", (0.0, 0.55, -1.1), (0.18, 0.18, 1.6), "cube"),
            ("Fuel pipe riser A", (-1.5, 0.7, -0.6), (0.14, 0.9, 0.14), "cube"),
            ("Fuel pipe riser B", (1.8, 0.7, -0.6), (0.14, 0.9, 0.14), "cube"),
            ("Fuel pipe cross", (0.15, 1.05, -0.6), (3.3, 0.12, 0.12), "cube"),
            ("Fuel valve A", (-0.6, 0.55, -1.5), (0.28, 0.28, 0.28), "cube"),
            ("Fuel valve B", (0.6, 0.55, -1.5), (0.28, 0.28, 0.28), "cube"),
            ("Fuel sign post", (3.2, 1.1, -2.4), (0.1, 1.8, 0.1), "cube"),
            ("Fuel sign board", (3.2, 1.9, -2.4), (0.08, 0.55, 0.9), "cube"),
            ("Fuel hazard stripe", (0.0, 0.06, -2.8), (3.5, 0.03, 0.25), "cube"),
            ("Fuel catch basin", (0.0, 0.12, 0.0), (1.6, 0.12, 1.2), "cube"),
            ("Fuel light pole", (-3.2, 1.6, 2.2), (0.12, 2.8, 0.12), "cube"),
            ("Fuel flood head", (-3.2, 3.05, 2.0), (0.35, 0.18, 0.35), "cube"),
            ("Fuel tank A manway", (-1.5, 2.0, 1.4), (0.45, 0.12, 0.45), "cylinder"),
            ("Fuel tank B manway", (1.8, 2.0, 1.4), (0.45, 0.12, 0.45), "cylinder"),
            ("Fuel tank A stair", (-2.4, 1.0, 1.2), (0.7, 0.12, 0.35), "cube"),
            ("Fuel tank B stair", (2.7, 1.0, 1.2), (0.7, 0.12, 0.35), "cube"),
            ("Fuel tank A rail", (-1.5, 2.35, 0.5), (2.0, 0.06, 0.06), "cube"),
            ("Fuel tank B rail", (1.8, 2.35, 0.5), (2.0, 0.06, 0.06), "cube"),
            ("Fuel pump meter", (-0.35, 1.15, -2.55), (0.35, 0.35, 0.2), "cube"),
            ("Fuel pump nozzle rest", (0.55, 0.85, -2.85), (0.25, 0.12, 0.35), "cube"),
            ("Fuel bund step", (0.0, 0.35, -2.7), (1.4, 0.15, 0.45), "cube"),
            ("Fuel fence post A", (-3.7, 0.7, -2.7), (0.12, 1.2, 0.12), "cube"),
            ("Fuel fence post B", (3.7, 0.7, -2.7), (0.12, 1.2, 0.12), "cube"),
            ("Fuel fence rail", (0.0, 1.15, -2.7), (7.2, 0.06, 0.06), "cube"),
            ("Fuel drain grate", (0.0, 0.08, 1.8), (1.2, 0.05, 0.8), "cube"),
            ("Fuel catwalk", (0.15, 1.35, 0.5), (3.4, 0.08, 0.55), "cube"),
            ("Fuel earthing post", (3.4, 0.55, 1.8), (0.1, 0.9, 0.1), "cube"),
        ],
        base=(0.72, 0.55, 0.18),
        accent=(0.95, 0.85, 0.35),
        step=(0.28, 0.3, 0.32),
    )


if __name__ == "__main__":
    main()
