#!/usr/bin/env python3
"""Generate the real Adelaide Airport (YPAD) airside layout for Airside.

Reads the committed OpenStreetMap snapshot docs/data/osm/ypad-aeroways-2026-09-14.json
(© OpenStreetMap contributors, ODbL) and writes
game/Airside/Assets/Airside/Simulation/AdelaideLayout.cs.

Coordinates are the game's runway frame, in metres:
  x — along runway 05/23 from the midpoint of its thresholds, positive towards 23 (NE)
  z — across it, positive to the left of 05→23, i.e. the north-west / terminal side

Beyond raw geometry it bakes the ground routes aircraft actually follow, found on
the real taxiway network and smoothed through junctions:
  vacate   runway 05 rollout end -> exit E2 -> holding point clear of the runway
  lineup   runway 05 holding point (F6) -> takeoff start at the 05 threshold
  taxi-in  E2 holding point -> taxilane T4 -> nose into a regional bay (50A-50D)
  pushback bay -> tail-first arc back onto T4, nose pointing along the taxilane
  taxi-out end of pushback -> T4 / K / A / F -> runway 05 holding point

Run: python3 scripts/generate-ypad-layout.py
"""
import heapq
import json
import math
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "docs/data/osm/ypad-aeroways-2026-09-14.json")
OUTPUT = os.path.join(ROOT, "game/Airside/Assets/Airside/Simulation/AdelaideLayout.cs")

LAT0, LON0 = -34.95, 138.53
RWY05 = {"lat": -34.9585244, "lon": 138.5172171}
RWY23 = {"lat": -34.9406962, "lon": 138.5431392}
RWY12 = {"lat": -34.9412401, "lon": 138.5219814}
RWY30 = {"lat": -34.9493491, "lon": 138.5368788}

# Game stations (CircuitProfile): the landing rolls out here; takeoff starts here.
ROLLOUT_END_X = -200.0
TAKEOFF_START_X = -1500.0
HOLD_05 = (-1530.0, 90.0)
E2_HOLD = (237.0, 199.0)
# Regional bays along taxilane T4. BAY-1..BAY-4 are 50D..50A (the original four); BAY-5/6
# are 50E/50F, added for more regional traffic. 50G is left out: it sits on the bend of T4,
# so the straight lead-in this generator builds would start on the grass.
BAYS = [("BAY-1", "50D"), ("BAY-2", "50C"), ("BAY-3", "50B"), ("BAY-4", "50A"),
        ("BAY-5", "50E"), ("BAY-6", "50F")]
BAY_LEAD = 26.0          # metres of straight taxilane before turning into a bay
PUSHBACK_TAIL = 18.0     # metres the tail travels along the lane after the pushback (50A sits at the end of T4)

# Terminal gates (ADR 0047) are a separate stand system from the regional BAYS. Gate 13
# hosts the domestic 737; Gate 15 hosts trans-Tasman A321neo traffic. Both parking lines
# come directly from the committed OSM snapshot.
TERMINAL_GATES = [("GATE-13", "13"), ("GATE-15", "15")]

# Gate 13 ground geometry, all anchored on OSM features. Its real parking line starts at
# the T1/T2/B1 junction, but OSM's terminal apron only begins at z = 358, leaving a 50 m
# unpaved band. GATE13_LINK fills exactly that band — T2/T1 centreline below, the OSM
# apron edge above, inside that edge's x-extent — so it never overlaps the OSM apron.
GATE_LEAD_IN = 60.0                    # straight nose-in stretch before each stop
GATE13_LINK = [(1520.0, 294.0), (1568.0, 295.0), (1577.0, 297.0), (1611.0, 304.0), (1626.0, 307.2),
               (1626.0, 358.0), (1520.0, 358.0)]
GATE15_LINK = [(1435.0, 300.0), (1518.0, 300.0), (1518.0, 358.0), (1435.0, 358.0)]

GATE_ROUTE = {
    "13": ((1543.0, 294.0), (1600.0, 301.7), (1568.0, 295.0)),
    "15": ((1483.0, 300.7), (1515.0, 301.0), (1483.0, 300.7)),
}


def xy(p):
    return ((p["lon"] - LON0) * 111320.0 * math.cos(math.radians(LAT0)), (p["lat"] - LAT0) * 110574.0)


A, B = xy(RWY05), xy(RWY23)
LENGTH = math.dist(A, B)
U = ((B[0] - A[0]) / LENGTH, (B[1] - A[1]) / LENGTH)
N = (-U[1], U[0])
MID = ((A[0] + B[0]) / 2.0, (A[1] + B[1]) / 2.0)


def local(p):
    q = xy(p)
    v = (q[0] - MID[0], q[1] - MID[1])
    return (v[0] * U[0] + v[1] * U[1], v[0] * N[0] + v[1] * N[1])


def polyline_length(pts):
    return sum(math.dist(a, b) for a, b in zip(pts, pts[1:]))


def resample(pts, step):
    out = [pts[0]]
    carry = 0.0
    for a, b in zip(pts, pts[1:]):
        seg = math.dist(a, b)
        d = step - carry
        while d <= seg:
            t = d / seg
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            d += step
        carry = (carry + seg) % step
    if math.dist(out[-1], pts[-1]) > 0.05:
        out.append(pts[-1])
    return out


def dedupe(pts, tol=0.5):
    out = [pts[0]]
    for p in pts[1:]:
        if math.dist(p, out[-1]) > tol:
            out.append(p)
    return out


def round_corners(pts, radius):
    """Replace each corner with a circular arc tangent to both legs (smooth taxi turns)."""
    pts = dedupe(pts)
    if len(pts) < 3:
        return pts
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        p0, p1, p2 = out[-1], pts[i], pts[i + 1]
        d1 = (p1[0] - p0[0], p1[1] - p0[1])
        d2 = (p2[0] - p1[0], p2[1] - p1[1])
        l1, l2 = math.hypot(*d1), math.hypot(*d2)
        if l1 < 1e-6 or l2 < 1e-6:
            continue
        u1, u2 = (d1[0] / l1, d1[1] / l1), (d2[0] / l2, d2[1] / l2)
        turn = math.acos(max(-1.0, min(1.0, u1[0] * u2[0] + u1[1] * u2[1])))
        if turn < math.radians(4):
            out.append(p1)
            continue
        tangent = min(radius * math.tan(turn / 2.0), 0.45 * l1, 0.45 * l2)
        r = tangent / math.tan(turn / 2.0)
        a = (p1[0] - u1[0] * tangent, p1[1] - u1[1] * tangent)
        b = (p1[0] + u2[0] * tangent, p1[1] + u2[1] * tangent)
        cross = u1[0] * u2[1] - u1[1] * u2[0]
        side = 1.0 if cross > 0 else -1.0
        centre = (a[0] - u1[1] * r * side, a[1] + u1[0] * r * side)
        start = math.atan2(a[1] - centre[1], a[0] - centre[0])
        steps = max(2, int(math.degrees(turn) / 6))
        out.append(a)
        for s in range(1, steps):
            ang = start + side * turn * s / steps
            out.append((centre[0] + r * math.cos(ang), centre[1] + r * math.sin(ang)))
        out.append(b)
    out.append(pts[-1])
    return dedupe(out, 0.3)


def bezier(p0, p1, p2, p3, steps=16):
    pts = []
    for i in range(steps + 1):
        t = i / steps
        mt = 1 - t
        pts.append((
            mt ** 3 * p0[0] + 3 * mt * mt * t * p1[0] + 3 * mt * t * t * p2[0] + t ** 3 * p3[0],
            mt ** 3 * p0[1] + 3 * mt * mt * t * p1[1] + 3 * mt * t * t * p2[1] + t ** 3 * p3[1]))
    return pts


def main():
    data = json.load(open(SOURCE))
    taxiways, aprons, terminals, holds, parking = [], [], [], [], {}
    nodes, adj, label = {}, {}, {}

    def key(p):
        return (round(p[0] / 2) * 2, round(p[1] / 2) * 2)

    def link(a, b, name):
        d = math.dist(nodes[a], nodes[b])
        adj.setdefault(a, {})[b] = d
        adj.setdefault(b, {})[a] = d
        label[(a, b)] = label[(b, a)] = name

    for e in data["elements"]:
        tags = e.get("tags", {})
        kind = tags.get("aeroway")
        if e["type"] == "node":
            if kind == "holding_position":
                holds.append(local(e))
            continue
        pts = [local(p) for p in e["geometry"]]
        if kind == "taxiway":
            width = float(str(tags.get("width", "23")).split()[0])
            taxiways.append((tags.get("ref", ""), width, pts))
        elif kind == "apron":
            aprons.append((tags.get("name", "Apron"), pts[:-1] if pts[0] == pts[-1] else pts))
        elif kind == "terminal":
            terminals.append((tags.get("name", "Terminal"), pts[:-1] if pts[0] == pts[-1] else pts))
        elif kind == "parking_position" and tags.get("ref"):
            parking.setdefault(tags["ref"], pts)
        if kind in ("taxiway", "runway"):
            prev = None
            for p in pts:
                k = key(p)
                nodes.setdefault(k, p)
                if prev is not None and prev != k:
                    link(prev, k, tags.get("ref") or kind)
                prev = k

    keys = list(nodes)
    for i, a in enumerate(keys):
        for b in keys[i + 1:]:
            if 0 < math.dist(nodes[a], nodes[b]) < 6 and b not in adj.get(a, {}):
                link(a, b, "join")

    runway_labels = {"05/23", "12/30", "12", "23", "30", "runway"}

    def route(p, q):
        start = min(nodes, key=lambda k: math.dist(nodes[k], p))
        goal = min(nodes, key=lambda k: math.dist(nodes[k], q))
        dist, prev, heap = {start: 0.0}, {}, [(0.0, start)]
        while heap:
            cost, k = heapq.heappop(heap)
            if k == goal:
                break
            if cost > dist[k]:
                continue
            for m, w in adj[k].items():
                weight = w * (25.0 if label[(k, m)] in runway_labels else 1.0)
                if cost + weight < dist.get(m, 1e18):
                    dist[m], prev[m] = cost + weight, k
                    heapq.heappush(heap, (cost + weight, m))
        path = [goal]
        while path[-1] != start:
            path.append(prev[path[-1]])
        path.reverse()
        names = []
        for a, b in zip(path, path[1:]):
            if label[(a, b)] not in names and label[(a, b)] not in ("join", "taxiway"):
                names.append(label[(a, b)])
        return [nodes[k] for k in path], names

    report = []

    # Vacate: roll forward to E2 and follow it clear of the runway strip.
    e2, e2_names = route((-45.0, 0.0), E2_HOLD)
    vacate = round_corners([(ROLLOUT_END_X, 0.0)] + e2 + [E2_HOLD], 45.0)
    report.append(("vacate", vacate, ["05/23"] + e2_names))

    # Lineup: from the F6 holding point onto the centreline at the 05 threshold.
    lineup = [HOLD_05, (-1537.0, 55.0)] + bezier((-1537.0, 55.0), (-1540.0, 20.0), (-1530.0, 0.0), (TAKEOFF_START_X, 0.0), 12)[1:]
    lineup = dedupe(lineup)
    report.append(("lineup", lineup, ["F6", "05"]))

    bays = []
    for bay_id, ref in BAYS:
        lane, stop = parking[ref][0], parking[ref][-1]
        d = (stop[0] - lane[0], stop[1] - lane[1])
        dl = math.hypot(*d)
        d = (d[0] / dl, d[1] / dl)
        heading = math.degrees(math.atan2(d[0], d[1]))

        entry = (lane[0] - BAY_LEAD, lane[1])
        arrive, arrive_names = route(E2_HOLD, entry)
        arrive = round_corners(arrive + [entry], 35.0)
        turn_in = bezier(entry, (lane[0] - 8.0, lane[1]), (stop[0] - d[0] * 14.0, stop[1] - d[1] * 14.0), stop)
        taxi_in = dedupe(arrive + turn_in[1:])

        push_end = (lane[0] + PUSHBACK_TAIL, lane[1])
        pushback = bezier(stop, (stop[0] - d[0] * 14.0, stop[1] - d[1] * 14.0), (lane[0] + 6.0, lane[1]), push_end)

        depart, depart_names = route(push_end, HOLD_05)
        taxi_out = round_corners([push_end] + depart + [HOLD_05], 35.0)

        bays.append((bay_id, ref, stop, heading, taxi_in, pushback, taxi_out))
        report.append((f"taxi-in {bay_id}/{ref}", taxi_in, ["E2"] + arrive_names))
        report.append((f"pushback {bay_id}/{ref}", pushback, ["stand"]))
        report.append((f"taxi-out {bay_id}/{ref}", taxi_out, depart_names))

    # Terminal gates: routes follow the jet's nose datum (AIR-005's model root), nose in.
    aprons.append(("Gate 13 apron link", GATE13_LINK))
    aprons.append(("Gate 15 apron link", GATE15_LINK))
    terminal_gates = []
    for gate_id, ref in TERMINAL_GATES:
        route_entry, push_end, route_junction = GATE_ROUTE[ref]
        entry, stop = parking[ref][0], parking[ref][-1]
        d = (stop[0] - entry[0], stop[1] - entry[1])
        dl = math.hypot(*d)
        d = (d[0] / dl, d[1] / dl)
        heading = math.degrees(math.atan2(d[0], d[1]))
        lead = (stop[0] - d[0] * GATE_LEAD_IN, stop[1] - d[1] * GATE_LEAD_IN)

        # Taxi-in: E2 -> ... -> T2 eastbound, one left turn onto the lead-in, straight to the stop.
        arrive, arrive_names = route(E2_HOLD, route_entry)
        arrive = round_corners(arrive, 35.0)
        turn_in = bezier(route_entry, (route_entry[0] + 17.0, route_entry[1]),
                         (lead[0] - d[0] * 32.0, lead[1] - d[1] * 32.0), lead, 14)
        taxi_in = dedupe(arrive + turn_in[1:] + [stop])

        # Pushback, tail first: straight back down the lead-in, then the tail swings east onto
        # T1 so the nose ends on the T1 centreline pointing west, ready to taxi out forward.
        back = (stop[0] - d[0] * 58.0, stop[1] - d[1] * 58.0)
        t1 = (1611.0 - 1577.0, 304.0 - 297.0)
        t1l = math.hypot(*t1)
        t1 = (t1[0] / t1l, t1[1] / t1l)
        # The curve arrives along T1's own direction, so the taxi-out leaves without a heading step.
        pushback = dedupe([stop] + bezier(back, (back[0] - d[0] * 30.0, back[1] - d[1] * 30.0),
                                          (push_end[0] - t1[0] * 20.0, push_end[1] - t1[1] * 20.0),
                                          push_end, 14))

        # Taxi-out: forward west along T1 to the junction, then the normal route to runway 05.
        depart, depart_names = route(route_junction, HOLD_05)
        taxi_out = round_corners([push_end, route_junction] + depart + [HOLD_05], 35.0)

        terminal_gates.append((gate_id, ref, stop, heading, taxi_in, pushback, taxi_out))
        report.append((f"taxi-in {gate_id}/{ref}", taxi_in, ["E2"] + arrive_names + ["lead-in"]))
        report.append((f"pushback {gate_id}/{ref}", pushback, ["stand", "T1"]))
        report.append((f"taxi-out {gate_id}/{ref}", taxi_out, ["T1"] + depart_names))

    cross_a, cross_b = local(RWY12), local(RWY30)

    def fmt(v):
        return f"{v:.1f}f"

    def arr(pts, step=None):
        pts = resample(pts, step) if step else pts
        return "new float[] { " + ", ".join(f"{fmt(x)}, {fmt(z)}" for x, z in pts) + " }"

    lines = [
        "// <auto-generated>",
        "// Generated by scripts/generate-ypad-layout.py from docs/data/osm/ypad-aeroways-2026-09-14.json.",
        "// Source data © OpenStreetMap contributors, available under the ODbL. Do not edit by hand.",
        "// </auto-generated>",
        "",
        "namespace Airside.Simulation",
        "{",
        "    /// <summary>A named taxiway centreline and its paved width.</summary>",
        "    public readonly struct AdelaideTaxiway",
        "    {",
        "        public AdelaideTaxiway(string reference, float width, float[] xz) { Reference = reference; Width = width; Xz = xz; }",
        "        public string Reference { get; }",
        "        public float Width { get; }",
        "        /// <summary>Centreline as x,z pairs in runway-frame metres.</summary>",
        "        public float[] Xz { get; }",
        "    }",
        "",
        "    /// <summary>A named outline (apron or terminal footprint), x,z pairs, not closed.</summary>",
        "    public readonly struct AdelaideOutline",
        "    {",
        "        public AdelaideOutline(string name, float[] xz) { Name = name; Xz = xz; }",
        "        public string Name { get; }",
        "        public float[] Xz { get; }",
        "    }",
        "",
        "    /// <summary>A regional stand and the real routes that serve it.</summary>",
        "    public readonly struct AdelaideBay",
        "    {",
        "        public AdelaideBay(string id, string reference, float stopX, float stopZ, float headingDegrees,",
        "            float[] taxiIn, float[] pushback, float[] taxiOut)",
        "        {",
        "            Id = id; Reference = reference; StopX = stopX; StopZ = stopZ; HeadingDegrees = headingDegrees;",
        "            TaxiIn = taxiIn; Pushback = pushback; TaxiOut = taxiOut;",
        "        }",
        "        public string Id { get; }",
        "        /// <summary>The real bay number at Adelaide.</summary>",
        "        public string Reference { get; }",
        "        public float StopX { get; }",
        "        public float StopZ { get; }",
        "        /// <summary>Nose heading when parked, degrees clockwise from +z.</summary>",
        "        public float HeadingDegrees { get; }",
        "        /// <summary>E2 holding point to the stop, nose first.</summary>",
        "        public float[] TaxiIn { get; }",
        "        /// <summary>Stop to the taxilane, tail first.</summary>",
        "        public float[] Pushback { get; }",
        "        /// <summary>End of pushback to the runway 05 holding point.</summary>",
        "        public float[] TaxiOut { get; }",
        "    }",
        "",
        "    /// <summary>",
        "    /// An OSM-derived terminal gate (ADR 0047): a stand system separate from the regional",
        "    /// bays, with routes for a jet's nose datum — nose-in taxi, tail-first pushback onto T1,",
        "    /// forward taxi-out to runway 05.",
        "    /// </summary>",
        "    public readonly struct AdelaideTerminalGate",
        "    {",
        "        public AdelaideTerminalGate(string id, string reference, float noseX, float noseZ, float headingDegrees,",
        "            float[] taxiIn, float[] pushback, float[] taxiOut)",
        "        {",
        "            Id = id; Reference = reference; NoseX = noseX; NoseZ = noseZ; HeadingDegrees = headingDegrees;",
        "            TaxiIn = taxiIn; Pushback = pushback; TaxiOut = taxiOut;",
        "        }",
        "        public string Id { get; }",
        "        public string Reference { get; }",
        "        /// <summary>Parking-position stop / aircraft nose datum in runway-frame metres.</summary>",
        "        public float NoseX { get; }",
        "        public float NoseZ { get; }",
        "        /// <summary>Nose heading, degrees clockwise from +z.</summary>",
        "        public float HeadingDegrees { get; }",
        "        /// <summary>E2 holding point to the nose stop, nose first.</summary>",
        "        public float[] TaxiIn { get; }",
        "        /// <summary>Nose stop to the T1 centreline, tail first.</summary>",
        "        public float[] Pushback { get; }",
        "        /// <summary>End of pushback to the runway 05 holding point, nose first.</summary>",
        "        public float[] TaxiOut { get; }",
        "    }",
        "",
        "    /// <summary>",
        "    /// Real Adelaide Airport airside geometry in the game's runway frame (x along 05/23",
        "    /// from its midpoint, positive towards 23; z positive to the north-west, terminal",
        "    /// side). Generated from OpenStreetMap; see scripts/generate-ypad-layout.py.",
        "    /// </summary>",
        "    public static class AdelaideLayout",
        "    {",
        '        public const string Attribution = "Airfield layout © OpenStreetMap contributors (ODbL)";',
        "",
        f"        public const float MainRunwayLengthMetres = {fmt(LENGTH)};",
        f"        public const float RolloutEndX = {fmt(ROLLOUT_END_X)};",
        f"        public const float TakeoffStartX = {fmt(TAKEOFF_START_X)};",
        "",
        f"        public const float CrossRunwayCenterX = {fmt((cross_a[0] + cross_b[0]) / 2)};",
        f"        public const float CrossRunwayCenterZ = {fmt((cross_a[1] + cross_b[1]) / 2)};",
        f"        public const float CrossRunwayLengthMetres = {fmt(math.dist(cross_a, cross_b))};",
        "        /// <summary>Yaw of 12/30 in the runway frame, degrees (Unity Y rotation).</summary>",
        f"        public const float CrossRunwayYawDegrees = {fmt(math.degrees(math.atan2(-(cross_b[1] - cross_a[1]), cross_b[0] - cross_a[0])))};",
        "",
        f"        public static readonly float[] Runway05Hold = {{ {fmt(HOLD_05[0])}, {fmt(HOLD_05[1])} }};",
        f"        public static readonly float[] E2Hold = {{ {fmt(E2_HOLD[0])}, {fmt(E2_HOLD[1])} }};",
        "        public static readonly float[] HoldingPositions = { " + ", ".join(f"{fmt(x)}, {fmt(z)}" for x, z in holds) + " };",
        "",
        "        public static readonly float[] Vacate = " + arr(vacate, 3.0) + ";",
        "        public static readonly float[] Lineup = " + arr(lineup, 3.0) + ";",
        "",
        "        public static readonly AdelaideBay[] Bays =",
        "        {",
    ]
    for bay_id, ref, stop, heading, taxi_in, pushback, taxi_out in bays:
        lines.append(
            f'            new AdelaideBay("{bay_id}", "{ref}", {fmt(stop[0])}, {fmt(stop[1])}, {fmt(heading)},\n'
            f"                {arr(taxi_in, 3.0)},\n                {arr(pushback, 1.5)},\n                {arr(taxi_out, 3.0)}),")
    lines += ["        };", "", "        public static readonly AdelaideTerminalGate[] TerminalGates =", "        {"]
    for gate_id, ref, stop, heading, taxi_in, pushback, taxi_out in terminal_gates:
        lines.append(
            f'            new AdelaideTerminalGate("{gate_id}", "{ref}", {fmt(stop[0])}, {fmt(stop[1])}, {fmt(heading)},\n'
            f"                {arr(taxi_in, 3.0)},\n                {arr(pushback, 1.5)},\n                {arr(taxi_out, 3.0)}),")
    lines += ["        };", "", "        public static readonly AdelaideTaxiway[] Taxiways =", "        {"]
    for ref, width, pts in taxiways:
        lines.append(f'            new AdelaideTaxiway("{ref}", {fmt(width)}, {arr(pts)}),')
    lines += ["        };", "", "        public static readonly AdelaideOutline[] Aprons =", "        {"]
    for name, pts in aprons:
        lines.append(f'            new AdelaideOutline("{name}", {arr(pts)}),')
    lines += ["        };", "", "        public static readonly AdelaideOutline[] Terminals =", "        {"]
    for name, pts in terminals:
        lines.append(f'            new AdelaideOutline("{name}", {arr(pts)}),')
    lines += ["        };", "    }", "}", ""]

    with open(OUTPUT, "w") as f:
        f.write("\n".join(lines))

    print(f"05/23 {LENGTH:.0f} m · 12/30 {math.dist(cross_a, cross_b):.0f} m centred "
          f"({(cross_a[0] + cross_b[0]) / 2:.0f}, {(cross_a[1] + cross_b[1]) / 2:.0f})")
    print(f"{len(taxiways)} taxiways, {len(aprons)} aprons, {len(terminals)} terminals, {len(holds)} holding positions")
    for name, pts, names in report:
        print(f"  {name:<22} {polyline_length(pts):6.0f} m  via {' '.join(names)}")
    print(f"wrote {os.path.relpath(OUTPUT, ROOT)}")


if __name__ == "__main__":
    main()
