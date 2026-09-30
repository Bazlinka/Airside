# Aircraft sound pass — 30 September 2026

## Task packet

Bailey requested realistic audio for all aircraft, including increasing engine
revs and landing sounds. Cover every one of the 13 flying catalogue types and
their authored/live sky counterparts. The parked Bell 412 remains engine-off.

Scope: aircraft audio presentation, its phase hooks, recorded/derived AudioClips,
audio profile data, deterministic sound generation, audio tests and review tools.
ADR 0087/0089/0136/0151/0161 supply the existing recording, mute, mixing and
propeller/fan contracts. ADR 0192 describes this revision.

Acceptance:

- Each catalogue type has a distinct, registered profile with seamless idle,
  loaded-engine and reverse layers. Pitch and loudness rise continuously with
  engine start and commanded power; governed props principally grow in load.
- Tyre contact fires once at visual touchdown, then rolling noise follows actual
  ground speed. Reverse is absent before contact and fades out before taxi-in.
- Both engines contribute to startup/shutdown loudness. Muting is immediate,
  unmuting does not replay touchdown, and cold/hidden/out-of-range aircraft do
  not leave a persistent bed. All aircraft sounds are spatial.
- Every new source has a licence/cost/fallback record. Existing source beds stay
  byte-identical. Derived assets regenerate deterministically with --check.
- Unity EditMode and the generated headless harness pass; a Mac build includes
  the assets. Capture the actual Unity mixer for all types plus a live game
  smoke check. Numerical checks establish timing/levels/packaging; Bailey's
  listening assessment establishes perceived realism.

Remain unchanged: simulation schedules, motion, reservations, engine-start
timing, saves, liveries, other ambience, UI sound and player sound settings.

## Source selection

New tyre source: craigsmith, Freesound 479498, CC0 1.0. The original HQ MP3
preview is retained in `docs/data/audio/src_aircraft_tyre_skid_479498.mp3`.
Page and preview licence were checked on 30 September 2026. It is a historical
aircraft tyre recording, shared by the author as CC0; it is not type-specific.

The existing Dash 8-300, Q400 and turbine beds remain the source for derived
engine layers. These are representative family recordings with sound-design
tuning, not recordings of every named engine variant. A windy A320 recording
and a 737 cabin recording with voices were rejected. A purported landing-gear
recording made for another game was rejected because its production provenance
was ambiguous. No unvetted sample from a commercial simulator is included.

## Verification

Baseline: Unity 1465 passed / 1467 total, zero failures, two known Assume
inconclusives. Final tests, mixer captures and build identity are recorded here
after completion.

Audio branch before integration: Unity 1473 passed / 1475 total, zero failures,
two known Assume inconclusives. Headless 1102 passed, zero failures.
Generator byte check (41 clips / 13 profiles), generated harness check and
Unity asset audit (1522 GUIDs, 347 mirrors, 70 character materials) passed.
The final combined main-plus-audio branch is checked again before packaging.
