# 0215 — Turboprop-only cockpit rollout

Date: 2026-10-01. Status: scope approved by Bailey; implementation under verification.

Support SF34 Saab 340B, ATR42 ATR 42-600 and DH8D Dash 8-400. This replaces
ADR 0214's planned 737-first expansion. Jets remain unavailable. New aircraft
must opt in through a fitted builder, not through a generic engine-category guess.

Use a shared transient interior base for mesh/material lifetime, exterior hiding
and restoration. Preserve the Saab's fitted geometry. Add ATR and Dash glass
layouts with separate eye positions, windows, panel proportions and overheads;
their layout is based on manufacturer references and the existing runtime kits.
Avionics remain decorative, with a separate live local telemetry inset.

The clicked aircraft's actual type selects the builder on entry and view rebuild.
An unsupported factory request allocates no interior. All three retain first-spool,
local visibility and shutdown rules, camera controls and existing audio behavior.
Complete lower shells close the footwells and side lining; windows remain open.

Affected systems: cockpit presentation, availability/card hint, native review
capture and tests. Simulation, reservations, routes, pacing and saves are unchanged.
Migration: none. New runtime assets are original code-built geometry/artwork;
no reference photographs or manufacturer diagrams ship.

Validation and the licensing blocker are tracked in
`docs/testing/turboprop-cockpits-2026-10-01/README.md`; native compilation/tests,
rendered review and clean Mac build are required before merging the candidate.
