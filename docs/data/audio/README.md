# Aircraft sound source and derivatives

The engine beds AUD-007/008/009 are existing registered CC0/public-domain recordings. They remain byte-identical under Resources/Airside/Audio. The 13 profiles are representative sound-design variants of those family beds, rather than exact engine-variant recordings.

## Tyre source (AUD-011)

- Author: craigsmith.
- Source: [Freesound 479498](https://freesound.org/people/craigsmith/sounds/479498/).
- Download: [HQ MP3 preview](https://cdn.freesound.org/previews/479/479498_2524442-hq.mp3).
- Licence: CC0 1.0; checked 30 September 2026 on the source page. Cost: $0.
- The author describes a vintage aircraft tyre/tarmac recording digitised from an old Hollywood tape. It is not aircraft-type-specific.
- The original downloaded preview is retained; the WAV is its mono 22,050 Hz, 16-bit PCM decode using macOS afconvert.
- Processing: the generator takes 0.16–0.64 s, filters and fades it, staggers a second wheel by 75 ms, and adds a small original gear thump.

`src_aircraft_tyre_skid_479498.mp3` SHA-256: `e559692b142730c616c1ef5248d285e099ca98c2984b65dc07d89ebdf2d3e96f`.

`src_aircraft_tyre_skid_479498.wav` SHA-256: `9b9959e5418fc4f5ae27ea546609ff78133fff5ead3da2d6dab681a143930cb6`.

## Reproduction

Use Python 3.9+ and NumPy 2.0.2 (BSD 3-Clause; offline tooling only). Run `python3 scripts/audio/generate_aircraft_audio.py`, or `--check` for a read-only byte check. It uses retained inputs and performs no downloads.

`aircraft_audio_manifest_v01.json` records all 41 derived clips and their SHA-256, duration, RMS, peak and loop-boundary difference. Loops are mono 8-second PCM; their boundary step must be below three times the mean adjacent sample difference (or 0.006), avoiding an exceptional discontinuity. Unity imports the new clips as PCM, with normalisation disabled.

AUD-010 covers engine layers; AUD-011 covers tyre contact; AUD-012 covers original seeded tyre-rolling synthesis. Original source beds and the procedural engine/contact clips remain fallbacks. Missing optional tyre roll stays silent.
