#!/usr/bin/env python3
"""Asset-only jet fan radius probe; no editor/player or external Python packages.

Reads the exact shipped metre-authored glTF vertices, rebasing to the fan hub
bounds centre as RebakeJetFanPivots does at identity. This verifies coverage for
the shipping asset path, not alternative prefabs or native transform integration.
The pure C# tests separately exercise the actual production diameter helper.
"""
import json
import math
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "game/Airside/Assets/Airside"
FOLDER = ASSETS / "Art/Models/Aircraft"
HELPER = ASSETS / "Presentation/AircraftFanDiscGeometry.cs"


def vertices(gltf, blob, node):
    result = []
    for primitive in gltf["meshes"][node["mesh"]]["primitives"]:
        accessor = gltf["accessors"][primitive["attributes"]["POSITION"]]
        assert accessor["componentType"] == 5126 and accessor["type"] == "VEC3"
        view = gltf["bufferViews"][accessor["bufferView"]]
        start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride", 12)
        result.extend(struct.unpack_from("<fff", blob, start + i * stride)
                      for i in range(accessor["count"]))
    return result


def main():
    # Fail loudly if production changes its formula: do not silently check a stale mirror.
    formula = re.search(r"Math\.Max\((\d+\.\d+)f \* (\d+\.\d+)f, radiusSquared\)\) \* (\d+\.\d+)f", HELPER.read_text())
    assert formula, "Update the probe for the production diameter formula"
    minimum_a, minimum_b, multiplier = map(float, formula.groups())
    for name in ("mdl_a320_200_v01.gltf", "mdl_737_8_narrowbody_v01.gltf",
                 "mdl_a350_900_v01.gltf", "mdl_787_10_v01.gltf"):
        gltf = json.loads((FOLDER / name).read_text())
        blob = (FOLDER / gltf["buffers"][0]["uri"]).read_bytes()
        # Current glTFs bake world geometry into each node. Do not apply this
        # identity-pose probe to future hierarchical/TRS assets without adapting it.
        assert all(not any(key in node for key in ("matrix", "translation", "rotation", "scale", "children"))
                   for node in gltf["nodes"]), "Probe requires baked identity nodes"
        for side in ("left", "right"):
            hub = next(n for n in gltf["nodes"] if n["name"] == "fan_" + side)
            points = vertices(gltf, blob, hub)
            centre = [(min(p[i] for p in points) + max(p[i] for p in points)) / 2 for i in range(3)]
            blades = [p for node in gltf["nodes"] if node["name"].startswith("fan_blade_" + side[0])
                      for p in vertices(gltf, blob, node)]
            assert blades, name + " has no blades"
            radius = max(math.hypot(p[0] - centre[0], p[1] - centre[1]) for p in blades)
            disc_radius = math.sqrt(max(minimum_a * minimum_b, radius * radius)) * multiplier / 2
            assert disc_radius >= radius and disc_radius > 0
            print(f"{name} {side}: blades {radius:.4f} m; disc {disc_radius:.4f} m; coverage {disc_radius / radius:.3f}")


if __name__ == "__main__":
    main()
