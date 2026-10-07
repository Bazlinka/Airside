# Aircraft lighting by type (2026-10-07)

ADR `2026-10-07-aircraft-lighting-profiles`. Headless: `AircraftLightingProfileTests` (profile table, strobe
pattern, beacon rate, taxi/takeoff lamp rules). **Unity playtest still needed** (night, follow camera):

1. A320/737 on the roll: nose lamp lit with the wing-root landing beams; off after liftoff.
2. ATR 42 / Saab 340 / Dash 8: single-flash wingtip strobes, no tail strobe, wider shorter landing beam.
3. 787/A350/A330: top and belly beacons, bright strobes, longest narrow landing beams.
4. Bell 412: searchlight points down at the pad on approach.
5. Landing beams hit the runway ahead rather than the horizon; left/right lamps toe outwards (check the sign).
6. Frame time at night with a busy apron unchanged (beam ranges grew on the jets).

## Beam-aim checks (added)

`AircraftLightingProfile.AimGroundHitMetres` gives where a lamp's axis meets level ground; EditMode tests require each
family's landing beam axis to land ahead of the nose (> 3 m) and inside 90% of the lamp range, and the taxi axis 10 m to 80%
of range ahead. `python3 scripts/render-lighting-beams.py` repeats the same checks and draws `beam-geometry.svg` (side view of
landing and taxi cones per family, approximate lamp heights). Offline geometry only, not a Unity capture; it does not settle
the toe-out sign or night look (items 1-6 above).
