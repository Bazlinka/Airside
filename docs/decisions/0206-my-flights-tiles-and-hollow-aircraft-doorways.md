# 0206 — My Flights tiles carry the whole story; aircraft doors open onto a hollow doorway

Date: 2026-10-01. Owner: Claude. Requested by Bailey: make the My Flights panel show more
while staying easy to read, make aircraft doors look hollow so people walking in do not vanish
into a grey wall, and check every aircraft unlocked so far.

## Decision

**My Flights.** A tile is now 78 pt tall (was 52) and has four lines: flight number, airframe and
destination code; route ("Adelaide → Kingscote") with the registration; state with the time that
matters ("Departs 14:05", "ETA 15:20", "Check ends 18:00"); and a progress bar for departure prep
or the flight leg. The data is derived in `OperationsSummary.FillPlayerRows` from existing
simulation state (`FleetAircraft`, `DeparturePrep`, `StateProgress`); nothing new is stored.

**Doorways.** Behind every passenger and cargo door a presentation-only hollow is built at
aircraft-build time (`AirsidePrototype.Doorways.cs`, `AircraftDoorwayGeometry`): a dark reveal, a
black cabin and a lit vestibule, cut from the door leaf's own skin-sampled rings so they follow
any fuselage section. It is drawn only while the door is away from the hull, so a shut door is
unchanged. Jet and cargo leaves are re-hinged on the edge that swings them clear of the opening
(they pivoted about their centre and stood edge-on across it).

**A320 door fit.** The A320 inherited its doors from the 737's skin but has its own fuselage, so
its door tops were buried up to 6 cm. `scripts/fit-aircraft-doors.py` audits every runtime kit
(CI runs `audit`) and `fit A320` moved the A320's door parts onto its hull using the 737-800's
proud/back offsets. Art and StreamingAssets glTF/bin were patched in place; the editable FBX and
`generate-air-adelaide-fleet.py` still carry the old door cut, so a future regenerate must re-cut
the A320 doors with its own `surface()` (or re-run `fit A320`).

## Reason

The tile showed only a registration, a code and a state word, so the player had to select each
aircraft to learn the route, the time or the progress. Open doors showed bare fuselage behind
them, and a swung jet door stood across its own opening.

## Affected systems

Presentation only: `HudShell`, `OperationsSummary`, aircraft view build and `UpdateCabinDoor`.
No simulation, save, schedule, economy or random-draw change. One runtime model (A320) edited.

## Migration

None. Eyeball the panel and an open door at follow distance at day, dusk and night.
