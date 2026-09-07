# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-07 by Cursor (materials AO/glass + props + day/wet/trees)
- **Branch / working tree:** `cursor/authored-fbx-turboprop-terminal-8515` (PR #131)
- **Do this next:** Continue 0025 — Addressables/DCC path, more authored kit fidelity, wet VFX densify. No new economy. Do not block on Mac playtest.
- **In progress / half-done:** AO strength fixed; glass alpha forced; wet mask keyword; tapered 3-blade props (104 meshes); REF day/dusk grade; eucalyptus clumps; Stand 3 + apron joints wet. Still ~20% of REF.
- **Watch out for:** ALS lamp / flood / star / puddle / figure / cloud / bird / tree count vs Mac Play perf; ClearCoat no-op on older URP; preserve art .meta GUIDs when regenerating.
- **Open questions for Bailey:** DCC-authored FBX replace later, or keep procedural authored kits rolling?
- **Visual assets:** Authored Batch C densifying in place; presentation track mid-backlog.

## Current milestone

First playable aircraft loop + presentation track toward REF (decision 0025).
See `docs/product/PROJECT_PLAN.md` and `docs/decisions/0025-packaged-art-path-and-visual-reality.md`.

## Invariants (do not break)

- Domain/Simulation independent of Unity scenes; injected clock; seeded random.
- Reserve runways/taxiways/stands before use.
- Frame rate must not change simulation outcomes.
- Preserve save compatibility.
- No unlicensed assets.

## Controls (prototype)

Space pause · Tab speed · Enter accept offer · F follow · O overview · P priority · M mute.
