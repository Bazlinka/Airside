# Full map and shared controls — 6 October 2026

ADR 0237 corrects ADR 0235's scope after Bailey clarified the normal map, then
requested shared icons/buttons across the whole game with the map first.
Click simulated flights on the full map to inspect live status and available
cockpit, window-seat and exterior views. All operators appear by default, repeated
clicks cycle overlapping markers, and the South Australia shortcut centres the map.
Destination selection restores the booking pane without replacing its aircraft.
The airport mini map has simpler markers and a more solid frame.

37 original glyphs share a 32-unit grid, rounded strokes and safe margins. Editable
SVGs and `scripts/generate-ui-icons.py` reproduce the supersampled PNGs. Shared
buttons now use slate secondary fills and Coastal Blue primary actions. No external
art or new licence dependency. The full map uses an opaque background and fewer
unselected flight labels/routes.

Validation before integration with #531:
- Supplementary headless: 1,509 pass, no failures (`domain.log`).
- Full native Unity: 1,961 pass, one inherited ground-separation failure, two
  existing inconclusives, 1,964 total (`unity-results.xml`). The two failure episodes
  exactly match the main baseline in the aircraft-identity evidence: parked/taxi
  VH-QOK/VH-QOM and runway VH-SAR/VH-FDH. This change does not modify separation.
- Asset audit: 1,741 unique GUIDs, 386 matching art mirrors and 70 materials.
- Shared glyph sheet inspected (`icons.png`); old packaged map captured (`before.png`).

Final focused native checks: 52/52 pass (`focused-unity-results.xml`), including
full-map overlap cycling. After integrating #531, native checks including Adelaide
map coverage/suburb data: 58/58 pass (`integrated-unity-results.xml`).
Integration exposed #531's credits-page overflow (the final weather/live-traffic
heading was omitted). Move its two scenery sections to a dedicated page, preserving
their complete attribution. Final native suite including manual/terrain tests:
73/73 pass (`final-unity-results.xml`). Integrated headless and clean packaged
review follow below. Screenshots are presentation evidence; they do not establish
hardware mouse input, full-flight weather behaviour or performance acceptance.

First packaged review found the fixed SA zoom could crop the southern edge and a
redundant header type/destination label clipped. The preset now fits 129–141 E,
26–38.5 S with control margins at both window aspects; the full identity remains
in the route line. Dark operator markers are lightened for legibility. Final
native suite: 82/82 pass (`visual-final-unity-results.xml`), including two
region-fit viewport cases.

Local flights (including landing, go-around and taxi/departure) now use their
visible actor's real position including render-origin offset. They share the
full-map picker and inspector with distant flights. Final complete focused native
suite: 85/85 (`complete-unity-results.xml`). Integrated full headless after the
credits fix: 1,509 passed (`integrated-domain.log`); subsequent lens fit cases are
covered by native checks and final GitHub CI.

Clean final Mac player: `00e43f21`, `dirty=false` (`build.log`). Actual packaged
captures inspected: `player-map.png` (1440×900 SA inspector),
`player-map-small.png` (1024×768), `player-planning.png` (destination/booking pane)
and `player-airport.png` (shared navigation and airport mini map). The SA preset
includes CPD, CED, WYA, PLO, KGC and MGB; Rex ZL3497 shows inbound speed/altitude,
landing time/progress and all three available camera actions. Flight-card headings,
bottom map controls and booking controls fit without overlap in both inspected
window sizes. Real-feed decorations do not expose simulated camera/status actions.

Capture command: `bash scripts/capture-game.sh --out work/captures/map.png --delay
10 --minutes 1 -- -airsideReviewPanel map-flight -screen-width 1440 -screen-height
900 -screen-fullscreen 0`. Use `map` for booking review or omit the review argument
for the airport. Final PR: #532. Full native remains non-green because of the
recorded inherited ground-separation failure; static captures do not prove physical
mouse input or full-flight weather/performance acceptance.
