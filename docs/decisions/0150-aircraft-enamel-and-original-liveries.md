# ADR 0150: Aircraft enamel and original fleet liveries

Date: 2026-09-28
Owner: Codex (Astra)
Request: Bailey asked to revisit the aircraft designs, paint and appearance, fix their poor presentation and invent liveries for every aircraft.

## Outcome and scope

All 13 modelled types receive a fitted two-tone fuselage sweep, an original fin
symbol and coordinated cowl bands. White enamel, pale grey lifting surfaces,
dark glazing and restrained bare metal establish a consistent material finish.
The player's existing airline colour and name still drive their aircraft. The
fictional reference colourways use Coastline Regional teal, Emu Air ochre and
Southern Cross Link navy; they are original designs, not copied airline logos.

This task covers the aircraft presentation loaders, material policy, finishing
generator, runtime glTF/bin and editable FBX kits, thumbnails and packaged art.
Airframe envelopes, moving-part names, performance, routes, economy and saves
retain their existing contracts.

## Evidence and decision

Unity renders reproduced the severe tiled noise on aircraft skin and fins.
The authored-material fast path kept the old v01 texture set, and nacelles and
small metal parts could receive the corrugated-metal building material. The
software thumbnail renderer showed neither defect. A separate ATR material
replacement also bypassed the common aircraft finish after assembly.

`AircraftLiveryPaint.MaterialFor` is now the aircraft-only material policy for
both glTF and prefab loading. It uses clean dielectric enamel and untextured
machined metal, retaining transparent glazing and shared material caching.
The rest of the airport keeps its own material policy. Fine detail comes from
the existing modelled openings, doors and control surfaces, rather than coarse
random normal/AO tiles across the fuselage.

`finish-aircraft-liveries.py` clips paint polygons directly against each type's
actual surface triangles. It creates paired fuselage bands, a contrasting
pinstripe, an original fin mark and separate engine bands that follow their
respective wings. Paint is a few millimetres outside the skin. It does not
stretch a decal over unrelated UVs. Three motif families (coastal feathers,
rising sun, compass star) are composed individually for all 13 airframes.

The same finishing pass fits spoiler panels to the upper wing and trims the
pylon tops that previously protruded through it. The original generators remain
the editable source; `polish-aircraft-glazing.py` calls the finishing pass after
opening the glazing. Existing Unity GUIDs are preserved on regeneration.

The packaged review also exposed an existing lamp-cache error: lights are created
on a `Lamp pivot` child, but rebuilt caches searched only the parent and attempted
to add a duplicate light. All four aircraft lamp helpers now resolve the existing
pivot light before creating one. Repeated-cache regression cases cover navigation,
beacon, landing and taxi lights; this prevents per-frame exceptions during review.

## Type-specific compositions

| Type | Design | Motif |
|---|---|---|
| ATR 42 | Saltwater | Coastal feathers |
| Saab 340 | Ochre Country | Rising sun |
| Dash 8 Q400 | Coastal Current | Coastal feathers |
| E190 | Southern Star | Compass star |
| A220-300 | Morning Light | Rising sun |
| A320-200 | Tidal Arc | Coastal feathers |
| 737-800 | Outback Horizon | Rising sun |
| 737-8 | Crosswind | Compass star |
| A321neo | Long Coast | Coastal feathers |
| A350-900 | Southern Aurora | Compass star |
| A330-900neo | Desert Dawn | Rising sun |
| 787-9 | Ocean Reach | Coastal feathers |
| 787-10 | Southern Meridian | Compass star |

## Acceptance and fallback

Check each type from both sides and elevated front/rear views in Unity, not just
in the offline renderer. Check coloured roles, dark/light player accents, clean
skin and cowls, connected geometry, dimensions, paint-to-surface distances,
wing attachments, retained glazing, metadata and packaged asset parity.
`AircraftAppearanceReview.Render` builds the real runtime aircraft in Unity for
repeatable inspection; its studio lighting is explicitly distinct from a live
airport playtest. Record packaged build and day/dusk/night evidence separately.

No new external assets, licences, image generation or paid services are used.
The original asset IDs and runtime paths are retained; the parent git revision
is the complete fallback. No save migration is needed.

Validation results and images: `docs/testing/aircraft-liveries-2026-09-28/`.
