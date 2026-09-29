# 0181 — The airport fence follows the real aerodrome boundary

Date: 29 September 2026. Author: Claude, from Bailey's request for airport boundaries.

## Player-visible outcome

A chain-link security fence runs around the real Adelaide Airport boundary (about 15 km), with vehicle
gates where the airside roads leave it. It is on by default.

## Decision

- Data: OSM way 146489105 (`aeroway=aerodrome`, YPAD) from a committed Overpass snapshot
  (`docs/data/osm/ypad-boundary-2026-09-29.json`, ODbL, registered as DAT-YPAD-BOUNDARY). Baked to
  `Simulation/AdelaideBoundary.cs` by `scripts/generate-ypad-boundary.py` (runway frame, 2 m
  simplification, 155 points). Gates are the two places the airside-only OSM service roads cross the outline.
- `AdelaideBoundaryFence` (pure) cuts the outline into runs of at most 40 m with an opening at each gate;
  `AirsidePrototype.BoundaryFence` builds posts, mesh and top guard as three batched meshes on the landform
  (the same height rules as the old fence), plus yellow gate leaves.
- `-airsideNoBoundaryFence` leaves it out. The old 3.4 × 2.3 km rectangle (`-airsidePerimeterFence`) is no
  longer built; its code stays for now.

## Notes

- The outline runs along the terminal's landside wall, so terminal corners sit on the line (tested to 5 m).
- The other fence ways in the extract (a golf-club fence, short unnamed pieces) are not used: they are not
  the perimeter.
- Not yet seen in a rebuilt game: check fence height at overview and from the ends of the runway, and the
  gates.
