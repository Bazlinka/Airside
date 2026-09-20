#!/usr/bin/env python3
"""Render the Hangar's aircraft thumbnails from the actual runtime glTF models (ADR 0048).

Every catalogue type with a genuine runtime model gets the same unbranded three-quarter
view: front-left, looking slightly down, orthographic, one sun from the upper left, on a
transparent background. Geometry is read straight from the runtime `.gltf`/`.bin` the game
loads — never a photograph — and parts are coloured by name in a neutral, logo-free scheme.
Placeholder types (no model of their own) get no thumbnail on purpose.

Run: python3 scripts/render-aircraft-thumbnails.py   (then scripts/sync-art-streaming-assets.sh)
"""
import json
import math
import os
import struct

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "game/Airside/Assets/Airside/Art")

# (catalogue id, runtime model, output thumbnail) — must match Domain/AircraftCatalogue.cs.
MODELS = [
    ("ATR42", "Models/Aircraft/mdl_atr42_starter_v03.gltf", "UI/Aircraft/thb_air_atr42_v01.png"),
    ("SF34", "Models/Aircraft/mdl_saab_340b_v01.gltf", "UI/Aircraft/thb_air_sf34_v01.png"),
    ("DH8D", "Models/Aircraft/mdl_dash8_q400_v01.gltf", "UI/Aircraft/thb_air_dh8d_v01.png"),
    ("B38M", "Models/Aircraft/mdl_737_8_narrowbody_v01.gltf", "UI/Aircraft/thb_air_b38m_v01.png"),
    ("A21N", "Models/Aircraft/mdl_a321neo_v01.gltf", "UI/Aircraft/thb_air_a21n_v01.png"),
    ("A359", "Models/Aircraft/mdl_a350_900_v01.gltf", "UI/Aircraft/thb_air_a359_v01.png"),
    ("B78X", "Models/Aircraft/mdl_787_10_v01.gltf", "UI/Aircraft/thb_air_b78x_v01.png"),
]

WIDTH, HEIGHT, SUPERSAMPLE = 480, 320, 3
AZIMUTH_DEG, ELEVATION_DEG = 38.0, 18.0          # from dead ahead towards the left wing, then up
LIGHT = np.array([-0.45, 0.8, 0.4])

WHITE = (236, 239, 242)
LIGHT_GREY = (205, 210, 216)
SLATE = (96, 110, 122)          # neutral accent: no airline colour
GLASS = (40, 70, 92)
DARK = (48, 50, 54)
RUBBER = (30, 30, 33)
METAL = (140, 143, 148)


def colour(name):
    n = name
    if n.startswith(("tire_",)):
        return RUBBER
    if n.startswith(("wheel_", "rim_")):
        return METAL
    # Pillars / brow must win before the broad windscreen/cockpit glass match,
    # otherwise the flight deck collapses into one dark mask in the Hangar thumb.
    if n.startswith(("cabin_window_frame", "cockpit_frame", "windscreen_pillar", "cockpit_glare", "cockpit_sill")):
        return LIGHT_GREY
    if n.startswith(("cabin_window", "cockpit", "windscreen")):
        return GLASS
    if "_tip" in n and n.startswith("propeller_"):
        return (210, 180, 70)  # readable tip stripe at Hangar distance
    if n.startswith(("spinner", "prop_hub", "hub_cap")):
        return LIGHT_GREY
    if n.startswith(("gear_door", "gear_fairing")):
        return METAL
    if n.startswith(("gear_", "flap_track", "exhaust", "propeller", "fan_")):
        return DARK
    if n.startswith(("livery_",)):
        return SLATE
    if n.startswith(("engine_", "nacelle_", "intake_", "pylon_", "oil_cooler", "cowl_flap")):
        return LIGHT_GREY
    if n.startswith(("wing", "flap", "spoiler", "aileron", "tail", "elevator", "rudder", "dorsal", "winglet")):
        return LIGHT_GREY
    return WHITE


def load_parts(path):
    """Every node of a runtime glTF as (name, triangles[N,3,3])."""
    with open(path) as f:
        gltf = json.load(f)
    blob = open(os.path.join(os.path.dirname(path), gltf["buffers"][0]["uri"]), "rb").read()

    def accessor(index):
        acc = gltf["accessors"][index]
        view = gltf["bufferViews"][acc["bufferView"]]
        start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
        kind = {5126: ("f", 4), 5123: ("H", 2), 5125: ("I", 4)}[acc["componentType"]]
        comps = {"SCALAR": 1, "VEC3": 3}[acc["type"]]
        count = acc["count"] * comps
        data = struct.unpack_from("<" + kind[0] * count, blob, start)
        return np.array(data).reshape(-1, comps)

    parts = []
    for node in gltf["nodes"]:
        mesh = gltf["meshes"][node["mesh"]]
        chunks = []
        for prim in mesh["primitives"]:
            pos = accessor(prim["attributes"]["POSITION"])
            idx = accessor(prim["indices"]).reshape(-1).astype(int)
            chunks.append(pos[idx].reshape(-1, 3, 3))
        parts.append((node["name"], np.concatenate(chunks)))
    return parts


def load(path):
    return [(tri, colour(name)) for name, tri in load_parts(path)]


def view_matrix(azimuth_deg, elevation_deg):
    """Model +Z forward, +Y up. Azimuth 0 looks at the nose; it swings the camera towards the left wing."""
    az, el = math.radians(azimuth_deg), math.radians(elevation_deg)
    ry = np.array([[math.cos(az), 0, math.sin(az)], [0, 1, 0], [-math.sin(az), 0, math.cos(az)]])
    rx = np.array([[1, 0, 0], [0, math.cos(el), -math.sin(el)], [0, math.sin(el), math.cos(el)]])
    return rx @ ry


def rasterise(tris, cols, view, width, height, supersample, bounds=None, margin=0.06):
    """Orthographic z-buffered flat-shaded raster.

    A painter's algorithm sorts by triangle centre, so a long buried livery stripe painted
    over the fuselage it sits inside (the slate slab on the 737-8 / A321neo thumbnails).
    Depth is compared per pixel here, so hidden geometry stays hidden.
    """
    v = (tris.reshape(-1, 3) @ view.T).reshape(-1, 3, 3)
    normals = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    lengths = np.linalg.norm(normals, axis=1)
    keep = lengths > 1e-12
    v, cols, normals = v[keep], cols[keep], normals[keep] / lengths[keep, None]
    light = LIGHT / np.linalg.norm(LIGHT)
    shade = 0.42 + 0.58 * np.abs(normals @ light)          # two-sided: thin parts stay lit

    lo, hi = bounds if bounds is not None else (v[:, :, :2].reshape(-1, 2).min(axis=0), v[:, :, :2].reshape(-1, 2).max(axis=0))
    scale = min(width * (1 - 2 * margin) / (hi[0] - lo[0]), height * (1 - 2 * margin) / (hi[1] - lo[1])) * supersample
    centre = (lo + hi) / 2
    w, h = width * supersample, height * supersample
    sx = w / 2 + (v[:, :, 0] - centre[0]) * scale
    sy = h / 2 - (v[:, :, 1] - centre[1]) * scale
    depth = v[:, :, 2]

    zbuf = np.full((h, w), -np.inf)
    rgb = np.zeros((h, w, 3), np.uint8)
    covered = np.zeros((h, w), bool)
    colours = np.clip(cols * shade[:, None], 0, 255).astype(np.uint8)
    for i in range(len(v)):
        x0, x1, x2 = sx[i]
        y0, y1, y2 = sy[i]
        minx, maxx = max(int(math.floor(min(x0, x1, x2))), 0), min(int(math.ceil(max(x0, x1, x2))), w - 1)
        miny, maxy = max(int(math.floor(min(y0, y1, y2))), 0), min(int(math.ceil(max(y0, y1, y2))), h - 1)
        if maxx < minx or maxy < miny:
            continue
        den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(den) < 1e-12:
            continue
        gx, gy = np.meshgrid(np.arange(minx, maxx + 1) + 0.5, np.arange(miny, maxy + 1) + 0.5)
        l0 = ((y1 - y2) * (gx - x2) + (x2 - x1) * (gy - y2)) / den
        l1 = ((y2 - y0) * (gx - x2) + (x0 - x2) * (gy - y2)) / den
        l2 = 1.0 - l0 - l1
        inside = (l0 >= -1e-9) & (l1 >= -1e-9) & (l2 >= -1e-9)
        if not inside.any():
            continue
        z = l0 * depth[i, 0] + l1 * depth[i, 1] + l2 * depth[i, 2]
        window = zbuf[miny:maxy + 1, minx:maxx + 1]
        win = inside & (z > window)
        window[win] = z[win]
        rgb[miny:maxy + 1, minx:maxx + 1][win] = colours[i]
        covered[miny:maxy + 1, minx:maxx + 1][win] = True

    alpha = (covered * 255).astype(np.uint8)
    image = Image.fromarray(np.dstack([rgb, alpha]), "RGBA")
    return image.resize((width, height), Image.LANCZOS), len(v)


def render_view(parts, out_path, azimuth_deg, elevation_deg, width=WIDTH, height=HEIGHT,
                supersample=SUPERSAMPLE, colour_fn=colour, margin=0.06):
    tris = np.concatenate([t for _, t in parts])
    cols = np.concatenate([np.tile(np.array(colour_fn(n), float), (len(t), 1)) for n, t in parts])
    image, count = rasterise(tris, cols, view_matrix(azimuth_deg, elevation_deg),
                             width, height, supersample, margin=margin)
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    image.save(out_path)
    return count


def render(model_path, out_path):
    return render_view(load_parts(model_path), out_path, AZIMUTH_DEG, ELEVATION_DEG)


def main():
    for type_id, model, thumb in MODELS:
        count = render(os.path.join(ART, model), os.path.join(ART, thumb))
        print(f"{type_id}: {count} triangles from {model} -> {thumb} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    main()
