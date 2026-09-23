# Pushback tugs — implementation plan

Status: planned (Bailey asked for it on 23 September 2026, after the Terminal 1
aerobridges, ADR 0113). Written as the AGENTS.md task packet. Build it as its own
ADR, 0114.

## Player-visible outcome

Every aircraft that is pushed back has a real tug:
- it drives up to the nose before departure;
- it is visibly coupled by its towbar through the whole tail-first push;
- it stops, disconnects, and drives back to its stand-by line.

Aircraft that taxi out under their own power never get a tug. Nothing about when an
aircraft pushes, taxis or takes off changes.

## Which aircraft

| Stand | Pushback today | Tug |
|---|---|---|
| T1 gates (all jets) | tail-first `GatePushback` at 3 kt, then a 25 s tug disconnect | yes: conventional towbar tug; a larger tug for code E |
| Regional 50-series bays (ATR, Q400, Saab) | tail-first `BayPushback` | yes: small towbar tug |
| Walk-outs 10A–10D, 2A (Saab only) | reversed stand line | no: taxi out under power (follow-up: a power-turn exit instead of a reverse) |
| RFDS / player at walk-outs | as above | no |

## What already exists

- **Pushback timing.** `AdelaideGround.TaxiOut` already models the push. Part 1 is the
  tail-first pushback path (`GroundSpeedLimits.Pushback`, 3 kt, gentle starts and
  stops). Part 2 starts after `TugDisconnectSeconds = 25`. The presentation pose
  (`FleetGroundPose`, `GroundLeg.PoseAt`) already knows where the nose is at every
  instant of the push.
- **Tug model.** `VEH-004` is the pushback tug kit (v03 authored, v02/v01 fallbacks)
  with towbar, towbar head, tow pivot, cab and wheels (`BuildPushbackTug`,
  `AirsidePrototype.cs`). Today one static instance is built for the stand-equipment
  focus mode and never moves.
- **Pattern to copy.** `EngineStartSequence` and `AerobridgeTimeline` are pure
  functions of fleet state and time that drive presentation without the simulation
  depending on them.

## Design

1. **`PushbackTugTimeline` (Simulation, pure, UnityEngine-free).** Phases per aircraft,
   keyed on its scheduled departure and its actual `TaxiOut` start:
   - `Standby`: no tug for this aircraft.
   - `Approach`: from push − 300 s, the tug drives from its stand-by point to the
     nose, arriving by push − 150 s, after the bridge has cleared (ADR 0113, −190 s).
   - `Coupled`: until the push starts; the aircraft may be held for ground or runway
     clearance, and the tug waits coupled.
   - `Pushing`: while in `TaxiOut` part 1.
   - `Disconnect`: during the 25 s pause.
   - `Return`: about 45 s back to stand-by.
2. **Nose gear datum.** Extend `generate-aircraft-title-layout.py` with the nose-gear
   contact point per type (`gear_nose` / `tire_nose`), next to the L1 door table, with
   `--check`.
3. **Stand-by points.** Derived like the bridge sites, not hand-placed: T1 gates get a
   tug line along the airside road ahead of the gate row; the 50-series gets a spot
   beside T4. Both come from `AdelaideLayout` and are tested for clearance of every
   stand and taxi path.
4. **Pose while coupled or pushing.** The tug is locked to the nose-gear point, facing
   the aircraft, with the towbar along the nose-wheel steering angle. The existing
   `NoseWheelSteerDegrees` already gives the steering; the towbar yaws with it and the
   tug follows the towbar head. Tail-first motion comes free from the aircraft pose.
5. **Approach and return path.** Drive the stand's own pushback path in reverse, from
   the throat to the nose. It is reserved lead-in pavement, so it is clear of other
   stands by construction. The return is the same path out, then back to stand-by. It
   is eased at tug speeds (about 10 kt empty, 3 kt coupled).
6. **Presentation (`AirsidePrototype.PushbackTugs.cs`).**
   - **Pool:** a pool of tug views, sized to the number of phases other than `Standby`
     (peak about 8). The static VEH-004 instance is retired.
   - **Tug size:** code E jets use the same kit scaled 1.3× until a widebody tug asset
     exists. The asset gap is recorded in the register.
   - **Details:** the beacon is on while moving; engine audio uses the existing
     vehicle-audio hooks. An optional wing-walker figure uses CHR-001.
7. **Later (not this ADR).** Tugs as a scarce simulation resource that a push must
   reserve, like runways and stands (AGENTS invariant). That changes timing and saves,
   so it needs its own decision and migration.

## Files in scope

- `Simulation/PushbackTugTimeline.cs` (new)
- `Simulation/AdelaideAerobridges.cs` (or a sibling file) for the generated nose-gear
  table
- `Presentation/AirsidePrototype.PushbackTugs.cs` (new)
- `Presentation/AirsidePrototype.cs` (retire the static tug; add the update hook)
- `scripts/generate-aircraft-title-layout.py`
- `Tests/EditMode/PushbackTugTests.cs`
- ADR 0114, `GAME.md` and `CHANGELOG.md`

## Acceptance criteria

1. Every T1 and 50-series departure shows a tug coupled at the nose for the entire
   tail-first push. It stays within 0.3 m of the nose-gear point, from the push's first
   metre to its last.
2. No walk-out departure and no aircraft on a power exit ever gets a tug.
3. A tug never overlaps any aircraft's footprint, a bridge, or another tug, in a
   two-day soak sampled every second.
4. The tug arrives only after the aerobridge has cleared, and is gone before the
   aircraft starts its taxi (the disconnect pause, then `Return`).
5. A delayed push keeps the tug coupled and waiting. A cancelled departure sends it
   back without coupling.
6. The simulation timeline is identical with tugs on and off. This is presentation
   only, so the timeline determinism test stays green.

## Tests / playtest

- **Headless:** timeline phases for gate, bay and walk-out aircraft; coupling distance
  over sampled push poses; stand-by clearance; the soak overlap check.
- **Mac:**
  - follow a Qantas 737 from gate 21 through push, disconnect and taxi;
  - watch a Rex Saab leave a walk-out without a tug;
  - check a Q400 push from 50D;
  - check an A350 at 18 with the larger tug.

## Must not change

- Pushback timing, taxi paths, runway sequencing and save format.
- The aerobridge timeline (the tug fits around it).
