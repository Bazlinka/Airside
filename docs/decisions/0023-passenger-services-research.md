# Decision 0023: passenger services research

**Date:** 2026-09-06  
**Status:** Accepted  
**Decision owner:** Bailey (via keep-building brief)

## Decision

After **Operations Efficiency** completes, the airport can start a second research
project, **Passenger Services**. It costs 3500, takes one simulated day, and when
complete permanently adds **+$75 route income per departed commercial flight**.

Only one research project runs at a time. Starting Passenger Services is a
persisted command (`start-research-passenger-services`) replayed on load and
offline catch-up — no save-schema field. Flight timing and reservations are
unchanged.

## Reason

Phase four needs a second spend/progression beat after the first research
project. A pure economy bonus keeps seed and timing tests stable while giving the
player a clear next unlock once Operations Efficiency is done.

## Affected systems

- `AirportResearch`, `AirportSimulation` settlement and daily finance brief
- `PersistentAirportSession` command replay
- Operations HUD research panel
- Edit-mode research tests

## Migration impact

None. Existing saves without the new command behave as before; completing Ops
Efficiency unlocks the new button.
