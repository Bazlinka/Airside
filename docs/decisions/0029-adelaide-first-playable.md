# Decision 0029: Adelaide is the first-playable airport

Date: 2026-09-09

## Decision

New games start at Adelaide Airport (West Beach, id `ADL`), not Kingscote.
Kingscote, Port Lincoln and Coober Pedy remain loadable by id. Adelaide Tower
phraseology uses runway 23 and hands off to Adelaide Departures; other
locations keep runway 09 and Adelaide Centre.

The presentation airfield is rebuilt as large level slabs (one grass deck,
one runway, shared pavement height) plus a visual-only 12/30 cross runway and
Gulf St Vincent to the west. Runway paint is 05/23. A distant CBD cluster sits
northeast of West Beach. Simulation taxi coordinates, reservations and save
schema are unchanged.

## Reason

Bailey asked for Adelaide as the starting airport, a much bigger readable
field, ground with no gaps, and a start that actually reaches a first frame.
Tens of thousands of slightly offset cubes were the gaps and the startup stall.

## Affected systems

- `AirportLocation` default and presets
- `AerodromeAtc` location-aware phraseology
- `AirportRoutes` destinations (no longer offers Adelaide as a destination)
- Presentation airfield construction in `AirsidePrototype`

## Migration impact

Save schema stays at version 2. Existing saves with `locationId` `KGC` stay on
Kingscote. Schema 1 saves still migrate to `AirportLocation.Default`, which is
now Adelaide. New games are Adelaide.
