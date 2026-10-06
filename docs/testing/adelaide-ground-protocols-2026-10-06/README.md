# Adelaide ground protocols task packet — 6 October 2026

Owner: Codex root, branch `fix/adelaide-ground-protocols-20261006`. Separate ownership
from background stall work (`fix/long-flight-stall-20261006`, PR #545).

## Requested outcome

Compare authoritative Adelaide/general taxi rules with the actual simulation, then
implement the agreed priorities: permanent stationary blocking, controlled runway
intersection clearance, correct follower wake spacing and Adelaide route restrictions.
Do not run Unity/player/builds or reproduce a full journey. Sources and comparison are
in `docs/data/ADELAIDE_GROUND_PROTOCOLS.md`; decisions/migration in ADR 0243.

## Scope and acceptance

- No 180-second stationary-blocker escape in clearance or hold explanation.
- Arrival vacates and first taxi-in stretch respect stationary traffic.
- Manual stand selection keeps the choice reserved until that same clearance is available.
- Crossing reservation starts before the nose enters and ends after the tail clears, including vacates still in Landing state.
- Two intersecting runways cannot clear simultaneous conflicting occupancy; queues remain separate.
- Weight/category pair minima, correct airborne/touchdown anchors and persistent save records.
- No prohibited named graph edges by code letter/departure bay; no typed straight-line fallback.
- Eastbound Code C push on either main-runway end; continuous compatible widebody exit/taxi joins.
- Shared service vehicle speed caps from the handbook.
- Deterministic event stepping, legacy save handling, liveness and existing arrival-estimate accuracy preserved.

Existing permitted early/baked rollout locations remain. Code C 23 uses an ATC-advised
E2 variation; implementing default D1 requires an explicit early-rollout profile rather
than a runway backtrack after passing D1. Holds conservatively withhold the whole taxi
route at the stand/exit. No mid-route stop state, radio interface, permit system or
certified swept-envelope physics is claimed. OSM A2 is conservatively filtered in full
above C, and combined L is treated as L1. Unlabelled map edges need a later naming audit.

## Verification

Pre-integration branch checks: 1,744 executed/passed, zero failed (4.3631 minutes).
TRX SHA256: `f5494c02e0501c5b5fbc6ae1798d7cd592ea16460557cad0d14193572483114b`.
Two existing conditional cases were not executed. C# 9 syntax: 21 files, zero errors;
asset audit: 1,772 GUIDs, 386 mirrored art files, 70 character materials; harness fresh;
whitespace check passed. Focused policy/anchor/save/route/blocker tests pass.
Final validation against the newly merged fleet/cockpit main is pending.
The arrival-prediction regression exposed by the intersecting-strip guard was corrected
by sharing that guard with `LandingGroundClear`; focused result: 57/63 within 2 seconds
(>=85% existing acceptance), no early estimates. Code C push direction is checked against
true-east projected into YPAD world coordinates.

No Unity execution by instruction. Headless C# compilation does not establish native
Presentation type resolution. No visual quality or native performance assertion.
