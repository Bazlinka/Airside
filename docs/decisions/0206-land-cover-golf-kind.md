# 0206 — Distinct Golf land-cover kind

Date: 2026-10-01 · Owner: Cursor

**Decision:** OSM `leisure=golf_course` paints `AdelaideLandCover.Kind.Golf` (byte 8)
instead of `Park`. Surroundings tint uses a richer irrigated green so courses
(Royal Adelaide, Glenelg, Kooyonga, …) read at overview. Parks stay olive.

**Reason:** Phase 1 land-cover polish (visual overhaul, Bailey-approved). Golf and
city parks were the same tint; courses were invisible as courses.

**Affected systems:** land-cover generator + `AdelaideLandCover` grid; surroundings
vertex tint. Simulation gameplay, routes and saves unchanged.

**Migration impact:** none. New enum value appended (`Golf = 8`); existing class
bytes 0–7 keep their meaning.
