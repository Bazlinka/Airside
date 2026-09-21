#!/usr/bin/env python3
"""Import and generate Adelaide Airport operational building footprints.

The optional XML import consumes an official OpenStreetMap API map response,
selects airport-side operational buildings, and writes the small committed JSON
snapshot used by the deterministic C# generator.

Run:
  python3 scripts/generate-ypad-buildings.py --import-xml work/ypad-terminal-map-2026-09-21.osm
  python3 scripts/generate-ypad-buildings.py
"""
import argparse
import json
import math
import os
import re
import xml.etree.ElementTree as ET


ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SNAPSHOT = os.path.join(ROOT, "docs/data/osm/ypad-buildings-2026-09-21.json")
OUTPUT = os.path.join(ROOT, "game/Airside/Assets/Airside/Simulation/AdelaideBuildings.cs")

LAT0, LON0 = -34.95, 138.53
RWY05 = {"lat": -34.9585244, "lon": 138.5172171}
RWY23 = {"lat": -34.9406962, "lon": 138.5431392}


def xy(point):
    return (
        (point["lon"] - LON0) * 111320.0 * math.cos(math.radians(LAT0)),
        (point["lat"] - LAT0) * 110574.0,
    )


A, B = xy(RWY05), xy(RWY23)
LENGTH = math.dist(A, B)
U = ((B[0] - A[0]) / LENGTH, (B[1] - A[1]) / LENGTH)
N = (-U[1], U[0])
MID = ((A[0] + B[0]) / 2.0, (A[1] + B[1]) / 2.0)


def local(point):
    q = xy(point)
    v = (q[0] - MID[0], q[1] - MID[1])
    return (v[0] * U[0] + v[1] * U[1], v[0] * N[0] + v[1] * N[1])


def airport_building(tags, centre):
    name = tags.get("name", "").lower()
    building = tags.get("building", "").lower()
    aeroway = tags.get("aeroway", "").lower()
    amenity = tags.get("amenity", "").lower()
    tourism = tags.get("tourism", "").lower()
    x, z = centre

    # The existing high-detail terminal and RFDS shell remain authoritative.
    if "domestic & international terminal" in name or "flying doctor" in name:
        return False
    if not (-950.0 <= x <= 1800.0 and 175.0 <= z <= 1450.0):
        return False
    if tourism == "hotel" or building in {"retail", "supermarket", "house", "residential", "carport", "roof"}:
        return False
    if aeroway in {"hangar", "control_tower"} or building == "hangar" or amenity == "fire_station":
        return True
    if not building:
        return False

    operational_words = (
        "airport", "aviation", "aerobond", "cobham", "dnata", "freight", "helistar",
        "pilatus", "pulse", "regional express", "rex", "sapol", "sharp", "toll priority",
    )
    if any(word in name for word in operational_words):
        return True

    # Unnamed support buildings inside the airfield-side band are still valuable context.
    return not name and building in {"yes", "industrial", "commercial", "service"}


def import_xml(path):
    tree = ET.parse(path)
    root = tree.getroot()
    nodes = {
        node.attrib["id"]: {"lat": float(node.attrib["lat"]), "lon": float(node.attrib["lon"])}
        for node in root.findall("node")
    }
    elements = []
    for way in root.findall("way"):
        tags = {tag.attrib["k"]: tag.attrib["v"] for tag in way.findall("tag")}
        geometry = [nodes[nd.attrib["ref"]] for nd in way.findall("nd") if nd.attrib["ref"] in nodes]
        if len(geometry) < 4 or geometry[0] != geometry[-1]:
            continue
        footprint = geometry[:-1]
        centre = tuple(sum(value) / len(footprint) for value in zip(*(local(p) for p in footprint)))
        if not airport_building(tags, centre):
            continue
        kept_tags = {
            key: tags[key]
            for key in ("name", "building", "aeroway", "amenity", "height", "building:levels")
            if key in tags
        }
        elements.append({
            "type": "way",
            "id": int(way.attrib["id"]),
            "tags": kept_tags,
            "geometry": geometry,
        })

    os.makedirs(os.path.dirname(SNAPSHOT), exist_ok=True)
    with open(SNAPSHOT, "w", encoding="utf-8") as target:
        json.dump({
            "version": 0.6,
            "generator": "Airside scripts/generate-ypad-buildings.py",
            "copyright": "OpenStreetMap contributors",
            "license": "https://opendatacommons.org/licenses/odbl/1-0/",
            "source_bbox": "138.520,-34.955,138.540,-34.935",
            "retrieved": "2026-09-21",
            "elements": sorted(elements, key=lambda e: e["id"]),
        }, target, indent=2)
        target.write("\n")
    print(f"imported {len(elements)} operational buildings to {os.path.relpath(SNAPSHOT, ROOT)}")


def kind_for(tags):
    name = tags.get("name", "").lower()
    if tags.get("aeroway") == "control_tower" or "control tower" in name:
        return "ControlTower"
    if tags.get("amenity") == "fire_station" or "fire station" in name:
        return "FireStation"
    if tags.get("aeroway") == "hangar" or tags.get("building") == "hangar":
        return "Hangar"
    if "freight" in name or "dnata" in name or "toll priority" in name:
        return "Freight"
    if tags.get("building") == "parking" or "car park" in name:
        return "CarPark"
    return "Support"


def parse_number(value):
    if not value:
        return None
    match = re.search(r"[-+]?\d+(?:\.\d+)?", value)
    return float(match.group(0)) if match else None


def height_for(tags, kind):
    height = parse_number(tags.get("height"))
    if height:
        return height
    levels = parse_number(tags.get("building:levels"))
    if levels:
        return levels * 3.4
    return {
        "ControlTower": 34.0,
        "FireStation": 8.0,
        "Hangar": 10.0,
        "Freight": 8.0,
        "CarPark": 10.2,
        "Support": 6.5,
    }[kind]


def cs(value):
    return value.replace("\\", "\\\\").replace('"', '\\"')


def fmt(value):
    return f"{value:.1f}f"


def generate():
    with open(SNAPSHOT, encoding="utf-8") as source:
        data = json.load(source)
    buildings = []
    for element in data["elements"]:
        tags = element.get("tags", {})
        kind = kind_for(tags)
        points = [local(p) for p in element["geometry"][:-1]]
        buildings.append({
            "id": f"OSM-{element['id']}",
            "name": tags.get("name", f"Airport support building {element['id']}"),
            "kind": kind,
            "height": height_for(tags, kind),
            "points": points,
        })
    buildings.sort(key=lambda b: (b["kind"], b["name"], b["id"]))

    lines = [
        "// Generated by scripts/generate-ypad-buildings.py. Do not hand-edit.",
        "namespace Airside.Simulation",
        "{",
        "    public enum AdelaideBuildingKind { Support, Hangar, ControlTower, FireStation, Freight, CarPark }",
        "",
        "    public readonly struct AdelaideBuilding",
        "    {",
        "        public AdelaideBuilding(string id, string name, AdelaideBuildingKind kind, float heightMetres, float[] xz)",
        "        {",
        "            Id = id; Name = name; Kind = kind; HeightMetres = heightMetres; Xz = xz;",
        "        }",
        "",
        "        public string Id { get; }",
        "        public string Name { get; }",
        "        public AdelaideBuildingKind Kind { get; }",
        "        public float HeightMetres { get; }",
        "        public float[] Xz { get; }",
        "    }",
        "",
        "    /// <summary>Operational YPAD building footprints from OpenStreetMap (ODbL).</summary>",
        "    public static class AdelaideBuildings",
        "    {",
        '        public const string Attribution = "Building data © OpenStreetMap contributors (ODbL)";',
        "        public static readonly AdelaideBuilding[] All =",
        "        {",
    ]
    for building in buildings:
        coords = ", ".join(fmt(value) for point in building["points"] for value in point)
        lines.append(
            f'            new AdelaideBuilding("{building["id"]}", "{cs(building["name"])}", '
            f'AdelaideBuildingKind.{building["kind"]}, {fmt(building["height"])}, new[] {{ {coords} }}),'
        )
    lines.extend(["        };", "    }", "}", ""])
    with open(OUTPUT, "w", encoding="utf-8") as target:
        target.write("\n".join(lines))
    print(f"wrote {len(buildings)} buildings to {os.path.relpath(OUTPUT, ROOT)}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--import-xml", help="official OSM API .osm file to normalize")
    args = parser.parse_args()
    if args.import_xml:
        import_xml(args.import_xml)
    generate()


if __name__ == "__main__":
    main()
