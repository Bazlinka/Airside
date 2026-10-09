# Tower cab / rain motion — task #756

Reported: tower view feels like being on the roof; rain drops look slow in flight.
Expected: an enclosed controller cab with clear airfield sight lines and rain streaming
past at the aircraft-relative speed.

## Baseline

Pushed diagnostic revision a05ec7d0. Private Mac runner was offline; request
37903724832 was cancelled. An isolated local checkout with cloned Unity cache built
successfully and completed all 17 scenario steps, zero runtime errors. Personal
running game/save untouched. Local evidence:
`work/issue-diagnostics/diag-6706a62891e94cc8/runs/20261009-184739-803687a2/`.
Inspected tower-up/down/side: no ceiling underside, no console, detached-looking posts.
First tower capture is still in the entry blend; subsequent fixed-eye captures are
valid comparisons. Rain fast probe injected an explicitly synthetic 220 m/s observer
velocity; report reads 89.998 m/s, confirming the low cap. This is not a flown journey.

## Cause / fix

Exterior roof prism faces up only and there was no cab interior. Add original inward
ceiling, carpet, knee/sill/fascia/post structure and low consoles on the existing cab
footprint. Keep unobstructed windows and existing eye/camera controls.

Rain was capped at 90 m/s, motion discarded frame time beyond 0.1 seconds and streaks
were capped at three times a radius-derived base. Preserve normal fleet speed, full
elapsed time, fixed exposure length and absolute-world motion across origin shifts.

## Verification

Fixed-revision identical native scenario pending. C# Unity build pending.
`git diff --check` passes. Quick headless attempt unavailable: dotnet absent from PATH;
no SDK installation. No full suites, journeys, performance pass or personal-save tests.
