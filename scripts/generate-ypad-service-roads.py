#!/usr/bin/env python3
"""Generate the Adelaide Airport (YPAD) airside service-road network for Airside.

Reads the committed OpenStreetMap snapshot docs/data/osm/ypad-service-roads-2026-09-23.json
(© OpenStreetMap contributors, ODbL) and writes
game/Airside/Assets/Airside/Simulation/AdelaideServiceRoads.cs.

Coordinates are the game's runway frame, in metres, matching generate-ypad-layout.py:
  x — along runway 05/23 from the midpoint of its thresholds, positive towards 23 (NE)
  z — across it, positive to the left of 05→23, i.e. the north-west / terminal side

Only airside roads are kept. Landside parking aisles and driveways are dropped by tag;
the survivors are the roads whose OSM tags mark them airside (access=no,
motor_vehicle=private) plus the named Airside Access Road, Security Road and
Localiser Road.

The apron frontage road (OSM way 230893941) runs along the Terminal 1 face beneath the
aerobridges modelled by ADR 0113. OSM carries no tunnel, covered or layer tag anywhere in
this extract, so the under-terminal undercroft is authored here rather than imported, and
is marked as such in the generated file.

Run: python3 scripts/generate-ypad-service-roads.py
"""
import json
import math
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "docs/data/osm/ypad-service-roads-2026-09-23.json")
OUTPUT = os.path.join(ROOT, "game/Airside/Assets/Airside/Simulation/AdelaideServiceRoads.cs")

LAT0, LON0 = -34.95, 138.53
RWY05 = {"lat": -34.9585244, "lon": 138.5172171}
RWY23 = {"lat": -34.9406962, "lon": 138.5431392}

# The apron frontage road: the one airside way that runs the length of the T1 face.
APRON_FRONTAGE_WAY = 230893941

# Named ways worth keeping as the wider airside network.
NAMED = {
    230893942: "Airside Access Road",
    230893944: "Security Road",
    93177136: "Localiser Road",
}

# Authored undercroft spur. Two facts from the real data decide this:
#   * OSM carries no tunnel, covered or layer tag anywhere in the extract; and
#   * the real frontage road runs about 91 m off the terminal's airside wall (road z~346,
#     wall z~437), which is 44 m clear even of a fully extended aerobridge (reach 47.5 m).
# So the real road passes under neither the building nor the bridges, and an undercroft
# cannot be marked on it honestly. Instead a short spur is authored from the frontage in
# to the baggage hall beneath the terminal, and flagged as authored (ADR 0115).
UNDERCROFT_SPUR_X = 1250.0          # where the spur leaves the frontage
TERMINAL_WALL_Z = 437.0             # airside wall, from AdelaideTerminalArchitecture
SPUR_LANDSIDE_Z = 470.0             # baggage hall, inside the footprint


def xy(p):
    return ((p["lon"] - LON0) * 111320.0 * math.cos(math.radians(LAT0)),
            (p["lat"] - LAT0) * 110574.0)


A, B = xy(RWY05), xy(RWY23)
LENGTH = math.dist(A, B)
U = ((B[0] - A[0]) / LENGTH, (B[1] - A[1]) / LENGTH)
N = (-U[1], U[0])
MID = ((A[0] + B[0]) / 2.0, (A[1] + B[1]) / 2.0)


def local(p):
    q = xy(p)
    v = (q[0] - MID[0], q[1] - MID[1])
    return (v[0] * U[0] + v[1] * U[1], v[0] * N[0] + v[1] * N[1])


def resample(pts, step=6.0):
    """Even spacing so a vehicle advancing by metres never skips a corner."""
    if len(pts) < 2:
        return list(pts)
    out = [pts[0]]
    carry = 0.0
    for a, b in zip(pts, pts[1:]):
        seg = math.dist(a, b)
        if seg <= 1e-6:
            continue
        t = (step - carry) / seg
        while t <= 1.0:
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            t += step / seg
        carry = (carry + seg) % step
    if math.dist(out[-1], pts[-1]) > 0.5:
        out.append(pts[-1])
    return out


def load():
    doc = json.load(open(SOURCE))
    nodes = {e["id"]: e for e in doc["elements"] if e["type"] == "node"}
    ways = [e for e in doc["elements"] if e["type"] == "way"]
    return doc, nodes, ways


def fmt(pts):
    return ", ".join(f"{x:.1f}f, {z:.1f}f" for x, z in pts)


def main():
    doc, nodes, ways = load()
    base = doc.get("osm3s", {}).get("timestamp_osm_base", "unknown")

    frontage = None
    others = []
    for w in ways:
        pts = [local(nodes[n]) for n in w["nodes"] if n in nodes]
        if len(pts) < 2:
            continue
        if w["id"] == APRON_FRONTAGE_WAY:
            frontage = resample(pts)
        elif w["id"] in NAMED:
            others.append((NAMED[w["id"]], resample(pts, 12.0)))

    if frontage is None:
        raise SystemExit("apron frontage way not found in the snapshot")

    # Order the frontage nose-to-tail along the terminal (increasing x).
    if frontage[0][0] > frontage[-1][0]:
        frontage.reverse()

    length = sum(math.dist(a, b) for a, b in zip(frontage, frontage[1:]))

    # Authored spur: leaves the frontage, crosses the apron and runs beneath the terminal
    # into the baggage hall. Everything from the wall inwards is under the building.
    join = min(range(len(frontage)), key=lambda i: abs(frontage[i][0] - UNDERCROFT_SPUR_X))
    jx, jz = frontage[join]
    spur = resample([(jx, jz), (jx, TERMINAL_WALL_Z - 26.0), (jx, SPUR_LANDSIDE_Z)], 5.0)
    lo = next(i for i, (_, z) in enumerate(spur) if z >= TERMINAL_WALL_Z - 20.0)
    hi = len(spur) - 1

    lines = []
    w = lines.append
    w("// GENERATED by scripts/generate-ypad-service-roads.py — do not edit by hand.")
    w("// Source: docs/data/osm/ypad-service-roads-2026-09-23.json")
    w(f"// © OpenStreetMap contributors, ODbL. OSM base {base}.")
    w("")
    w("namespace Airside.Simulation")
    w("{")
    w("    /// <summary>")
    w("    /// The real airside service-road network at Adelaide, in the game's runway frame.")
    w("    ///")
    w("    /// Ground vehicles use these instead of appearing beside the aircraft: the apron")
    w("    /// frontage road runs the length of the Terminal 1 face, beneath the aerobridges")
    w("    /// modelled by ADR 0113, and every stand is reached by a short lead off it.")
    w("    ///")
    w("    /// No UnityEngine types: the routing contract is checked by the headless harness.")
    w("    /// </summary>")
    w("    public static class AdelaideServiceRoads")
    w("    {")
    w('        public const string Attribution = "Service roads © OpenStreetMap contributors";')
    w(f'        public const string OsmBase = "{base}";')
    w("")
    w("        /// <summary>Apron frontage road along the Terminal 1 face, ordered south-west to north-east.</summary>")
    w("        public static readonly float[] ApronFrontage =")
    w("        {")
    for i in range(0, len(frontage), 6):
        w("            " + fmt(frontage[i:i + 6]) + ",")
    w("        };")
    w("")
    w(f"        /// <summary>Total length of the frontage road, metres.</summary>")
    w(f"        public const float ApronFrontageMetres = {length:.1f}f;")
    w("")
    w("        /// <summary>")
    w("        /// Authored spur from the frontage into the baggage hall beneath the terminal.")
    w("        ///")
    w("        /// Unlike the frontage this is NOT imported: OSM has no tunnel, covered or layer")
    w("        /// tag at Adelaide, and the real frontage runs about 91 m off the airside wall —")
    w("        /// 44 m clear even of a fully extended aerobridge — so nothing in the real data")
    w("        /// passes under the building. The spur is a design decision (ADR 0115).")
    w("        /// </summary>")
    w("        public static readonly float[] UndercroftSpur =")
    w("        {")
    for i in range(0, len(spur), 6):
        w("            " + fmt(spur[i:i + 6]) + ",")
    w("        };")
    w("")
    w(f"        /// <summary>Index on the spur where it passes under the terminal wall.</summary>")
    w(f"        public const int UndercroftFirstIndex = {lo};")
    w(f"        public const int UndercroftLastIndex = {hi};")
    w("")
    w(f"        /// <summary>Frontage index the spur branches from.</summary>")
    w(f"        public const int UndercroftJoinIndex = {join};")
    w("")
    for name, pts in sorted(others):
        ident = name.replace(" ", "")
        w(f"        /// <summary>{name} (OSM name tag).</summary>")
        w(f"        public static readonly float[] {ident} =")
        w("        {")
        for i in range(0, len(pts), 6):
            w("            " + fmt(pts[i:i + 6]) + ",")
        w("        };")
        w("")
    w("    }")
    w("}")

    open(OUTPUT, "w").write("\n".join(lines) + "\n")
    print(f"  wrote {os.path.relpath(OUTPUT, ROOT)}")
    print(f"  frontage: {len(frontage)} points, {length:.0f} m")
    print(f"  undercroft spur: {len(spur)} points, under-terminal from index {lo} to {hi}, joins frontage at {join}")
    for name, pts in sorted(others):
        print(f"  {name}: {len(pts)} points")


if __name__ == "__main__":
    main()
