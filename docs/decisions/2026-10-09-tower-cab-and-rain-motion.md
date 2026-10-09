# Tower cab and rain motion

Status: Accepted
Date: 2026-10-09

Bailey requested a believable tower interior and faster apparent rain while flying.
The existing mapped tower and standing eye remain authoritative. Original procedural
ceiling facing inward, knee walls, canted posts, sills and low controller consoles
provide interior context. Console displays are decorative abstract tracks, not a
second operational radar. The interior is constructed once and visible only in tower
view; the existing exterior remains the fallback.

Rain remains one 768-drop mesh. Preserve normal aircraft-relative speeds through a
350 m/s safety envelope rather than truncating at 90 m/s. Reject implausible camera
cuts; compare positions with the floating origin included. Integrate full unscaled
elapsed time and derive streak length from an approximately 1/35-second exposure,
with a bounded fraction of the local volume to avoid unbounded geometry.

Affected systems: tower Presentation, weather Presentation, hidden diagnostic probes.
No operational, economy, routing, time, RNG or persisted schema changes. No migration.
No external art, licences or cost. Native and synthetic evidence limitations are
recorded in `docs/testing/tower-rain-2026-10-09/README.md`.
