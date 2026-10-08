# Aircraft and soundscape audio presentation v02

- Date: 2026-10-08
- Task: #614; Owner: Codex / Bazlinka, cloud Agent 2
- Decision: extend ADR 0192/0227's presentation-only layers with a v02 bank,
  independent left/right rotating core and starter, spatial/directive exhaust,
  cabin pack/airflow/rain, phase/door mechanical cues, bounded traffic mix and a
  stereo-linked final listener limiter. The v02 runtime selection supersedes the
  v01 bank selection; v01/family assets and pure phase mix remain fallback/lineage.

The old three-family beds changed level/pitch, but the fan/blade identity could
not move independently of exhaust power, starts were one undifferentiated sound,
network aircraft had no update, and aircraft interiors merely filtered exterior
sound. The working apron and PA were nonspatial and could accompany distant flight
views. One host's filter also covered every aircraft band.

Four loops per type now separate exhaust load from rotating machinery. Independently
spooled cores/starters sit on two hosts and retain stereo identity inside the hull;
forward/aft orientation shapes exterior exhaust. A governed prop's core follows
rotation while a jet core sweeps with N1, leaving recorded exhaust's pitch modest.
Rising spool drives the starter; shutdown's falling machinery does not re-trigger it.
Gear/flap/door and touchdown edges are consumed while muted and are not replayed on
restore or re-entry. Regional journey phases drive approach/contact/reverse, rather
than Adelaide's often still-Departed operation phase. Network views update their
engines, listener and cabin too.

Six audible airframes receive leases by listener distance, with camera subject
priority; disabled, out-of-range and muted sources stop. Interior aircraft use
insulation/panning without exterior distance attenuation or Doppler. Exterior
camera-follow also disables teleport/floating-origin Doppler. Cabin/airflow/rain
have their own filtered hosts and release on camera exit; cached resource clips
are never destroyed. The local terminal PA/apron are spatial at mapped terminal-gate coordinates in
the default real-metre scene (independent of the legacy BareWorld flag) and suppressed away
from Adelaide; weather fades with listener height and coast sound stays local.
Original felt-tine HUD cues, quiet switch acknowledgement and ambience/aircraft
celebration ducking finish the mix. The master limiter leaves normal audio intact,
links all channels for peak protection and releases over 90 ms, with no allocations
or Unity calls on the audio thread. A 64-real-voice Unity budget accommodates the
six-airframe cap plus environmental/interior/UI cues (512 virtual voices retained).

All audio acquisition/processing is offline and registered, including the newly
verified CC0 richwise cabin recording. Designed signatures are representative,
not manufacturer/engine-variant recordings. The raw sources and v01 assets remain
immutable. No simulation, clock, reservations, route economics, persisted data or
save schema changes. Migration impact: none; presentation asset bank changes only.

Validation and remaining native listening/device/performance checks are recorded
in `docs/testing/audio-overhaul-2026-10-08/README.md`. Numerical checks cannot establish
perceived quality or Unity playback behaviour; no packaged audio listening pass is
claimed.
