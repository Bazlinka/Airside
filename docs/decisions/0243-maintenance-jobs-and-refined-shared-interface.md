# ADR 0243 — Maintenance jobs and refined shared interface

Date: 7 October 2026. Status: implemented under Bailey's approval; native verification pending.

A timed maintenance check previously projected a visual tow rather than moving the
actual aircraft through startup and traffic. Add a distinct pure `MaintenanceJob`
and append `FleetState.Maintenance`, keeping commercial flight phases unchanged.
The job owns saved phase boundaries; presentation reads its pose and engines.
Reuse aircraft-specific pushback, startup/shutdown ramps, taxi routing, ground
traffic admission and runway-crossing checks. Suppress passenger/cargo services and
reject requests until unloading clears. Charge once, clear wear after the actual
repair, reserve an available return stand and release the job only after parking.

The engine-powered journey stops outside the shed, with enough apron distance for
the complete fuselage. Engines shut down before straight tug positioning to the
existing fitted interior pose. Exit reverses along the entry line; turning is delayed
until outside the shed. Keep each shed exclusively reserved for the entire job.
This sacrifices multi-bay throughput until native swept-clearance evidence supports
concurrent occupancy. Rotorcraft/no-fitting-shed checks use the existing timed
pad/outsourced behavior. This does not add outstation maintenance animation.

Save v21 stores the job and validates the shed, phase, times and return reservation
before resuming. Derived paths are rebuilt, not serialized. Existing v20 and earlier
timed checks continue unchanged without replaying movement or payment. The injected
clock and event boundaries give the same outcome for regular ticks and offline catch-up.

Implement ADR 0242's composition in the existing shared IMGUI painters: full-width
status, slim navigation, compact objective, optional radar and a right aircraft
inspector. Inspector identity and commands stay fixed; body details scroll.
Management sheets replace overview surfaces; a separate message lane below them
preserves command feedback. Increase Fleet/Operations row spacing and preserve
main's unified multi-base Fleet functionality. The game remains real-time: there
are no invented speed controls. Existing saved radar preference is honored; new
preferences default off.

Evidence: `docs/testing/refined-interface-maintenance-2026-10-07/`. Pure simulation
and painter code is compiled/tested headlessly; changed Unity-facing files receive
syntax parsing only. Native appearance, swept aircraft/tug clearance, import,
interaction and performance remain unverified under Bailey's no-Unity instruction.
