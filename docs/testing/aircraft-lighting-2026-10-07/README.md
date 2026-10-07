# Aircraft lighting by type (2026-10-07)

ADR `2026-10-07-aircraft-lighting-profiles`. Headless: `AircraftLightingProfileTests` (profile table, strobe
pattern, beacon rate, taxi/takeoff lamp rules). **Unity playtest still needed** (night, follow camera):

1. A320/737 on the roll: nose lamp lit with the wing-root landing beams; off after liftoff.
2. ATR 42 / Saab 340 / Dash 8: single-flash wingtip strobes, no tail strobe, wider shorter landing beam.
3. 787/A350/A330: top and belly beacons, bright strobes, longest narrow landing beams.
4. Bell 412: searchlight points down at the pad on approach.
5. Landing beams hit the runway ahead rather than the horizon; left/right lamps toe outwards (check the sign).
6. Frame time at night with a busy apron unchanged (beam ranges grew on the jets).
