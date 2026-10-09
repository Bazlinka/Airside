# Directional twilight sky

Date: 2026-10-09. Owner: Codex. Task: #758.

Decision: replace the uniform camera backdrop with one original analytic URP skybox. A cool zenith blends into pale horizon haze; amber/rose twilight concentrates toward Adelaide's actual sun azimuth, and blue hour fades smoothly into the existing night/stars. Keep mild neutral horizon fog instead of an orange wash. Follow the approved art direction: warm long light, cool shadows, readable operations.

The existing celestial elevation/clock and observer weather determine colours continuously. Cloud cover/visibility suppress clear-sky colour; fog breakout and above-deck clearing retain their existing altitude behaviour. Lightning shares the existing flash envelope. No texture, external source, network request, new simulation state or per-frame mesh is added. The shader is included explicitly for packaged builds; a missing/unsupported shader retains the solid-colour backdrop. Runtime material is released on teardown.

Affected: Presentation sky/day cycle, atmosphere haze, shader inclusion. Existing sun/moon geometry, star fading, terrain, weather geometry, night-brightness settings and operational rules remain. Save migration: none.

This extends ADR 0143's single atmosphere with spatial sky variation; fog remains its weather-aware horizon colour. This is an artistic approximation, not physical atmospheric scattering. GPU timings, all flight altitudes and every seasonal date are not established by the focused review.
