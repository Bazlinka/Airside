# Aircraft presence and camera eligibility

Date: 2026-10-09
Status: Accepted for the continuity fixes; parked activity window remains proposed

Physical aircraft presence must not depend on camera-cycle eligibility or the local
airport climb-out timer. Use journey poses for in-range fixed-wing outbound/inbound
models and known regional ground poses. Explicit ordinary follow retains its target
and shares journey terrain/origin handling with cockpit view. Keep view identity
lookup independent of cycle filtering and allow direct follow of a present model.

Simulation, runway/stand reservations and saved fleet state remain authoritative.
No save schema migration or operational timing changes. Headless presence compile
and syntax checks do not verify Unity rendering or performance. Other arrival timer
extensions and limited network/feed presentation remain documented in the audit.
