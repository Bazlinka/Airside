# 0042 — Camera control scheme and single-owner input

Date: 2026-09-13. Requested by Bailey: "Fix when i press follow and toggle off
should be free zoom. Come up with some logical moving controls to look around."

## Decision and reason

### Follow off means free, not "go back"

`ToggleFollow` called `ReturnToOverview()`, which eased the camera all the way
to a fixed pose — centre (0,0,0), fixed yaw, pitch, distance and FOV. Turning
follow off therefore dragged the player back across the field and overrode any
framing they had set. Follow-off now calls `ReleaseFollow()`, which stops
following and hands the camera back exactly where it is, free to orbit, pan and
zoom from there. `ReturnToOverview()` survives as an explicit reset bound to R,
so returning to the overview is something the player asks for rather than
something that happens to them.

### Two input owners was a bug, not a style problem

`AirsideCameraController.ReadInput` handled F and O itself, while
`AirsidePrototype.ReadSimulationControls` also handled F for the HUD's Follow
button. The camera reads input in `LateUpdate`, the prototype in `Update`, so a
single F press ran `ToggleFollow()` (follow off) and then
`CycleOrStartFollow()` (follow straight back on) in the same frame. **F could
never turn follow off.** Follow and reset-view now live only in the prototype,
which owns the HUD bar; the camera owns only camera movement. Exactly one owner
each.

### Zoom that survives the follow

While following, the controller lerps `_distance` toward the phase framing
distance every frame, so a scroll was erased before the next frame drew — zoom
simply did not work in follow. Scroll while following now adjusts `_followZoom`,
a multiplier applied to the phase distance, so the player's framing rides on top
of the phase-aware camera instead of fighting it. It is sticky across follow
sessions, because a preferred framing is a preference.

### The scheme

Right-drag orbits, middle-drag pans, scroll zooms, WASD pans, Q/E orbit without
a mouse, Z/X raise and lower, R resets, F follows. Middle-drag while following
drops follow first: panning a followed aircraft can only fight the follow, so
the pan is read as "I want the camera back".

Pitch opens up from the old 18°–72° to 4°–85°, which is what makes looking
along the runway possible. That can put the camera under the airfield at close
range, so `ApplyTransform` clamps the final position to 2.5 m above
`AirsideAdelaideGround.WorldHeight` beneath it — clamping the result rather than
the angle the player asked for.

## Affected systems

`AirsideCameraController` (input, zoom bias, release, ground clearance; dead
`CycleOrStartFollow` and `SetFollowTarget` removed — with one aircraft there is
nothing to cycle) and `AirsidePrototype` (`ToggleFollow`, new `ResetView`).
Simulation untouched.

## Acceptance and evidence

`scripts/test-domain.sh` **121 passed, 0 failed**. The camera itself needs
UnityEngine and is not covered headlessly, so the control scheme is **unverified
until a Mac Play session**: check that F releases in place, that scroll zooms in
both modes, that middle-drag pans, and that the camera never dips through the
ground at a low orbit angle.
