# Fleet flight performance and camera telemetry — 8 October 2026

Bailey requested researched flight altitude, vertical speed and takeoff/speed corrections
for the entire fleet after seeing an E190 near FL400 with a fixed 2,000ft/min reading.

Use immutable, cached derived profiles with height-dependent rates, smooth cruise capture,
a separate normal cruise target and CAS/Mach speed constraints. Map distance integrates
the same speed schedule. New jet schedules add twenty minutes beyond cruise travel
(previously ten) to accommodate climb, restrained airspeed and the reserved final.
Existing saved deadlines remain authoritative: an impossible manually supplied
schedule can lag instead of manufacturing overspeed. No save or reservation migration.

Camera telemetry samples actual pose using simulation seconds at both normal and accelerated
rates, cancels floating-origin changes, and shows CAS-as-IAS/Mach alongside GS. This assumes
ISA and zero-wind route motion and does not model instrument error. The aircraft's flight
trajectory is owned by its timed state, independent of the selected camera.

Reserve the same inbound final segment for horizontal and vertical profiles. Regional
runway takeoff integrates the individual representative roll/Vr rather than a universal
40-second, 800m departure. Landing follows a 3-degree slope until its small flare segment.

The 250 CAS below 10,000ft AMSL SOP is conservative game policy; Australian legal applicability
varies by airspace and flight rules. Manufacturer ceilings, published performance anchors,
family estimates and game planning choices remain distinguished in
`docs/data/FLIGHT_PERFORMANCE_RESEARCH.md`. The separate Bell VTOL model remains authoritative.

Acceptance: all fixed-wing types stay within profile heights and low-altitude CAS policy;
height/speed derivatives agree with telemetry/distance; cruise is level with zero V/S;
E190 rate varies with height; camera clocks/origin shifts do not freeze/spike readings;
regional takeoff stays on ground until the type's Vr; arrival joins final without a height or distance jump.
Native math tests/builds are separate from a packaged camera journey and wind-aware physics.
