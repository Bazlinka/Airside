# 0055 — Living airport: visible circuit, per-flight pay, sky traffic

Date: 18 September 2026. Requested by Bailey after liking the current git
state: (1) holding traffic and go-arounds should be visible; (2) game
objectives should be more than one contract — start with money, spend to fly,
get paid per flight, contracts still pay a bonus; (3) air traffic flying
between other airports should be visible. Implemented as one first-playable
cut on `cursor/living-airport-and-career-7e45`, not three mixed dumps.

## Decision and reason

### Visible circuit and go-around

Holding-for-landing used to map to a hidden visual, so a queue of arrivals
vanished until the tower cleared one. A go-around skipped the approach and
jumped straight to departed/hidden. The simulation still owns who holds and
who goes around; presentation now draws a right-hand racetrack south of
runway 05 (`CircuitTraffic`) and a missed-approach that starts on short final,
climbs to circuit height, then joins that same lap. The tower flies the
approach (`ApproachSeconds` as `Landing` with `WentAroundThisTrip`) then
aborts onto `GoAround` so the missed approach is actually seen. The runway
frees at the abort, not after the four-minute circuit.

### Per-flight economy and authored objectives

ADR 0053's career layer had funds and one Kingscote contract, but a new
airline started at $0 and only earned on that contract. The loop Bailey
asked for is: start with money, spend to dispatch, get paid when the
aircraft returns, and treat contracts as bonuses on top of that operating
revenue.

- Opening float: `$4,000` (`FlightEconomics.StartingFunds`).
- Dispatch cost is charged at `ScheduleDeparture` and refunded on cancel
  before pushback. AI traffic is not charged.
- Every player rotation pays `FlightPay` on return to stand, contract or
  not. A matching active contract still adds its per-rotation bonus and
  completion reward.
- Turboprops (regional-bay types) are cheaper than jets. ATR / Saab / Dash 8
  cruise above 500 km/h, so a cruise-speed cutoff would have priced the
  starter ATR as a 787.
- Catalogue: Kingscote and Port Lincoln from Provisional (each unlocks
  Regional); Whyalla once Regional; Melbourne (`DOM-MEL-INTRO`) once
  Regional, still inside the starter ATR's 1 100 km planning range, unlocks
  Domestic. Completed contract ids persist (save v7) so they cannot be
  farmed.

Numbers are tuning placeholders. Bailey can change them without a schema
bump.

### Sky traffic between other airports

Authored corridors that never use Adelaide as an endpoint
(`SkyTraffic.Routes`). Snapshot is a pure function of the injected clock —
no runway, taxiway or stand reservations, so frame rate and reload cannot
change who is where. The destinations map always draws them. 3D models
appear when a flight's true position is within 260 km of Adelaide,
compressed into a 7.5 km draw radius so they fit the 10 km camera far clip.
PER–MEL's great circle bottoms out ~230 km from the field; that is why the
radius is that wide.

## Affected systems

Domain (`RouteContractDefinition` / catalogue), Simulation (`CircuitTraffic`,
`FlightEconomics`, `SkyTraffic`, `AirlineCareerState`, `AirlineOperations`,
`AirlineSave` v7, `FleetVisual`, `AirportCircuit`, `CircuitProfile`,
`AircraftPerformance`, `FirstFlightGuide`), Presentation (circuit poses,
sky-traffic pool, map icons, planner cost/pay, contracts panel, start-screen
copy, soak driver). Save schema 7.

## Migration impact

Save version 7. A v6 save loads with its existing funds, reliability, tier
and active contract, plus an empty completed-contract set. Pre-v6 saves
still get a fresh Provisional career with the opening float and no
retroactive pay, as in ADR 0053.

## Guardrails

- Simulation owns money, reservations and who goes around. Presentation only
  draws and issues identifiable commands.
- Sky traffic never reserves Adelaide resources.
- Frame rate must not change simulation outcomes; sky positions are a
  function of simulation time.
- Player aircraft still reserve runways / taxiways / stands before use.
- Completed contracts cannot be re-accepted.
- The retired Kingscote airport-manager economy stays retired.

## Acceptance and evidence

`scripts/test-domain.sh` on this branch (headless Domain/Simulation
EditMode). Live Mac Play still needed to judge the racetrack, missed
approach and compressed overflights at overview / follow.
