#!/usr/bin/env python3
"""Fit each aircraft type's painted fuselage title and registration to its real mesh.

Reads the runtime glTF the game loads (same list as render-aircraft-thumbnails.py) and
places the operator title where airlines paint it: on the upper fuselage above the
cabin windows, tangent to the skin, starting just aft of the flight deck, clear of a
high wing root; the registration small on the aft fuselage. Output is the C# table in
Presentation/AircraftTitlePaint.cs (AircraftIdentityMarkings.For), in the game's art-
root frame (model file y + the profile's ground offset).

  python3 scripts/generate-aircraft-title-layout.py          # print the C# table
  python3 scripts/generate-aircraft-title-layout.py --check  # fail if the C# table differs

ADR 0112. UnityEngine glyph metrics used: TextMesh line box = fontSize * characterSize /
10 metres; Arial/Liberation cap height = 0.716 em, so cap = 64 * c / 10 * 0.716.
"""
import argparse
import importlib.util
import math
import os
import re
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("thumbs", os.path.join(HERE, "render-aircraft-thumbnails.py"))
thumbs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(thumbs)

CS_FILE = os.path.join(thumbs.ROOT, "game/Airside/Assets/Airside/Presentation/AircraftTitlePaint.cs")
DOOR_FILE = os.path.join(thumbs.ROOT, "game/Airside/Assets/Airside/Simulation/AdelaideAerobridges.cs")

# Catalogue id -> (C# AircraftType member, art-root ground offset from AircraftVisualProfile).
TYPES = {
    "ATR42": ("Atr42", -0.70),
    "SF34": ("Saab340", -0.70),
    "DH8D": ("Dash8Q400", -0.70),
    "E190": ("EmbraerE190", -0.68),
    "A223": ("AirbusA220300", -0.68),
    "A320": ("AirbusA320200", -0.68),
    "B738": ("Boeing737800", -0.68),
    "B38M": ("Boeing7378", -0.68),
    "A21N": ("AirbusA321Neo", -0.68),
    "A359": ("AirbusA350900", -0.68),
    "A339": ("AirbusA330900", -0.68),
    "B789": ("Boeing7879", -0.68),
    "B78X": ("Boeing78710", -0.68),
}

CAP_PER_CHARACTER_SIZE = 64.0 / 10.0 * 0.716
PAINT_OFFSET = 0.025          # metres proud of the skin, like a thick coat of paint
TITLE_FRACTION = 0.34         # AircraftTitlePaint.TitleLengthFraction


def fuselage_frame(parts):
    fus = np.concatenate([tri.reshape(-1, 3) for name, tri in parts if name == "fuselage"])
    z0, z1 = fus[:, 2].min(), fus[:, 2].max()
    bins = np.arange(z0, z1 + 0.5, 0.5)
    rows = []
    for a, b in zip(bins[:-1], bins[1:]):
        sel = fus[(fus[:, 2] >= a) & (fus[:, 2] < b)]
        if len(sel) < 8:
            continue
        rows.append(((a + b) / 2, np.abs(sel[:, 0]).max(), sel[:, 1].min(), sel[:, 1].max()))
    rows = np.asarray(rows)
    rmax = rows[:, 1].max()
    height = rows[:, 3] - rows[:, 2]
    full = rows[(rows[:, 1] >= 0.985 * rmax) & (height >= 0.985 * height.max())]
    za = full[:, 0].min()
    # Titles may start on the gentle forward taper, where the skin is within 3 % of the
    # cabin radius, the way real titles run forward over door 1.
    nearly = rows[(rows[:, 1] >= 0.97 * rmax) & (height >= 0.97 * height.max())]
    zf = nearly[:, 0].max()
    rx = float(np.median(full[:, 1]))
    ymin, ymax = float(np.median(full[:, 2])), float(np.median(full[:, 3]))
    return zf, za, rx, (ymin + ymax) / 2, (ymax - ymin) / 2, z1 - z0


def window_top(parts):
    ys = [tri.reshape(-1, 3)[:, 1] for name, tri in parts
          if name.startswith("cabin_window") and "frame" not in name]
    return float(np.concatenate(ys).max())


def wing_conflict_z(parts, rx, below_y):
    """Leading edge (max z) of any wing/nacelle part reaching above below_y near the fuselage."""
    lead = None
    for name, tri in parts:
        if not name.startswith(("wing", "nacelle", "engine", "propeller", "spinner", "pylon")):
            continue
        p = tri.reshape(-1, 3)
        # Only structure on the fuselage side (a high wing root) blocks paint; a nacelle
        # standing off the side merely hides the title from some angles, as on the real thing.
        near = p[np.abs(p[:, 0]) <= rx + 0.3]
        near = near[near[:, 1] >= below_y]
        if len(near):
            z = float(near[:, 2].max())
            lead = z if lead is None else max(lead, z)
    return lead


def tangent(rx, cy, ry, y):
    s = max(-0.999, min(0.999, (y - cy) / ry))
    theta = math.asin(s)
    x, yy = rx * math.cos(theta), cy + ry * math.sin(theta)
    nx, ny = math.cos(theta) / rx, math.sin(theta) / ry
    n = math.hypot(nx, ny)
    nx, ny = nx / n, ny / n
    return x + nx * PAINT_OFFSET, yy + ny * PAINT_OFFSET, math.degrees(math.atan2(ny, nx))


def layout(cid):
    member, offset = TYPES[cid]
    path = [os.path.join(thumbs.ART, m) for c, m, _ in thumbs.MODELS if c == cid][0]
    parts = thumbs.load_parts(path)
    zf, za, rx, cy, ry, length = fuselage_frame(parts)
    crown = cy + ry
    top = window_top(parts)
    wide = rx > 2.5
    turboprop = rx < 1.5
    gap = 0.06 * rx
    cap_bottom = top + gap
    cap = min(0.60 * (crown - cap_bottom), 1.20 if wide else 0.42 if turboprop else 0.75)
    tx, ty, ttilt = tangent(rx, cy, ry, cap_bottom + cap / 2)
    front = zf - (0.2 if turboprop else 0.4)
    stop = za + 1.0
    conflict = wing_conflict_z(parts, rx, cap_bottom - 0.05)
    if conflict is not None and conflict < front:
        stop = max(stop, conflict + 0.6)
    budget = min(TITLE_FRACTION * length, front - stop)

    reg_cap = 0.16 * rx
    rx_, ry_, rtilt = tangent(rx, cy, ry, top + gap + reg_cap / 2)
    reg_aft = za + (0.4 if turboprop else 0.8)
    return dict(member=member, x=tx, y=ty + offset, z=front, tilt=ttilt, c=cap / CAP_PER_CHARACTER_SIZE,
                budget=budget, rx=rx_, ry=ry_ + offset, rz=reg_aft, rtilt=rtilt, rc=reg_cap / CAP_PER_CHARACTER_SIZE)


def cs_line(l):
    f = lambda v: f"{v:.2f}f"
    g = lambda v: f"{v:.3f}f"
    return (f"            if (Is(type, AircraftType.{l['member']}))\n"
            f"                return new AircraftIdentityMarkingLayout({f(l['x'])}, {f(l['y'])}, {f(l['z'])}, "
            f"{f(l['rx'])}, {f(l['ry'])}, {f(l['rz'])}, {g(l['c'])}, {g(l['rc'])}, "
            f"{f(l['tilt'])}, {f(l['rtilt'])}, {f(l['budget'])});")


def table():
    return "\n".join(cs_line(layout(cid)) for cid in TYPES)


def door_table():
    """Each jet's forward-left (L1) passenger door, where an aerobridge docks (ADR 0113)."""
    lines = []
    for cid, (member, offset) in TYPES.items():
        if offset != -0.68:
            continue                      # turboprops use the regional bays, never a bridge
        path = [os.path.join(thumbs.ART, m) for c, m, _ in thumbs.MODELS if c == cid][0]
        parts = dict(thumbs.load_parts(path))
        name = "door_fwd" if "door_fwd" in parts else "door_left_1"
        p = parts[name].reshape(-1, 3)
        c = p.mean(axis=0)
        lines.append(f"            if (Is(type, AircraftType.{member}))\n"
                     f"                return new AircraftDoor({c[0]:.2f}f, {c[2]:.2f}f, {p[:, 1].min() + offset:.2f}f, "
                     f"{p[:, 1].max() + offset:.2f}f);")
    return "\n".join(lines)


def _block(path, begin, end):
    source = open(path).read()
    return source[source.index(begin) + len(begin):source.index(end)].strip("\n").rstrip()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    generated = table()
    doors = door_table()
    if not args.check:
        print(generated)
        print("// ---- doors (Simulation/AdelaideAerobridges.cs) ----")
        print(doors)
        return 0
    if _block(CS_FILE, "// <generated title layout>", "// </generated title layout>").strip() != generated.strip():
        print("AircraftIdentityMarkings table is stale: rerun scripts/generate-aircraft-title-layout.py")
        return 1
    if _block(DOOR_FILE, "// <generated door layout>", "// </generated door layout>").strip() != doors.strip():
        print("AircraftDoors table is stale: rerun scripts/generate-aircraft-title-layout.py")
        return 1
    print(f"PASS: {len(TYPES)} title layouts and the jet L1 doors match their meshes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
