# Opening and Options clarity

Status: Accepted for implementation. Date: 2026-10-08. Task #601.

## Decision

Keep the approved dawn art, brand, live Adelaide clock and three-step airline
setup. Give the title card more room and clearer first-time/returning actions.
Shorten the camera entrance from 4.2 to 2.8 seconds and show its skip hint sooner.
An Opening animation preference disables both the entrance glide and slow title
image movement; existing players retain animation by default.

Replace the long Options list with General, Camera, Display and World tabs.
Every row has a setting name, explanation and value button. Keep all existing
options and expose the existing Cockpit motion preference. Changes remain
immediate and saved automatically. Explain restart-only suburbs in both states.
Back/Escape from title Options returns directly to the title; in-game Options
returns to the existing game menu.

## Scope and reason

The single long list mixed camera controls, live feeds and diagnostic graphics
wording. Grouping makes each choice easier to find and explains what it does.
The title return path previously exposed an in-game Resume menu before an airline
had started. Presentation-only: no simulation timing, economy or save changes.

## Migration and validation

No airline-save migration. One new PlayerPrefs key, `openinganimation.v1`, defaults
to enabled. Runtime actions are isolated in AirsidePrototype.Options; layout and
painting remain Unity-free. Native UI review uses the actual HudPainter and
labelled sample airline data; it does not claim a packaged gameplay journey.
