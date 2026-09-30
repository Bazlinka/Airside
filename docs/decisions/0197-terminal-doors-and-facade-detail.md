# 0197 — Terminal 1 doors and facade detail

Date: 2026-09-30 · Owner: Claude · Branch: `claude/terminal-doors-detail`

## Decision
Add presentation-only doors and facade detail to Terminal 1 in `BuildingDetail.ForTerminal`:
- **Gate-lounge doors** at every aerobridge rotunda (`AdelaideAerobridges.Sites`), at the departures-level floor
  (5.0 m), with frame, two glass leaves, gate-number board and threshold plate.
- **Apron-level staff doors** every 44 m along the real curved airside wall, clear of the baggage undercroft portal.
- **Landside entrance banks**: four-leaf automatic sliding doors with sidelights, sign fascia and bollards under the
  kerb canopy, one per ~45 m; vertical expansion joints and downpipes every ~30 m.

## Reason
Bailey asked for a more detailed, immersive terminal with doors accurate to Adelaide Airport.

## Accuracy limits (be honest)
Positions of gate-lounge doors come from the repo's surveyed OSM/AIP gate and aerobridge data, so they cannot drift
from the gates. Public sources confirm only a single terminal with arrivals on the ground level and departures/gates
on level 2. Real landside door positions, dimensions and signage were **not** available, so the entrance banks and
staff doors are regular, plausible placements, not surveyed. Replace with measured data if Bailey supplies it.

## Affected systems / migration
Presentation only. No footprints, gates, routes, saves or simulation change.
