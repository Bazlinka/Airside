# 0171 — Aircraft windows that read like the real thing

Date: 28 September 2026. Author: Claude, at Bailey's request ("the windows on the plane … just
look nothing like irl", then "fix them as well as the front windows on each aircraft").

## Context

The pane sizes, spacing and height were already close to the real aircraft (a 737 pane is about
25 × 36 cm at a 51 cm pitch). What made the windows look wrong was how they were drawn:

- **Blue-teal glass.** Six aircraft colour functions each tinted the glass blue (for example
  `0.12, 0.26, 0.34`). From outside in daylight, real windows look close to black, with only a
  faint sky reflection.
- **A fixed shine.** `polish-aircraft-glazing.py` added a small light crescent to the same upper
  corner of every pane. It never moved, whatever the viewing angle or time of day.
- **Octagons.** `aircraft_skin.window` used 2 segments per corner, so cabin panes showed their
  facets at follow zoom. Real cabin windows are rounded rectangles.
- **A light metal ring** (`_trim`, grey 0.79) sat round every pane. Real windows are flush, with a
  thin dark seal.
- **Flight deck: separate panes in white skin.** On a real airliner the windscreen reads as one
  dark band broken by thin posts. Here each pane was a separate dark shape with wide white gaps
  between them. On the A350, the model's own painted mask also had a jagged white notch.

## Decision

In the glazing pass (`scripts/polish-aircraft-glazing.py`, which rebuilds all 13 runtime aircraft):

- Cabin panes have 5 segments per corner and a radius of 0.40 × the width.
- The `_trim` ring and the `_reflection` glint are gone. The seal (`_gasket`) runs from 1.03 to
  0.70 (cabin) or 0.74 (flight deck) of the pane outline. It sits on the pane's curved surface
  (interpolated between the pane's own rings), not on a chord, so it can't sag under the skin on a
  tight nose. It covers the rough edge of the skin opening.
- New `glazing_flightdeck_{left,right,centre}_mask`: a dark painted surround. It is the convex hull
  of each group of flight-deck panes plus a 3.5 cm border, laid on the skin 4 mm proud (under the
  7 mm glass front). It is built by ray-casting a mesh through the hull onto the skin, using three
  rays 1 mm apart per vertex so hairline cracks in the skin can't catch it. It also runs under the
  glass rim, which hides white skin showing through the translucent pane. It is removed only over
  the openings, so the pilots stay visible. The centre piece joins the two front windscreens across
  the centre post. On the A350 it also covers the model's own mask. On the ATR and Q400 it
  covers the modelled windscreen posts, and on the Q400 the centre sill as well.
- The A350's own `cockpit_mask_*` is cut only where a face lies wholly inside a pane, so its large
  faces no longer leave holes beyond the pane.

In Unity (`AirsidePrototype`):

- One `AircraftGlass` colour (`0.06, 0.07, 0.08`, translucent) for every cabin window, cockpit side
  window and windscreen on every type, replacing the per-type blue tints. The smooth
  `AircraftGlazing` material still supplies the reflections, and the night and stand glow is
  unchanged.
- Seals, the new surround, windscreen posts, cockpit frames and the A350 mask share one dark
  `AircraftGlazingSurround` colour. `_mask` meshes use painted metal. The Q400's centre
  `cockpit_sill` also uses the surround colour, and its `cockpit_glare` is glass, because it glows
  with the windscreen at night. The ATR's paired `cockpit_sill_*`/`cockpit_glare_*` pieces are skin
  fairings below the band, so they are painted fuselage white.
- Hangar thumbnails use the same scheme.

## Evidence

- `AircraftDispatchTests.EveryModel_HasNeutralDarkGlassInsideADarkFlightDeckSurround`: all 13
  types have dark, neutral glass and a dark surround on both sides.
- `test-aircraft-paint.py`: surrounds are present, and no trim or glint comes back. Also passing:
  `test-aircraft-connectivity.py` and the title-layout check. The A350 title table moved by 1 cm.
- Unity EditMode 1296/1301. The 3 failures are simulation tests that fail the same way on
  `origin/main` a4b7be62.
- Offline close-up renders of every nose were checked in false colour so that skin showing through
  would stand out. No skin shows through after the fixes above.

## Not verified

- Not yet seen in the running game at follow and overview zoom, by day, at dusk or at night.
- Every airliner now gets a dark surround. Some real 737 liveries leave the window frames white,
  but the dark band is the more familiar look, and on every type the posts read as posts.
