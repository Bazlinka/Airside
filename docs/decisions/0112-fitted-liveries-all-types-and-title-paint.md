# 0112 — Fitted liveries on every type, and fuselage titles that are paint

Date: 23 September 2026. Bailey asked for a livery and paint pass on the aircraft, and
for the airline names to stop reading through the wings.

## What was wrong

- **Titles were boards, not paint.** Every title was one vertical TextMesh plane at a
  fixed `SideX`. The layout table was authored without the art root's -0.68 / -0.70 m
  ground offset, so on the 737 the title centre sat in the window band. It was also
  1.4 m tall, level with the wing root (glTF y 4.07–4.46), and ran up to 13 m aft,
  past the wing's leading edge. Because the plane stood 0.1–0.4 m off the curved skin,
  its lower part came out through the top of the wing root. On the ATR the fallback
  layout put the title at x 0.88 m, inside the ATR's 1.40 m fuselage.
- **The title material could drop out in a build.** It looked up
  `Universal Render Pipeline/Unlit` at runtime and flipped alpha-clip keywords.
  Neither is guaranteed to survive shader stripping, and the fallback was the GUI font
  shader.
- **Liveries were missing on six types.**
  - E190, A220 and A321neo: the "stripe" was a buried box.
  - A350, 787-9 and 787-10: no livery geometry at all.
  All six fell back to the repeating traffic decal that barcodes the fuselage (the
  problem ADR 0109 fixed on the 737s).
- **Engines were painted blue.** Narrowbody and turboprop engines and nacelles fell
  through to the shared part colour, a steel blue `(0.15, 0.38, 0.55)`.

## Decision

### Titles and registrations are generated from the mesh

`scripts/generate-aircraft-title-layout.py` reads each runtime glTF and writes the
`AircraftIdentityMarkings` table in the art-root frame. For each type:

- **Title height:** the title sits above the cabin windows, with the cap centre at
  window top + a small gap + half the cap. Cap height is about 0.6 of the skin between
  the windows and the crown, capped at 0.42 m (turboprop), 0.75 m (narrowbody) or
  1.2 m (widebody). The plane sits 2.5 cm off the skin and leans back to be tangent to
  the fuselage (about 35°), so no edge floats off the tube.
- **Title position along the fuselage:** the forward end is just aft of the flight
  deck, where the tube reaches 97% of its cabin radius. Text is anchored at the nose
  end on both sides, so a long name grows toward the tail, never past the nose.
- **Title length:** limited to the smaller of 34% of fuselage length and the clear run.
  The clear run stops 1 m before the aft taper, or 0.6 m ahead of any wing root that
  rises into the title band, as on the high-wing ATR and Q400.
- **Registration:** small (cap 0.16 × cabin radius), on the aft fuselage, anchored at
  its aft end.

`AircraftTitlePaint.TitleLengthBudgetMetres` respects the fitted clear run.
`--check` fails if the table no longer matches the meshes.

### Title paint shader

`Art/Shaders/FuselagePaint.shader` (`Airside/FuselagePaint`) replaces the runtime-found
URP Unlit material:

- opaque and alpha-tested on the font atlas's alpha;
- `ZWrite On`, `ZTest LEqual`;
- a constant `Offset 0, -1`, with no slope term, so it can never pull through a wing;
- lit by the main light and sky like the fuselage;
- DepthOnly and DepthNormals passes, so depth priming and SSAO treat it as surface.

It is on GraphicsSettings' Always Included Shaders, like the project's other runtime
shaders. The material follows `Font.textureRebuilt`, so a growing dynamic-font atlas
cannot garble the titles. The daylight tint no longer darkens this lit paint a second
time. The URP-Unlit / GUI depth-test path remains only as a fallback if the shader is
missing.

### Liveries on every type

- **E190, A220:** skin-conforming `livery_ribbon` sashes replace the buried boxes
  (generators AIR-013 / AIR-014).
- **A350 (AIR-009):** gains the sash. The 787-10 (AIR-010) carries it as
  fuselage-attached, and the 787-9 inherits it. The A330 already had its own.
- **A321neo (AIR-008):** regenerated from the current 737-8 base. That brings the
  737's fitted sash plus the ADR 0108/0109 nose, door and engine refinements the kit
  had not yet picked up (up to 0.2 m on the fuselage loft).

Presentation now drops the barcode decal for any kit with a fitted sash (it checks for
a `Livery stripe lower` part). Only the primitive fallback keeps the decal.

### Engine paint

Jet cowlings are pale grey with a bare-metal intake lip. Turboprop nacelles are white
to match the fuselage. Pylons are light grey. This applies to the 737 / A320 family,
E-Jet and A220 through the narrowbody path, and to the Q400, ATR and Saab. The A350 /
787 path already had neutral engines.

Airline colour stays on the sash, fin, rudder and winglets.

## Evidence

- `scripts/test-aircraft-paint.py`: 13 types wear a two-sided sash on the skin, and the
  title table matches the meshes.
- `scripts/test-aircraft-connectivity.py`: every part is within 5 cm.
- The per-type generator tests (AIR-005, -008, -009, -010, fleet) pass.
- `scripts/render-aircraft-paint.py` z-buffers the TextMesh glyphs with the real mesh
  from side, front-high, rear-low and rear-high views. Every title lies on the upper
  skin above the windows. The low rear view shows the wing passing beneath it, not
  through it.
- `scripts/test-domain.sh`: 737/737 passing.
- Not yet run: the Unity editor compile and a Mac play check.

## Affected systems

- **Generators:** AIR-008 / 009 / 010 / 013 / 014 and the fleet 787-9.
- **Runtime:** the glTF / FBX kits, their StreamingAssets copies, and six Hangar
  thumbnails.
- **Presentation:** `AircraftTitlePaint`, `AirsidePrototype.FleetVisuals` (title
  placement, paint material, tint) and `AirsidePrototype` (engine paint).
- **Project settings:** GraphicsSettings (Always Included Shaders).

## Migration

None. Saves and simulation are unchanged.

## Guardrails

- Liveries stay generic operator colour (sash, fin and winglets). There are no real
  airline graphics or trademarks.
- The turboprop titles on the forward fuselage can be partly hidden by a propeller or
  nacelle in a pure side view. That is correct depth, as on the real aircraft, not
  see-through.
