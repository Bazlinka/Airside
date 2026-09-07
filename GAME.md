# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-07 by Cursor (service GSE / belt-loader / ALS tip)
- **Branch / working tree:** `cursor/authored-fbx-turboprop-terminal-8515` (PR #131)
- **Do this next:** Continue 0025 — HUD polish / day-wet / env silhouette (fence/trees need
  new assets or Addressables); DCC when Mac-ready. No new economy. Do not block on Mac playtest.
- **In progress / half-done:** Fixed belt-loader kit path (was props, now service); stairs/GPU/
  chocks prefer denser service kit; FOD bin kit; ALS stations reuse lighting kit; apron-safety +
  chocks/GPU Resources densify; markings-kit wet collect. Still ~33% of REF — DCC/Addressables +
  real materials remain the jump.
- **Watch out for:** ALS lamp / flood / star / puddle / figure / cloud / bird / tree / shrub / fence brace / flood densify mesh count vs Mac Play perf; ClearCoat no-op on older URP; preserve art .meta GUIDs when regenerating; wet collect must not match lighting `taxi_*` / `runway_edge_light`.
- **Open questions for Bailey:** DCC-authored FBX replace later, or keep procedural authored kits rolling?
- **Visual assets:** Authored kits wired denser; presentation track mid-backlog.
