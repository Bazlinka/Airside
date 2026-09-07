# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-07 by Cursor (fence + ops HUD)
- **Branch / working tree:** `cursor/authored-fbx-turboprop-terminal-8515` (PR #131)
- **Do this next:** Continue 0025 — more env/terrain/roads, URP wet/day, HUD/motion polish, presentation bugs, further kit fidelity. No new economy. Do not block on Mac playtest.
- **In progress / half-done:** Batch C authored kits densified across aircraft/buildings/vehicles/props; denser fence mesh + ops HUD coastal accent; wet puddles/day volume/stars from prior tips. Still far from REF (~20% baseline).
- **Watch out for:** Spot flood / star/cloud/puddle/fence count vs Mac Play perf; ClearCoat no-op on older URP; preserve art .meta GUIDs when regenerating.
- **Open questions for Bailey:** DCC-authored FBX replace later, or keep procedural authored kits rolling?
- **Visual assets:** Authored Batch C densifying in place; presentation track mid-backlog.
