#!/usr/bin/env python3
"""Emit mdl_parked_car_v01 Resources prefab (decision 0025 item 1)."""

from __future__ import annotations

from pathlib import Path

_NS: dict = {}
exec(Path(__file__).with_name("generate-apron-gse-prefabs.py").read_text(), _NS)


def main() -> None:
    # Dimensions match the procedural PlaceParkedCar fallback (body along Z).
    # Avoid the name "cabin" — AirsideRuntimeMaterialBinder treats it as glass.
    # ~32 parts: split glass, mirrors, grille, wheel arches, door seams.
    _NS["emit_prefab"](
        "mdl_parked_car_v01",
        "596a7b8c9d0e1f202122232425263748",
        [
            ("Car body", (0.0, 0.45, 0.0), (1.7, 0.55, 3.6), "cube"),
            ("Car hood", (0.0, 0.55, 1.05), (1.55, 0.22, 1.1), "cube"),
            ("Car boot", (0.0, 0.55, -1.2), (1.55, 0.28, 0.9), "cube"),
            ("Car roof", (0.0, 0.95, -0.15), (1.5, 0.42, 1.7), "cube"),
            ("Car window", (0.0, 1.0, -0.1), (1.4, 0.22, 1.4), "cube"),
            ("Car glass front", (0.0, 1.05, 0.65), (1.35, 0.32, 0.08), "cube"),
            ("Car glass rear", (0.0, 1.05, -0.9), (1.35, 0.32, 0.08), "cube"),
            ("Car glass side L", (-0.78, 1.0, -0.1), (0.06, 0.28, 1.2), "cube"),
            ("Car glass side R", (0.78, 1.0, -0.1), (0.06, 0.28, 1.2), "cube"),
            ("Car door L", (-0.88, 0.55, 0.1), (0.08, 0.55, 1.1), "cube"),
            ("Car door R", (0.88, 0.55, 0.1), (0.08, 0.55, 1.1), "cube"),
            ("Car mirror L", (-0.95, 0.85, 0.55), (0.18, 0.12, 0.22), "cube"),
            ("Car mirror R", (0.95, 0.85, 0.55), (0.18, 0.12, 0.22), "cube"),
            ("Car bumper front", (0.0, 0.32, 1.88), (1.7, 0.28, 0.2), "cube"),
            ("Car bumper rear", (0.0, 0.32, -1.88), (1.7, 0.28, 0.2), "cube"),
            ("Car grille", (0.0, 0.5, 1.92), (1.0, 0.22, 0.08), "cube"),
            ("Car headlight L", (-0.55, 0.5, 1.95), (0.28, 0.18, 0.1), "cube"),
            ("Car headlight R", (0.55, 0.5, 1.95), (0.28, 0.18, 0.1), "cube"),
            ("Car taillight L", (-0.55, 0.5, -1.95), (0.28, 0.16, 0.08), "cube"),
            ("Car taillight R", (0.55, 0.5, -1.95), (0.28, 0.16, 0.08), "cube"),
            ("Car number plate", (0.0, 0.35, 1.98), (0.4, 0.12, 0.04), "cube"),
            ("Car stripe", (0.0, 0.55, 0.0), (1.72, 0.08, 2.8), "cube"),
            ("Car wheel arch FL", (-0.78, 0.35, 1.1), (0.35, 0.28, 0.55), "cube"),
            ("Car wheel arch FR", (0.78, 0.35, 1.1), (0.35, 0.28, 0.55), "cube"),
            ("Car wheel arch RL", (-0.78, 0.35, -1.1), (0.35, 0.28, 0.55), "cube"),
            ("Car wheel arch RR", (0.78, 0.35, -1.1), (0.35, 0.28, 0.55), "cube"),
            ("Car wheel FL", (-0.78, 0.17, 1.1), (0.28, 0.28, 0.22), "cylinder"),
            ("Car wheel FR", (0.78, 0.17, 1.1), (0.28, 0.28, 0.22), "cylinder"),
            ("Car wheel RL", (-0.78, 0.17, -1.1), (0.28, 0.28, 0.22), "cylinder"),
            ("Car wheel RR", (0.78, 0.17, -1.1), (0.28, 0.28, 0.22), "cylinder"),
            ("Car hub FL", (-0.78, 0.17, 1.1), (0.12, 0.12, 0.1), "cylinder"),
            ("Car hub FR", (0.78, 0.17, 1.1), (0.12, 0.12, 0.1), "cylinder"),
        ],
        base=(0.35, 0.4, 0.38),
        accent=(0.85, 0.85, 0.88),
        step=(0.12, 0.12, 0.13),
    )


if __name__ == "__main__":
    main()
