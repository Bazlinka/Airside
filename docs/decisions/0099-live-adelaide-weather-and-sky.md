# 0099 — Live Adelaide weather and a complete celestial sky

Date: 22 September 2026.

## Decision

Airside may poll Open-Meteo's general Forecast API for the fixed Adelaide Airport coordinate
(-34.945, 138.531) and use the returned current conditions for **presentation only**. The
sample controls cloud cover, rain density, visibility fog, ground wetness, puddles, tyre spray,
weather tint and wind-reactive visuals. It never enters `Weather`, `RunwayWeather`,
`AirlineOperations`, command handling, saves or replay.

The feed polls no more often than every 15 minutes, is stale after two hours, and falls back to
the existing deterministic weather without interrupting play. It is enabled by default and can
be disabled in Options. The only location sent is the fixed airport coordinate; no player
location, identifier or API key is requested.

The real Adelaide scene must also build the astronomical presentation that previously existed
only in the legacy miniature scene: the `CelestialSky` sun and moon, golden-hour transition and
a camera-centred star mesh. Clouds obscure all three. The star field uses one mesh/draw call,
and wet-ground accents reuse the existing runtime materials and procedural VFX.

## Why this boundary

The player asked for the sky and weather to reflect the real world. Live data is useful for
that visual promise, but it must not make operational outcomes depend on a network service or
on whether two players launched at different times. Deterministic simulation remains the source
of truth for runway choice, ground stops, costs and flight timing. The Operations board labels
the visual sample as `Live` so it cannot be mistaken for the deterministic operating model.

## Provider, licence and release gate

The implementation uses `https://api.open-meteo.com/v1/forecast`, requesting current WMO code,
cloud cover, precipitation, visibility, temperature, humidity and ten-metre wind. Runtime
attribution reads `Weather · Open-Meteo (CC BY 4.0)` while a live sample is displayed.

Open-Meteo's free endpoint is documented for non-commercial use and the returned data is
licensed CC BY 4.0. That is acceptable for this private non-commercial prototype. A commercial
or public release must re-check the current terms and use an approved paid/customer endpoint,
self-hosted source, or disable live weather. This is a release gate, not an assumption that the
free endpoint can ship commercially.

## Acceptance criteria

- A valid current response maps WMO weather, cloud, rain, visibility and wind into an Airside
  presentation snapshot; malformed/offline responses fail softly.
- Live weather never changes operational wind, runway selection, simulation weather or save data.
- The real Adelaide field renders stars, sun and moon; stars fade through twilight and cloud.
- Rain strength, cloud coverage and fog visibility respond continuously rather than only to six
  discrete weather labels.
- Wet Adelaide aprons show pooled-water accents and moving aircraft can show tyre spray.
- Options exposes live-weather state and the on-screen credit appears only while live data is used.

## Migration impact

No save migration. One new `PlayerPrefs` option (`liveweather.v1`) defaults on. No third-party
art or binary asset is introduced.

