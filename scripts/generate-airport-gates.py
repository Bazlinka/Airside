#!/usr/bin/env python3
"""Generate AirportGateData.cs (real gate numbers per terminal) from OpenStreetMap aeroway data; --check fails if stale.

Input: docs/data/osm/airport-gates-2026-10-08.json (trimmed from Overpass: aeroway gate/terminal/runway, (c) OpenStreetMap
contributors, ODbL). Each gate joins the nearest OSM terminal that matches one of the terminal rules below. Airports with
no gate data, or with unnamed terminals, are not listed here and keep the Wikipedia/generic template in AirportTemplates.cs.
"""
import json, math, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "docs/data/osm/airport-gates-2026-10-08.json"
OUT = ROOT / "game/Airside/Assets/Airside/Simulation/AirportGateData.cs"

# role: Dom / Intl / Swing; code: widest ICAO letter; bridge: aerobridge (inferred for the main jet terminals);
# carriers: airline codes known to use it (Airline.cs). rule: regex on the OSM terminal name.
RULES = {
    "YMML": [
        ("T1", "Terminal 1 (Qantas)", "Dom", "C", True, "QFA QLK", r"T1"),
        ("T2", "Terminal 2 (International)", "Intl", "E", True, "", r"T2"),
        ("T3", "Terminal 3 (Virgin Australia)", "Dom", "C", True, "VOZ", r"T3"),
        ("T4", "Terminal 4 (Jetstar, Rex)", "Dom", "C", False, "JST REX", r"T4"),
    ],
    "YSSY": [
        ("T1", "Terminal 1 (International)", "Intl", "E", True, "", r"International"),
        ("T2", "Terminal 2 (Jetstar, Virgin)", "Dom", "C", True, "JST VOZ", r"Terminal 2"),
        ("T3", "Terminal 3 (Qantas, Rex)", "Dom", "C", True, "QFA QLK REX", r"Terminal 3"),
    ],
    "YBBN": [
        ("INT", "International Terminal", "Intl", "E", True, "", r"International"),
        ("DOM", "Domestic Terminal", "Dom", "C", True, "QFA QLK VOZ JST REX", r"Domestic"),
    ],
    "YBCG": [("T1", "Terminal", "Dom", "C", True, "QFA QLK VOZ JST REX", r"Terminal")],
    "YSCB": [("T1", "Terminal", "Dom", "C", True, "QFA QLK VOZ JST REX", r"Terminal")],
    "YPDN": [("T1", "Terminal", "Swing", "C", True, "QFA QLK VOZ JST REX", r"Darwin")],
    "YBAS": [("T1", "Terminal", "Dom", "C", False, "QFA QLK VOZ JST REX", r"Alice Springs")],
}

# Perth's OSM terminals are airline-named buildings, not T1-T4, so its gates are grouped by number range instead.
PERTH_GROUPS = [
    ("T1", "Terminal 1 (gates 10-24)", "Swing", "E", True, "VOZ", lambda n: 10 <= n <= 24),
    ("Q1", "Qantas gates 143-156", "Dom", "C", True, "QFA QLK", lambda n: 143 <= n <= 156),
    ("Q2", "Qantas gates 201-219", "Dom", "C", True, "QFA QLK JST", lambda n: 201 <= n <= 219),
    ("T2", "Regional gates 501-604", "Dom", "B", False, "REX", lambda n: 501 <= n <= 604),
]

# Airport-specific gate fixes: OSM combines swing gates into "D10 / I31" (domestic number / international number).
def gate_id(ref):
    ref = ref.strip()
    if re.fullmatch(r"\d+(;\d+){3,}", ref) or re.fullmatch(r"\d+ - \d+", ref):
        return None  # a range label that the individual gates already cover
    return re.sub(r"\s*;\s*", "/", re.sub(r"\s*/\s*", "/", re.sub(r"\s+-\s+", "-", ref))).replace(" ", "")


def distance(a, b):
    return math.hypot((a[0] - b[0]) * 111.0, (a[1] - b[1]) * 111.0 * math.cos(math.radians(a[0])))


def build():
    data = json.loads(SRC.read_text())
    out = {}
    for icao, airport in sorted(data.items()):
        gates = [g for g in airport["gates"] if g.get("ref") and g["ref"] != "?"]
        if icao == "YPPH":
            groups = {}
            for g in gates:
                m = re.match(r"\d+", g["ref"])
                if not m:
                    continue
                for tid, name, role, code, bridge, carriers, test in PERTH_GROUPS:
                    if test(int(m.group())):
                        groups.setdefault(tid, []).append(gate_id(g["ref"]))
                        groups[tid] = [x for x in groups[tid] if x]
            out[icao] = [(tid, name, role, code, bridge, carriers, sorted(set(groups.get(tid, [])), key=natural))
                         for tid, name, role, code, bridge, carriers, _ in PERTH_GROUPS if groups.get(tid)]
            continue
        rules = RULES.get(icao)
        if not rules:
            continue
        centres = []
        for t in airport["terminals"]:
            name = t.get("name") or ""
            for rule in rules:
                if re.search(rule[6], name):
                    centres.append((rule[0], (t["lat"], t["lon"])))
                    break
        if not centres:
            continue
        by_terminal = {}
        for g in gates:
            tid = min(centres, key=lambda c: distance(c[1], (g["lat"], g["lon"])))[0]
            by_terminal.setdefault(tid, set()).add(gate_id(g["ref"]))
            by_terminal[tid].discard(None)
        # Combined "D10/I31" swing gates mark an international-capable gate at Gold Coast.
        listing = []
        for tid, name, role, code, bridge, carriers, _ in rules:
            ids = sorted(by_terminal.get(tid, ()), key=natural)
            if ids:
                listing.append((tid, name, role, code, bridge, carriers, ids))
        out[icao] = listing
    return out


def natural(text):
    return [int(p) if p.isdigit() else p for p in re.split(r"(\d+)", text)]


ROLE = {"Dom": "GateUse.Domestic", "Intl": "GateUse.International", "Swing": "GateUse.Swing"}


def render(terminals_by_icao):
    lines = [
        "// <auto-generated>",
        "// Generated by scripts/generate-airport-gates.py from docs/data/osm/airport-gates-2026-10-08.json.",
        "// Source data (c) OpenStreetMap contributors, available under the ODbL. Do not edit by hand.",
        "// </auto-generated>",
        "",
        "using System.Collections.Generic;",
        "using Airside.Domain;",
        "",
        "namespace Airside.Simulation",
        "{",
        "    /// <summary>Real gate numbers by terminal for the airports OpenStreetMap maps in enough detail.</summary>",
        "    internal static class AirportGateData",
        "    {",
        "        public static bool TryFor(string icao, out TerminalTemplate[] terminals)",
        "        {",
        "            terminals = null;",
        "            switch (icao)",
        "            {",
    ]
    for icao, terminals in terminals_by_icao.items():
        lines.append(f'                case "{icao}":')
        lines.append("                    terminals = new[]")
        lines.append("                    {")
        for tid, name, role, code, bridge, carriers, ids in terminals:
            quoted = ", ".join(f'"{i}"' for i in ids)
            roles = ROLE[role]
            lines.append(f'                        Terminal("{tid}", "{name}", {roles}, \'{code}\', {str(bridge).lower()}, "{carriers}", {quoted}),')
        lines.append("                    };")
        lines.append("                    return true;")
    lines += [
        "            }",
        "",
        "            return false;",
        "        }",
        "",
        "        private static TerminalTemplate Terminal(string id, string name, GateUse use, char code, bool aerobridge,",
        "            string carriers, params string[] gateRefs)",
        "        {",
        "            var gates = new List<GateTemplate>(gateRefs.Length);",
        "            foreach (var gate in gateRefs)",
        "            {",
        "                // A \"D10/I31\" gate serves both flows; a \"D5\" or \"I3\" gate only its own.",
        "                var swing = gate.Contains(\"/\") && gate.StartsWith(\"D\") && gate.Contains(\"I\");",
        "                gates.Add(new GateTemplate(id + \"-\" + gate, id, code, aerobridge, swing ? GateUse.Swing : use, FactBasis.Inferred));",
        "            }",
        "",
        "            return new TerminalTemplate(id, name, gates, carriers.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));",
        "        }",
        "    }",
        "}",
        "",
    ]
    return "\n".join(lines)


def main():
    text = render(build())
    if "--check" in sys.argv:
        if not OUT.exists() or OUT.read_text() != text:
            print("AirportGateData.cs is stale: run scripts/generate-airport-gates.py")
            sys.exit(1)
        print("AirportGateData.cs is up to date")
        return
    OUT.write_text(text)
    print("wrote", OUT.relative_to(ROOT))


if __name__ == "__main__":
    main()
