#!/usr/bin/env python3
"""Emit mdl_parked_car_v01 Resources prefab (decision 0025 item 1)."""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    # Dimensions match the procedural PlaceParkedCar fallback (body along Z).
    # Avoid the name "cabin" — AirsideRuntimeMaterialBinder treats it as glass.
    _NS["emit_prefab"](
        "mdl_parked_car_v01",
        "596a7b8c9d0e1f202122232425263748",
        [
            ("Car body", (0.0, 0.45, 0.0), (1.7, 0.55, 3.6), "cube"),
            ("Car roof", (0.0, 0.9, -0.15), (1.55, 0.5, 1.8), "cube"),
            ("Car window", (0.0, 1.0, -0.1), (1.45, 0.28, 1.5), "cube"),
            ("Car bumper front", (0.0, 0.35, 1.85), (1.65, 0.28, 0.18), "cube"),
            ("Car bumper rear", (0.0, 0.35, -1.85), (1.65, 0.28, 0.18), "cube"),
            ("Car headlight L", (-0.55, 0.5, 1.9), (0.28, 0.18, 0.12), "cube"),
            ("Car headlight R", (0.55, 0.5, 1.9), (0.28, 0.18, 0.12), "cube"),
            ("Car stripe", (0.0, 0.55, 0.0), (1.72, 0.08, 2.8), "cube"),
            ("Car wheel FL", (-0.7, 0.17, 1.1), (0.28, 0.28, 0.35), "cube"),
            ("Car wheel FR", (0.7, 0.17, 1.1), (0.28, 0.28, 0.35), "cube"),
            ("Car wheel RL", (-0.7, 0.17, -1.1), (0.28, 0.28, 0.35), "cube"),
            ("Car wheel RR", (0.7, 0.17, -1.1), (0.28, 0.28, 0.35), "cube"),
        ],
        base=(0.35, 0.4, 0.38),
        accent=(0.85, 0.85, 0.88),
        step=(0.12, 0.12, 0.13),
    )


if __name__ == "__main__":
    main()
