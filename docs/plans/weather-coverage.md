# Weather across the visible landscape

Approved direction: Bailey, 1 October 2026. Weather covers the visible landscape;
fade only at rendering limits, not into clear weather a set distance from the airport.

## Task packet

Outcome: cloudy/foggy/rainy views continue beyond the airport, including moving
cockpit/follow cameras. No abrupt airport-edge or cloud-height disappearance.
Scope: cloud placement/recycling, shadow fade, atmosphere look, ground-fog and
ceiling shaders, pure coverage helper/tests and repeatable packaged capture script.
Invariants: weather simulation/feed, scheduling, reservations, save schema, pacing,
weather-driven operations and existing rain geometry remain unchanged. Retain
sixteen cloud bodies, their shadows, and the 768-streak rain mesh; no new art imports.

Acceptance: stationary world-space cloud positions while the camera moves inside
the recycling window; distant view/teleport coverage; fade before cloud and shadow
recycling; fog outside the former 8 × 7 km airport rectangle; overcast visible from
above/below; clear weather and disabled layers handled correctly. Run domain/native
checks, clean Mac build, real packaged cloud/fog/storm views beyond the old limits,
above/below deck views and camera movement/weather toggle checks.

## Rendering contract

A jittered 4 × 4 grid distributes the existing sixteen cloud bodies. A 9 × 5.6 km
rendering window moves with the viewer; individual bodies retain their world
positions and wind motion until they recycle across an invisible edge. Fade bands
are 700 m horizontally and 500 m in depth; shadows use the same edge fade.
This is bounded repeated scenery, not a simulation of individual regional clouds.

The low fog ray integral no longer masks an airport-centred rectangle. Its height
profile remains a shallow sea-level bank, with weather haze still driven by the
existing global fog. Ray samples fade over 20–30 km; this is the rendering limit,
not a geographic boundary that makes airport weather turn clear. The overcast
proxy spans 60 km and uses a radial 20–30 km fade with world-space noise. The
cloud deck remains visible from above; it is not switched off below crossing height.
Rain already follows the camera and is retained. Weather-layer disabling also
hides cloud bodies/shadows; normal weather rules/settings/save format are retained.

A screenshot proves one rendered view, not smooth moving-cloud behavior, all
weather/time combinations or frame-time acceptance. Track evidence in
`docs/testing/weather-coverage-2026-10-01/README.md`.
