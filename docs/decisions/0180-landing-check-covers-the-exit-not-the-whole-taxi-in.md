# 0180 — The landing check covers the exit, not the whole taxi-in

Date: 29 September 2026. Author: Claude, from Bailey's report: "planes still hold on landing about
50 m before the runway in the air … holding for no reason when an aircraft is on the other side of
the runway".

## Player-visible outcome

Arrivals hover short of the runway far less. Over a simulated day (seed 2026, 1 s steps) the time an
arrival was held with the runway already free fell from 3,733 s to 745 s (−80 %), and total time
spent holding for landing fell from 18,177 s to 11,453 s.

## Cause found

The tower cleared a landing only when both the vacate and the **whole taxi-in to the stand** (170–236 s
of taxiway beyond the runway exit) were clear of moving traffic, projected minutes ahead. 59 % of
the free-runway holds were a conflict on the taxiway more than a minute after the exit (a departure
taxiing out along the same taxiway, another arrival on its own taxi-in), with 0–5 m predicted
separation. The arrival could not do anything about that on final, and it does not need to: ground
control already re-checks the whole taxi-in before an `AwaitingStand` aircraft moves, holding it at the exit.

## Decision

`GroundTraffic.PathClear` takes an optional horizon. The landing check
(`AirlineOperations.VacateClearOfTaxiing`) checks the vacate in full and only the first
`GroundTraffic.LandingTaxiInHorizonSeconds` (30 s) of the taxi-in, the stretch the arrival is committed
to at the exit. The presentation's landing estimate uses the same function, so the drawn arrival slows to
match instead of arriving early and hovering.

## Also in this change

The propeller blur and fan discs (and painted stand labels) use `Universal Render Pipeline/Unlit`
through `Shader.Find`. It was not in Always Included Shaders, so the built game stripped it (the build has
URP/Lit and Unlit/Color only), `Shader.Find` returned null, and each prop blur fell back to a flat
translucent glass quad: the "square" propeller. Now included in `ProjectSettings/GraphicsSettings.asset`;
`ShaderInclusionTests` guards it.

## Not changed

Runway-crossing holds (a taxiing aircraft due across the strip still holds a landing, about 590 s a
day), the vacate check, separation rules and every ground-control check on taxi-in and pushback.
A variant that made aircraft yield their crossing to a holding arrival was tried and dropped: it moved
time around without a clear gain.

## Cost

A landing estimate costs about 25 % more (5.05 ms against 4.04 ms average in a 12 h run) because clear
checks now run to the end more often; the frame-spread search from ADR 0173 absorbs it.
`ArrivalClearanceTests.Inbound_ExpectsClearanceNoEarlierThanItArrives` now samples every eighth event
(it asks for an estimate of every inbound at every event and had become 2.3× slower).

## Migration

None.
