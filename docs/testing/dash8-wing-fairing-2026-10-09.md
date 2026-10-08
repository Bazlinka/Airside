# Dash 8 wing-body fairing repair — 9 October 2026

Task #691. Bailey's screenshot showed overlapping raised shell edges above the
fuselage. The two oval side pods and oval centre saddle were separate closed
lofts with exposed caps, inconsistent local winding and disconnected roof
silhouettes. The replacement is one continuous closed crown fairing.

## Scope and source

Only AIR-006's fairing changes. Existing asset paths/GUIDs, wing/engine/gear/tail
geometry, liveries, simulation and saves remain. No external asset or design
scope change. `generate-air-006-dash8-q400.py --fairing-only` repairs the finished
kit and exports glTF/bin/editable FBX. Full generator also uses the revised
saddle. Whole-aircraft finishing was avoided for delivery because it also
moved existing dorsal-fin/beacon geometry and slightly changed paint/gear.

## Bounded evidence

- Python syntax and diff whitespace checks pass.
- Saddle: 5,742 shared vertices, 11,480 nondegenerate triangles, positive signed
  volume 20.839 m3. Every indexed edge belongs to two triangles.
- Top, bottom and both end caps have outward winding. Concave caps use matched
  strips rather than a centre fan that can cross outside the ring.
- Both end caps lie inside the fuselage. Outboard joins follow the actual
  cosine-sampled wing section, with 12 mm overlap above/below its skin.
- Finished-kit comparison with baseline: all 196 other retained nodes have
  byte-identical triangle coordinates; only `wing_centre_saddle` changes and
  `wing_fairing_left/right` are removed.
- Focused DH8D floating-part audit passes. Existing hangar thumbnail rebuilt;
  model/bin/thumbnail packaged mirrors are byte-identical, metadata retained.
- Full asset audit is blocked by existing local numbered duplicate files without
  metadata and `tx_adelaide_sentinel2_l2a_v02.jpg` source/mirror mismatch. No
  duplicate assets or satellite data were modified by this task.

## Unverified

No Unity launch, packaged build, full test suite or gameplay/lighting/performance
review. Bailey should inspect the centre fairing from front/rear/side follow
views in the next chosen playtest. The thumbnail update is an asset-generation
step, not native visual validation.
