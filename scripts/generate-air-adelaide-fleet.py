#!/usr/bin/env python3
"""Generate six missing scheduled-passenger types seen at Adelaide Airport.

AIR-011 A320-200, AIR-012 737-800, AIR-013 E190, AIR-014 A220-300,
AIR-015 A330-900neo and AIR-016 787-9.  The variants reuse the project's
audited procedural topology, then apply each type's real envelope and visible
family cues.  No downloaded geometry, airline branding or copied livery is used.

Coordinates follow the Airside convention: X span, Y up, +Z forward; tyres
touch Y=0 and the nose-stop datum is Z=0.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import numpy as np


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
AIRCRAFT = REPO / "game" / "Airside" / "Assets" / "Airside" / "Art" / "Models" / "Aircraft"


def _load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / filename)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


b737 = _load("airside_737_8_for_adelaide", "generate-air-005-narrowbody-737-8.py")
a350 = _load("airside_a350_for_adelaide", "generate-air-009-a350-900.py")
b78x = _load("airside_787_10_for_adelaide", "generate-air-010-787-10.py")
e190 = _load("airside_e190_for_adelaide", "generate-air-013-e190.py")
a223 = _load("airside_a220_300_for_adelaide", "generate-air-014-a220-300.py")


SPECS = {
    "A320": ("mdl_a320_200_v01", 35.80, 11.76, 37.57),
    "B738": ("mdl_737_800_v01", 35.80, 12.50, 39.47),
    "E190": ("mdl_e190_v01", 28.72, 10.55, 36.24),
    "A223": ("mdl_a220_300_v01", 35.10, 11.50, 38.70),
    "A339": ("mdl_a330_900neo_v01", 64.00, 16.79, 63.66),
    "B789": ("mdl_787_9_v01", 60.12, 17.02, 62.81),
}


FUSELAGE_ATTACHED = (
    "fuselage", "radome", "flightdeck", "windscreen", "cockpit", "cabin_window",
    "door_", "cargo_door", "livery_", "belly_fairing", "antenna", "beacon_", "taxi_light",
)


def _scale(meshes, scale, fuselage_x=None, drop=()):
    scale = np.asarray(scale, np.float32)
    result = {}
    for name, (vertices, indices) in meshes.items():
        if any(name.startswith(prefix) for prefix in drop):
            continue
        applied = scale.copy()
        if fuselage_x is not None and any(name.startswith(prefix) for prefix in FUSELAGE_ATTACHED):
            applied[0] = fuselage_x
        result[name] = (vertices * applied, indices.copy())
    return result


def _narrowbody(span, height, length, fuselage_width, *, drop_chevrons=True):
    source = b737.narrowbody_737_8_meshes()
    drop = ["wingtip_left", "wingtip_right"]
    if drop_chevrons:
        drop.append("exhaust_chevron_")
    meshes = _scale(
        source,
        (span / b737.TARGET_SPAN_M, height / b737.TARGET_HEIGHT_M, length / b737.TARGET_LENGTH_M),
        fuselage_x=fuselage_width / (b737.FUSE_RX * 2.0),
        drop=tuple(drop),
    )
    return meshes


def airbus_a320_200_meshes():
    # Short A320ceo fuselage and single Airbus-style sharklets rather than the
    # MAX split-scimitar lower feathers.
    return _narrowbody(35.80, 11.76, 37.57, 3.95)


def boeing_737_800_meshes():
    # The NG shares the -8's length but has a slightly different envelope,
    # blended upper winglets and no MAX chevrons.
    return _narrowbody(35.80, 12.50, 39.47, 3.76)


def embraer_e190_meshes():
    # Lofted from its own tables by AIR-013, not scaled from the 737. A squashed 737 kept a
    # six-abreast section, a 737 wing planform and 737 nacelle proportions at E-Jet size.
    return e190.e190_meshes()


def airbus_a220_300_meshes():
    # Lofted from its own tables by AIR-014, not scaled from the 737. The A220's slim
    # five-abreast tube, high-aspect-ratio raked wing and oversized geared-fan nacelles
    # cannot come out of an axis scale.
    return a223.a220_300_meshes()


def airbus_a330_900neo_meshes():
    source = a350.a350_900_meshes()
    # A330neo: narrower legacy A330 fuselage, no A350 cockpit mask, A350-inspired
    # raked wingtip and large Trent 7000-class nacelles.
    return _scale(
        source,
        (64.00 / a350.TARGET_SPAN_M, 16.79 / a350.TARGET_HEIGHT_M, 63.66 / a350.TARGET_LENGTH_M),
        fuselage_x=5.64 / (a350.FUSE_RX * 2.0),
        drop=("cockpit_mask_",),
    )


def boeing_787_9_meshes():
    # Same 787 wing/cabin/gear family as AIR-010, shortened to the -9's exact
    # airport-planning envelope while retaining its four-pane deck and chevrons.
    return _scale(
        b78x.boeing_787_10_meshes(),
        (60.12 / b78x.TARGET_SPAN_M, 17.02 / b78x.TARGET_HEIGHT_M, 62.81 / b78x.TARGET_LENGTH_M),
    )


BUILDERS = {
    "A320": airbus_a320_200_meshes,
    "B738": boeing_737_800_meshes,
    "E190": embraer_e190_meshes,
    "A223": airbus_a220_300_meshes,
    "A339": airbus_a330_900neo_meshes,
    "B789": boeing_787_9_meshes,
}


def validate(type_id, meshes):
    if not meshes:
        raise ValueError(f"{type_id}: no meshes")
    for name, (vertices, indices) in meshes.items():
        if not len(vertices) or not len(indices) or len(indices) % 3:
            raise ValueError(f"{type_id}/{name}: invalid triangle mesh")
        if not np.isfinite(vertices).all() or int(indices.max()) >= len(vertices):
            raise ValueError(f"{type_id}/{name}: invalid geometry")
    vertices = np.concatenate([vertices for vertices, _ in meshes.values()])
    minimum, maximum = vertices.min(axis=0), vertices.max(axis=0)
    _, span, height, length = SPECS[type_id]
    expected = np.asarray((span, height, length), np.float32)
    if not np.allclose(maximum - minimum, expected, atol=0.03, rtol=0.0):
        raise ValueError(f"{type_id}: bounds {(maximum - minimum).tolist()} != {expected.tolist()}")
    if abs(float(minimum[1])) > 0.03 or abs(float(maximum[2])) > 0.03:
        raise ValueError(f"{type_id}: datum y={minimum[1]:.3f}, z={maximum[2]:.3f}")
    return minimum, maximum


def main():
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    for type_id, builder in BUILDERS.items():
        basename, _, _, _ = SPECS[type_id]
        meshes = builder()
        minimum, maximum = validate(type_id, meshes)
        b737.write_kit(AIRCRAFT, basename, meshes)
        dimensions = maximum - minimum
        triangles = sum(len(indices) // 3 for _, indices in meshes.values())
        print(f"{type_id}: {basename} ({len(meshes)} meshes, {triangles} triangles; "
              f"{dimensions[0]:.2f} x {dimensions[1]:.2f} x {dimensions[2]:.2f} m)")


if __name__ == "__main__":
    main()
