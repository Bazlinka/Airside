# ADR 0231 — Calm shared HUD and structured return briefing

Date: 6 October 2026. Status: implementation candidate. Requested by Bailey.

Replace the bright instrument accents and pill-heavy controls with slate surfaces,
warm-white type, muted sea-glass selection and soft gold primary actions. Shared
palette/theme and button rendering apply across title/setup, fleet, operations,
contracts, map, career, statistics and menu surfaces. Hover keeps target geometry
stable. Palette changes affect interface chrome only, not the airport art palette.

Aircraft views now use pure shared HUD painters: a compact identity/readout above
and a four-view dock below. Current-view underlines, disabled seat choices for
cargo aircraft, recenter/overview shortcuts and saved vibration control keep the
central sightline clear. Selected-aircraft view buttons use the same style.

Replace the return modal's variable-height sentence stack with a bounded briefing:
three factual metrics, a separately scrolling fleet report and a fixed return
button. Typed catch-up facts avoid parsing narrative strings and show negative net
funds correctly. Old narrative summaries remain for compatibility/tests.

The project-owned synthesized button tick gets a rounded attack, low-passed noise,
short damped body and lower playback gain. A 75 ms acknowledgement guard prevents
duplicate stacked ticks. No external sound asset or font is introduced.

No save-schema or camera mechanics changes. Current playtest sign-off predates
this new work and is not new native validation. Native Unity compile/EditMode is
required before merge; offline renderings are layout previews, not game captures.
