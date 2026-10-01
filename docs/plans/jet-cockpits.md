# All-jet cockpit interiors

Approved scope: Bailey requested cockpit preparation for every jet, immersive and
complete across the types, on a branch for later merging (1 October 2026).
This implementation extends the existing spectator cockpit. Interactive preflight
switches/checklists and manual flying are separate scope unless requested.

## Task packet

- Player outcome: every locally visible, starting/running jet offers Cockpit with a
  fitted left-seat interior, family-specific controls/windows/displays, pedestal,
  overhead, floor, walls and roof. Existing look/zoom/recenter/exit controls remain.
- Scope: cockpit availability, shared interior lifecycle/geometry helpers, jet
  profiles/builders, cockpit runtime partial, native review and tests.
- Acceptance: explicit profile and geometry for all ten jets; no Saab fallback;
  closed opaque shell outside window openings; correct registration/pose; previous
  exterior renderer visibility restored on Leave/destroy; only one active interior.
- Invariants: autonomous flight, schedules, reservations, economy, save schema,
  exterior models and unrelated audio work stay unchanged.
- Checks: headless profile/access coverage; Unity full suite and all-type lifecycle
  tests; native type-by-type forward/side/panel/overhead/bank stills; Mac build and
  packaged journey/entry/exit/day/night/weather/audio/performance playtests.

## Coverage

| Type | Deck | Controls | Main displays | Fit: eye Y / Z (kit metres) |
|---|---|---|---|---|
| B738 | 737 NG | yokes | 6 | 5.62 / -2.65 |
| B38M | 737 MAX | yokes | 4 | 5.59 / -2.65 |
| A320 | A320 family | sidesticks | 6 | 5.46 / -2.95 |
| A21N | A320 family | sidesticks | 6 | 5.32 / -2.85 |
| E190 | E-Jet | ram-horn yokes | 5 | 4.52 / -2.70 |
| A223 | A220 | sidesticks | 5 | 4.38 / -3.05 |
| A359 | A350 | sidesticks | 6 | 7.92 / -4.45 |
| A339 | A330 | sidesticks | 6 | 7.92 / -3.55 |
| B789 | 787 | yokes | 5 | 7.70 / -4.30 |
| B78X | 787 | yokes | 5 | 7.70 / -4.65 |

Ground offsets are applied separately from `AircraftVisualProfiles`. Anchors were
chosen against each runtime glTF's windscreen accessor bounds; widebody and
narrowbody roots are nose-stop coordinates. Windows are open geometry so the
existing sky/weather remains visible. No transparent glazing layer or extra camera.
Shared geometry does not mean shared fits: every type has an explicit datum.

Panel readouts label ground speed, height above runway datum, heading and engine
spool (the game's 0–1 engine startup presentation state, not measured N1/N2).
The attitude graphic follows rendered bank. Decorative compass marks and controls
are representative; there are no fabricated navigation routes, airspeed, altimeter,
radio frequencies or working flight-control claims.

## Reference evidence

Manufacturer pages inform family layout, with no photograph/textures imported:

- [Airbus cockpit philosophy and family commonality](https://www.airbus.com/en/products-services/commercial-aircraft/cockpits).
- [A350 six-display cockpit](https://www.airbus.com/en/newsroom/press-releases/2019-12-airbus-begins-deliveries-of-first-a350-xwbs-with-touchscreen).
- [Airbus FAST 63: Flying the A220, cockpit photograph](https://www.aircraft.airbus.com/sites/g/files/jlcbta126/files/2021-08/Airbus-FAST63.pdf).
- [Boeing 787 by design, flight deck](https://www.boeing.com/commercial/787/by-design).
- [Boeing 737 NG flight deck](https://www.boeing.com/Commercial/737ng/737-next-generation-design-highlights).
- [Embraer E190 manufacturer page](https://embraer.com/e-jets/e190/en/).

These support a simplified visual candidate, not surveyed dimensions or a complete
manufacturer avionics implementation. All runtime shapes/markings are original
project code. Every type still needs visual acceptance in actual Unity renders.
