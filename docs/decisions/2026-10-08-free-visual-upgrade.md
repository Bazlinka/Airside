# Free sourced assets and coherent close-view materials

Date: 2026-10-08. Task #585. Owner: Codex / Bazlinka.

## Decision

Implement the ten authorised visual workstreams using free commercial assets and
project-owned refinements. Reuse existing CC0 pavement/terrain maps rather than
redownload equivalent textures. Keep source inputs and licence evidence in git;
regeneration runs offline through `scripts/import-free-visual-assets.py`.

1. **A320**: adapt manilov.ap's CC BY 4.0 engines, pylons, exhausts, fan blades,
   rounded tyres and horizontal tail. Keep the authored hollow fuselage, working
   cabin windows and doors, existing envelope, gear pivots and fictional liveries.
   This is a sourced derivative, not a wholesale original-airline model replacement.
   The old v01 remains a missing-asset fallback; mandatory attribution ships with the app.
2. **Scanned pavement**: one world-space URP shader uses consistent grain size,
   scanned albedo/normal/AO/mask companions, reduced concrete contrast, modest
   macro variation and native light/shadow/fog/depth/SSAO. Dielectric pavement
   stays nonmetallic; actual wetness releases its dry roughness cap.
3. **Wear**: sixteen-sided stand stains feather into the lit pavement using
   multiplicative blending, retaining joints and grain underneath. Two merged
   draws regardless of stand count. Repairs reuse scanned concrete.
4. **Service vehicles**: fitted Kenney cab/wheel derivatives for fuel, baggage,
   passenger bus, catering and pushback. Original functional equipment and
   articulation names/dimensions stay intact. Painted service panels use paint
   material instead of aircraft skin maps. Previous kits remain fallbacks.
5. **Architecture**: repeatable gutters, downpipes and collars on surveyed shells.
   Pipe bays omit doorway intervals. Fittings batch as trim; they are distinct
   from rooftop plant and add no collision geometry.
6. **Ground**: fine 2.2–3 m scanned grass/soil grain within close camera range;
   fade by 130 m while retaining large colour structure and satellite context.
7. **Vegetation**: rounded Kenney thin-tree lobes and branches; original tree
   locations, crown sizes and heights. At most 128 within 1100 m of ARP replace
   coarse crowns, merged into existing kilometre tiles. Remaining trees preserve
   the existing inexpensive distance bands. No new per-tree renderers.
8. **Materials/lighting**: a reproducible native reference scene under one sun,
   with dry/wet pavement, paint and sourced vehicles/foliage; terminal reflections
   now refresh on the existing quantised weather/daylight schedule. Menu:
   `Airside > Review > Free visual upgrade reference`.
9. **Service motion**: conveyor ribs travel at fixed physical speed from the
   injected simulation clock; paused simulation freezes them. Existing CC0 crew,
   loading paths and service tasks stay authoritative.
10. **Cabin**: desaturated Poly Haven fabric, subtle normal/roughness maps, pockets
    and cushion piping in the nearest five rows. Shared materials and merged
    fittings preserve the close-detail budget and distant seat continuation.

## Compatibility and acceptance

No save migration, airport layout, aircraft performance, service reservations or
simulation completion changes. Missing downloaded art retains existing fallbacks.
Runtime images and glTF/bin derivatives are mirrored into StreamingAssets; dynamic
shaders are always included. All asset sources, hashes and adaptations are in the
asset register and `docs/testing/free-visual-upgrade-2026-10-08/`.

Visual validation must inspect native renders and a packaged player separately.
Native reference/aircraft/cabin renders are not a whole-airport performance soak.
Tests protect retained A320 apertures, clear entrances, paused/length-independent
conveyor motion and shader compilation. The pre-existing helicopter test counted
45 original renderers but now saw six fitted detail renderers from task #578;
its count now excludes the clearly named detail children while retaining the
original 45-part silhouette and bounds guard.
