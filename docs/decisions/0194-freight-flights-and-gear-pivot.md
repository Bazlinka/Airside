# 0194 — Freight flights, and pitching about the main gear

Date: 2026-09-30. Owner: Claude. Requested by Bailey ("more ways to earn", freight with different liveries; tyres
sinking into the ground on landing and takeoff).

## Task packet

Player outcome:

1. **Freight.** A second way to earn. The player refits a parked aircraft to a **freighter** (a fixed fee). It flies with
   no passengers, is paid from the tonnes the destination offers against its payload, and wears a **cargo livery**: the
   airline's own colour in a deep working shade, and a "… CARGO" fuselage title. Refit it back to passengers for the
   same fee.
2. **Tyres stay on the ground.** On rotation and in the flare the main tyres no longer sink into the runway.

Scope: `Simulation/FreightRates` (new), `RouteForecast`, `FleetAircraft.IsFreighter`, `AirlineOperations.SetFreighter`
and `Forecast(…, FleetAircraft)`, `BoardingFlow` (no passengers), `AirlineSave` v19, Fleet workspace refit button,
planner/route-map load text, `AircraftLiveryPaint.FreightPrimary`, `Airline.FreightTitle`, fleet livery repaint;
`Presentation/AircraftGearPivot` with the fleet pose pass, skid marks and touchdown smoke.

## Decisions

- **Freight is an aircraft role, not a per-flight switch.** The livery is then a property of the airframe, it repaints in
  place when the role changes (no view rebuild), and the refit fee is a real choice.
- **Pay.** `FlightPay × (0.55 + 0.95 × fill)`, fill = tonnes offered ÷ payload. Passengers use `0.35 + 0.90 × fill`. The
  freight floor is higher (parcel contracts are guaranteed); a full hold pays 1.50× against 1.25×. Demand is its own
  pool per destination: Kangaroo Island produce, Coober Pedy stores, Alice Springs and Darwin supply runs, long-haul
  belly freight. Thin passenger routes can therefore pay better by freight (`FreightTests`).
- **Dispatch cost is unchanged**, so a cancelled flight still refunds exactly what it charged.
- **Refit:** turboprop $400, narrowbody/regional jet $2,400, widebody $9,000, either way. Refused with a flight booked,
  away from the stand, in a check, or short of cash. Player aircraft only.
- **No passengers walk to a freighter** (`BoardingFlow.PassengerCount` is 0). Bags and cargo choreography is unchanged.
- **Livery.** `FreightPrimary` keeps the hue, deepens it (V 0.34, S 0.40–0.80); a colourless livery goes slate. The
  title is the wordmark plus CARGO, shortened to the first word when longer than 18 characters.
- **Save v19** adds `IsFreighter` per aircraft. Older saves load all-passenger.
- **Not in this change:** AI freight carriers (DHL, Qantas Freight and the like), cargo apron/stands, freight contracts
  that require a freighter (the existing "Freight run" contract kind is unchanged). Next slice.

### The tyre fix

Pitch was applied about the model root. The jet kits are rooted at the nose stop, so pitching 9° nose-up on rotation
swung mains 17 m aft about 2.7 m below the path (2 m in the flare). The path's height is the wheels' height, so the
pose pass now raises the root by `AircraftGearPivot.LiftMetres`: the drop of the main-gear contact point (root-local
Z measured from the tyres, Y from the model ground offset) that the attitude would cause. It uses the actual damped
rotation, so it is continuous. Skid marks and the touchdown puff now sit at the real tyre contacts on the tarmac
instead of at the (now lifted) nose datum.

Invariants: presentation only; simulation, schedules and reservations do not read it. Frame rate does not change
outcomes.

## Validation

`FreightTests` (rates, refit, refusals, forecast, settlement, boarding, save v19 and v18 load), `FreightPaintTests`,
`AircraftGearPivotTests`. Full Unity EditMode; results in CHANGELOG. Not seen in a rebuilt game: check a freighter's
title/colour at follow distance, and a jet's rotation and flare from the side.
