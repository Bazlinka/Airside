# 0086 — AI-only metal, real Adelaide sun and moon

Date: 21 September 2026. Bailey, after playing the maintenance-planning build: the
Operations board listed many flights with nothing on the map; live ADS-B aircraft
are not wanted on the field yet; the sun should be a visible figure, with a moon,
moving the right way for Adelaide.

## Decision

### Board and ground are the sim's aircraft only

The Operations board lists **player and AI fleet that actually exist** — parked
with a booking, taxiing, holding, landing, departed. It no longer overlays
`AdelaideDayPlan` ghosts ("Listed" / "Expected" with stand "—"). Those rows were
honest about the stand, but they still read as missing aeroplanes.

The authored day plan still drives **sky overflights** (`SkyTraffic`) so the
airport feels busy without parking dozens of extra airframes. It does not appear
on the board or on the pavement.

Live ADS-B (adsb.lol, ADR 0081/0082) stays presentation-only and **defaults off**.
Even when turned on in Options it is **sky only** — never drawn on the ground,
stands or runways. The game is not ready for real metal to share the field with
the sim.

### Sun and moon

Lighting and the visible discs follow a real celestial path for Adelaide
(YPAD, −34.945°, 138.531°) at the same UTC instant as the HUD clock:

- Azimuth 0 = true north, 90 = east. Southern-hemisphere noon is **north** of
  the 05/23 strip, not a fake Unity yaw sweep.
- The sun walks **east → north → west**. Rise and set keep their golden-hour
  colour.
- The moon uses its own RA/Dec (not 180° from the sun), so a lit sphere can
  show phases. It can be up in the daytime.
- World directions go through `YpadFrame` like every other real coordinate.

`DayCycle`'s hour-based sine elevation remains for circuit-sandbox clocks and
tests; airline presentation reads `CelestialSky`.

## Affected systems

Domain (`CelestialSky`, `AirlineClock.UtcAt`), Simulation (`SkyDirection`),
Presentation (`OperationsWorkspace`, `AirsidePrototype` day cycle and discs,
`AirsideSettings.LiveTraffic` default).

## Migration impact

None for saves. Live-traffic Options uses a new pref key (`livetraffic.v2`) so a
previous default-on session does not keep drawing real aircraft.

## Guardrails

- Do not put day-plan or live ADS-B rows back on the Operations board.
- Do not draw live aircraft on the pavement until a later ADR owns that clash
  with stands and runways.
- Celestial math stays Unity-free and is tested: equinox noon north, morning
  east of evening, winter noon lower than summer.
