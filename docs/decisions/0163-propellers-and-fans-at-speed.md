# 0163 — Propellers that disappear at speed, and turbofans with a solid face

Date: 28 September 2026. Author: Claude, at Bailey's request ("when propellers spin at full speed
you can see [them] … they don't go invisible. So really fix that up. And for the jet engines as
well").

## Context

At full power the blades hid behind a blur disc (ADR 0148/0151), but the disc itself showed blades:

- **Propellers.** The procedural texture was a 30 % grey body with **painted blade ghosts**, and the
  disc was counter-rotated to 3 % of the shaft speed. At full power you saw grey blades turning
  slowly: a propeller that never went away.
- **Spinners.** Every child renderer of the propeller was switched off with the blades, so the
  spinner, hub and stripe vanished too, leaving an empty nacelle nose.
- **Blur timing.** Blur was judged on the frame's own time. A short frame (120 Hz, or a frame where
  the clock did not move) could briefly bring the solid blades back.
- **Turbofans.** The fan disc was a 26 % glass cylinder. Once the fan blades hid, the intake went
  see-through.

## Decision

- **Propeller disc = real blade coverage.** The texture alpha is the blade solidity at each radius:
  blades × chord ÷ circumference × 0.8 contrast, with a planform widest near the root and tapering
  to the tip. This gives a 5–20 % haze, densest near the hub, almost clear at the tips, plus a faint
  7 % painted-tip ring. There are no blade ghosts, and the radially symmetric disc turns with the
  propeller. It still thickens with blade pitch and fades edge-on.
- **Solid parts stay.** The spinner, hub, hub cap and stripe remain visible at speed
  (`BlursAtSpeed`).
- **Turbofan face.** Now a near-solid dark disc (94 %, with a faint lighter band where the blade
  twist catches the light), clear over the spinner. It is a quad in the fan plane using the same
  unlit blur material path.
- **Steady blur.** Blur is judged on at least a 60 Hz frame (`BlurStepDegrees`), so a short frame
  can't pop the blades back. A starting propeller at motoring speed still shows its blades.
- **Night.** Both unlit discs dim with daylight (`DiscLightLevel`) so they do not glow at night.

## Evidence

`PropellerDynamicsTests` (run headlessly against Unity shims, 12/12):

- `PropDisc_AllButDisappearsAtFullPower`
- `JetFanDisc_IsASolidFaceWithTheSpinnerShowing`
- `Blur_DoesNotPopBackOnAShortFrame`
- `Spinner_StaysWhenTheBladesBlur`

Headless harness 954/954.

## Not verified

**Not seen in Unity.** Whether 5–20 % reads right against the sky and the apron needs a play test.
The 737 kit has 12 fan blades where a CFM56-7B has 24; that shows only while the fan is slow.
