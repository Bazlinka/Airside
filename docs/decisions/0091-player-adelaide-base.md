# 0091 — Player Adelaide base is a real operating constraint

Date: 21 September 2026. Bailey approved implementing the physical player-base progression after the career/map/HUD convergence work.

## Decision

The player expands an airline operating footprint at Adelaide, not Adelaide Airport itself.

- **Regional starter base:** 1 aircraft; regional turboprop operation.
- **Expanded regional base:** 3 aircraft; regional growth and maintenance space.
- **Jet-gate base:** 5 aircraft; terminal-gate jet handling.
- **International base:** 6 aircraft; widebody and long-haul handling.

Upgrades spend normal airline funds and require existing career progress. There is no new XP, token or building currency.

## Simulation effect

Aircraft acquisition checks fleet capacity and aircraft handling capability before charging the purchase. A temporary lack of a free physical stand may still produce an inbound delivery: the base represents allocated airline capacity, while stand occupancy remains the live airport resource system.

## Persistence

Save schema v12 stores the base level. v11 and older saves infer the minimum base capable of supporting the fleet they already own, so migration never invalidates existing aircraft.

## Presentation

Career exposes the real persisted roadmap and an **EXPAND BASE** command. Fleet uses the same rules when enabling aircraft purchases. World presentation should represent the player's leased footprint without hiding or fabricating real Adelaide Airport buildings.


## World visibility

The runtime adds a presentation-only leased operations compound beside an existing Adelaide hangar precinct. Its modular footprint grows with the persisted base level and uses the player airline livery. It has no collider, stand reservation or simulation authority, and does not replace any real OSM airport building.


## Fleet ceiling

ADR 0091 raises the old global player fleet ceiling from four to six so later base stages do not advertise unusable slots. The compact overview remains deliberately small: when more aircraft exist than fit, it keeps the priority aircraft visible and points the player to the full Operations workspace for the remainder.
