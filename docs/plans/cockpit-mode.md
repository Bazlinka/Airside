# Aircraft cockpit spectator mode

Approved by Bailey in chat, 1 October 2026. First implementation is SF34 only.

## Player outcome

Select a local aircraft, choose Cockpit, and watch autonomous flight from the left
pilot seat. Departures are available from first engine spool through local climb-out;
arrivals from the existing local approach visibility boundary through taxi-in and
engine shutdown. This uses local presentation visibility, not the camera frustum.
Cold aircraft and aircraft outside the local area cannot be entered.

Right-drag looks within the seat limits, scroll adjusts viewing FOV, Recenter faces
forward, Esc returns to external follow of the same registration, and R resets to
an unselected overview. Target loss returns to overview with a toast; shutdown
returns to external follow. A compact strip replaces large management panels.

## Task packet: first milestone

- Outcome: an original SF34 interior and dependable cockpit camera/access controls.
- Scope: camera controller, selected-aircraft card, cockpit presentation partial,
  SF34 interior builder, listener/emitter mix, review capture guards and tests.
- Invariants: simulation, routes, reservations, injected time, real-time pacing,
  economy, existing assets, save schema and normal external camera remain unchanged.
- Acceptance: startup, taxi/holds, takeoff, climb, approach/go-around, landing,
  taxi-in and shutdown; repeated entry/exit; lost/rebuilt view; menu/input ownership;
  no shell occlusion; correct target; clear windows; restored exterior/settings;
  representative day/dusk/night/rain/fog/storm review and frame-time comparison.
- Checks: baseline and changed domain suite, native Unity EditMode, Mac build,
  genuine rendered SF34 cockpit and packaged game capture. Screenshots do not
  establish controls, all-phase motion, audio quality or a performance baseline.

## Implementation choices

One existing camera controller owns the transform. Cockpit mode branches before
normal overview/follow input. The aircraft runtime resolves registration after
final aircraft poses each Update; the controller applies the seat pose in
LateUpdate. Interior transform and head rotation follow the displayed aircraft.
No second flight model and no extra flight controls.

The original SF34 interior is a procedural 3D candidate with a left seat, lining,
window pillars, glare shield, instrument face, yokes and pedestal. It fits the
existing kit coordinate convention and ground offset. It is representative,
not a claim of a surveyed or certified Saab flight-deck reproduction. No external
assets or generated reference images are used. Exterior shell/glazing renderers
are hidden for the active interior and their prior forceRenderingOff values
restored on exit; wings, nacelles and propellers remain visible. Only one interior
is built at a time. Cold/inactive interiors have no persistent scene allocation.

The panel labels ground speed (horizontal rendered motion), height above the
runway datum (not MSL or terrain-following radar altitude), and heading. There is
no invented airspeed or functioning avionics. Audio listener follows the seat,
with reduced engine gain and a restrained low-pass mix; listening acceptance
remains separate from code evidence.

## Rollout

1. Prove SF34 view, controls, lifecycle and complete local journey; refine the
   candidate against actual game review.
2. Add the first jet (737) with its own seat/windows/panel proportions and
   pushback/start timing; no shared SF34 interior fallback for unsupported types.
3. Expand across all supported flying types using shared construction/materials
   but explicit type profiles and individual visual acceptance.
4. Optional restrained vibration, stronger instrument presentation and extra
   viewpoints only after stable movement and performance evidence.

## Deferred

Manual flying, interactive avionics, complete overhead panels, model-specific
recorded interior audio, opening cold cockpits, remote destination/cruise scenes,
VR and fleet-wide cockpit production are separate work.
