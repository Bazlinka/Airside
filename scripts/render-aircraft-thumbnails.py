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
    ("ATR42", "Models/Aircraft/mdl_atr42_starter_v02.gltf", "UI/Aircraft/thb_air_atr42_v01.png"),
    ("DH8D", "Models/Aircraft/mdl_dash8_q400_v01.gltf", "UI/Aircraft/thb_air_dh8d_v01.png"),
    ("B38M", "Models/Aircraft/mdl_737_8_narrowbody_v01.gltf", "UI/Aircraft/thb_air_b38m_v01.png"),
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
    if n.startswith(("cabin_window_frame", "cockpit_frame", "windscreen_pillar")):
        return LIGHT_GREY
    if n.startswith(("cabin_window", "cockpit", "windscreen")):
        return GLASS
    if n.startswith(("gear_", "flap_track", "exhaust", "propeller", "prop_hub", "spinner", "hub_cap", "fan_")):
        return DARK
    if n.startswith(("livery_",)):
        return SLATE
    if n.startswith(("engine_", "nacelle_", "intake_", "pylon_", "oil_cooler", "cowl_flap")):
        return LIGHT_GREY
    if n.startswith(("wing", "flap", "spoiler", "aileron", "tail", "elevator", "rudder", "dorsal", "winglet")):
        return LIGHT_GREY
    return WHITE


def load(path):
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

    triangles = []
    for node in gltf["nodes"]:
        mesh = gltf["meshes"][node["mesh"]]
        for prim in mesh["primitives"]:
            pos = accessor(prim["attributes"]["POSITION"])
            idx = accessor(prim["indices"]).reshape(-1).astype(int)
            tri = pos[idx].reshape(-1, 3, 3)
            triangles.append((tri, colour(node["name"])))
    return triangles


def render(model_path, out_path):
    parts = load(model_path)
    tris = np.concatenate([t for t, _ in parts])
    cols = np.concatenate([np.tile(np.array(c, float), (len(t), 1)) for t, c in parts])

    # View: model +Z forward, +Y up. Rotate so we look at the nose from front-left and above.
    az, el = math.radians(AZIMUTH_DEG), math.radians(ELEVATION_DEG)
    ry = np.array([[math.cos(az), 0, math.sin(az)], [0, 1, 0], [-math.sin(az), 0, math.cos(az)]])
    rx = np.array([[1, 0, 0], [0, math.cos(el), -math.sin(el)], [0, math.sin(el), math.cos(el)]])
    # Camera looks down -Z of view space; start looking at the nose (model +Z towards camera).
    view = rx @ ry
    v = tris.reshape(-1, 3) @ view.T
    v = v.reshape(-1, 3, 3)

    normals = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    lengths = np.linalg.norm(normals, axis=1)
    keep = lengths > 1e-9
    v, cols, normals = v[keep], cols[keep], normals[keep] / lengths[keep, None]
    light = LIGHT / np.linalg.norm(LIGHT)
    shade = 0.42 + 0.58 * np.abs(normals @ light)          # two-sided: thin parts stay lit

    xy = v[:, :, :2]
    lo = xy.reshape(-1, 2).min(axis=0)
    hi = xy.reshape(-1, 2).max(axis=0)
    margin = 0.06
    scale = min(WIDTH * (1 - 2 * margin) / (hi[0] - lo[0]), HEIGHT * (1 - 2 * margin) / (hi[1] - lo[1])) * SUPERSAMPLE
    centre = (lo + hi) / 2
    ss_w, ss_h = WIDTH * SUPERSAMPLE, HEIGHT * SUPERSAMPLE

    order = np.argsort(v[:, :, 2].mean(axis=1))              # far first (painter's algorithm)
    image = Image.new("RGBA", (ss_w, ss_h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    for i in order:
        pts = [(ss_w / 2 + (p[0] - centre[0]) * scale, ss_h / 2 - (p[1] - centre[1]) * scale) for p in xy[i]]
        c = tuple(int(min(255, ch * shade[i])) for ch in cols[i])
        draw.polygon(pts, fill=c + (255,))

    image = image.resize((WIDTH, HEIGHT), Image.LANCZOS)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    image.save(out_path)
    return len(v)


def main():
    for type_id, model, thumb in MODELS:
        count = render(os.path.join(ART, model), os.path.join(ART, thumb))
        print(f"{type_id}: {count} triangles from {model} -> {thumb} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    main()
