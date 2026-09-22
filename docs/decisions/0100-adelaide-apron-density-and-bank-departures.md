# 0100 — Adelaide apron density and bank-shaped departures

Date: 22 September 2026. Bailey: the Operations caption could say ~17 on field
while the apron looked empty; departure times felt spaced and unrealistic for ADL.
Wanted more metal on stands and simulation-style banks with doubles OK — not live
flight times.

## Decision

### Apron first, short inbound bank

Opening traffic still seeds a **short** arrival stream so short final stays alive,
but most authored AI stay **AtStand** so T1 and the regional bays look occupied.

Morning inbound (simulation-shaped, not a real timetable):

- QantasLink Port Lincoln, Rex Mount Gambier
- One each of Virgin Melbourne, Qantas Sydney, Jetstar Melbourne
- Air New Zealand Auckland, Singapore Singapore

Roughly seven aircraft over ~40 minutes. Evening still stretches that same set to
22:50 (ADR 0088). Emirates / Qatar stay parked for the 22:00 pin.

Rex gains two extra Saabs (`VH-ZRF`, `VH-ZRG`) on the spare walk-out bays so the
regional apron is fuller without touching player-leased jet gates (27 / 28 / 29).

### Departure banks with intentional doubles

`AiOpeningDepartureSeconds` is no longer a unique ~5-minute ladder. It is an
ADL-shaped cluster (pairs at the same minute, then gaps) based on how Adelaide
actually peaks around 06:00–08:00 and again late afternoon — dense early bank,
doubles normal, tower still serialises the strip.

Ongoing AI bookings snap ready times up onto **5-minute marks** during busy
hours (`AdelaideHourProfile.SnapToBankLocal`) so turns that finish a minute apart
can share a departure time. Quiet hours still jump via `NextUsefulLocal`.

### Still not a live feed

No FlightAware / ADS-B / AIP as simulation input (ADR 0071 / 0086). Times are
authored density, not copied slots. Board and pavement remain sim metal only.

## Affected systems

`AirlineOperations` (opening seed, Rex fleet, opening departure array,
`SnapCommercialDeparture`), `AdelaideHourProfile`, EditMode tests, Operations
caption already tied to `FleetVisual.Visible`.

## Migration impact

None for saves. Extra Rex registrations appear via `AddMissingRegionalCarriers`
on load when a bay is free.

## Guardrails

- Do not re-attach day-plan ghosts to the Operations board.
- Do not park AI on player-dedicated jet gates 27 / 28 / 29.
- Keep opening inbound sparse enough that AtStand metal stays the visual majority.
- Doubles are intentional; do not "fix" same-minute `DepartAt` by re-staggering.
