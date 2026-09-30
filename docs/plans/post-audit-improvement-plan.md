# Post-audit improvement plan

Status: **active** · 2026-09-30 · Author: Cursor (comprehensive code audit) · Branch `main` · ADR **0205**

This is the standing backlog for what to improve next before a major expansion.
It comes from a full Domain / Simulation / Presentation / tests / product-plan audit
on `main` (headless `scripts/test-domain.sh` **1129/1129** at audit time). Cloud agents
and other tools should follow this order unless Bailey overrides it.

Related: `docs/plans/visual-overhaul-plan.md` (ADR 0198, awaiting Bailey sign-off).

## Verdict (do not re-litigate)

Airside is already a production-grade Adelaide airline game, not a prototype.
Career, saves (v19), tower, AI traffic, Glass Cockpit and the map stack are solid.
The main risk before going bigger is **verification debt**, then **Presentation
concentration / performance**, then finishing half-done product slices (freight).

## Priority order

### P0 — Mac packaged playtest of unverified merges (do first)

**Status:** automated Mac captures recorded in #490; manual listening/play checks remain open. See
`docs/testing/post-audit-p0-2026-09-30/RESULTS.md`. Night-sky framing + review follow helpers
(`auto-landing` / `auto-takeoff` / freighter / hangar check) are in #491; Mac re-run:
`scripts/review-post-audit-p0-remaining.sh` after rebuild. A Unity player and awake display
are required. Cloud Linux cannot mark this complete. Self-hosted Mac workers may show
`eligibleForSubagent: true`, but `Task` cannot pin them. Pin via agents UI environment
picker or API v1 `env: { "type": "machine", "name": "Bailey's MacBook Pro" }` (machine
**name**, not `worker_id`). One-paste prompt: `docs/testing/post-audit-p0-mac-agent-prompt.md`.

Many ADRs merged green on EditMode / headless but were never seen or heard in a
rebuilt game. Attribute bugs before adding content.

| Check | ADR / notes |
|---|---|
| Aircraft audio at overview + follow; tune `AircraftAudioMix.ZoomGain` | 0192, 0196 |
| Sky traffic cruises at night overview; no double inbound on final | 0195 |
| Freighter livery at follow; jet rotation/flare tyres on the ground | 0194 |
| Weather clear/cloudy/overcast/rain/storm/fog from overview + follow | 0193 (prior Mac pack in `docs/testing/weather-2026-09-30/`; re-check tip) |
| Hangar tow / berths / quiet tow; temporary boarding tape | 0186–0188, 0196 |
| Far zoom, far land cover, follow-camera feel, zoom-in stays under cursor | 0185, 0189–0191 |
| Arrivals already on final land through a storm | 0190 |
| Terminal doors / facade detail (airside + kerb) | 0197 |
| Human-ops close matrix (clipping, scale, bridge glass) | 0174 |

**Run on Mac:** `scripts/build-mac.sh`, then
`scripts/review-post-audit-p0-remaining.sh` (night-sky + follow stills) or the full
`scripts/review-post-audit-p0.sh`. Checklist: `docs/testing/post-audit-p0-manual-checklist.md`.
Captures go to `work/captures/post-audit-p0-<date>/`.

Exit: Bailey marks each keep / fix / revert. Close or amend ADR “Unity look not
verified” lines when eyes-on is done.

### P1 — Visual overhaul gate

1. Bailey signs off (or rejects) `docs/plans/visual-overhaul-plan.md`.
2. If yes: **Phase 0 baseline** (named captures + fps / p95 / SetPass / batches)
   before Phase 1 ground/land. No visual phase lands without a before/after number.
3. Building audit: 71/78 heights are still rule defaults (`docs/data/ypad-buildings-audit.md`).

**In flight / ahead of gate:**
- Phase 0 bookmarks (ADR 0200) are **merged to `main` via #485** and in the canonical checkout;
  Mac `capture-visual-baseline.sh` metrics still owed.
- Phase 1 oil stains (ADR 0201) + apron wear (ADR 0202) **merged to `main` via #486**
  before P0 / Bailey sign-off. Treat as parallel visual work already on the Mac P0
  tip; it does **not** clear P0 keep/fix/revert. Eyeball those ground slices during P0.

### P2 — Finish freight as a mode (parked until P0)

**Parked until P0 is signed off.** Player freighter refit exists (save v19) but has
not been seen in a rebuilt game. Do not add AI freighters or a cargo apron on top of
unverified player freight.

When P0 clears the freighter/tyre rows, finish as one narrow ADR slice:

- AI freight carriers (DHL / Qantas Freight–style, night bank, own liveries)
- Cargo apron / stands
- Contracts that require a freighter (`ContractKind.Freight` today does not)
- Outstation settle using freighter forecast overload

Until then: leave player freight as-is; no freight AI / apron / freighter-gated
contracts.

### P3 — Pay down structure and performance debt

- Decouple `AirsidePrototype` partials into builder / presenter classes; remove statics
  (file split done; ownership not done — ~25.6k lines across 31 partials).
- Lock a graphics-on budget: 60 fps at 1600×900 overview on the dev Mac; storm/fog
  currently not locked (~37 fps storm in noisy runs).
- Keep `-airsideFullAirport` only until Bailey agrees to retire it
  (`docs/architecture/LEGACY_FULL_AIRPORT_PATH.md`).
- Prefer shared career resource rules when adding new pads/stands (cargo apron,
  helipad) instead of more special cases on `AirlineOperations`.

### P4 — Only then expand

Do not start these until P0–P3 are in a healthy place (or Bailey explicitly
overrides):

| Expansion | Blocker |
|---|---|
| Second playable airport / multi-hub | Restore hard-requires `HomeCode == ADL` |
| Companion / CloudKit | No `companion/` code; needs stable save contract |
| Dense city life / interiors / authored GSE atlas | Needs P1 FPS budget |
| Wages, fuel market, loans, pax-level fares | Intentionally deferred product non-goals |
| Broad new aircraft / airline catalogue | First-session playtest of current loop first |

## What is already solid (build on this)

- Domain → Simulation → Presentation split; injected clock; seeded RNG; exactly-once settlements
- Career ADR 0120: tiers, contracts, funds, reliability, goals, outstations, repeat schedules, finale → sandbox
- 13 genuine aircraft types; dual-runway tower; boarding; hangar checks
- Adelaide from OSM (roads, car parks, precinct, boundary, buildings)
- Glass Cockpit HUD, title screen, airline setup
- Headless CI (`.github/workflows/headless.yml`); Unity EditMode is the merge truth on a Mac
- Career balance bot reaches established airline with 0 stuck flags (human pacing still unproven)

## Standing rules for agents

1. Read this plan and the top of `GAME.md` before choosing work.
2. One change, one owner; `feature/<name>` or `cursor/<name>-…` with non-overlapping files.
3. Behaviour changes: `scripts/test-domain.sh` here; `scripts/test-unity.sh` + Mac look before merge.
4. Update `GAME.md` and `CHANGELOG.md` in the same commit; push to `origin`.
5. Do not treat stale `GAME.md` footer “Next work” or early `PROJECT_PLAN.md` “gaps”
   sections as current truth — this plan + top handoff + ADR 0120 win.
6. No Companion/CloudKit until Bailey opens that milestone.

## Evidence from the audit

- Headless: `scripts/test-domain.sh` **1129 passed / 0 failed** (2026-09-30 Cursor audit).
- Domain ~18 files / ~2.2k LOC; Simulation ~97 files / ~34k LOC; Presentation ~163 files / ~58.6k LOC.
- Save schema **v19**; ~201 ADRs; companion folder **absent**.
- Explore audits: Domain/Simulation, Presentation/assets, tests/plan gaps (same date).
