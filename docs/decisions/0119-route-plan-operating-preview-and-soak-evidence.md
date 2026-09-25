# ADR 0119 — Route-plan operating preview and soak evidence

Date: 2026-09-25

## Decision

Keep the existing rotation economy and maintenance rules. In the Route Map, show the actual incremental charge when changing a booking, an indicative return including current reliability and a matching contract, and the resulting route net after dispatch. Flag a check due or overdue at the point of planning, and disable departure times before a check ends. Estimates are conditional: punctuality, contract state and other events may change before the aircraft returns.

Use the packaged game's separate-save soak mode for stability evidence. Record frame-time distribution, slow frames, CPU thread timings and memory there, and allow a registration-based close follow-camera capture for visual QA. The normal game does not run this instrumentation.

## Why

A player deciding whether to fly, rebook or check an aircraft needs the near-term consequences before committing. The previous planner disabled valid rebookings because it ignored money already paid and could offer departures while an aircraft was grounded. Its return figure omitted current reliability and contract pay. A mean fps number also hid short stutters.

## Boundaries and verification

No new currency, generic XP, automatic maintenance, fleet balance, simulation timing, save schema or default airport presentation. The simulation remains authoritative for charges and settlements. Test booking/refund parity, check timing, low-reliability return and maintenance warning; verify HUD fit in a packaged build. Run the full-airport soak separately from editor/build work when judging frame pacing. A scripted soak is not an unguided human playtest.
