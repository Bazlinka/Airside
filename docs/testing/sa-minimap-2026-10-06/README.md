# South Australia mini-map selection — 6 October 2026

Base main: `0c0c752d` (#529). ADR 0235. Corner map now has Airport / South Australia
scopes. The regional scope lists simulated flights from journey coordinates,
including distant inactive actors and interstate services during their SA segment.
A flight click keeps the status card and available camera actions on the HUD;
repeated clicks cycle overlapping registrations. Live-feed decoration is unchanged.

- Full supplementary headless: 1,508 passed, no failures (`domain.log`).
- Full native Unity: 1,957 passed, one inherited ground-separation failure and two
  existing inconclusives (`unity-results.xml`, 1,960 total). The failure is the same
  two episodes already reproduced on main in the fleet-identity evidence. All six
  initial new feature tests pass.
- Final native focused suite: 29/29 (`focused-unity-results.xml`), after the
  coastline scanline optimisation, helicopter track/origin correction and soak
  review entry. Tests cover all served SA destinations, overlap cycling, geographic
  projection, coastline mask, interstate eligibility, hidden-flight readout/card,
  render-origin stability, helicopter exterior availability and existing window/
  exterior camera behaviour.
- Asset audit: 1,667 unique GUIDs, 349 matching runtime art mirrors and 70 character
  materials. No new external art/data. Whitespace check passes.

Mac build and packaged UI review are pending at this source commit. The soak-only
`-airsideReviewPanel flight-map` entry opens the regional scope and selects a
simulated in-scope flight through the same mini-map selection handler.

```sh
bash scripts/test-domain.sh
bash scripts/test-unity.sh
bash scripts/build-mac.sh
bash scripts/capture-game.sh --out work/captures/sa-flight-map.png --delay 12 --minutes 1 -- -airsideReviewPanel flight-map -screen-width 1440 -screen-height 900 -screen-fullscreen 0
```

The regional scope includes coastal routes (129–141 E, 26–38.5 S), not a legal
border polygon. Helicopters follow their existing authored track; cockpit/window
interiors are still unavailable for them. Cargo has no window-seat view. A static
UI capture does not prove hardware mouse input or full-flight weather/performance
acceptance. Simulation schedules, saves and reservation behaviour are unchanged.
