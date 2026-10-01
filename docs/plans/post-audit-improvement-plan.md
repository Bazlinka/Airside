# Post-audit improvement plan

Status: **active** · 2026-09-30 · Author: Cursor (comprehensive code audit) · Branch `main` · ADR **0205**

This is the standing backlog for what to improve next before a major expansion.
It comes from a full Domain / Simulation / Presentation / tests / product-plan audit
on `main` (headless `scripts/test-domain.sh` **1129/1129** at audit time). Cloud agents
and other tools should follow this order unless Bailey overrides it.

Related: `docs/plans/visual-overhaul-plan.md` (ADR 0198, Bailey-approved 2026-10-01).

## Verdict (do not re-litigate)

Airside is already a production-grade Adelaide airline game, not a prototype.
Career, saves (v19), tower, AI traffic, Glass Cockpit and the map stack are solid.
P0 verification debt is closed by Bailey (2026-10-01). Next risks before going
bigger are the **visual overhaul gate**, then **Presentation concentration /
performance**, then finishing half-done product slices (freight).

## Priority order

### P0 — Mac packaged playtest of unverified merges

**Status: done (2026-10-01) — Bailey closed P0 and asked agents to move on.**

Automated Mac captures from #490 are in `docs/testing/post-audit-p0-2026-09-30/`
(most automated rows **keep**). Remaining stills and manual listening/play rows stay
`unverified` in RESULTS — accepted as waived, not as eyes-on keep. Review/capture
helpers from #491 and freighter/hangar/boarding pick locks from **#492** are on
`main` for optional later use.

Do not reopen P0 unless Bailey asks. Next priority is **P1**.

### P1 — Visual overhaul (do next)

**Status: approved (Bailey 2026-10-01).** Continue Phase 1+ code; do **not** ask Bailey to
verify each slice — he rebuilds when he wants. Optional Mac baseline captures are never a merge gate.

1. Phase 1 ground/land checklist complete on tip (ADRs 0211 Golf, 0207 bunkers,
   0208 CBD, 0209 seasonal tint, 0210 Hills haze). Continue Phase 2 trees or
   Phase 3 building accuracy as narrow ADRs.
2. Building heights: 71/78 still rule defaults (`docs/data/ypad-buildings-audit.md`) — Phase 3.

**Already on `main`:**
- Phase 0 bookmarks (ADR 0200) via #485.
- Phase 1 oil stains (ADR 0201) + apron wear (ADR 0202) via #486.
- Phase 1 West Beach dunes / shore foam / Patawalonga outlet (ADR 0203) via #489.

### P2 — Finish freight as a mode

P0 no longer blocks this. Player freighter refit exists (save v19). Prefer a Mac look
at freighter livery + tyre rotation when convenient, then finish as one narrow ADR slice:

- AI freight carriers (DHL / Qantas Freight–style, night bank, own liveries)
- Cargo apron / stands
- Contracts that require a freighter (`ContractKind.Freight` today does not)
- Outstation settle using freighter forecast overload

Until that slice: leave player freight as-is; no freight AI / apron / freighter-gated
contracts unless Bailey prioritises P2 over P1.

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
3. Behaviour changes: `scripts/test-domain.sh` here; Unity EditMode on a Mac before merge when available.
   Do **not** ask Bailey to playtest or fill RESULTS — he builds when he wants.
4. Update `GAME.md` and `CHANGELOG.md` in the same commit; push to `origin`.
5. Do not treat stale `GAME.md` footer “Next work” or early `PROJECT_PLAN.md` “gaps”
   sections as current truth — this plan + top handoff + ADR 0120 win.
6. No Companion/CloudKit until Bailey opens that milestone.

## Evidence from the audit

- Headless: `scripts/test-domain.sh` **1129 passed / 0 failed** (2026-09-30 Cursor audit).
- Domain ~18 files / ~2.2k LOC; Simulation ~97 files / ~34k LOC; Presentation ~163 files / ~58.6k LOC.
- Save schema **v19**; ~201 ADRs; companion folder **absent**.
- Explore audits: Domain/Simulation, Presentation/assets, tests/plan gaps (same date).
