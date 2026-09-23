# 0110 — 05:00–23:00 operating day, banked schedule, apron hold and gate codes

Date: 23 September 2026. Bailey asked for the Operations page (arrivals and departures)
to feel like a real airport:
- first flight at 05:00 and last at 23:00, with only RFDS emergency flights outside
  that window;
- departures may share a time instead of being evenly spaced;
- every aircraft on the apron stays there until its own departure;
- every gate suits the aircraft on it.

## Decision

### Operating day 05:00–23:00

`AirportCurfew.OpensAtHour` moves from 06:00 to **05:00**. A movement scheduled for
**23:00 exactly** still goes, and the field is closed from 23:00:01 to 04:59:59.
`LastMovementMinute = 23 * 60`.

This supersedes the 06:00 opening in ADR 0085. The real Adelaide Curfew Act 2000 opens
at 06:00, so this is Bailey's gameplay call. The player and RFDS stay exempt, as before.

`AdelaideHourProfile.Density(5)` becomes a first wave (0.70), and the Operations day
strip covers 05:00–23:00 with a "first wave" label for that hour.

### Banked, clustered schedule times

`AdelaideHourProfile.BankAnchorMinutes` is the list of published marks: 05:30, 05:45,
06:00, 06:15, 06:30, 07:00, …, 22:30 and 23:00.

- An AI ready time up to 15 minutes before an anchor is published on that anchor, so
  several operators share one departure time. Otherwise it rounds up to the next
  5-minute mark.
- The tower still sequences the actual takeoffs.
- The evening wind-down no longer rolls into tomorrow while the day is open. Before this
  change, a jet ready at 22:10 was booked for 06:00 next morning.

**Night-stops.** An aircraft ready after the day closes night-stops on its stand. It
leaves in the next **first wave**, at one of 05:00, 05:30, 05:45, 06:00, 06:00, 06:15 or
06:30, picked by a stable hash of the registration. Previously every night-stopped
aircraft left at the same minute.

Opening pushbacks in a new game are published on the next 5-minute clock mark.

### The apron holds its aircraft until departure

- **Cathay off-season.** Out of season, Cathay no longer lifts a parked A350 off GATE-18.
  The aircraft flies its booked departure home, stays at Hong Kong and is retired while
  off-map. Only a parked Cathay with nothing booked is removed directly.
- **Sky traffic.** `AdelaideDayPlan.AirborneAt` no longer draws timetable-only
  "phantom" flights. It draws only the live fleet's own off-map legs: Outbound after the
  field hands over, and Inbound. Every aircraft in the sky therefore came from a gate or
  is going to one.
- **Day plan.** `ForLocalDay` now uses 05:00–23:00 and never gives one stand to two
  overlapping turns.

### Gates sized by ICAO code letter

`AircraftCatalogue.CodeLetter` derives the code letter from wingspan:

| Code | Wingspan |
|---|---|
| A | < 15 m |
| B | < 24 m |
| C | < 36 m |
| D | < 52 m |
| E | < 65 m |
| F | 65 m or more |

`AirlineOperations.CodeEGates` lists the widebody gates: 18(L), 20(L), 22L, 25, 26L and
28L. These are the pier MARS centre lines and the international gates where the
authored widebodies already park. Every other gate and bay is code C.

- **Fit rule.** `StandFits` now also requires the aircraft's code letter to be no
  larger than the stand's code letter.
- **Stand choice.** Stand suggestions prefer gates that are not larger than needed, so
  narrowbodies leave code E gates for widebodies.
- **Player widebodies.** The player's International widebody lease is 28L only; 28R is
  a code C line.
- **Operations pane.** The selected-flight pane shows the aircraft's code letter.

### Departures-screen statuses

An AI aircraft on its gate with a booked departure shows:
- **Scheduled** until boarding opens;
- **Boarding** from 40 minutes before departure for jets, or 25 minutes for turboprops;
- **Final call** from 15 minutes;
- **Gate closed** from 5 minutes.

RFDS reads **RFDS · Emergency**. Delay and cancellation labels keep priority.

## Affected systems

- `Domain/AirportCurfew`, `Domain/AircraftCatalogue`
- `Simulation/AdelaideHourProfile`, `Simulation/AirlineOperations`,
  `Simulation/AdelaideDayPlan`, `Simulation/PlayerBase`
- `Presentation/FlightBoard`, `Presentation/OperationsWorkspace`

## Migration

The save schema is unchanged. When a save is restored it uses the pre-0110 fit
(`StandClassFits`), so an older game with a widebody on a code C gate still loads. That
aircraft keeps the gate until it departs. Every new stand assignment uses the strict fit.

## Guardrails

- The code E list is an approximation from the OSM/AIP-derived layout and where
  widebodies are already authored. Check it against the current ADL AIP stand chart
  before it is treated as data.
- Actual takeoff spacing still comes from the tower and ground sequencing. Clustering
  applies only to published times.
