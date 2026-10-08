# 0214 — Aircraft cockpit spectator mode

Storm altitude/continuous vibration choices superseded by [2026-10-08-weather-altitude-and-restraint](2026-10-08-weather-altitude-and-restraint.md).

Date: 2026-10-01. Status: approved direction; SF34 candidate under verification.

Bailey approved a cockpit viewpoint from engine start until local departure,
including arrivals, landing and taxi. Adopt the existing local aircraft visibility
boundary rather than screen visibility: a following cockpit camera cannot leave
its own target's frustum. Arrival availability starts when the local view exists
and ends after shutdown. Departure availability starts at the first non-zero
engine spool and ends when the local aircraft is withdrawn.

Use one camera owner, stable registration resolution, a fitted per-type interior,
and no simulation or save changes. SF34 comes first, then a 737 proof, then type
rollout. Unsupported types show a disabled action rather than an incorrectly
positioned shared seat. Cockpit is a spectator mode. See
`docs/plans/cockpit-mode.md` for the task packet and acceptance gates.

Migration: none. Runtime camera/interior state is transient and never saved.

Build prerequisite discovered: remote main e55e3c30 calls `AttachDoorways(root)`
without any committed definition. Remove this single orphan call on the feature
branch; retain ongoing doorway work in the original dirty checkout untouched.

## 2026-10-01 visual revision requested by Bailey

Replace the rejected generic blockout with a gamified SF34B layout based on
visually inspected C&L Aerospace aircraft interior photographs. Keep the raked
window arrangement, paired stacked flight displays, grey panel, central round
engine gauges, squared yokes, radio/lever pedestal and overhead. Use original
geometry and original code-drawn dial art, with decorative avionics and a small
live local telemetry display. References and handling are recorded in
`docs/art/reference/saab340-cockpit/README.md`; no third-party photo pixels ship.
Camera ownership, eligibility, simulation, controls, audio and saves are unchanged.
