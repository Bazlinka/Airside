# Five-area bug sweep: buttons, 7 October 2026

## Task packet

- Player outcome: compact selected-aircraft cards keep Plan / Cancel / Follow and camera buttons distinct and clickable; the Flight Manual owns its click events while open.
- Scope: `AircraftInspector.cs`, `AirsidePrototype.Airline.cs`, `RefinedHudTests.cs`, and native HUD/minimap/settings assertions.
- Decisions: preserve ADR 0245 refined overview, optional radar, right-side inspector, and live simulation. Presentation never decides simulation results.
- Acceptance: at every supported inspector height (minimum 340 points), identity stays above the scrolling details; each footer button stays inside the footer, outside the scroll body, and does not overlap another button. Unavailable Window view remains disabled. Opening the manual prevents underlying controls from handling its clicks.
- Unchanged: aircraft view eligibility/routing, simulation commands, persistence/schema, real-time clock, radar option persistence, and graphics defaults.

## Fixes and triage

1. The inspector painter uses fixed text/button sizes, but its layout reduced the header/footer as a fraction of a short panel. At 340 points, the header was 102 points despite the phase line ending at 122, and the secondary button overlapped camera targets by 9 points. Reserve the existing 128-point header and 150-point footer; the scrolling body absorbs shorter window heights.
2. In a live session, the Flight Manual was drawn after enabled shell/workspace/inspector controls. IMGUI could let those controls consume its events before Close / Next / Previous processed them. Render the manual in its own early branch, matching the existing menu ownership approach. The airport keeps running and the full viewport still belongs to the HUD for camera input.
3. Six reported native failures retained the pre-refinement UI contract: three desktop readability cases demanded removed Operations tiles, the corner-placement test expected default-on radar right of the inspector, the minimap desktop test expected default-on radar, and the settings test demanded `MiniMap = true`. Updated these to the implemented refined interface, keeping assertions for opt-in radar geometry and readable inspector size. Minimap overlap checks now explicitly request radar so they exercise visible geometry.

## Evidence

- Baseline focused headless HUD suites: **20 passed**.
- Added six compact-panel cases (340 / 400 / 500 points, with and without Cancel). Before the fix: **2 failed, 4 passed**. After the fix they pass within the expanded focused run.
- Expanded headless command: `source /workspace/airside-tools/activate.sh && dotnet test scripts/dotnet-harness/Harness.csproj --filter 'FullyQualifiedName~HudShellTests|FullyQualifiedName~RefinedHudTests|FullyQualifiedName~HudWorkspacePainterTests|FullyQualifiedName~FleetWorkspaceTests|FullyQualifiedName~OperationsWorkspaceTests' --verbosity quiet`: **52 passed**, no failures.
- Source check confirms the live manual branch returns before field-tag and shell drawing, retains full-screen pointer capture, and leaves existing inspector camera action dispatch in place. This is source validation, not an IMGUI event test.
- `source /workspace/airside-tools/activate.sh && python3 scripts/update-harness.py --check`: **up to date**; no generated harness changes.

## Required native follow-up

Unity execution is prohibited for this session. The native settings/layout/minimap assertions have been updated but have not been executed. Headless checks do not validate Unity compilation, IMGUI events, native camera behavior, or visual output.

On the authorized Mac run the focused native `RefinedHudTests`, `PresentationLayoutTests`, `FieldMiniMapTests`, `AirsideSettingsTests`, then Play:

1. Resize until the inspector is 340–400 points tall; select a booked player aircraft and click Cancel, Follow, and each eligible camera target independently. Scroll details and confirm footer hitboxes remain fixed.
2. Open Operations and the Flight Manual over it; use Next / Previous / Close at locations intersecting the underlying workspace. Confirm no underlying row or command changes. Repeat with the selected-aircraft inspector and title screen.
3. Confirm default radar is hidden, toggle Airport map on, and verify it fits to the left of the inspector without hiding Career or any commands.
