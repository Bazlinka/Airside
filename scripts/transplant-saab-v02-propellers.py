#!/usr/bin/env python3
"""Put the AIR-007 v02 candidate's propeller blades on the runtime Saab 340B (AIR-007 v01).

Codex's Blender candidate (scripts/build-air-007-blender-v02.py) was built from the model as it
stood before the fleet glazing pass (ADR 0171), so adopting the whole candidate would undo that.
Its one visible improvement over the runtime model is the propellers: slim, tapered blades in
place of broad diamond paddles. This copies just those 16 blade meshes (propeller_{left,right}
{,_b,_c,_d} and their _tip parts) into the runtime glTF, keeping every other part, name and
material rule unchanged.

The candidate stores each blade relative to its hub (node translation); the runtime model bakes
vertices in the aircraft frame with every node at the origin, POSITION only. So each blade's
vertices are moved by its node translation and written as new accessors on the runtime buffer.

Usage: scripts/transplant-saab-v02-propellers.py [--check]
"""
import json
import struct
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
CANDIDATE = REPO / "docs/art/candidates/air-007-saab-340b-v02/mdl_saab_340b_v02_candidate.glb"
RUNTIME = REPO / "game/Airside/Assets/Airside/Art/Models/Aircraft/mdl_saab_340b_v01.gltf"
MIRROR = REPO / "game/Airside/Assets/StreamingAssets/Airside/Art/Models/Aircraft/mdl_saab_340b_v01.gltf"

BLADES = [f"propeller_{side}{tip}{suffix}" for side in ("left", "right") for tip in ("", "_tip")
          for suffix in ("", "_b", "_c", "_d")]


def load_glb(path):
    data = path.read_bytes()
    offset, js, binary = 12, None, None
    while offset < len(data):
        length, kind = struct.unpack_from("<I4s", data, offset)
        chunk = data[offset + 8: offset + 8 + length]
        offset += 8 + length
        if kind == b"JSON":
            js = json.loads(chunk)
        elif kind == b"BIN\x00":
            binary = chunk
    return js, binary


def read_accessor(gltf, binary, index):
    acc = gltf["accessors"][index]
    view = gltf["bufferViews"][acc["bufferView"]]
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    count = acc["count"]
    if acc["type"] == "VEC3":
        return [struct.unpack_from("<3f", binary, start + i * 12) for i in range(count)]
    fmt = {5121: "B", 5123: "H", 5125: "I"}[acc["componentType"]]
    size = struct.calcsize(fmt)
    return [struct.unpack_from("<" + fmt, binary, start + i * size)[0] for i in range(count)]


def candidate_blades():
    gltf, binary = load_glb(CANDIDATE)
    blades = {}
    for node in gltf["nodes"]:
        name = node.get("name", "")
        if name not in BLADES:
            continue
        if node.get("rotation") or node.get("scale"):
            raise SystemExit(f"{name}: candidate node is rotated or scaled; only translation is supported")
        tx, ty, tz = node.get("translation", [0.0, 0.0, 0.0])
        positions, indices = [], []
        for primitive in gltf["meshes"][node["mesh"]]["primitives"]:
            base = len(positions)
            positions += [(x + tx, y + ty, z + tz) for x, y, z in read_accessor(gltf, binary, primitive["attributes"]["POSITION"])]
            indices += [base + i for i in read_accessor(gltf, binary, primitive["indices"])]
        blades[name] = (positions, indices)
    missing = sorted(set(BLADES) - set(blades))
    if missing:
        raise SystemExit(f"candidate is missing {missing}")
    return blades


def transplant(path, blades):
    gltf = json.loads(path.read_text())
    bin_path = path.with_name(gltf["buffers"][0]["uri"])
    binary = bytearray(bin_path.read_bytes())
    for node in gltf["nodes"]:
        name = node.get("name", "")
        if name not in blades:
            continue
        positions, indices = blades[name]
        while len(binary) % 4:
            binary.append(0)
        pos_offset = len(binary)
        for p in positions:
            binary += struct.pack("<3f", *p)
        idx_offset = len(binary)
        wide = len(positions) > 65535
        for i in indices:
            binary += struct.pack("<I" if wide else "<H", i)
        views = gltf["bufferViews"]
        views.append({"buffer": 0, "byteOffset": pos_offset, "byteLength": idx_offset - pos_offset, "target": 34962})
        views.append({"buffer": 0, "byteOffset": idx_offset, "byteLength": len(binary) - idx_offset, "target": 34963})
        accessors = gltf["accessors"]
        accessors.append({"bufferView": len(views) - 2, "componentType": 5126, "count": len(positions), "type": "VEC3",
                          "min": [min(p[k] for p in positions) for k in range(3)],
                          "max": [max(p[k] for p in positions) for k in range(3)]})
        accessors.append({"bufferView": len(views) - 1, "componentType": 5125 if wide else 5123,
                          "count": len(indices), "type": "SCALAR"})
        mesh = gltf["meshes"][node["mesh"]]
        mesh["primitives"] = [{"attributes": {"POSITION": len(accessors) - 2}, "indices": len(accessors) - 1, "mode": 4}]
    gltf["buffers"][0]["byteLength"] = len(binary)
    return gltf, bytes(binary), bin_path


def main():
    check = "--check" in sys.argv
    blades = candidate_blades()
    gltf, binary, bin_path = transplant(RUNTIME, blades)
    if check:
        print("candidate blades read:", len(blades))
        return
    RUNTIME.write_text(json.dumps(gltf, separators=(",", ":")))
    bin_path.write_bytes(binary)
    MIRROR.write_text(RUNTIME.read_text())
    MIRROR.with_name(bin_path.name).write_bytes(binary)
    print(f"transplanted {len(blades)} blade meshes into {RUNTIME.name} (+ StreamingAssets mirror)")


if __name__ == "__main__":
    main()
