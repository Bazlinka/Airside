# Stable cloud coverage and visual weather variety

Date: 2026-10-09
Status: Accepted — Bailey's cloud continuity and weather variety instruction
Revises the cloud coverage choice in ADR 0225 and the 8 October altitude decision.

## Decision and reason

The moving coverage window was centred on the orbiting lens. Camera rotation/zoom
could move it kilometres and fade/recycle clouds above the same watched airport.
Anchor it to the camera focus, or the watched aircraft in cockpit. Keep a 24 km square
footprint, 2.5 km recycling fades and the sixteen-volume budget, giving developed
storm bodies more room. Cloud identities and wind drift persist across view motion.
Panning/flying to a genuinely different area still streams weather through soft edges;
this is bounded rendering, not a meteorological disappearance.

A ray-march proxy's back/exit face could lie beyond the far plane even while cloud
was in front. Clamp proxy vertex depth to the far plane; the fragment ray interval
and scene depth still determine actual visibility. Native shader validation is pending.

Morph existing volume models continuously between cumulus, wide stratiform banks,
thin streaked cirrus at roughly 6 km and the existing 1–10 km storm towers. Cloud
cover and storm development control morphology, never camera drag or zoom.
Authored Cloudy/Rain categories have deterministic visual variants: scattered/broken
cloud and drizzle/showers/continuous rain. Blend them across the same fifteen-minute
forecast transition plus the existing live/display easing. Describe live weather from
its continuous look rather than reducing every rain sample to the same display label.

## Scope and migration

Presentation and existing original project shader only. WeatherKind, operational
weather chain, storm holds, economy, seeded simulation RNG and saves remain unchanged.
No new third-party assets or acquisition cost. Existing atlas fallback and Weather
Layers setting remain available. The wider footprint trades local cumulus density
for coverage; the existing ceiling still provides broad overcast. Native appearance
and GPU cost remain unverified. Focused profile/coverage tests and syntax checks are
supplementary, not a Unity shader compile or rendered review.
