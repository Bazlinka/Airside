# Parafield first working airport — 8 October 2026

Scope: four runways at YPPF, 30 mapped taxiways, mapped apron/buildings and four
project-owned high-wing trainers. One reserved trainer movement at a time.

Controls: Operations → WATCH PARAFIELD. Pan/orbit/zoom as usual; ADELAIDE or R
returns to the Adelaide overview. Player airline bases and economics are unchanged.

Source: retained `docs/data/parafield-layout-source-v01.json` (OSM/ODbL) and
`parafield-runways-ourairports-v01.json` (public domain). Conflicting legacy
03R/21L surface/lighting facts are resolved against the official airport plan:
https://parafieldairport.com.au/wp-content/uploads/Parafield-Airport-Master-Plan_Final_Digital-Complete-copy.pdf

Regeneration: `python3 scripts/generate-parafield-layout.py` and
`python3 scripts/generate-parafield-trainer.py`, then sync runtime art.

## Validation

- Five focused headless checks passed: full roster/all phases, ground separation,
  clock jumps and continuity, blocked clearance, layout and terrain platform.
- Asset audit: 1,872 GUIDs, 407 byte-identical runtime art mirrors.
- Unity native compilation succeeded and the full suite started; no compilation errors.
- Full Unity and headless runs were stopped at Bailey's explicit request to briefly review and merge immediately.
- Mac rebuild, packaged screenshots, watch-button interaction and complete runtime motion remain unverified.

## Remaining scope limits

Uses one conservative 21R training circuit, not live ATC/wind runway scheduling.
Only 03L/21R is illuminated. Building heights/facades are authored estimates on
mapped footprints; this is not a survey model. Trainer is an original generic
four-seat high-wing type, not a licensed Cessna model. No passenger-route listing,
player base, charter business or aircraft-market addition.
