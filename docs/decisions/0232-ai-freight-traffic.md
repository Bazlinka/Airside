# ADR 0232 — AI freight operators and cargo banks

Date: 6 October 2026. Status: merged in PR #527 under Bailey’s explicit approval; native Unity verification remains pending.

## Decision and reason

Bailey approved the next existing-backlog slice: AI freight carriers with their
own liveries and night schedules. Add Qantas Freight (`QFR`) and DHL Air (`DHL`),
one narrowbody cargo aircraft each. The existing 737 model is a representative
freighter, not a new aircraft variant. Registrations VH-FQF/VH-FDH are authored
simulation identifiers, not researched real-world fleet claims.

Cargo flies a weighted MEL/SYD/BNE/PER network. Published departures use dawn
05:10/05:40 and evening 20:30/21:15/22:00 banks, with a stable 0/5/10-minute
registration offset. Arrivals use 05:15/06:00 and 19:15/20:00/21:00 banks with the
same offset. These are authored game schedules, not live airline timetables.
All times use Adelaide wall time, including daylight saving. No commercial cargo
curfew exemption is introduced. Existing weather, delays, runway sequencing and
reservation rules still apply, so actual movements can follow their booked bank.

Add cargo after the passenger opening bank is seeded. Use only free compatible
stands; if the apron is full, start away without a stand reservation. Airports
without a compatible stand gain neither cargo aircraft nor empty cargo airlines.
Dedicated carrier identity sets the freight role in the fleet constructor, also
covering restore. Loading the game adds missing registrations once after existing
catch-up. Cargo never charges player refit fees or earns player settlement rewards.

Retain red Qantas Freight and warm-yellow DHL identity through the shared runtime
paint and text system. Use original colour blocks and rendered titles, no imported
logos or textures. Player conversions retain their existing dark working paint.
Freighters have no passenger boarding mode, bus, stair truck or docked aerobridge;
cargo door timing remains available. Dedicated cargo loaders and stands remain
future backlog rather than being represented as completed here.

## Affected systems and migration

Airline identity; fleet creation/restore; AI scheduling and away turnaround;
startup/load integration; shared freight paint; passenger boarding presentation.
Save schema/version/fields are unchanged. Existing player freight flags retain the
v19 migration behaviour. Existing saves gain missing AI freight on normal game
loading; the generic save decoder does not populate arbitrary airport fixtures.

## Verification

See `docs/testing/ai-freight-2026-10-06/README.md`. Headless tests cover lifecycle,
load idempotence, saved role/times, compatible unique stands, capacity overflow,
curfew boundaries, daylight saving and two-night cargo operation. The existing
30-day full-airport soak retains its passenger wait bound and permits up to 17
hours for a cargo aircraft waiting across the daytime gap. Unity-only colour
assertions require native EditMode. Native runtime verification is separate.
