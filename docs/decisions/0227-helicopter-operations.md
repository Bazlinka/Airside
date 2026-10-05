# ADR 0227 — Helicopter operations (the Bell 412 and the helipad)

Date: 2026-10-01. Status: implemented on `feature/helicopter-operations`; Unity visual/audio acceptance noted below.

Bailey asked for the parked Bell 412 (ADR 0186) to become a working part of the game: flights, speed, schedules,
noise, compatibility. Decisions:

- **A helicopter is a fleet aircraft, not a special case outside the simulation.** `AircraftType.IsRotorcraft`
  marks it. It has a stand (a helipad spot), a schedule, a round trip and a save record like any other, so boards,
  events, saves and catch-up already work. Its flight skips the runway and the taxi network:
  `AtStand → TakingOff → Outbound → AtDestination → Inbound → Landing → AtStand`. The only shared resource is the
  pad (`HelipadFreeAt`, derived from state, not saved); the only other gates are weather
  (`RotorcraftRules.MayMove`: storm grounds all, fog grounds civil, hard wind grounds civil first) and, for civil
  flights, the curfew. Emergency and player flights are curfew-exempt, as ADR 0110 already rules.
- **Kept out of `AircraftCatalogue.All`.** Every runway, taxi, layout and purchase rule iterates that list; a type
  with no runway must not appear there. `AircraftCatalogue.Rotorcraft` holds helicopters; `TryFor`/`For`/
  `AircraftType.TryFromId` search both. `StandClass.Helipad` is new.
- **Helipad West is three stands.** `AdelaideHelipad` places `HELI-1..3` on a ring inside the 18.9 m OSM pad, rotor
  discs clear of each other, each facing outward. `HELI-1` is the SA Ambulance crew's; `HELI-2/3` are the player's
  from the Expanded regional base. Helicopters fit only these stands and nothing else fits them
  (`StandClassFits`).
- **SA Ambulance Rescue (`SAAS`) is an emergency AI operator** with one Bell 412EP (`VH-SAR`). Call-outs go to six
  Adelaide hospitals (`DestinationCatalogue.RescueSites`, resolvable by `TryFind`, not in `All`). Gaps and
  destinations come from a hash of airframe, trip and hour, never the random source, so no other timeline moves.
  The first call-out comes within eight minutes of a new game. A save from before this ADR gains the helicopter on
  load.
- **Flight shape.** `RotorcraftPerformance`/`HelicopterFlight` are pure: a 6 s lift to a 3.5 m hover, a 3 s hold,
  a 5 s pedal turn, a 22 s accelerating climb to 80 kt; the mirror on landing (a 30 s deceleration, a 6 s turn to
  the parked heading, a 7 s settle). `HelicopterTrack` turns state and clock into a world pose, drawn at true scale
  to the hospital, so the helicopter is seen flying there. The leg carries a 40 s allowance, not the jet climb and
  descent allowance (`LegTiming`).
- **Player helicopters.** The Bell is on sale (`$6,800`, Provisional, 75% reliability, 8 flights) and needs the
  Expanded regional base. It flies the Regional route band (ceiling `RouteBand.Regional`), pays as 0.7 of a
  turboprop (13 seats), costs a turboprop's rate to dispatch, preps in 60% of the time, has its check on the pad
  (no tow), and cannot be based at an outstation. It is a side branch of the fleet, not a rung of the career
  ladder (`CareerProgress.NextAircraft` skips it).
- **Sound.** `EngineClass.Rotorcraft`; `AircraftAudioMix.ForRotorcraft` (rotor speed sets pitch, load sets the
  blade-slap layer, no reverse or tyres). Loops are synthesised by `scripts/audio/generate_rotorcraft_audio.py`
  (4-blade 5.5 Hz rotor → 22 Hz blade pass, 2-blade 26.9 Hz tail rotor, turbine whine), byte-checked, project-owned.
- **Presentation.** `AirsidePrototype.Helicopter.cs`: the AIR-017 kit with main and tail rotors rigged on their own
  axes, a blur disc, hover wander, lights through the shared pass, a rotor sound path. Boarding, ramp crew, tugs,
  routing obstacles, tyre smoke and marshaller cues skip rotorcraft; the demo circuit's static helicopter is hidden
  while the fleet's own stands on the pad.

Affected systems: Domain (type, catalogue, airline, destinations, acquisition, route access), Simulation
(`AirlineOperations.Rotorcraft`, helipad, stands, restore, player base, economics, prep, hangar tow, ground traffic,
hold reasons), Presentation (rig, track, audio mix, flight number, readouts, cockpit message), tools
(`HelicopterReview`, thumbnail script), assets (thumbnail, two audio loops).

Migration: none required. `B412` and `SAAS` are new ids; older saves gain the rescue helicopter on load. Removing the
Bell leaves every other type unchanged.

Not done, on purpose: civil medevac contracts, passenger/patient flows, hangar/hospital ground handling at the far
end, rotor-wash effects, a helicopter cockpit, an AW139 or a light single (each needs a new authored model).

Evidence: headless 1,379 tests pass (incl. `RotorcraftTests`, `HelicopterTrackTests`, `PlayerHelicopterTests`:
state coverage, save in every state, skip-to-next-event equals second-by-second, pose continuity every second for a
day, pad exclusivity, sign and range checks); Unity EditMode for the touched suites; packaged capture of the
helicopter parked, lifting and flying to Flinders (`work/captures/heli-*.png`). The loops have been checked by
spectrum and waveform only; nobody has listened to them yet.
