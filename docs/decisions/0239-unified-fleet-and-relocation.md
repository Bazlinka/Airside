# ADR 0239 — one fleet across every base, with a ferry between them

Amended by [2026-10-07 fleet and flight clarity](2026-10-07-fleet-flight-clarity.md):
airborne network services now have read-only 3D views and pending services can be cancelled; the unified fleet and ferry contracts remain in force.

Date: 2026-10-06. Status: implemented in code; compile, headless and native checks not run.

Bailey asked for better ways to manage and view aircraft, after buying aircraft at
another airport and flying them. The code held two disconnected fleets: Adelaide
aircraft (`FleetAircraft`, fully simulated, drawn, selectable) and outstation aircraft
(`OutstationAircraft`, `VH-O##`, a timed round-trip record). Outstation aircraft were
bought on a separate NETWORK screen with a PREV/NEXT type cycler, never appeared in the
roster, tiles, map or contracts, could not be sold, and could never reach Adelaide.
Bailey chose one unified fleet with a ferry between bases, and the fleet overview board,
per-aircraft profile, map of everything and simpler buying. Unity tests, builds and
player execution are excluded by Bailey for this change.

## Decision

- **One read-model.** `PlayerFleet.Entries` merges both systems into `PlayerFleetEntry`
  (base, kind, destination, next time, check state, flights, revenue, resale value). It
  carries structured data only; `FleetStatusText` is the single status vocabulary the
  roster, profile and map read. The two simulations stay separate underneath.
- **Ferry, not conversion.** `RelocateToAdelaide` flies an outstation aircraft in as a
  real inbound flight, keeping its registration, flights, wear and logbook, for one
  dispatch cost. `FleetAircraft.IsFerry` (saved) makes the arrival earn nothing and
  count no flight, so the next real flight settles as `CompletedTrips + 1`. It is refused
  unless Adelaide has base capability, aircraft capacity and a stand for it (counting
  aircraft already out that will want one), the type can fly the leg, and funds cover it.
- **Outstation logbook.** Outstation aircraft record join time, revenue and routes so a
  profile and a later move lose nothing.
- **Selling.** `SellOutstationAircraft`; `SellAircraft` now refuses a booked flight and
  drops the aircraft's repeat plan. A sold `VH-O##` mark is not reissued once it has flown
  (its settlement keys are spent), matching `VH-P??`.
- **Fleet workspace.** A bases strip (Adelaide plus the four outstation cities: count,
  capacity, open cost) is the base filter and also where BUY delivers. The roster groups
  by base with SHOW and SORT cycles. The profile pane carries logbook, check, sell value
  and every action: plan or send (outstation aircraft list routes best profit first with
  one-click SEND), check, track, TO ADELAIDE, SELL (two clicks), refit, and the three
  camera views. Selecting a row no longer closes the sheet. The Network screen is gone.
  `PurchaseRefusal` is a read-only twin of both buy commands, so a market card is buyable
  only when the command would accept it.
- **Map and counts.** Outstation aircraft are drawn on the map (parked at their base, or
  along the great circle from their booked times) and open their profile when clicked.
  Stats, achievements, the return briefing and My Flights agree with `PlayerFleetCount`.

Save schema 20: `AircraftRecord.IsFerry`; outstation `JoinedAtSeconds`, `LifetimeRevenue`,
`HistoryFlights`, `RouteHistory`. Older saves load with no ferries and an empty
outstation logbook. Restore rejects a ferry flag on anything but a player aircraft on its
way in, and an outstation logbook whose totals do not match.

Left out: moving an Adelaide aircraft to an outstation; marking purchase deliveries as
ferries (they still pay a free flight, which changes career balance and needs CareerBot
review); the jet bought with no free gate and no stand; helicopters at outstations;
outstation cameras; multi-hub Adelaide (restore requires `HomeCode == ADL`).

Affected: `Simulation/PlayerFleet.cs`, `AirlineOperations.{Relocation,Purchase,Fleet,Restore,Rotorcraft}.cs`,
`FleetAircraft.cs`, `OutstationFleet.cs`, `AirlineSave.cs`, `AwayCatchUp.cs`;
`Presentation/FleetBoard.cs`, `FleetWorkspace.cs`, `AirsidePrototype.Fleet.cs`,
`AirsidePrototype.Airline.cs`, `StatsWorkspace.cs`, `FlightManual.cs`, `HudDraw.cs`.
