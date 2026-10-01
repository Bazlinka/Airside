#!/usr/bin/env python3
"""Audit, and where needed refit, how the doors sit on each runtime aircraft's real fuselage.

Every door the generators cut is a thin shell whose front face should sit about a centimetre
proud of the hull and never below it. The A320 inherited its doors from the 737's skin but has
its own fuselage, so its door tops were buried up to 6 cm in the hull: shut, the door looked
sunken; open, the doorway behind it was torn by the hull poking through. This reads the exact
glTF/bin the game loads (standard library only, no numpy) and:

  audit            how far each door part's front face sits above (+) or below (-) the hull
  fit TYPE         move the door parts of TYPE onto its hull, using the same proud/back offsets the
                   reference airframe (the 737-800, cut from its own hull) has for each part

Usage:
  python3 scripts/fit-aircraft-doors.py audit [--min 0.002]   (exit 1 if any door is buried)
  python3 scripts/fit-aircraft-doors.py fit A320              (writes Art/ and StreamingAssets/ in place)
"""
import argparse
import json
import shutil
import statistics
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "game/Airside/Assets/Airside/Art/Models/Aircraft"
MIRROR = ROOT / "game/Airside/Assets/StreamingAssets/Airside/Art/Models/Aircraft"

MODELS = {
    "ATR42": "mdl_atr42_starter_v03", "SF34": "mdl_saab_340b_v01", "DH8D": "mdl_dash8_q400_v01",
    "E190": "mdl_e190_v01", "A223": "mdl_a220_300_v01", "A320": "mdl_a320_200_v01",
    "B738": "mdl_737_800_v01", "B38M": "mdl_737_8_narrowbody_v01", "A21N": "mdl_a321neo_v01",
    "A359": "mdl_a350_900_v01", "A339": "mdl_a330_900neo_v01", "B789": "mdl_787_9_v01",
    "B78X": "mdl_787_10_v01",
}
DOOR_PARTS = ("door_outline_fwd", "door_fwd", "door_handle_fwd", "cargo_door_outline", "cargo_door",
              "cargo_door_latch", "door_service_aft", "door_left_1")
REFERENCE = "B738"


class Kit:
    def __init__(self, stem):
        self.gltf_path = ART / (stem + ".gltf")
        self.bin_path = ART / (stem + ".bin")
        self.gltf = json.loads(self.gltf_path.read_text())
        self.bin = bytearray(self.bin_path.read_bytes())

    def accessor(self, node_name):
        for node in self.gltf["nodes"]:
            if node.get("name") == node_name and "mesh" in node:
                prim = self.gltf["meshes"][node["mesh"]]["primitives"][0]
                return prim["attributes"]["POSITION"], prim.get("indices")
        return None, None

    def _range(self, accessor_index):
        acc = self.gltf["accessors"][accessor_index]
        view = self.gltf["bufferViews"][acc["bufferView"]]
        return acc, view.get("byteOffset", 0) + acc.get("byteOffset", 0)

    def positions(self, node_name):
        index, _ = self.accessor(node_name)
        if index is None:
            return None
        acc, off = self._range(index)
        return [list(struct.unpack_from("<3f", self.bin, off + 12 * i)) for i in range(acc["count"])]

    def triangles(self, node_name):
        _, index = self.accessor(node_name)
        acc, off = self._range(index)
        fmt = {5123: "<H", 5125: "<I"}[acc["componentType"]]
        size = struct.calcsize(fmt)
        flat = [struct.unpack_from(fmt, self.bin, off + size * i)[0] for i in range(acc["count"])]
        return [flat[i:i + 3] for i in range(0, len(flat), 3)]

    def write_positions(self, node_name, vertices):
        index, _ = self.accessor(node_name)
        acc, off = self._range(index)
        for i, v in enumerate(vertices):
            struct.pack_into("<3f", self.bin, off + 12 * i, *v)
        acc["min"] = [min(v[c] for v in vertices) for c in range(3)]
        acc["max"] = [max(v[c] for v in vertices) for c in range(3)]

    def save(self):
        self.gltf_path.write_text(json.dumps(self.gltf, indent=2) + ("\n" if self.gltf_path.read_text().endswith("\n") else ""))
        self.bin_path.write_bytes(bytes(self.bin))


class Hull:
    """The fuselage mesh, queried along X: where does the skin cross a given (y, z)?"""

    def __init__(self, kit):
        self.verts = kit.positions("fuselage")
        self.tris = kit.triangles("fuselage")
        self.cells = {}
        for t, (a, b, c) in enumerate(self.tris):
            ys = [self.verts[i][1] for i in (a, b, c)]
            zs = [self.verts[i][2] for i in (a, b, c)]
            for yi in range(int(min(ys) / 0.25) - 1, int(max(ys) / 0.25) + 2):
                for zi in range(int(min(zs) / 0.5) - 1, int(max(zs) / 0.5) + 2):
                    self.cells.setdefault((yi, zi), []).append(t)

    def x_at(self, y, z, sign):
        best = None
        for t in self.cells.get((int(y // 0.25), int(z // 0.5)), ()):
            a, b, c = (self.verts[i] for i in self.tris[t])
            det = (b[1] - a[1]) * (c[2] - a[2]) - (c[1] - a[1]) * (b[2] - a[2])
            if abs(det) < 1e-12:
                continue
            u = ((y - a[1]) * (c[2] - a[2]) - (c[1] - a[1]) * (z - a[2])) / det
            v = ((b[1] - a[1]) * (z - a[2]) - (y - a[1]) * (b[2] - a[2])) / det
            if u < -1e-6 or v < -1e-6 or u + v > 1 + 1e-6:
                continue
            x = a[0] + u * (b[0] - a[0]) + v * (c[0] - a[0])
            if x * sign > 0 and (best is None or abs(x) > abs(best)):
                best = x
        return best


def side_of(vertices):
    return 1 if sum(v[0] for v in vertices) > 0 else -1


def deviations(hull, vertices):
    """|x| of each vertex above the hull along X (None where no hull is found)."""
    sign = side_of(vertices)
    out = []
    for x, y, z in vertices:
        hx = hull.x_at(y, z, sign)
        out.append(None if hx is None else abs(x) - abs(hx))
    return out


def audit(minimum):
    worst = []
    for code, stem in MODELS.items():
        kit = Kit(stem)
        if kit.accessor("fuselage")[0] is None:
            print(f"{code:5} (no 'fuselage' node, skipped)")
            continue
        hull = Hull(kit)
        row = []
        for name in DOOR_PARTS:
            verts = kit.positions(name)
            if verts is None or name.endswith(("handle_fwd", "latch")):
                continue
            half = len(verts) // 2
            front = [d for d in deviations(hull, verts[:half]) if d is not None]
            if front:
                row.append((name, min(front), max(front)))
                if min(front) < minimum:
                    worst.append((code, name, min(front)))
        print(f"{code:5} " + "  ".join(f"{n} {lo * 100:+.1f}..{hi * 100:+.1f} cm" for n, lo, hi in row))
    for code, name, low in worst:
        print(f"BURIED: {code} {name} front face {low * 100:+.1f} cm from the hull", file=sys.stderr)
    return 1 if worst else 0


def fit(code):
    ref_kit, kit = Kit(MODELS[REFERENCE]), Kit(MODELS[code])
    ref_hull, hull = Hull(ref_kit), Hull(kit)
    for name in DOOR_PARTS:
        ref_verts, verts = ref_kit.positions(name), kit.positions(name)
        if ref_verts is None or verts is None or len(ref_verts) != len(verts):
            continue
        half = len(verts) // 2
        # How proud of its own hull the reference part's front and back faces are.
        offsets = []
        for face in (slice(0, half), slice(half, None)):
            d = [x for x in deviations(ref_hull, ref_verts[face]) if x is not None]
            offsets.append(statistics.median(d) if d else None)
        if None in offsets:
            continue
        sign = side_of(verts)
        moved = 0
        for face, offset in zip((range(0, half), range(half, len(verts))), offsets):
            for i in face:
                x, y, z = verts[i]
                hx = hull.x_at(y, z, sign)
                if hx is None:
                    continue
                target = sign * (abs(hx) + offset)
                if abs(target - x) > 1e-5:
                    verts[i][0] = target
                    moved += 1
        kit.write_positions(name, verts)
        print(f"  {name}: {moved} vertices moved")
    kit.save()
    for suffix in (".gltf", ".bin"):
        shutil.copyfile(ART / (MODELS[code] + suffix), MIRROR / (MODELS[code] + suffix))
    print(f"{code}: door parts fitted to its hull; Art/ and StreamingAssets/ updated")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("mode", choices=("audit", "fit"))
    ap.add_argument("type", nargs="?")
    ap.add_argument("--min", type=float, default=0.002, help="smallest allowed height of a door's front face above the hull (m)")
    args = ap.parse_args()
    if args.mode == "audit":
        sys.exit(audit(args.min))
    if args.type not in MODELS:
        ap.error("fit needs one of: " + ", ".join(MODELS))
    fit(args.type)


if __name__ == "__main__":
    main()
