# Maintenance journey and refined interface verification

7 October 2026; ADR 0245. Implemented on top of the unified multi-base Fleet work.

The retained PNGs are **offline shared-painter previews**, with a synthetic airport
backdrop and headless simulation fixtures. They are not Unity gameplay captures.
The 1440 and 1280 directories correspond to 1440×900 and 1280×720 logical layouts;
the established Pillow renderer supersamples them by 1.4. Font metrics differ from
IMGUI. Native scrollbars are not emulated: compact inspector previews show the
first visible details while the runtime body scrolls independently of fixed actions.

Verification:

- Integrated full headless regression: 1,826 passed on main `b8567209` plus this
  implementation; `full-tests.log`. `scripts/test-domain.sh` confirms the generated
  harness is current and compiles/tests the combined source.
- After that full build, added explicit tower protection for both strips on active
  maintenance legs, including the nominal assigned strip. All 48 relevant maintenance,
  migration, ERSA and dual-runway tests pass on the final source;
  `integrated-focused-tests.log`. v20 and v21 timed checks keep their original behavior.
- Final maintenance/inspector follow-up: 22 tests passed, including the blocker
  navigation button and visible workspace feedback lane; `focused-tests.log`.
- Final compact route planner/workspace geometry regression: 58 passed; `compact-tests.log`.
- After the full run, refined the 800×600 Career layout to keep the base action clear
  of livery swatches. All 29 relevant Career/inspector/workspace tests passed on the
  final source, including the new overlap check; `final-compact-career.log`.
- Integration checks across maintenance, Career, Route Map, Operations, Stats and
  shell: 95 passed before the final two maintenance edge cases and navigation check.
- Save/reload covers preparing, taxi, positioning, repairing and returning; v20/v21
  migration does not replay payment or startup. Three-second ticks and one large
  catch-up finish identically. Both Saab and 737 reverse straight before turning,
  and return to another compatible stand when the origin is occupied. A completely
  occupied apron leaves the aircraft safely waiting without another charge.
- Shared exporter builds 21 surfaces at both desktop sizes; retained previews cover
  overview, taxi, repair, Fleet, Contracts, Operations and Route Map. `control-bounds.json`
  confirms 125/118 visible buttons/hotspots stay within their viewports.
- Unity asset audit: 1,793 unique GUIDs, 386 mirrored runtime art files and 70
  committed character materials passed. `asset-audit.log`.
- Roslyn syntax parsing: six changed Unity-facing files, zero syntax errors.
  This is not semantic Unity compilation. `native-syntax.log`.

Reproduce using the project .NET 8 environment:

```
scripts/test-domain.sh
dotnet test scripts/dotnet-harness --filter 'FullyQualifiedName~MaintenanceJourneyTests|FullyQualifiedName~RefinedHudTests'
dotnet run --project scripts/hud-mockup/HudMockup.csproj -- work/hud-refined/draw-lists.json 1440 900
python3 scripts/render-hud-mockups.py work/hud-refined/draw-lists.json work/hud-refined
python3 scripts/audit-unity-assets.py
```

Native verification remains outstanding under Bailey's recorded no-Unity execution
restriction. After it is lifted, compile/playtest before merging:

1. Start a check with Saab and 737 after unloading. Observe closed doors, no passenger,
   baggage or cargo flow, sequential engine spool and ordinary pushback/tug disconnect.
2. Follow through busy taxi traffic, apron shutdown, engine-off tug entry, stopped
   fitted bay pose, actual repair, straight reverse exit before the turn, startup,
   return to a reserved stand and parking. Inspect nose/main-gear/tug alignment and
   whole-aircraft swept doorway/wall clearance from overview and follow cameras.
3. Reload each phase and compare pose, funds, wear and completion. Occupy the origin
   and all compatible stands in turn; confirm named waiting reasons and no jumps.
4. At 1440×900 and 1280×720, scroll inspector details, use its fixed flight/follow/camera
   commands, follow a blocking-aircraft link and go back. Check Fleet multi-base flows,
   optional radar, guide objective, workspace refusal messages and menu controls.
5. Check day/dusk/night readability, input interception, rendered text and performance.

Limitations: one active aircraft reserves a whole shed until it has returned; no
new outstation maintenance animation; rotorcraft/no-fitting-shed checks remain timed.
The offline checks do not establish native collision clearance or performance.
