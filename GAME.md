# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-07 by Cursor (HUD layout + night lighting tip)
- **Branch / working tree:** `cursor/authored-fbx-turboprop-terminal-8515` (PR #131)
- **Do this next:** Continue 0025 — more REF-004 HUD hierarchy, env densify, Addressables path later. No new economy. Do not block on Mac playtest.
- **In progress / half-done:** Wiring tip + night/wet/HUD tip: GSE headlight aim, fuel truck lamps (51), cabin glow glass-only, wet ClearCoat fallback, Toolkit REF-004 layout (ops TL / economy TC / turnaround BL / speed BC) + Batch E icons. Still ~20% of REF.
- **Watch out for:** ALS lamp / flood / star / puddle / figure / cloud / bird / tree count vs Mac Play perf; ClearCoat no-op on older URP; preserve art .meta GUIDs when regenerating.
- **Open questions for Bailey:** DCC-authored FBX replace later, or keep procedural authored kits rolling?
- **Visual assets:** Authored Batch C densifying in place; presentation track mid-backlog.
