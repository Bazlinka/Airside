# 0189 — Smoother follow camera

Status: accepted (Unity feel not yet verified)

The follow camera low-passed its centre with `Lerp(centre, target, 1 - exp(-dt * rate))`. A first-order ease trails a moving
target by speed / rate, so a fast aircraft slid across the frame, and the look-ahead point (placed along the aircraft's
raw heading) swung when it turned.

Now: the centre follows the look point with a critically damped spring that is given the aircraft's filtered velocity
(`AirsideCameraFeel.SmoothFollow`): no lag once settled, no overshoot, the same at any frame rate. Height has its own slower
spring so climb-outs and flares do not bob the frame. The heading used for the look-ahead is filtered with a turn-rate
ceiling (`TurnToward`), the look-ahead scales with ground speed, and yaw turns at a capped rate. Pressing Follow glides in
(the smoothing starts three times softer and settles over 1.4 s). Field of view widens up to 3 degrees at speed. A recycled
slot still cuts straight to the new aircraft; pause freezes everything; player orbit still wins for 0.9 s after a drag.

Pure functions are tested in `CameraFeelTests`; `AirsideCameraController` only wires them. The feel itself needs a look
in a Mac build.
