# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-07 by Cursor (ops/props densify + atmosphere)
- **Branch / working tree:** `cursor/authored-fbx-turboprop-terminal-8515` (PR #131)
- **Do this next:** Continue 0025 — densify remaining kits (terminal/hangar/vehicles further), env vegetation/roads, wet/day polish, presentation bugs. No new economy systems. Do not block on Mac playtest.
- **In progress / half-done:** Ops 40 / service 42 / lighting 28 / props 34 authored meshes; stairs/GPU/cones/barriers/signs/dollies place denser parts; night star field + ops dish sweep; wet AO + day-profile golden-hour; landside overflow cars + bush belt. Still ~far from REF (~20% baseline).
- **Watch out for:** Spot flood / star/cloud count vs Mac Play perf; ClearCoat no-op on older URP; preserve art .meta GUIDs when regenerating; full generator rewrite may touch unchanged FBX timestamps.
- **Open questions for Bailey:** DCC-authored FBX replace later, or keep procedural authored kits rolling?
- **Visual assets:** Authored Batch C densifying in place; presentation track mid-backlog.