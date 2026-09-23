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
import argparse
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
skin = _load("airside_aircraft_skin_for_adelaide", "aircraft_skin.py")


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
    """Appearance-first A320: rounder Airbus nose, cabin and sharklet silhouette.

    Retain the audited 737-family running gear and engine animation nodes for now,
    but replace the skin, flight deck, passenger rhythm, wing planform and livery
    belt. This is intentionally about the view at stand/follow distance, not a
    millimetre-accurate manufacturing model.
    """
    meshes = _narrowbody(35.80, 11.76, 37.57, 3.95)
    half_length = 37.57 / 2.0
    # Own cross-section stations: broad continuous six-abreast cabin and a round,
    # almost level nose. The inherited 737 nose had a very visible drooped chin.
    stations = np.asarray([
        (18.785, 0.62, 0.56, 4.04),
        (18.30, 0.96, 0.86, 4.05),
        (17.85, 1.26, 1.15, 4.07),
        (17.25, 1.55, 1.44, 4.10),
        (16.45, 1.72, 1.66, 4.12),
        (15.30, 1.92, 1.89, 4.13),
        (13.90, 1.975, 1.96, 4.13),
        (8.00, 1.975, 1.98, 4.13),
        (0.00, 1.975, 1.98, 4.13),
        (-8.50, 1.975, 1.98, 4.13),
        (-12.40, 1.89, 1.87, 4.14),
        (-15.15, 1.56, 1.52, 4.19),
        (-17.20, 0.94, 0.91, 4.25),
        (-18.785, 0.17, 0.16, 4.31),
    ], np.float32)
    ascending = stations[::-1]

    def surface(z, angle, offset=0.0):
        rx = float(np.interp(z, ascending[:, 0], ascending[:, 1]))
        ry = float(np.interp(z, ascending[:, 0], ascending[:, 2]))
        cy = float(np.interp(z, ascending[:, 0], ascending[:, 3]))
        radians = np.deg2rad(angle)
        return np.asarray(((rx + offset) * np.cos(radians),
                           cy + (ry + offset) * np.sin(radians), z), np.float32)

    def nose_stop(mesh):
        vertices, indices = mesh
        return b737.orient_outward(vertices + np.asarray((0.0, 0.0, -half_length), np.float32), indices)

    # Dense station interpolation keeps the long passenger tube smooth in a close orbit.
    dense_z = np.linspace(stations[0, 0], stations[-1, 0], 90)
    meshes["fuselage"] = nose_stop(b737.oval_lathe_fuselage(
        [(float(z), float(np.interp(z, ascending[:, 0], ascending[:, 1])),
          float(np.interp(z, ascending[:, 0], ascending[:, 2])),
          float(np.interp(z, ascending[:, 0], ascending[:, 3]))) for z in dense_z],
        segments=64))
    meshes["radome"] = nose_stop(b737.oval_lathe_fuselage(
        [(float(z), float(np.interp(z, ascending[:, 0], ascending[:, 1])) + 0.006,
          float(np.interp(z, ascending[:, 0], ascending[:, 2])) + 0.006,
          float(np.interp(z, ascending[:, 0], ascending[:, 3])))
         for z in (18.785, 18.50, 18.30, 18.05, 17.85, 17.65)], segments=64))

    # The Airbus flight deck is a broad pair of swept panes with compact side
    # windows, rather than the three little 737 brow panes.
    for name in ("flightdeck_crown", "windscreen_c", "windscreen_l", "windscreen_r",
                 "cockpit_side_l", "cockpit_side_r"):
        meshes.pop(name, None)
    for suffix, angle in (("l", 115.0), ("r", 65.0)):
        meshes[f"windscreen_{suffix}"] = nose_stop(skin.skin_patch(
            surface, 16.96, angle, 0.48, 0.40, front=0.015, radius=0.12, rings=3))
    for suffix, angle in (("l", 145.0), ("r", 35.0)):
        meshes[f"cockpit_side_{suffix}"] = nose_stop(skin.skin_patch(
            surface, 16.35, angle, 0.38, 0.23, front=0.012, radius=0.10, rings=2))

    for name in list(meshes):
        if name.startswith("cabin_window_"):
            del meshes[name]
    for index, z in enumerate(np.arange(13.20, -11.0, -1.34), start=1):
        for side, suffix in ((-1, ""), (1, "r")):
            angle = 165.0 if side < 0 else 15.0
            pair = skin.merge_meshes([
                skin.window(surface, float(z - i * 0.67), angle, width=0.22, height=0.34)
                for i in range(2)
            ])
            meshes[f"cabin_window_{suffix}{index}"] = nose_stop(pair)

    # Replace the barely visible buried rectangle with paint that hugs the tube.
    meshes["livery_stripe"] = nose_stop(skin.livery_ribbon(
        surface, 12.80, -14.80, -1, half_width=0.28, rise_degrees=22.0))
    meshes["livery_stripe_lower"] = nose_stop(skin.livery_ribbon(
        surface, 12.80, -14.80, 1, half_width=0.28, rise_degrees=22.0))

    # A320ceo-style wider-chord wing and single rising sharklet. The 737's
    # split-scimitar planform was the most obvious silhouette giveaway.
    wing = ((1.70, 4.10, 5.45, 7.70, 0.50),
            (5.20, 4.38, 4.30, 6.35, 0.38),
            (10.20, 4.80, 2.65, 4.45, 0.24),
            (15.40, 5.20, 1.05, 2.65, 0.14),
            (17.45, 5.36, 0.48, 1.85, 0.10))

    def wing_panel(side, x_in, x_out, from_te, to_te, thickness, placement=0.0):
        """Lay a shallow control panel on this wing, not the 737's old chord."""
        corners = []
        for x in (x_in, x_out):
            y, z_le, chord, wing_t = (
                float(np.interp(x, [s[0] for s in wing], [s[j] for s in wing]))
                for j in range(1, 5)
            )
            z_te = z_le - chord
            # The loft's upper/lower skins peak midway through the chord.
            panel_y = y + placement * wing_t * 0.51
            corners.append((side * x, panel_y, z_te + from_te * chord,
                            z_te + to_te * chord))
        (x0, y0, z0, z1), (x1, y1, z2, z3) = corners
        h = thickness * 0.5
        vertices = np.asarray(((x0, y0-h, z0), (x1, y1-h, z2),
                               (x1, y1-h, z3), (x0, y0-h, z1),
                               (x0, y0+h, z0), (x1, y1+h, z2),
                               (x1, y1+h, z3), (x0, y0+h, z1)), np.float32)
        triangles = np.asarray((0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6,
                                0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2,
                                2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0), np.uint16)
        return nose_stop((vertices, triangles))

    for side, suffix in ((-1, "left"), (1, "right")):
        meshes[f"wing_{suffix}"] = nose_stop(b737.lofted_aerofoil(
            [(side * x, y, z, chord, thick) for x, y, z, chord, thick in wing], chord_points=20))
        # The loft already meets the fuselage. A separate rectangular root cap
        # projects through its upper skin and reads as a box at overview zoom.
        meshes.pop(f"wing_root_{suffix}", None)
        meshes[f"wing_fairing_{suffix}"] = wing_panel(side, 1.74, 4.20, 0.03, 0.22, 0.08, -1)
        meshes[f"flap_{suffix}"] = wing_panel(side, 2.50, 12.40, 0.00, 0.29, 0.045, -1)
        meshes[f"spoiler_{suffix}"] = wing_panel(side, 4.20, 12.00, 0.34, 0.59, 0.025, 1)
        meshes[f"aileron_{suffix}"] = wing_panel(side, 12.50, 16.60, 0.00, 0.31, 0.040)
        meshes[f"winglet_{suffix}"] = nose_stop(b737.lofted_aerofoil(
            [(5.25, side * 17.18, -0.32, 1.86, 0.14),
             (6.22, side * 17.67, -0.66, 1.35, 0.09),
             (7.15, side * 17.875, -1.08, 0.72, 0.05)], chord_points=12, vertical=True))
    for side, prefix in ((-1, "l"), (1, "r")):
        for index, x in ((1, 4.62), (2, 8.52)):
            meshes[f"flap_track_{prefix}{index}"] = wing_panel(
                side, x-0.12, x+0.12, -0.11, 0.13, 0.10, -1)
        meshes[f"flap_fairing_{prefix}"] = wing_panel(
            side, 4.00, 10.80, -0.03, 0.12, 0.08, -1)
    # Shorten the inherited pylons to bridge the nacelle crown and underside of
    # this wing; their former top projected as a tall white block through it.
    for side, suffix in ((-1, "left"), (1, "right")):
        meshes[f"pylon_{suffix}"] = nose_stop(b737.box(
            side * 5.33, 3.93, 3.04, 0.38, 1.10, 2.20))
    for suffix in ("left", "right"):
        name = f"static_wick_{suffix}"
        vertices, indices = meshes[name]
        meshes[name] = (vertices + np.asarray((0.0, 0.0, -0.58), np.float32), indices)

    # Re-seat small inherited hardware after changing the skin and wing. These
    # parts are visual only, but a detached light or probe is obvious in follow.
    meshes.pop("belly_fairing", None)  # old 737 keel is buried inside the new tube
    for name, shift in {
        "antenna_aft": (0.0, 0.22, 0.0),
        "tail_nav_light": (0.0, 0.77, 0.16),
        "nav_light_left": (0.07, 0.0, -0.70),
        "nav_light_right": (-0.07, 0.0, -0.70),
        "landing_light_l": (0.0, 0.28, -0.15),
        "landing_light_r": (0.0, 0.28, -0.15),
    }.items():
        vertices, indices = meshes[name]
        meshes[name] = (vertices + np.asarray(shift, np.float32), indices)
    for name, angle in (("pitot", 188.0), ("pitot_b", -8.0)):
        anchor = surface(17.52, angle, 0.02)
        meshes[name] = nose_stop(b737.box(float(anchor[0]), float(anchor[1]),
                                            float(anchor[2]) + 0.10, 0.032, 0.032, 0.36))
    return meshes


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


def main(type_ids=None):
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    for type_id in (type_ids or BUILDERS):
        builder = BUILDERS[type_id]
        basename, _, _, _ = SPECS[type_id]
        meshes = builder()
        minimum, maximum = validate(type_id, meshes)
        b737.write_kit(AIRCRAFT, basename, meshes)
        dimensions = maximum - minimum
        triangles = sum(len(indices) // 3 for _, indices in meshes.values())
        print(f"{type_id}: {basename} ({len(meshes)} meshes, {triangles} triangles; "
              f"{dimensions[0]:.2f} x {dimensions[1]:.2f} x {dimensions[2]:.2f} m)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", choices=tuple(BUILDERS), help="regenerate one type")
    args = parser.parse_args()
    main([args.only] if args.only else None)
