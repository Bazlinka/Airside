# Cloud lighting and depth

Date: 2026-10-09
Status: Accepted — Bailey authorised continued renderer implementation and merging.

## Decision and reason

Replace fixed height-based cloud highlights with bounded directional scattering.
The actual main light supplies direction, strength and a restrained hue contribution;
weather/day tint remains the base. Sky spherical harmonics supply bounded ambient
fill. Two fixed density probes toward the sun estimate local lobe and body absorption,
with stronger absorption and less ambient fill in storms. A bounded forward-scattering
phase brightens thin sun-facing edges without a permanent white storm cap at night.
This remains an artistic real-time approximation, not full multiple scattering.

Ease low-density eroded silhouettes. Integrate reveal/wrap opacity into extinction
so fading clouds thin out instead of becoming opaque bodies blended as translucent
cards. Use premultiplied radiance output and matching blend factors. This smooths
individual fades/compositing but does not solve order-dependent sorting of intersecting
transparent volumes or introduce mutual shadowing between separate volumes.

## Affected systems and migration

Existing original WeatherVolume shader only. No new asset, third-party source or cost.
Retain sixteen camera samples and sixteen cloud volumes. Occupied samples add one
sun-density evaluation (two shadow probes instead of one); empty samples skip lighting.
There is no nested light-marching loop. GPU cost remains unmeasured.

Cloud identities/placement, wind motion, morphology, operational weather timeline,
lightning timing/glow, observer altitude, origin shifts, scene-depth clipping, proxy
far-plane retention, atlas fallback, settings and saves remain unchanged. No migration.

## Verification limits

Quick scoped diff and source-contract inspection only, as instructed. No Unity launch,
packaged build, gameplay journey or broad suite. Native shader compilation, actual
appearance and GPU timings remain unverified; the implementation is merged as source.
