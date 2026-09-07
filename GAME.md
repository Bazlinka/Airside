# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-07 by Cursor (wrap for Bailey merge of PR #131)
- **Branch / working tree:** `cursor/authored-fbx-turboprop-terminal-8515` (PR #131) — tip
  `443eb20`, clean, pushed. Ready for Bailey to merge after Mac Unity confirm.
- **Do this next:** Bailey: merge PR #131 when satisfied. Post-merge on `main`: continue 0025
  (Addressables/DCC materials; authored fence/tree kits). No new economy.
- **In progress / half-done:** Nothing half-done on this branch. Session tips include belt-loader
  kit-path fix, service GSE densify, HUD/day/wet/terrain, hangar 165 / ops 127 / terminal 191.
  Honest REF ~37% — DCC/Addressables remain the jump.
- **Watch out for:** Mac Play mesh-count/perf (flood/ALS/fence densify); ClearCoat no-op on older
  URP; preserve art .meta GUIDs; wet collect must not match lighting `taxi_*` / `runway_edge_light`.
  Cloud evidence is `scripts/test-domain.sh` 105/105 only — run `scripts/test-unity.sh` / Play
  before treating the merge as Unity-verified.
- **Open questions for Bailey:** DCC-authored FBX replace later, or keep procedural authored kits rolling?
- **Visual assets:** Authored kits denser; presentation track mid-backlog after merge.
