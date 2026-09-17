# 0054 — Field camera zooms toward the pointer and grabs the ground

Date: 2026-09-17. Requested by Bailey: zooming and navigating mechanics need
lots of work.

## Decision and reason

The destinations map already kept its pivot under the cursor (`ZoomAtGui`) and
grabbed lon/lat under the finger when dragging (`PanByGuiDelta`). The 3D field
camera did neither: scroll only changed orbit distance around a fixed centre, and
drag pan used a flat "metres per pixel × distance" approximation. On a real-metre
YPAD that felt stuck and disconnected — zooming toward a stand on the edge of
frame did not bring that stand closer, and dragging near the horizon slipped.

### Zoom toward the pointer

Free-camera scroll latches the ground point under the pointer and, as the eased
log-zoom applies, shifts the orbit centre with
`AirsideCameraFeel.ZoomTowardPivot` so that point stays under the cursor. Follow
mode is unchanged: scroll still biases `_followZoom` on the phase framing, which
owns the centre.

### Grab-the-ground pan

Left- and middle-drag now move the orbit centre by the ground delta between the
previous and current pointer rays (same idea as the map). The old distance-scaled
planar pan survives only as a fallback when the ray hits the sky. WASD matches
drag: it drops follow first, then pans — previously WASD was simply ignored
while following, which fought the "pan means give me the camera back" rule.

### Soft pan limit

The free-camera centre is soft-clamped to 3.8 km of the overview focus so
navigation cannot lose the airfield in empty ocean. Rates (one notch ≈ 25 %,
drag scale, orbit degrees per pixel) stay as tuned in #278.

Pure orbit / screen-ray / ground-hit / zoom-toward math lives in
`AirsideCameraFeel` (no UnityEngine) so the headless harness locks it.

## Affected systems

`AirsideCameraFeel`, `AirsideCameraController`, `CameraFeelTests`. Simulation,
saves and follow framing curves are unchanged.

## Acceptance and evidence

`scripts/test-domain.sh` **316 passed** (12 new camera-feel locks). Live Mac
playtest still needed to judge the feel at overview and apron distances.
