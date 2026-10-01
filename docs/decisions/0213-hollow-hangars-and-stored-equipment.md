# 0213 — Hollow hangars, open bays and stored equipment

Date: 2026-10-01. Requested by Bailey (more wall detail; buildings that are hollow, with vehicles kept inside).

Before: every hangar, fire station and freight shed was a solid prism with a flat door slab stuck on the wall, so
the aircraft towed in for a check (ADR 0186) vanished into a solid block.

Decision:
- `HangarFront` (Simulation) is the one rule for a hangar's front door: the wall facing the field, square to the axes,
  spanning the building. `BuildingDetail` cuts a real doorway there (`DetailOpening`) and `HangarTow` brings aircraft in
  through that same side, and only aircraft whose wings and tail fit the opening. Awkward footprints (L-shapes) keep
  the old closed door and old tow behaviour.
- A hollow building gets a room behind the doorway: inside wall faces, a concrete floor and a ceiling hung 0.35 m under
  the roof (`AddHollowInterior`), plus trusses and strip lights that glow at night.
- Kept inside, placed in a 2.3 m band by the walls so the middle stays clear for aircraft (`HangarTow` leaves 2.5 m beside
  wing tips): tugs, ground power carts, boarding stairs, tool chests, baggage dollies. Fire-station bays that fit open
  with a crash tender inside; every other freight dock opens onto a raised floor with pallets and sometimes a van.
- Wall detail: standing ribs every 3 m, girt line, eaves gutter, downpipes, louvres, a personnel door with a lit pack and
  plinths on hangars and freight sheds; vertical fins, a spandrel band and a plinth on the terminal landside.
- Cobham's hangar is 17 m (was 10 m) so a 737's tail clears the roof now that the inside is visible.

Not done: doors do not open and close with traffic (always open); the terminal's baggage undercroft is unchanged; the
equipment is simple boxes, not the real vehicle models.

Validation: headless suite; Mac build and captures. Look in play not yet confirmed by Bailey.
