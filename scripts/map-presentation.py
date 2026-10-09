#!/usr/bin/env python3
"""Regenerate docs/architecture/PRESENTATION_MAP.md: what each AirsidePrototype partial file owns.

`AirsidePrototype` is one MonoBehaviour split across ~39 partial files. This map is the one-page index of them:
the numbers (lines, instance fields declared there, methods) are measured from the source every run; the
one-line responsibilities are curated below (read from each file's own summary, ADR references and method names).

  python3 scripts/map-presentation.py            rewrite the map
  python3 scripts/map-presentation.py --check    fail if the map is stale or a file has no description

A new `AirsidePrototype.*.cs` file must get a line in OWNERS (area, responsibility) in the same commit; --check says so.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "game/Airside/Assets/Airside/Presentation"
OUT = ROOT / "docs/architecture/PRESENTATION_MAP.md"

AREAS = [
    ("Core", "The single instance, its loop and the opening"),
    ("Player airline UI", "Panels, HUD sheets and maps the player uses"),
    ("Field and lighting build", "Geometry and lights of the Adelaide field, built once"),
    ("Aircraft visuals", "How an aircraft looks and moves on screen (state comes from the simulation)"),
    ("Ground operations visuals", "Vehicles, passengers and the turnaround as drawn"),
    ("Flight views and traffic", "Cockpit, the regional flight world and other aircraft in the sky"),
    ("Sky, weather and audio", "Day cycle, weather presentation and sound"),
]

OWNERS = {
    "AirsidePrototype.AgentGameplay.cs": ("Flight views and traffic", "Hidden isolated agent gameplay command scenarios and evidence reports"),
    "AirsidePrototype.Parafield.cs": ("Independent airports", "Builds and updates Parafield trainer traffic and watches the airport from Operations"),
    "AirsidePrototype.Tower.cs": ("Flight views and traffic", "Control-tower cab view: click the tower to enter, 360-degree look, Esc or LEAVE TOWER to return"),
    "AirsidePrototype.OutstationView.cs": ("Flight views and traffic", "Network aircraft views: read-only timed journey rendering, camera entry/exit and return to Fleet"),
    "AirsidePrototype.cs": ("Core", "The single instance; per-frame loop, selection / follow / reset ownership and most shared state"),
    "AirsidePrototype.Options.cs": ("Core", "Grouped Options, setting actions and title/in-game return routing"),
    "AirsidePrototype.Intro.cs": ("Core", "Title screen and the opening dissolve into the live airport (ADR 0122)"),
    "AirsidePrototype.Soak.cs": ("Core", "Unattended soak mode for packaged builds (heartbeat log, stall detection)"),
    "AirsidePrototype.Airline.cs": ("Player airline UI", "Player-airline layer: start-your-airline panel, fleet panel, destinations map, HUD sheets (ADR 0045)"),
    "AirsidePrototype.Fleet.cs": ("Player airline UI", "Unified Fleet workspace runtime: roster filters and sort, selection inside the sheet (ADR 0239)"),
    "AirsidePrototype.AerodromeBeacon.cs": ("Sky, weather and audio", "Rotating white/green aerodrome beacon on the real-scale tower cab, night only"),
    "AirsidePrototype.FlightMap.cs": ("Flight views and traffic", "Moving map in the cockpit/window/exterior views: own ship, route, airports, fleet (N toggles)"),
    "AirsidePrototype.MiniMap.cs": ("Player airline UI", "Corner map of the airfield with every aircraft as a dot"),
    "AirsidePrototype.FieldTags.cs": ("Player airline UI", "Registration tags floating over aircraft on the field"),
    "AirsidePrototype.FieldBuild.cs": ("Field and lighting build", "Field construction: surfaces and their wet sheen, vegetation, perimeter, stand markings"),
    "AirsidePrototype.YpadPavement.cs": ("Field and lighting build", "Real Adelaide taxiways, aprons, taxi paint and terminal footprints"),
    "AirsidePrototype.YpadLighting.cs": ("Field and lighting build", "Published YPAD lighting: runway edge, threshold, PAPI, approach and stand lights"),
    "AirsidePrototype.BoundaryFence.cs": ("Field and lighting build", "Security fence along the aerodrome boundary (ADR 0181)"),
    "AirsidePrototype.Aerobridges.cs": ("Field and lighting build", "Terminal 1 aerobridges, one per bridged gate (ADR 0113)"),
    "AirsidePrototype.Lights.cs": ("Aircraft visuals", "Aircraft lamps, strobes and cabin glow; collects airfield lights; sun light and lightning"),
    "AirsidePrototype.AircraftVisuals.cs": ("Aircraft visuals", "Engine heat and exhaust, propeller and jet-fan spin and blur, gear tyres, commercial aircraft views"),
    "AirsidePrototype.AircraftLightingFit.cs": ("Aircraft visuals", "Fits crown/belly/aft lamp installations to runtime hulls and carries fallback taxi lamps with nose gear"),
    "AirsidePrototype.Articulation.cs": ("Aircraft visuals", "Control surfaces hinged on their real axes"),
    "AirsidePrototype.Helicopter.cs": ("Aircraft visuals", "Bell 412 in the airline game: rotors and VTOL pose (ADR 0207)"),
    "AirsidePrototype.Doorways.cs": ("Aircraft visuals", "Door hollows on aircraft (AircraftDoorwayGeometry)"),
    "AirsidePrototype.FleetVisuals.cs": ("Aircraft visuals", "Fleet flights along their real tracks; preparing a fleet aircraft's view and ground pose"),
    "AirsidePrototype.ArrivalFinal.cs": ("Aircraft visuals", "Arrivals waiting for the runway fly an extended final"),
    "AirsidePrototype.GroundFlow.cs": ("Aircraft visuals", "Ground movement that never jumps (smoothing across simulation legs, ADR 0144)"),
    "AirsidePrototype.GroundService.cs": ("Ground operations visuals", "Ground vehicles drawn where the service run says they are"),
    "AirsidePrototype.GroundRouting.cs": ("Ground operations visuals", "Vehicles and walkers route round obstacles"),
    "AirsidePrototype.PushbackTugs.cs": ("Ground operations visuals", "A real tug for every tail-first pushback (ADR 0126)"),
    "AirsidePrototype.ServiceWork.cs": ("Ground operations visuals", "Loose bags, galley boxes and trolleys on their way into an aircraft"),
    "AirsidePrototype.Boarding.cs": ("Ground operations visuals", "Boarding: doors and airstairs, ramp, boarding root"),
    "AirsidePrototype.TerminalPeople.cs": ("Ground operations visuals", "Terminal doors, door queue, gate agent, background people"),
    "AirsidePrototype.WalkwayTape.cs": ("Ground operations visuals", "Temporary barrier tape along the route passengers walk (ADR 0187)"),
    "AirsidePrototype.Cockpit.cs": ("Flight views and traffic", "Entering, binding and leaving a cockpit or flight view; cockpit HUD"),
    "AirsidePrototype.CockpitAudio.cs": ("Flight views and traffic", "Cockpit audio"),
    "AirsidePrototype.FlightWorld.cs": ("Flight views and traffic", "Regional flight world: floating origin, streamed terrain, wide overview (ADR 0215, 0251)"),
    "AirsidePrototype.FlightWorldReview.cs": ("Flight views and traffic", "Journey review capture (phase and frame capture helpers)"),
    "AirsidePrototype.LiveTraffic.cs": ("Flight views and traffic", "Real aircraft around Adelaide from adsb.lol, drawn in the sky (ADR 0081)"),
    "AirsidePrototype.SkyTraffic.cs": ("Flight views and traffic", "Sky flights of other airlines shown and hidden"),
    "AirsidePrototype.DistantLights.cs": ("Flight views and traffic", "Far-off aircraft as visible lights (ADR 0142)"),
    "AirsidePrototype.Sky.cs": ("Sky, weather and audio", "Weather presentation (puddles, taxi spray, windsock, gloom), day cycle and star field"),
    "AirsidePrototype.Atmosphere.cs": ("Sky, weather and audio", "Cloud ceiling, horizon banks and ground fog (ADR 0193)"),
    "AirsidePrototype.WeatherEffects.cs": ("Sky, weather and audio", "Rain mesh"),
    "AirsidePrototype.Contrails.cs": ("Sky, weather and audio", "Jet contrails in sky traffic"),
    "AirsidePrototype.LiveWeather.cs": ("Sky, weather and audio", "Adelaide forecast polling and recorded weather input shared with airport rules"),
    "AirsidePrototype.Soundscape.cs": ("Sky, weather and audio", "Working-airport sound bed under the weather (ADR 0136)"),
    "AirsidePrototype.AircraftAudio.cs": ("Sky, weather and audio", "Aircraft heard from the ground point the camera looks at (ADR 0196)"),
}

FIELD = re.compile(r"^        (?:private|internal|public|protected)(?! static| const)(?:\s+readonly)?\s+[\w<>\[\],.? ()]+\s+_\w+\s*(?:=|;)", re.M)
METHOD = re.compile(r"^        (?:private|internal|public|protected)(?:\s+(?:static|override|async|virtual))*\s+[\w<>\[\],.? ()]+\s+\w+\s*\(", re.M)


def measure(path: Path):
    text = path.read_text(encoding="utf-8", errors="replace")
    return text.count("\n"), len(FIELD.findall(text)), len(METHOD.findall(text))


def build() -> tuple[str, list[str]]:
    files = sorted(p.name for p in SRC.glob("AirsidePrototype*.cs"))
    missing = [f for f in files if f not in OWNERS]
    gone = [f for f in OWNERS if f not in files]
    stats = {f: measure(SRC / f) for f in files if f in OWNERS}
    total = sum(s[0] for s in stats.values())
    out = [
        "# AirsidePrototype — map of the partial files",
        "",
        "Generated by `scripts/map-presentation.py` (numbers measured from the source; responsibilities curated in that script).",
        "`AirsidePrototype` is one MonoBehaviour split across partial files; this is the one-page index of what each owns.",
        f"**{len(stats)} files, {total:,} lines.** \"Fields\" counts instance fields *declared in that file*: a file with 0 fields still uses state declared",
        "elsewhere (mostly `AirsidePrototype.cs` and `AirsidePrototype.Airline.cs`), so the real coupling is not visible from these counts.",
        "",
        "Regenerate with `python3 scripts/map-presentation.py`; `--check` fails when a file is missing a description or the map is stale.",
        "A new `AirsidePrototype.*.cs` needs a line in `OWNERS` in the same commit.",
        "",
    ]
    for area, blurb in AREAS:
        rows = [(f, *OWNERS[f], *stats[f]) for f in files if f in OWNERS and OWNERS[f][0] == area]
        if not rows:
            continue
        rows.sort(key=lambda r: -r[3])
        out += [f"## {area}", "", f"*{blurb}* — {sum(r[3] for r in rows):,} lines", "",
                "| File | Lines | Fields | Methods | Owns |", "|---|---:|---:|---:|---|"]
        for f, _, text, lines, fields, methods in rows:
            out.append(f"| `{f}` | {lines:,} | {fields} | {methods} | {text} |")
        out.append("")
    out += [
        "## Reading notes",
        "",
        "- Shared state is concentrated in two files: `AirsidePrototype.cs` and `AirsidePrototype.Airline.cs` declare most instance fields. Splitting by",
        "  responsibility means moving state with the code that owns it, which cannot be verified without a Unity run.",
        "- File names are not always responsibilities: `Sky.cs` is mostly weather presentation, and `Lights.cs` holds aircraft lamps, airfield lights, the sun",
        "  light and lightning.",
        "- Roughly 14 static methods across these files are free of `UnityEngine` and could move to testable classes headlessly; the rest are instance",
        "  methods that read shared fields (survey of 2026-10-07, crude parser).",
    ]
    problems = [f"no description in OWNERS: {f}" for f in missing] + [f"described but missing from the source: {f}" for f in gone]
    return "\n".join(out) + "\n", problems


def main():
    text, problems = build()
    if problems:
        sys.exit("scripts/map-presentation.py: " + "; ".join(problems))
    if "--check" in sys.argv:
        if not OUT.exists() or OUT.read_text() != text:
            sys.exit(f"{OUT.relative_to(ROOT)} is out of date: run python3 scripts/map-presentation.py")
        print("PRESENTATION_MAP.md is up to date")
        return
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text)
    print(f"Wrote {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
