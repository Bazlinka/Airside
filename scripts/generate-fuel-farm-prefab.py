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
            ("Fuel tank A", (-1.5, 1.1, 0.5), (2.2, 2.2, 2.2), "cylinder"),
            ("Fuel tank B", (1.8, 1.1, 0.5), (2.2, 2.2, 2.2), "cylinder"),
            ("Fuel pump", (0.0, 0.7, -2.4), (1.2, 1.2, 0.8), "cube"),
            ("Fuel pipe", (0.0, 0.55, -1.1), (0.18, 0.18, 1.6), "cube"),
            ("Fuel accent band A", (-1.5, 1.5, 0.5), (2.35, 0.18, 2.35), "cube"),
            ("Fuel accent band B", (1.8, 1.5, 0.5), (2.35, 0.18, 2.35), "cube"),
        ],
        base=(0.72, 0.55, 0.18),
        accent=(0.95, 0.85, 0.35),
        step=(0.28, 0.3, 0.32),
    )


if __name__ == "__main__":
    main()
