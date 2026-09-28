# 0167 — Individual aircraft logbooks

Date: 28 September 2026. Author: Codex, implementing Bailey's direction that aircraft should
accumulate history and feel like the player's own aircraft.

## Player-visible outcome

Every owned aircraft has a small persistent story in Fleet: when it joined, completed flights,
recorded earnings, its most-flown destination and a named distinction. The original VH-PAX is
identified as the founding aircraft and cannot be sold. Reaching 1, 10, 25, 50, 100 or 250 flights
earns First flight, Familiar face, Route regular, Workhorse, Veteran or Airline icon, with a full
aircraft celebration card.

## Decision

- Identity and history live on `FleetAircraft`, not in presentation: joined time, founding flag,
  lifetime recorded revenue, detailed-history flight count and per-destination counts.
- A settled player flight records its destination and total payment exactly once, beside the
  existing idempotent settlement.
- Distinctions derive from the existing completed-flight count. They do not alter simulation
  performance, reliability or revenue.
- The founding flag, not aircraft type, protects VH-PAX. A later Saab can still be sold when the
  existing fleet rules allow it.
- Save schema v18 persists the logbook. A pre-v18 save preserves its historical completed-flight
  count but begins route/revenue detail at zero rather than inventing history. Pre-v18 player
  VH-PAX is migrated as the founding aircraft.

## Scope and invariants

Fleet simulation data, save/restore, Fleet workspace facts and the existing celebration surface
are in scope. Aircraft models and liveries, purchase prices, route access, flight timing, economy
formulas and the long-term product plan remain unchanged. The simulation owns the history; UI and
animation only represent it.

## Acceptance

- New and migrated games identify VH-PAX as founding and refuse its resale.
- A later purchased Saab remains a normal resellable aircraft.
- v18 round-trips join time, revenue, route counts and founding identity.
- Pre-v18 saves load without fabricated route or revenue entries.
- Fleet shows the individual record and next distinction.
- An exact distinction threshold produces one aircraft celebration with registration and type.

