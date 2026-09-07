# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-07 by Cursor (ops/props densify + headlight bugfix)
- **Branch / working tree:** `cursor/authored-fbx-turboprop-terminal-8515` (PR #131)
- **Do this next:** Continue 0025 — more motion/life, presentation bugs, further env/URP polish, Addressables path later. No new economy. Do not block on Mac playtest.
- **In progress / half-done:** Ops shed 53, airfield props 43, lighting 33; foam layer pulse; vehicle headlights fixed (no longer beacon-orange). Prior kit densify still in place. Still far from REF (~20% baseline).
- **Watch out for:** ALS lamp / flood / star / puddle / figure / cloud / bird / tree count vs Mac Play perf; ClearCoat no-op on older URP; preserve art .meta GUIDs when regenerating.
- **Open questions for Bailey:** DCC-authored FBX replace later, or keep procedural authored kits rolling?
- **Visual assets:** Authored Batch C densifying in place; presentation track mid-backlog.
