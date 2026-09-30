# 0201 — Post-audit improvement plan is the standing backlog

Date: 2026-09-30. Owner: Cursor. Requested by Bailey after a comprehensive code audit
(“find what is missing / good / not good before expanding”).

## Decision

`docs/plans/post-audit-improvement-plan.md` is the active ordered backlog for agents
and tools until Bailey replaces it. Priority: Mac playtest of unverified merges →
visual-overhaul gate → finish or park freight → Presentation/performance debt →
only then major expansion (second airport, companion, denser world).

Companion/CloudKit, wages/fuel/loans, and multi-airport remain deferred. Trust this
plan and the top of `GAME.md` over stale footer “Next work” text and early
`PROJECT_PLAN.md` gap sections.

## Reason

The Domain/Simulation career loop is solid (save v19; headless green). Expanding on
top of a large stack of “Unity look not yet verified” Presentation merges and an
Adelaide-hardcoded restore would make regressions hard to attribute.

## Affected systems

Docs and agent handoff only. No simulation, presentation, or save behaviour change.

## Migration

None.

## Follow-up (same day)

Mac P0 tooling added without changing game behaviour: `scripts/review-post-audit-p0.sh`
and `docs/testing/post-audit-p0-playtest.md`. Freight AI / cargo apron / freighter-gated
contracts stay parked until P0 signs off the player freighter and tyre rows.

Renumbered from 0200 → **0201** so it does not collide with
`0200-visual-baseline-phase0.md` (PR #485).
