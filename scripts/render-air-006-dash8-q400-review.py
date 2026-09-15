#!/usr/bin/env python3
"""Orthographic front / side / top / game-camera reviews of AIR-006 from the runtime glTF."""

from __future__ import annotations

import json
import struct
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from mpl_toolkits.mplot3d.art3d import Poly3DCollection

ROOT = Path(__file__).resolve().parents[1]
GLTF = (
    ROOT
    / "game/Airside/Assets/Airside/Art/Models/Aircraft/mdl_dash8_q400_v01.gltf"
)
OUT = ROOT / "work/review"


def load_parts(path: Path):
    gltf = json.loads(path.read_text())
    blob = (path.parent / gltf["buffers"][0]["uri"]).read_bytes()

    def accessor(index: int) -> np.ndarray:
        acc = gltf["accessors"][index]
        view = gltf["bufferViews"][acc["bufferView"]]
        start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
        kind = {5126: ("f", 4), 5123: ("H", 2), 5125: ("I", 4)}[acc["componentType"]]
        comps = {"SCALAR": 1, "VEC3": 3}[acc["type"]]
        count = acc["count"] * comps
        data = struct.unpack_from("<" + kind[0] * count, blob, start)
        return np.asarray(data, np.float64).reshape(-1, comps)

    def colour(name: str) -> np.ndarray:
        n = name.lower()
        # Pillars / brow / sill must win before the broad windscreen glass match.
        if any(k in n for k in ("pillar", "cockpit_glare", "cockpit_sill", "cockpit_frame")):
            return np.array([0.82, 0.84, 0.86])
        if any(k in n for k in ("window", "windscreen", "glass", "cockpit_side")):
            return np.array([0.08, 0.16, 0.22])
        if "tire" in n or "tyre" in n:
            return np.array([0.08, 0.08, 0.08])
        if any(k in n for k in ("wheel", "rim", "gear", "oleo", "scissors")):
            return np.array([0.28, 0.30, 0.32])
        if "_tip" in n and "propeller" in n:
            return np.array([0.82, 0.70, 0.18])
        if any(k in n for k in ("spinner", "prop_hub", "hub_cap")):
            return np.array([0.72, 0.74, 0.76])
        if any(k in n for k in ("propeller",)):
            return np.array([0.12, 0.13, 0.14])
        if any(k in n for k in ("engine", "nacelle", "intake", "exhaust", "pylon", "cowl", "oil")):
            return np.array([0.18, 0.38, 0.48])
        if any(k in n for k in ("wing", "flap", "aileron", "spoiler", "winglet", "fence")):
            return np.array([0.42, 0.50, 0.46])
        if any(k in n for k in ("tail", "rudder", "elevator", "dorsal", "fin")):
            return np.array([0.40, 0.48, 0.44])
        if "livery" in n or "stripe" in n:
            return np.array([0.20, 0.42, 0.52])
        if "radome" in n:
            return np.array([0.90, 0.92, 0.93])
        return np.array([0.86, 0.88, 0.90])

    parts = []
    for node in gltf["nodes"]:
        name = node.get("name", "")
        prim = gltf["meshes"][node["mesh"]]["primitives"][0]
        pos = accessor(prim["attributes"]["POSITION"])
        idx = accessor(prim["indices"]).astype(int).reshape(-1, 3)
        step = max(1, len(idx) // 2800)
        # Display axes: longitudinal Z → X, lateral X → Y, up Y → Z.
        tri = pos[idx[::step]][:, :, [2, 0, 1]]
        parts.append((name, tri, colour(name)))
    return parts


def draw(ax, parts, elev, azim, title, limits=None):
    light = np.array([-0.35, -0.25, 0.9], dtype=np.float64)
    light /= np.linalg.norm(light)
    triangles = []
    colours = []
    for _, tri, col in parts:
        normals = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        normals /= np.maximum(np.linalg.norm(normals, axis=1)[:, None], 1e-8)
        shade = 0.55 + 0.45 * np.maximum(0.12, np.abs(normals @ light))
        triangles.extend(tri)
        colours.extend(np.clip(col * shade[:, None], 0, 1))
    ax.add_collection3d(
        Poly3DCollection(triangles, facecolors=colours, edgecolors="none", linewidths=0)
    )
    if limits is None:
        allv = np.concatenate([t.reshape(-1, 3) for _, t, _ in parts])
        lo, hi = allv.min(0), allv.max(0)
        pad = 0.6
        limits = [(lo[i] - pad, hi[i] + pad) for i in range(3)]
    ax.set(xlim=limits[0], ylim=limits[1], zlim=limits[2])
    ax.set_box_aspect(tuple(b - a for a, b in limits))
    ax.view_init(elev=elev, azim=azim)
    ax.set_proj_type("ortho")
    ax.set_axis_off()
    ax.set_title(title, fontsize=11, pad=4)
    ax.set_facecolor("#eef1ec")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    parts = load_parts(GLTF)
    views = [
        ("dash8-q400-review-front.png", 8, 0, "Front", ((14, 18), (-15, 15), (0, 9))),
        ("dash8-q400-review-side.png", 4, -90, "Left side", ((-17, 17), (-3, 3), (0, 9))),
        ("dash8-q400-review-top.png", 90, -90, "Top", ((-17, 17), (-15, 15), (0, 9))),
        (
            "dash8-q400-review-game-camera.png",
            18,
            -42,
            "Game camera (Hangar / follow)",
            None,
        ),
    ]
    for filename, elev, azim, title, limits in views:
        fig = plt.figure(figsize=(10, 7), facecolor="#eef1ec")
        ax = fig.add_subplot(1, 1, 1, projection="3d")
        draw(ax, parts, elev, azim, title, limits)
        fig.tight_layout(pad=0.4)
        out = OUT / filename
        fig.savefig(out, dpi=160, facecolor=fig.get_facecolor())
        plt.close(fig)
        print("wrote", out)


if __name__ == "__main__":
    main()
