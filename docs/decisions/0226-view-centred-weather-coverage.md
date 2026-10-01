# 0226 — View-centred weather coverage

Date: 2026-10-01. Status: approved scope; rendering acceptance in progress.

Bailey requested clouds/fog/weather beyond the airport and selected coverage of
all visible landscape, with fade only at rendering limits. Remove airport-relative
cloud wrapping and the ground-fog rectangle. Keep cloud bodies stationary in world
space while the camera travels; recycle a fixed seeded grid only at faded window
edges. Use the same fade on cloud shadows. Retain the sixteen-volume object budget.

Extend fog ray/overcast proxy coverage to the 30 km presentation range; use smooth
20–30 km fading. Overcast remains visible from above instead of clearing the entire
deck as the camera approaches its height. Existing camera-parented fog proxy and
world-space cloud/ceiling noise remain. This is rendering coverage, not spatial
weather simulation. Rain already follows the camera; do not change operations.

Affected: atmosphere/cloud rendering, two existing shaders, coverage tests and
review captures. No new textures/models/shaders, save migration or economy/schedule
change. Asset handling/cost: original project code, zero acquisition cost.

Task packet: `docs/plans/weather-coverage.md`.
Evidence and remaining checks: `docs/testing/weather-coverage-2026-10-01/README.md`.
