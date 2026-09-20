# 0059 — Storm lightning/thunder, and a sun/moon that hides behind cloud

Date: 2026-09-20

## Decision

1. **Lightning cadence.** New `Simulation/Lightning.cs`: `Lightning.StrikesAt(SimulationTime)`
   is a pure hash of simulated time, in the same style as `Weather.At` — no random source
   touched, so a live view and an offline catch-up (or two players on the same seed) agree on
   exactly which seconds strike. It only ever returns true during a `WeatherKind.Storm` block;
   the strike ladder restarts at that block's own first second (which always strikes), with
   gaps of 5–13 s after that. `Lightning.DistanceFor` draws a 0 (overhead) .. 1 (horizon) value
   per strike from the same kind of hash; `ThunderDelaySeconds` turns that into how long sound
   lags light (0.3–7.3 s).
2. **Presentation.** `AirsidePrototype.Update()` checks `Lightning.StrikesAt(_clock.Now)` at
   the one point per frame the simulated second actually advances (never per-frame — see
   Determinism below), and on a strike records the real-time moment and schedules a thunder
   clap. `ApplyDayCycle()` overlays a ~0.5 s double-pulse flash (`LightningFlashEnvelope`) on
   the sun, ambient light, fog colour, camera background and horizon dome; `UpdateAmbientAudio()`
   fires the thunder one-shot once real time reaches its delayed cue. The clap is a procedural
   crack-plus-rumble (`CreateThunderClip`, no CC0 sample sourced yet — see
   `docs/data/ASSET_AND_DATA_REGISTER.md` AUD-006); a real sample can replace it with no code
   change, the same `Resources.Load ?? CreateXClip()` pattern already used for wind/rain/coast.
3. **Sun/moon discs now respect cloud cover.** `UpdateSunAndMoonDiscs` used to show a full-
   brightness sun disc any time it was above the horizon and it was daytime, regardless of
   forecast — a crisp sun during a thunderstorm. It now fades both discs by
   `WeatherLook.CloudCover` (light haze under Cloudy, gone by Overcast/Rain/Storm) instead of
   only dimming the directional light and ambient values, which is what "sunny weather" was
   missing to read as unrealistic. Presentation only; no simulation-facing change.

## Reason

Bailey: "add lightning and thunder visuals for storms" and asked whether cloud/overcast/fog/
rain/sunny weather look realistic. The storm ground stop (ADR 0058) gave weather a mechanical
consequence; this pass is its look — a storm block was otherwise indistinguishable from a
plain rainy hour except for slightly more rain and gloom. On investigating what the "realistic
weather" ask actually needed, the fog/rain/cloud-band system (`UpdateCloudDrift`,
`BuildCloudBands`) was already thorough — cloud alpha, tint and ground-umbra strength already
scale with `WeatherLook.CloudCover`, and rain drops already thicken/darken/speed up for storms.
The sun/moon discs were the one genuine gap found by reading that code, not guessed at.

## Determinism

`Weather.At` and now `Lightning.StrikesAt` are both pure functions of simulated time — no RNG,
identical under any step size or offline catch-up. Lightning does not need the ADR 0058
NextEventAt fix (it never gates anything the tower or a save depends on), but it does need to
be asked at the right granularity: `Update()` calls it exactly once, at the single point each
frame where the simulated second changes, not once per frame. Asking every frame would
re-answer the same question for as long as that second stays current without ever detecting
the edge that should trigger a new flash; a big real-time jump (an alt-tab, a slept Mac) only
asks about the second it lands on, so it never replays a backlog of missed strikes — correct
for a cosmetic effect, unlike a tower clearance a save must reconstruct exactly.

Verified, not just reasoned about: `LightningTests` (6 new tests) checks the timeline's known
`Storm` block (122400–125999s, same block `RunwayWeatherTests`/ADR 0058 use) never strikes
outside it, strikes on its own first second, is deterministic under repeated queries, keeps
every gap in the authored 5–13 s range across the whole block, matches an independently
computed ladder at named seconds, and keeps distance/delay in bounds.
`scripts/test-domain.sh`: 494/494.

## Consequences

- No save field, no simulation effect, no change to flight timing, the ground stop, or any
  existing test's expected outcome — purely additive Presentation and one new Simulation file
  that nothing else calls.
- `AirsidePrototype.cs`, `UpdateSunAndMoonDiscs`, `UpdateAmbientAudio`, `ApplyDayCycle` and the
  audio-source construction block all changed; none of this compiles in the headless harness
  (Presentation, UnityEngine-dependent), so it is reviewed by inspection only here.
  `scripts/test-unity.sh` and a Play-mode look at a storm — flash timing, thunder delay/pitch,
  and the sun/moon fading in and out of Cloudy/Overcast/Storm — are still needed before this is
  trusted at the level `Lightning.cs`'s own tests are.
- A real CC0 thunder sample (encoded and registered the way AUD-002/004 were) is left for a
  follow-up rather than attempted here: those three clips were vetted, downloaded and
  losslessly re-encoded (`afconvert`, a Mac tool) by someone who could listen to the result
  before it shipped — this session has neither a Mac nor a way to judge audio quality, and
  guessing at a licence/attribution match without either felt like the wrong place to cut a
  corner. The procedural clap is a complete, working implementation on its own in the meantime.

## Tests

`LightningTests` (+6, new file). `scripts/test-domain.sh`: 494/494.
