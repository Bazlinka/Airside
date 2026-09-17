## Where to resume — session handoff

- **2026-09-17 Cursor — field camera zoom/nav mechanics (ADR 0054).**
  - **Branch:** `cursor/camera-zoom-nav-mechanics-b9dd` (open as a PR; `main` is
    protected).
  - **Player-visible outcome:** free-camera scroll now zooms toward the ground
    under the pointer (a stand on the edge of frame comes closer instead of the
    orbit centre alone shrinking). Left/middle drag grabs the ground under the
    cursor the same way the destinations map does. WASD drops follow then pans,
    matching drag. Soft pan limit keeps you within ~3.8 km of the overview so
    you cannot lose the airfield. Follow-mode scroll bias is unchanged.
  - **Scope/invariants:** presentation-only (`AirsideCameraFeel`,
    `AirsideCameraController`). Simulation, saves, follow framing curves and
    zoom/pan *rates* from #278 are unchanged.
  - **Evidence:** `scripts/test-domain.sh` **316/316** (12 new camera-feel
    locks). No Unity editor in this cloud session — live feel at overview/apron
    still needs a Mac Play / packaged build check.
  - **NEXT:** Mac playtest — scroll toward a stand off-centre, drag-pan near the
    horizon, WASD while following, confirm soft pan limit at the edges. Merge
    once that feels right.

- **2026-09-17 Claude — second bug-hunting pass (a dedicated research subagent plus manual
  review), 5 more fixes: 2 latent Domain lookup bugs, 1 wrong dead constant, 2 real
  Presentation perf/behaviour bugs.**
  - **`AircraftType.TryFromId` and `AircraftCatalogue.TryFor` never returned on a match** —
    both kept scanning the whole catalogue and silently kept the *last* match instead of the
    first. Harmless today because every catalogue id is unique, but a future copy-paste
    duplicate in `AircraftCatalogue.cs` would have silently resolved to the wrong type with
    no error — exactly the kind of bug that stays invisible until it randomly isn't. Both now
    return immediately on the first match. New tests lock in first-match semantics and a
    catalogue-uniqueness invariant so a future duplicate id fails loudly instead of silently
    picking the wrong aircraft.
  - **`AirportLocation`'s UTC offset was wrong and couldn't have been right** — every South
    Australian preset (Adelaide, Kingscote, Port Lincoln, Coober Pedy) was `9` (a flat UTC+9);
    South Australia is ACST, UTC+9:30, and the field was an `int`, so it structurally could
    not hold the correct value. Confirmed unread anywhere outside its own declaration (the
    real in-game clock goes through `AirlineClock`/`TimeZoneInfo` instead) — inert today, but
    a wrong, unfixable-as-typed constant left for "a later build" (its own doc comment) to
    trip over. Changed the field to `float` and the value to `9.5f`.
  - **The gate-servicing GSE team could get stuck servicing one aircraft forever.**
    `UpdateGateServicing` picked the *first* terminal-gate aircraft found in fleet order and
    animated the fuel truck/baggage cart/bus around it — with more than one jet on the gates
    at once (very plausible once VOZ/ANZ/QF/SQ traffic is all running), every aircraft after
    the first in fleet order got no ground service vehicles, ever, for as long as that first
    one kept using the gates. Fixed to rotate the one GSE team between every currently-parked
    terminal aircraft a full 120 s service cycle at a time, instead of camping on whichever
    one happened to be first.
  - **The always-visible Fleet sidebar and the Hangar panel both scanned the whole fleet
    twice per frame, per airline.** `FleetOf(airline)` itself scans the entire fleet; both
    panels called it once per airline to total up the panel's content height and again to
    actually draw the rows — for every AI airline, every single `OnGUI` invocation (which
    IMGUI calls several times per real frame). Not a correctness bug, just wasted CPU that
    scales with fleet size and airline count; fixed both panels to group the fleet by airline
    in one pass and reuse that grouping for both the height total and the draw loop.
  - **Evidence:** the two catalogue lookup fixes are Domain, fully headless-tested (new tests
    added, full suite green). The UTC-offset and gate-servicing/fleet-panel fixes are
    Presentation-only — reviewed by inspection, no Unity editor available in this session.
    `scripts/test-domain.sh`: **304/304**, up from 302 (itself up from 299 after bringing
    `RouteMap.cs`'s great-circle/zoom math into the harness earlier this pass — it had no
    UnityEngine dependency but had never been added, same gap as `FlightPlanner.cs` last
    time).
  - **NEXT:** the research subagent's report also flagged `DestinationCatalogue`/route-
    reachability, `AirlineCareerState` settlement idempotency, `AirlineSave` migrations,
    `GroundMotion` taxi pose math and `RunwayWeather` as checked closely and found correct —
    no need to re-audit those next time.

- **2026-09-17 Claude — 10 fixes across performance, realism, game logic and taxi behaviour,
  found by re-reading the codebase from scratch plus a dedicated research pass.**
  - **#1, critical: the game did not compile.** `AirsidePrototype.Airline.cs`'s
    `DrawWorkspaceNav` destructured `WorkspaceTabs[i]` as a 3-element tuple
    (`var (workspace, label, _) = ...`) but the array itself had been trimmed to 2 elements
    in an earlier commit this session (`863687b`, the hotkey-field removal) — the call site
    was never updated to match. This has been sitting on `main` since PR #293 merged. Fixed
    to `var (workspace, label) = WorkspaceTabs[i];`. No headless test catches a Presentation
    compile error (the harness only compiles a UnityEngine-free allowlist), so this kind of
    break is invisible to `scripts/test-domain.sh` — a real Unity compile (or at minimum a
    grep sweep of every tuple destructure after touching a tuple's shape) is the only thing
    that would have caught it, and neither ran between the two commits.
  - **#2: sister aircraft went around, or didn't, in lockstep forever.** `ShouldGoAround`'s
    "random" seed used `aircraft.Registration.Length * 7` — every registration in the fleet
    is the same "VH-XXX" format, so every aircraft's length is identical and contributed
    nothing. Two aircraft with the same completed-trip count in the same hour (e.g. any two
    of Rex's three fresh Saab 340s) got the exact same go-around decision. Fixed to hash the
    whole registration string. New test proves VH-ZRC/ZRD/ZRE (same length, real fleet regos)
    now get distinct seeds; mutation-tested.
  - **#3: three of seven jets planned cruise altitude like a turboprop.** `EnrouteProfile.
    PlannedCruiseFeet`'s jet-vs-turboprop climb formula only checked `type?.Id == "B38M"` —
    the A321neo, A350-900 and 787-10 fell through to the turboprop formula (6,000 ft base,
    25 ft/km) instead of the jet one (8,000 ft base, 38 ft/km), planning a widebody
    international service at ~22,000 ft on a medium leg instead of ~33,000 ft — visible
    directly in the HUD's "FLxxx" cruise readout. Fixed to check all four authored jet IDs.
    New test asserts every jet type plans well above a turboprop on the same leg;
    mutation-tested.
  - **#4/#5: the flight planner's departure preview used an ATR 42's timing for every
    aircraft.** `FlightPlanner.Estimate` (the "when will it be airborne" preview) and
    `ExpectedBackAt` (the "when is it back" HUD line) both called the untyped
    `AirlineOperations.TaxiOutSecondsFrom`/`TakeoffRunwaySeconds` overloads, which silently
    default to an ATR 42 — so a 787's departure preview showed exactly the same taxi/takeoff
    time as a turboprop's. Fixed to thread the aircraft's actual type through to the
    type-aware overloads that already existed and were already used correctly everywhere
    else in the render path. `FlightPlanner.cs` was UnityEngine-free but not in the headless
    harness's compile list — added it (plus un-excluded `FlightPlannerTests.cs`), so this
    file now gets real headless coverage going forward. Two new tests pin the exact expected
    value against the type-aware formula directly (not just "differs from ATR") so a
    regression back to the untyped overloads can't slip past a stand-class heuristic that
    partly masks it; both mutation-tested.
  - **#6: a bay departure and a gate departure delayed each other for no physical reason.**
    `NextTaxiReleaseAt` scanned the *entire* fleet for any aircraft currently taxiing out and
    blocked the next pushback fleet-wide for 60 s — even though the regional bays and
    terminal gates are on physically separate aprons with entirely separate taxi routes
    (`AirportTaxiNetwork`) that never share pavement. A Rex Saab pushing back from a bay
    could hold up an unrelated Virgin 737 push from the terminal gates. Fixed to scope the
    release gate per apron (bay vs. gate), so only pushbacks sharing actual pavement wait on
    each other. `NextEventAt`'s skip-to-next-event logic updated to match. New test drives
    one bay departure and one gate departure at the same instant and asserts neither is held
    up by the other; mutation-tested.
  - **#7: wake-turbulence separation was a second, disconnected classification.**
    `WakeSeparationSeconds` hardcoded a switch on literal type-ID strings ("A359"/"B78X" →
    180s, "B38M"/"A21N" → 120s), duplicating a classification the catalogue already encodes
    properly via `AircraftCatalogue.WingspanMetres`. Every current type still classifies
    identically, but any future type added to the catalogue without a matching case here
    would have silently fallen back to the smallest 90s separation regardless of how large
    it actually is. Fixed to derive the band from the aircraft's own wingspan. New test
    confirms the medium-jet band (737-8, A321neo) and the turboprop default (Saab 340,
    Dash 8) still classify correctly under the new derivation.
  - **#8: the "quickest stand" ranking always used an ATR 42's taxi time.**
    `StandNames.QuickestToTaxiIn` called the untyped `TaxiInSecondsTo` overload. Currently
    unreachable in the live game (nothing calls it — `SuggestStand` has its own separate
    ranking), but it's the same untyped-overload foot-gun as #4/#5/#7's root cause and would
    misrank stands the moment anything does call it for a non-ATR aircraft. Fixed to accept
    an optional `AircraftType` and use the type-aware overload when given one.
  - **#9/#10: two real per-frame/per-click allocation and O(n²) spots in the fleet render
    path.** `TrySelectAircraftAtScreen` allocated a fresh `List<AircraftPickHit>` on every
    field click — every other per-frame collection in the same file already reuses a field
    for exactly this reason, this one was missed; now reuses `_pickCandidates`. The livery
    slot assignment in `SyncCommercialAircraftViews` rescanned the *entire* current
    slot-assignment dictionary once per candidate slot per newly-appearing aircraft
    (O(n²) per batch of new arrivals); now tracks used slots in a reused `HashSet<int>` for
    an O(1) "is this slot free" check. Both unnoticeable at the current fleet size (~13
    aircraft) but exactly the kind of per-frame/per-batch full-fleet work that stops scaling.
  - **Evidence:** #2, #3, #4, #5, #6, #7 are Domain/Simulation or now-headless-testable
    Presentation logic — all mutation-tested (broke the fix, confirmed the new test failed,
    restored it, confirmed the full suite passes). `scripts/test-domain.sh` **299/299**,
    up from 285 (14 new tests, plus `FlightPlannerTests.cs` — 10 tests — coming online for
    the first time by adding `FlightPlanner.cs` to the harness). #1, #8, #9, #10 are
    Presentation-only with no Unity editor available in this session — reviewed by inspection
    and, for #1, by manually re-deriving the C# tuple-arity rule that made it a compile
    error, not run through an actual compiler.
  - **On the "completely new interface for viewing flights" ask:** before building anything
    new, I read what already exists — it's substantial. The **Operations** tab already has a
    full arrivals/departures board (`DrawFlightsPanel`/`FlightBoard.cs`) with scheduled/
    estimated times, gate, route, live status, a progress bar per flight, and ownership
    dimming so AI traffic reads visually distinct from the player's own aircraft. The **Map**
    tab already draws live great-circle routes for every in-progress flight, coloured by each
    airline's own livery, with aircraft icons oriented to their heading and click-to-track.
    Building a second, parallel system risked duplicating this rather than improving it, and
    I have no Unity editor to visually verify a new screen against the existing ones. **#1
    above was actively blocking all of it** — with the compile break, none of these panels,
    nor the workspace nav strip that switches between them, would have built at all. Fixing
    that arguably does more for "being able to see flights, the map and what's mine" than a
    new screen would have. **NEXT:** if the existing Operations/Map tabs, now that they'll
    actually compile, still don't cover what "who has gone where / what's mine" was asking
    for once seen rendered, the more precise ask (a combined view? a history log of
    completed trips? something else?) would sharpen what to build next.

- **2026-09-17 Codex — live modal-boundary playtest and fix.**
  - **Branch:** `codex/modal-hud-isolation` (commit and push this review branch; `main` is
    protected and requires a pull request).
  - **Player-visible outcome:** the airline setup and away-summary screens now stand alone;
    the live speed/altitude readout and Follow / Overview controls are no longer visible or
    clickable beneath them. Pressing Escape still exposes Resume/Quit, but the menu no longer
    stacks its labels and buttons over the airline setup or other airline HUD panels.
  - **Reproduction:** the existing packaged Mac build showed both defects immediately: the
    start form had a changing speed readout and flight controls underneath it, and Escape drew
    the Menu panel directly over the form. The current `main` source had the same unconditional
    draw paths, so this was not treated as an old-build-only artefact.
  - **Scope/invariants:** presentation-only changes in `AirsidePrototype.cs` and
    `AirsidePrototype.Airline.cs`. Simulation, live clock, saves, fleets, routes and camera
    movement are unchanged. The menu continues to record full-screen pointer capture, so the
    earlier no-camera-movement-behind-menu invariant remains intact.
  - **Evidence:** `scripts/test-domain.sh` passes 285/285 and `git diff --check` is clean.
    Unity 6000.3.23f1 imported the project, but its headless licensing client repeatedly lost
    its IPC channel, so EditMode did not produce a result and a fresh post-fix build could not
    be claimed in this session.
  - **NEXT:** once the Unity licensing client is healthy, run `scripts/test-unity.sh`, make a
    fresh Mac build, and visually confirm setup, away-summary and Escape-menu states at
    1280x720 and 1440x900 before merging further presentation work.

- **2026-09-17 Claude — the speed/altitude readout showed no indication of which aircraft
  it was for ("this as well - makes no sense what it does", per Bailey's screenshot of
  "120 kt · 310 ft ▲" with no other context).**
  - **What was actually wrong:** `DrawSpeedReadout`/`TryReadoutFlight` always show a reading —
    the followed aircraft if one is followed, else the first visible aircraft on the field —
    but the box never said whose it was. In the old single-ATR42 demo circuit that ambiguity
    didn't matter (there was only ever one aircraft). Now that an airline is running, the
    field has the player's fleet *and* every AI carrier's traffic, and "player aircraft come
    first" (`RefreshFleetFlights`) only guarantees the default reading is the player's own
    when the player actually has a visible aircraft on the field — if their one plane is away
    on a leg, the same unlabelled box would show an AI competitor's speed with nothing to
    say so. Bailey's screenshot is exactly that failure mode: a number with no explanation
    is not confusing about *what it does* (it's a speed/altitude readout), it's confusing
    about *whose* it is.
  - **Fix:** `DrawSpeedReadout` now resolves the reading's aircraft and labels the box with
    its flight number (falling back to registration), reusing the same `FlightNumber` helper
    already used on the map and flight board. `ReadoutText` takes the label as an optional
    parameter so the demo-circuit call path (no label) is unaffected.
  - **Evidence:** Presentation-only IMGUI change, no Unity editor in this session — reviewed
    by inspection, not rendered. `scripts/test-domain.sh` unaffected (285/285, this file isn't
    Unity-free so it was never in that harness's scope).
  - **NEXT:** a fresh screenshot would confirm the label reads cleanly at the readout's small
    size and doesn't crowd the "kt / ft" figures next to it.

- **2026-09-17 Claude — found and fixed the likely cause of the landing "stutter" Bailey
  reported, plus a real speed-accuracy bug in the touchdown wheel smoke.**
  - **The stutter:** `RefreshFleetFollowTargets` (`AirsidePrototype.FleetVisuals.cs`) excludes
    an inbound aircraft still more than 1800 m from the runway threshold from the
    follow/cycling candidate list — a filter borrowed from `AircraftPickRouting`'s
    click-to-select rules (a distant aircraft is too small to be a sensible click target).
    But that same filter was also applied to the list `SetFollowTargets` uses to decide
    whether your *currently followed* aircraft is still valid. The moment your own inbound
    aircraft's visual phase switched to `Approach` — which happens well out over the field,
    long before it is anywhere near the runway — it dropped out of that list, `IndexOfSame`
    came back -1, and `AirsideCameraController.ReleaseFollow()` fired: the camera stopped
    tracking it and just sat still while the aircraft kept flying on. That is exactly what
    "stutter... before approaching runway" would look like: motion suddenly interrupted, well
    before touchdown, well before the runway. Fixed by never excluding the aircraft the camera
    is already following, whatever its distance from the threshold — the far-approach filter
    still keeps distant aircraft out of new-follow/cycling candidates, it just no longer yanks
    an active follow out from under you.
  - **Speed-accuracy audit (the second ask):** re-verified every aircraft type's derived
    circuit figures (approach/landing/takeoff seconds, rotate/touchdown/flare progress
    fractions) are positive, monotonic and sane across all 7 authored types — they are; the
    underlying `AircraftPerformance.cs` table and `CircuitProfile`/`AircraftPerformanceProfile`
    derivation are correct. The bug wasn't in the numbers, it was in *applying* them: the
    touchdown wheel-smoke functions (`EmitTouchdownWheelSmoke`, `UpdateRollingWheelSmoke`)
    called the type-less `AirsideFlightPath.GroundSpeedMetresPerSecond` overload, which
    silently defaults to the ATR 42's speed curve for every aircraft — so a landing Boeing
    737, A321, A350 or 787 (each with a materially different touchdown speed) had its
    wheel-smoke intensity computed off the wrong aircraft's numbers. Fixed both call sites to
    resolve and pass the aircraft's actual type, matching every other render-path call site
    (position, pitch, gear roll, airspeed readout), which were already correct.
  - **Evidence:** both fixes are Presentation-only (IMGUI/Unity scene code), so
    `scripts/test-domain.sh` can't compile or run them — still 285/285, unaffected. The
    follow-drop mechanism was traced by reading the exact call chain
    (`RefreshFleetFollowTargets` → `AirsideCameraController.SetFollowTargets` →
    `AircraftPickRouting.IndexOfSame` → `ReleaseFollow`), not observed directly (no Unity
    editor in this session) — a fresh play session following your own aircraft in from its
    Approach phase is the only way to confirm the stutter is actually gone. The speed table
    itself was checked with a standalone script reproducing the exact `CircuitProfile`/
    `AircraftPerformanceProfile` formulas for all 7 types.
  - **NEXT:** confirm in a live build that following your own aircraft in no longer drops out
    around the Approach phase. If a stutter is still visible even after this fix, the next
    place to look is the whole-second `FleetState.Landing` clock (`FleetVisual.For`) versus
    the fractional presentation clock (`_preciseTime`) that drives `VisualPhaseProgress` — I
    traced through this by hand and found it consistent, but it's the next candidate if the
    camera-follow fix doesn't fully explain what was seen.

- **2026-09-17 Claude — bug-hunting pass across the codebase ("massive bug fix"), 6 files, all
  verified where verification is possible.**
  - **Away-summary can misreport a save migration as something that happened while you were
    away.** `AwaySummary.Build` compared the *fresh* v6-migrated career (starts at 100%
    reliability, 0 funds) against the *raw* pre-6 save fields, which JsonUtility leaves at 0
    (no such fields existed before v6). Loading an old save and stepping forward would then
    read "Reliability rose 100 points to 100%." — a migration artifact, not something that
    happened in the time away. Fixed in `Simulation/AwayCatchUp.cs`: the "before" values used
    for the delta are now the fresh Provisional starting values when the save predates v6,
    not the raw zeroed fields. New regression test
    `Summary_MigratingAPre6Save_DoesNotMisreportTheFreshCareerAsChangedWhileAway` added to
    `AwayCatchUpTests.cs`, mutation-tested (reverted the fix, confirmed the new test fails;
    restored it, confirmed 285/285 pass).
  - **Duplicate FNV-1a hash implementation.** `FlightNumber.For` hand-rolled its own hash
    function instead of reusing `AirsidePrototype.StableNameHash` (the canonical shared
    implementation already used elsewhere for deterministic display values). Two
    implementations of the same algorithm drift silently if one is ever tuned. Made
    `StableNameHash` `internal` (was `private`) and had `FlightNumber.For` call it directly,
    deleting the duplicate.
  - **Workspace nav button dead-clicked the Map tab.** `DrawWorkspaceNav`'s button handler
    called a plain `SetWorkspace(HudWorkspace.Map)` for the Map tab, which — unlike
    `TogglePlanner`/`OpenPlanner` (what Tab and field-tag selection already use to enter the
    map) — doesn't choose a planning aircraft or reset the lens. First click on the Map tab
    before selecting any aircraft opened an empty "No aircraft to plan" planner instead of the
    map. Fixed to route through `TogglePlanner()` for the Map tab specifically.
  - **Style-cache collision in the intro screen.** `AirsidePrototype.Intro.cs` had two
    different visual roles (the title, and a fallback mark) sharing one `??=`-cached
    `_introTitleStyle` field — whichever one rendered first "won" and silently applied its
    style to the other. Split into `_introTitleStyle` and a new `_introFallbackMarkStyle`.
  - **Unused hotkey field and a stale comment.** `WorkspaceTabs`' tuple carried a hotkey string
    field that was never read after the hotkey suffixes were dropped from tab labels (see the
    nav-strip fix above) — removed. A doc comment above the destination marker still described
    the old ring-and-crossed-bars glyph after it was replaced with a plain dot — corrected.
  - **Evidence:** found via two passes of the `code-review` skill (the first was interrupted by
    a session usage limit partway through applying fixes; re-run to completion) plus manual
    read-through of the affected files. The `AwayCatchUp.cs` fix is the only one with behavior
    provable by a real test — it's Simulation, so `scripts/test-domain.sh` covers it directly
    (285/285 passing, up from 284). The `FlightNumber`/nav-button/intro-style/comment fixes are
    all Presentation-layer IMGUI code with no Unity editor available in this session — reviewed
    by inspection and by tracing the exact call sites, not rendered or clicked.
  - **On "fix planes":** re-ran all 8 aircraft-geometry generator/test scripts
    (`scripts/test-air-*.py`) — all still pass, no geometry bugs found. If "fix planes" meant
    something else (aircraft behavior, visuals only visible in a build), that needs a more
    specific pointer — nothing else aircraft-related turned up in this pass.
  - **NEXT:** a fresh screenshot would confirm the Map-tab click fix and let button theming
    (previous entry) actually be judged. No further bug-hunting queued unless asked.

- **2026-09-16 Claude — themed every button in the HUD; the status line and guide card now
  match. The single biggest lever found for "it still looks ugly."**
  - **What I found, re-reading Bailey's screenshots:** they don't just show the nav-strip
    overflow bug (fixed separately) — the whole HUD's buttons look like generic Unity UI
    against the navy/charcoal panels. Root cause: `AirsideTheme.cs` had `PanelStyle` (themed
    box backgrounds) and `TextStyle` (themed text colour) but **no themed button background
    at all** — every button in the entire game used `GUI.skin.button`'s stock grey bevel with
    only its text colour ever touched. Checked how widespread this was: only **two** places in
    the whole codebase ever construct a button GUIStyle (`AirsidePrototype.cs`'s
    `_hudButtonStyle`, `AirsidePrototype.Airline.cs`'s `_hudSmallButton`), and every other
    button style in the game (`Styled(smallButton, ...)` variants, the nav tabs, etc.) derives
    from one of those two by copying it — so fixing those two construction sites themes
    essentially every clickable control in the HUD in one small, targeted change.
  - **How:** new `AirsideTheme.ButtonStyle` sets normal/hover/active backgrounds to solid
    fills already in the approved palette — Tarmac at rest, Coastal Blue on hover/press (the
    palette's own documented "selection/accent" colour, so it reads consistently with the row-
    selection highlight colour used elsewhere). No new hues introduced; no art asset added.
  - **Also fixed:** the persistent status line (from an earlier slice) drew as bare floating
    text with no panel behind it, inconsistent with the guide card it replaces, which does
    have one — gave it the same quiet panel chrome.
  - **Evidence:** reviewed by inspection only — `AirsideTheme.cs` isn't Unity-free, so it
    can't be checked by the headless `scripts/test-domain.sh` harness either (still 284/284,
    unaffected). **This is a real, structural fix backed by a from-scratch audit of every
    button-style construction site in the codebase, not a guess** — but it has not been seen
    rendered. A fresh screenshot is the only way to confirm it actually reads better.
  - **NEXT:** waiting on a screenshot. If buttons still look wrong, the likely next thing to
    check is whether Unity's runtime IMGUI actually honours `.hover`/`.active` states outside
    the Editor the way I'm assuming — if not, the `.normal` background alone still fixes the
    resting-state mismatch, which was the main complaint.

- **2026-09-16 Claude — fixed a real HUD bug from Bailey's first screenshots of the nav-shell
  work: the workspace nav strip's text overflowed off the left edge of the window.**
  - **What the screenshots showed:** the four nav tabs read "erations / Map (Tab) / Fleet (H) /
    Contracts" — "Operations" was cut down to "erations", missing its first two letters,
    because the button text overflowed past the window's own left edge. Also flagged (broader,
    not yet resolved): the overall HUD still reads as sparse/dark and needs more style work.
  - **Root cause:** `AirlineHudLayout.NavStrip` was locked to the clock column's width
    (≤300 px) — four tabs at ~75 px each, nowhere near enough for labels like
    "Operations (T)". Unity's `GUI.Button` centres and does not clip overflowing text to its
    own rect, so the extra width spilled out both sides — left far enough to go off-window.
  - **Fix:** the nav strip is no longer tied to the clock's width. It now uses the same
    "room beside/below the fleet panel" the destinations map already gets, capped at
    `NavStripMaxWidth` (420 px) so it doesn't stretch absurdly on very wide windows — computing
    that required reordering `AirlineHudLayout.Create` so the fleet panel's horizontal
    placement (which only ever depended on width, not on anything below the clock) is worked
    out before the nav strip is sized, not after. Tab labels also dropped their `(T)`/`(Tab)`/
    `(H)` hotkey suffixes — they were making an already-tight fit worse, and Controls Help
    (F1) already lists every hotkey.
  - **Also fixed, same evidence:** the destination-map marker glyph (ring + crossed runway
    bars, added in the same earlier slice) risked reading as a target/"no entry" symbol at the
    14-30 px it actually draws at. Replaced with a clean antialiased dot — same texture-baking
    approach, much safer at small sizes, no longer claims to be a literal airport glyph.
  - **Evidence:** the by-hand math for the widened nav strip was checked against every
    existing overlap/fits-on-screen invariant in `PresentationLayoutTests.cs` (all still hold —
    reasoned through explicitly, not just re-run, since this file needs Unity and none is
    available here) and a **new** `AirlineHudLayout_NavStripFitsFourReadableTabs` test locks in
    a minimum 65 px/tab at all 6 existing resolutions specifically so this exact regression
    can't come back silently. `scripts/test-domain.sh` still 284/284 (unaffected — these are
    Presentation-only files the headless harness doesn't compile).
  - **NEXT:** genuinely need a fresh screenshot to confirm the nav strip actually reads right
    now — this was diagnosed from a screenshot, not from running the game. The broader "still
    looks ugly, style needs work" feedback is real and larger than this one bug; wants either
    more screenshots pointing at specific panels, or a proper Mac visual pass, before guessing
    further at what else to change.

- **2026-09-16 Claude — reliability cost for cancelling a contract flight (closes the
  `CancelDeparture` TODO from the Task 2/3 PR, #291).**
  - **Player-visible:** cancelling a scheduled departure that would have counted towards the
    active career contract now costs reliability (`REG-KGC-INTRO`: 3 points), with a toast
    saying so ("...reliability down 3."). Cancelling a flight that has nothing to do with the
    active contract (wrong route, or no contract at all) still costs nothing — this was never
    a blanket penalty on cancelling.
  - **How:** `RouteContractDefinition` gained `ReliabilityLossOnCancel` (new optional
    constructor parameter, default 0, so the earlier 9-arg call in `AirlineCareerTests` still
    compiles unchanged). `AirlineCareerState.PenalizeCancellation` is a no-op unless the
    cancelled flight's route/type matches the contract currently active — mirrors
    `TrySettleFlight`'s own eligibility check. Wired into `AirlineOperations.CancelDeparture`
    right where the TODO comment was.
  - **Evidence:** genuinely verified — `scripts/test-domain.sh`, **284/284** (2 new tests:
    cancelling a contract flight costs reliability; cancelling an unrelated one costs
    nothing). The one-line toast change in `CancelPlannedFlight`
    (`AirsidePrototype.Airline.cs`) is Presentation and reviewed by inspection only, same
    standing caveat as everything else this session — no Unity editor available.
  - **NEXT:** pushed to `claude/great-heisenberg-qitw2l`, PR not yet opened/merged — small
    enough to fold into the same Mac review pass as #291 rather than needing its own.

- **2026-09-16 Codex — Airside identity and launch sequence.**
  - **Player-visible:** the Dock/Finder icon and opening now use a new original approach-runway
    mark. The former seven-second distant glide is a 4.8-second, skippable runway-signal
    hand-off: brand mark first, regional-operations context second, then an unobstructed playable
    overview.
  - **How:** `BRD-002 v02` is the opaque macOS icon; `BRD-003` is its transparent in-game mark.
    Their editable SVG sources live under `docs/art/source/`. The opening does not alter airport,
    flight, save, clock or camera-control behaviour once it ends.
  - **Evidence:** Unity 6000.3.23f1 EditMode passed 493/493; a fresh macOS build succeeded.

- **2026-09-16 Claude — Task 3: wire the career (Task 2) into the HUD (ADR 0053).**
  - **Player-visible:** the Contracts workspace is a real panel now, not a placeholder — it
    shows funds/reliability/tier, `REG-KGC-INTRO`'s terms (route, aircraft, rotations,
    payment, reliability gain, tier requirement) with an **Accept contract** button, and once
    accepted, a progress card (rotations complete / required, a progress bar, the terms again)
    in place of the offer. Completing an eligible rotation now shows a toast
    ("VH-PAX earned $400 on REG-KGC-INTRO (2 rotations so far)."), with a distinct one on the
    contract's last rotation ("...— REG-KGC-INTRO complete!"). The clock panel gained a third
    line — funds and reliability, always visible, the persistent status strip ADR 0053 asked
    for. The objective line (from the earlier status-line slice) now shows contract progress
    once one is active, instead of falling straight to the generic fleet-count line. The away
    summary reports funds/reliability change while you were gone, when there was one.
  - **How:** `AirlineOperations` gained `RecentSettlements`/`TotalSettlements`, mirroring the
    existing `RecentEvents`/`TotalEvents` pattern exactly, so Presentation can detect new
    settlements the same way it already detects new state-change events
    (`AnnounceNewSettlements`, next to `AnnounceNewEvents`). `OperationsSummary.Line` takes an
    optional `AirlineCareerState` and prefers contract progress over the fleet-count fallback
    when nothing more urgent (an aircraft needing attention) is going on. `AwaySummary.Build`
    diffs `before.CareerFunds`/`CareerReliability` (the v6 save fields from Task 2) against
    `after.CareerState`. Nothing here touches Domain/Simulation's actual settlement logic —
    every new behaviour is read-only against what Task 2 built, or a new command
    (`AcceptContract`) that already existed on `AirlineOperations`.
  - **Evidence:** the Domain/Simulation side of this (the away-summary diff logic) is real,
    verified evidence — `scripts/test-domain.sh` still passes, **282/282** (one new test,
    `Summary_ReportsCareerEarningsWhileAway`, added to `AwayCatchUpTests.cs`; it initially
    failed for an unrelated reason — the hardcoded return stand collided with an AI carrier's
    bay — fixed by picking a free stand instead of assuming one, same mistake the codebase's
    own `FreeStands()` helper exists to avoid). The HUD-drawing side
    (`AirsidePrototype.Airline.cs`, `OperationsSummary.cs`) is Presentation: reviewed carefully
    by inspection, matches established chrome/pattern precedent throughout, but **not run** —
    still no Unity editor in this session. Same open items as every presentation slice this
    session: `scripts/test-unity.sh`, a packaged build, the 1280x720/1440x900/Retina visual
    pass.
  - **Not done (real remaining scope):** nothing stops re-accepting `REG-KGC-INTRO` after it's
    fulfilled once — there's no "completed contracts" ledger yet, so the player can keep
    farming the starter contract indefinitely. That's consistent with Task 2's scope (no tier
    advancement, no second contract) but is worth Bailey's call before Task 4: either that's
    fine as a bridge until real progression exists, or it needs a guard now.
  - **NEXT:** all of Task 1 (HUD shell), Task 2 (career domain) and Task 3 (this) are pushed
    and ready for one batched Mac review — build, `scripts/test-unity.sh`, and the visual pass
    across every screen the earlier slices' handoffs listed. Task 4 (regional growth: a second
    contract, capacity, a second aircraft) waits on that review and on Bailey's read of the
    re-acceptance question above.

- **2026-09-16 Claude — Task 2: airline career domain and save v6 migration (ADR 0053).**
  - **Process note:** `GAME.md`'s own standing instruction said not to begin Task 2 until
    Bailey reviewed the cleaned HUD on a packaged build — that review has not happened (still
    no Unity editor in this session). The user explicitly chose to proceed with Task 2 anyway
    when asked directly. Recording that here rather than silently skipping the gate.
  - **Scope, matching the plan doc exactly:** Domain, Simulation, save schema v6, migration and
    tests — deliberately **no HUD/economy UI**. The Contracts workspace placeholder from the
    earlier slice is unchanged; nothing here is visible to the player yet. That wiring is
    Task 3.
  - **What exists now:** a real `AcceptContract` command and one authored contract,
    `REG-KGC-INTRO` (Adelaide↔Kingscote, ATR 42, 5 rotations, tuning-placeholder payment/
    reward numbers — not balanced, Bailey tunes these). Completing an eligible rotation as the
    player pays per-rotation, nudges reliability, and — on the contract's last required
    rotation — pays a completion reward and clears the active contract. Everything else
    (AI traffic, routes, taxi timing, saves for existing fields) is untouched; the hook lives
    entirely inside the existing `TaxiIn → AtStand` transition
    (`AirlineOperations.AdvanceAircraft`), the same place `CompletedTrips` already increments.
  - **Idempotency — the acceptance bar the plan doc calls out by name:** every settlement gets
    a `SettlementId` (registration + completed-trip number) and `AirlineCareerState` refuses a
    repeat one. Proved directly (not just asserted): a test calls the internal guard twice
    with the same id and checks the second call pays nothing — and, to make sure that test
    isn't vacuous, I mutated the guard out, watched the test fail, then restored it and
    confirmed it passes again. Save/restore reconstructs the exact processed-settlement set,
    so a rotation already paid before a save can never be paid again after loading it.
  - **Save v6:** `AirlineSaveData.CurrentVersion` is 6. A pre-6 save loads to a **fresh**
    Provisional career (0 funds, 100 reliability, no contract) — it never retroactively pays
    for trips flown before the career existed, per the plan doc's rule. Every existing v5
    field (fleet, schedules, stands, movement data) is untouched and still restores exactly as
    before; proved with a hand-built v5-shaped fixture in the new test file.
  - **Evidence — this slice is different from the presentation-only ones below:** I installed
    the .NET 8 SDK in this session (`/opt/dotnet`, via the official `dotnet-install.sh` script
    — apt's cached package index 404'd) specifically so `scripts/test-domain.sh` could
    actually run rather than being flagged as "not run here" again. **274/274 pre-existing
    headless tests still pass; 7 new tests in `Tests/EditMode/AirlineCareerTests.cs` pass —
    281/281 total.** This is real, verified evidence, not a claim. Unity EditMode (the real
    source of truth) and a packaged build are still open — no Unity editor here — but the
    Domain/Simulation logic itself has actually been exercised, unlike every earlier
    Presentation-only slice this session.
  - **One structural note for reviewers:** added `Simulation/AssemblyInfo.cs` with
    `[assembly: InternalsVisibleTo("Airside.Tests.EditMode")]` — the first use of that
    attribute in this codebase. It exists solely so the idempotency test can call the internal
    settlement guard directly; every other new behaviour is still exercised only through
    `AirlineOperations`'s public commands, matching how the rest of the test suite works.
  - **Explicitly not done (real remaining scope, not silently skipped):** `CancelDeparture`
    does not yet cost reliability for breaking a commitment against an active contract (left a
    `// TODO(ADR 0053)` at the exact spot); no tier ever advances past Provisional yet (Task 4);
    no HUD/away-summary wiring (Task 3).
  - **NEXT:** Task 3 — wire `REG-KGC-INTRO` into the Contracts workspace placeholder, an
    objective/status presentation, and the away-summary flow. Before any of that ships, still
    needs: `scripts/test-unity.sh` on Mac, a packaged build, and the 1280x720/1440x900/Retina
    visual pass this whole session's presentation work has been accumulating.

- **2026-09-16 Claude — persistent status/objective line, closing the remaining ADR 0053
  Task 1 gap from the nav-shell slice below.**
  - **Player-visible:** once the first-flight guide finishes, the card it used to occupy under
    the clock doesn't just vanish — it becomes a quiet one-line objective: the most urgent player
    aircraft and what it needs (`VH-ABC needs you — choose a stand.`), tinted safety-yellow or
    signal-red to match its severity, or a calm fleet-wide line (`3 of 4 aircraft flying — on
    schedule.` / `All aircraft on stand — ready for a flight.`) when nothing needs attention. This
    is the "current operation, next objective" the Task 1 acceptance list asked for outside the
    tutorial.
  - **How:** new pure `Presentation/OperationsSummary.cs` picks the worst-severity player
    aircraft via the existing `AircraftStatus.Severity`, or falls back to a fleet count. The
    `Guide` region in `AirlineHudLayout` — previously zero-sized once the tutorial finished — is
    now always sized (`StatusLineHeight` post-guide, `GuideHeight` during it), so `DrawAirlineHud`
    draws either the tutorial card or the new `DrawStatusLine` in the same slot. `FieldMiniMap`
    already cleared the guide column via the nav-strip fix below, so this needed no further
    change there.
  - **Evidence:** `PresentationLayoutTests.AirlineHudLayout_PanelsFitAndNeverOverlap` now checks
    the guide/status rect unconditionally (it's never zero-sized any more) at the same 6
    resolutions — not run here, no Unity editor in this session. `OperationsSummary` itself has
    no dedicated unit test: `FleetAircraft` has an internal constructor and no existing test
    fixture builds one directly, so it needs eyeballing on a Mac instead.
  - **NEXT:** fold into the same Mac verification pass as everything below — nav shell, flight
    numbers/airport glyphs, and this status line are all presentation-only and safe to review
    together.

- **2026-09-16 Claude — flight numbers and airport-glyph map markers (player-requested polish,
  outside the ADR 0053 task sequence).**
  - **Player-visible:** the Australia destinations map draws each destination — and Adelaide's
    own ADL marker — as a small airport glyph (a ringed compass with crossed runway bars) instead
    of a plain coloured square, growing modestly with zoom so it still reads as a field once
    you're in close. Flight identity through the HUD now leads with a flight number
    (e.g. `REX 404`, or two letters from the player's own airline name, e.g. `SC 217`) instead of
    a bare registration: the map's in-flight labels, the Flights board's small aircraft line
    (flight number · registration) and the floating field tags (flight number, falling back to
    registration for an idle aircraft with nothing planned) all read this way. The map's detail
    line (zoomed/selected/tracked) still spells out registration and type alongside the running
    commentary, so both identities stay available.
  - **How:** new pure `Presentation/FlightNumber.cs` — `AirlineCode` (the airline's own short id
    for AI operators, two letters drawn from the player's chosen name otherwise) and `For`/
    `ForAircraft`/`OrRegistration`, deterministic from airline + registration + destination via
    an FNV hash so the same aircraft on the same route always reads the same number. No Domain
    or save change: nothing is stored, it's derived fresh every draw. The airport glyph is a
    baked 48×48 texture (`AirportIcon`/`DrawAirportIcon` beside the existing `PlaneIcon` in
    `AirsidePrototype.Airline.cs`), tinted per marker exactly like the square it replaced.
  - **Evidence:** two new `FlightNumber` tests in `PresentationLayoutTests` (airline-code
    derivation, determinism/route-sensitivity) — not run here, no Unity editor in this session;
    same open items as below (Unity EditMode, packaged build, visual pass).
  - **NEXT:** fold into the same Mac verification pass as the HUD shell slice below — nothing
    here touches simulation/save, so it's safe to review together.

- **2026-09-16 Claude — Task 1 HUD shell cleanup, first slice: workspace nav consolidation.**
  - **Player-visible:** the clock panel's three ad hoc buttons (Plan/Hangar/Flights) are replaced
    by a nav strip directly under it with the four ADR 0053 workspaces — **Operations** (the
    flight board), **Map** (destinations/planner), **Fleet** (Hangar) and **Contracts** (a
    "coming in a future update" placeholder; no career state exists yet). Exactly one workspace
    is open at a time; the active tab reads in safety yellow. Existing hotkeys are unchanged
    (Tab → Map, H → Fleet, T → Operations). Dev Tools (F8) now shows a red frame and "DEV" badge
    so it reads as a diagnostic overlay rather than one more player tab.
  - **How:** new `HudWorkspace` enum (`Presentation/HudWorkspace.cs`) and one `_activeWorkspace`
    field replace the four independent `_mapOpen`/`_hangarOpen`/`_flightsOpen`/`_devToolsOpen`
    booleans that were hand-kept mutually exclusive across a dozen call sites. `AirlineHudLayout`
    gains a pure `NavStrip` rect (same tested pattern as `Clock`/`Guide`/`FleetArea`/`Map`/
    `Toast`), placed under the clock/guide column; everything below it (fleet, map, toast) now
    starts below the strip instead of directly under the clock. `FieldMiniMap.PanelFor` was
    updated to also clear the new strip so the mini-map can't sit under it on a short window.
    All existing panel content (`DrawFlightsPanel`, `DrawDestinationsMap`, `DrawHangarPanel`) is
    unchanged — only which field selects them.
  - **Invariants / unchanged:** no Domain, Simulation, save, route, schedule, reservation or
    traffic code touched. Grepped for every remaining reference to the removed booleans across
    `Assets/` before deleting them (including `AirsidePrototype.MiniMap.cs` and
    `AirsidePrototype.Soak.cs`'s review-panel dispatcher) to keep the project compiling.
  - **Evidence:** `PresentationLayoutTests.AirlineHudLayout_PanelsFitAndNeverOverlap` extended
    for the new `NavStrip` rect (fits-on-screen + no-overlap) at the existing 6 resolutions,
    `showGuide` true/false — not run here (no Unity editor in this session).
    `scripts/test-domain.sh` is unaffected (Domain/Simulation untouched).
  - **NEXT:** run `scripts/test-unity.sh` and a packaged Mac build, then the visual pass at
    1280x720, 1440x900 and Retina across setup, first-flight guide, planner, map, fleet, Hangar,
    Flights, away summary and stand assignment (Task 1's own acceptance list) before merging.
    Remaining Task 1 scope not attempted in this slice: a restyled persistent status strip/single
    objective layer and any further contextual-primary-action polish beyond what already exists
    at `DrawSelectedAircraftDetail`. Do not begin career state (Task 2) until Bailey has reviewed
    the cleaned HUD on a packaged build.

- **2026-09-16 Bailey/Codex — airline career progression and HUD direction approved (ADR 0053).**
  - **Product identity:** Airside is a player-airline growth game inside an autonomous Adelaide
    Airport. The retired Kingscote airport-management economy is not the direction.
  - **Progression:** authored service contracts turn completed rotations into funds, reliability
    and capability-based operating tiers. Feedback occurs after each eligible flight, while
    regional, domestic and international growth is paced over days, weeks and months. No generic
    XP, daily-login rewards, paid acceleration or waiting-only gates.
  - **HUD:** simplify before adding career information: one quiet status/objective layer, four
    workspaces (Operations, Map, Fleet, Contracts), one workspace at a time and one contextual
    primary aircraft action. Keep the first pass on the current IMGUI path.
  - **Authoritative detail:** `docs/product/PROJECT_PLAN.md`,
    `docs/product/AIRLINE_PROGRESSION_AND_HUD_PLAN.md` and
    `docs/decisions/0053-airline-career-and-hud-direction.md`.
  - **NEXT:** implement Task 1, HUD shell cleanup only. Preserve all simulation, traffic, routes,
    commands, saves and aircraft behaviour. Verify setup, first-flight guide, planner, map, fleet,
    Hangar, Flights, away summary and stand assignment at 1280x720, 1440x900 and Retina. Do not
    begin career state until Bailey has reviewed the cleaned HUD.
- **2026-09-16 Codex — AIR-010 Singapore Airlines 787-10 implemented.**
  - **Player-visible:** Singapore Airlines' Gate 20 aircraft is now the published Adelaide-route
    type, a Boeing 787-10, rather than an A350 stand-in. It has its own long/narrow widebody
    proportions, four-pane flight deck, swept/raked wing, chevron nacelles and ten-wheel gear.
  - **Scope:** new B78X catalogue/performance/presentation type, AIR-010 runtime model, Hangar
    thumbnail and the existing Singapore Airlines rotation only.
  - **Invariants / unchanged:** Gate 20 route and timing, destination network, reservation rules,
    other operators are unchanged. Existing v5 Singapore `9V-SMA`/A350 placeholder saves migrate
    once to `9V-SCA`/787-10 while preserving stand, schedule, movement state and completed trips.
    The new project-owned model contains no
    copied livery or third-party art.
  - **Evidence:** deterministic AIR-010 geometry regression passes at the exact 68.30 × 60.12 ×
    17.02 m envelope; generated output is 227 meshes / 14,256 triangles. Headless passed
    **274/274**, Unity 6000.3.23f1 EditMode passed **492/492**, and a fresh packaged Mac build
    completed successfully. The generated runtime-model thumbnail was visually inspected; the
    packaged fleet UI confirms `9V-SCA · Boeing 787-10` at Gate 20L.
  - **NEXT:** use an interactive follow-camera playtest for a close Gate 20 day/dusk/night review;
    unattended overview framing confirms integration but does not isolate the aircraft closely.
- **2026-09-16 Codex — AIR-009 A350-900 rebuilt as a genuine widebody silhouette.**
  - **Player-visible:** Cathay Pacific's A350-900 now reads as an A350 before its colour: broad
    six-metre cabin, long tapered nose, dark wraparound cockpit mask, flexed/raked wing tips,
    large high-bypass engines and ten-wheel gear replace the former stretched-737 geometry.
  - **Scope:** AIR-009 model, material hierarchy, identity-marking placement and runtime-model
    Hangar thumbnail only. The exact 66.80 × 64.75 × 17.05 m envelope is unchanged.
  - **Invariants / unchanged:** airline operations, Gate 18 route, performance, schedules,
    runway separation, saves and all other aircraft are unchanged. Geometry is project-owned;
    no external art or livery was added.
  - **Evidence:** deterministic AIR-009 regression passes; generated output is 229 meshes /
    14,192 triangles with 18 blades per fan and ten tyres. `scripts/test-domain.sh` passed
    **274/274** and Unity 6000.3.23f1 EditMode passed **490/490**.
  - **NEXT:** completed by AIR-010 immediately above.

- **2026-09-16 Codex — real Adelaide night readability corrected and packaged.**
  - **Player-visible:** at 23:30 the terminal, parked aircraft and apron surface remain readable
    in a restrained blue night grade; 28 separated terminal bays carry warm interior light.
    Daylight still uses the approved blue-glass facade.
  - **How:** the focused real-airport world now retains only the terminal's seven roof floods
    while the broader decorative light set stays excluded. Real-scale flood throw, cool ambient
    key/fill and night-only HDR interior cards replace the prior near-black terminal/apron view.
  - **Invariants / unchanged:** routes, stands, collision, operations, aircraft and saves are
    unchanged. No external asset or licence was added.
  - **Evidence:** `scripts/test-domain.sh` **252/252 passed**; Unity 6000.3.23f1 built the packaged
    Mac app; matched 23:30/day captures passed at `work/review/night-lighting-accepted.png` and
    `work/review/night-lighting-day-accepted.png`. Unity EditMode passed **448/449**; only the
    existing unrelated Gate 13 route tolerance miss remains.
  - **NEXT:** merge this lighting slice, then inspect landside roads/car-park structure in daylight.

- **2026-09-16 Codex — real Adelaide terminal facade implemented and packaged.**
  - **Player-visible:** the full-scale OSM terminal is no longer a single blank block. Its apron
    side now has separated dark-glass bays, a projecting brow, roof skylights and plant boxes.
  - **Invariants / unchanged:** the real terminal footprint and height, Gate 13 route, all
    pavement, collision, aircraft, operations and saves are unchanged. No external asset or
    licence was added.
  - **Evidence:** `scripts/test-domain.sh` **251/251 passed**; Unity 6000.3.23f1 produced a fresh
    packaged Mac build; close day and 23:30 captures were inspected at
    `work/review/terminal-facade-final-*.png`. Unity EditMode compiled and passed **447/448**;
    only the existing unrelated Gate 13 tolerance miss remains. The unattended review camera now
    accepts explicit centre coordinates and correctly seeds that centre when soak skips the intro.
  - **NEXT:** merge this presentation slice, then address the very dark night apron/terminal
    lighting as a separate lighting pass rather than making facade materials glow all day.

- **2026-09-16 Codex — ATR high-wing hierarchy corrected and packaged.**
  - **Player-visible:** the player/Emu Air ATR now reads as a stocky high-wing, T-tail regional
    aircraft before its livery. Broad wing and horizontal-tail surfaces use a neutral finish;
    airline colour stays on the fin and compact wingtips. Dark cabin/flight-deck glazing remains
    legible against both white operator paint and the generated Soak Air livery.
  - **Invariants / unchanged:** AIR-001 v03 geometry, exact 22.67 × 24.57 × 7.59 m envelope,
    183 parts / 19,704 triangles, six-blade prop rig, wheel scale, routes, schedules and saves are
    unchanged. No external asset or licence was added.
  - **Evidence:** `scripts/test-domain.sh` **249/249 passed**; Unity 6000.3.23f1 produced a fresh
    packaged Mac build and the Bay 50D daylight view was inspected. Unity EditMode compiled and
    passed **445/446**; the new ATR material test passed and only the existing unrelated Gate 13
    tolerance miss remains (0.878 m against a 0.873 m cap).
  - **NEXT:** merge this final aircraft-colour slice. The four operational aircraft types then
    share a consistent visual hierarchy; switch focus back to airport structures or lighting.

- **2026-09-16 Codex — Q400 high-wing hierarchy corrected and packaged.**
  - **Player-visible:** QantasLink's Q400 now reads as a long high-wing, T-tail turboprop instead
    of a bright red wing block. The wing, control surfaces and horizontal tail use a neutral
    painted-metal finish; operator red stays on the tall fin and compact tip devices. Darker
    cabin/flight-deck glazing remains legible in the follow view.
  - **Invariants / unchanged:** AIR-006 geometry, exact 32.83 × 28.42 × 8.34 m envelope, 182
    parts / 27,288 triangles, six-blade prop rig, wheel scale, routes, schedules and saves are
    unchanged. No external asset or licence was added.
  - **Evidence:** `scripts/test-domain.sh` **249/249 passed**; Unity 6000.3.23f1 produced a fresh
    packaged Mac build and the Bay 50A daylight view was inspected. Unity EditMode compiled and
    passed **444/445**; the new Q400 material assertions passed and only the existing unrelated
    Gate 13 tolerance miss remains (0.878 m against a 0.873 m cap).
  - **NEXT:** merge this presentation-only slice, then assess whether the ATR needs the same
    restraint or whether its existing ochre scheme already reads acceptably.

- **2026-09-16 Codex — 737 livery hierarchy corrected and packaged.**
  - **Player-visible:** Wattlebird Jet's 737 keeps its blue fin and split winglets, but the broad
    wing, flap, spoiler, aileron and horizontal-tail surfaces now use a neutral painted-metal
    finish. Darker cabin/flight-deck glazing holds the long narrowbody read without turning the
    whole aircraft cyan at Gate 13.
  - **Invariants / unchanged:** AIR-005 geometry, exact 39.47 × 35.92 × 12.42 m envelope, 199
    parts / 17,404 triangles, nose-stop datum, wheel scale, fan rig, Gate 13 route and saves are
    unchanged. No external asset or licence was added.
  - **Evidence:** `scripts/test-domain.sh` **249/249 passed**; Unity 6000.3.23f1 produced a fresh
    packaged Mac build and the Gate 13 daylight follow view was compared before/after. Unity
    EditMode compiled and passed **444/445**; the new 737 colour/material assertions passed and
    only the existing unrelated Gate 13 tolerance miss remains (0.878 m against a 0.873 m cap).
  - **NEXT:** merge this presentation-only slice, then inspect the Q400 from the same camera.

- **2026-09-16 Codex — Saab 340 silhouette/material pass implemented and packaged.**
  - **Player-visible:** Rex's Saab now reads as a compact aluminium/white commuter aircraft
    rather than a solid airline-colour toy. Broad wings, flaps, ailerons and the horizontal tail
    use a restrained neutral finish; the operator accent stays on the vertical tail. Slightly
    larger, skin-proud cabin glazing remains visible in the oblique follow camera.
  - **Invariants / unchanged:** AIR-007 stays at 19.73 × 21.44 × 6.97 m with 120 named meshes,
    6,868 triangles, four-blade props, animation names, schedules, routes and saves unchanged.
    No external asset or licence was added.
  - **Evidence:** deterministic AIR-007 geometry regression passed; `scripts/test-domain.sh`
    **249/249 passed**; Unity 6000.3.23f1 produced a fresh packaged Mac build and the close
    daylight and 23:30 follow captures at `work/review/saab-*.png` were inspected against the
    baseline; the neutral surfaces remain readable without glowing at night.
    Unity EditMode compiled and passed **444/445**; only the existing unrelated Gate 13 tolerance
    miss remains (0.878 m against a 0.873 m cap). The new Saab integration/material test passed.
  - **NEXT:** merge this narrow aircraft slice, then assess the 737 at the same follow-camera
    distance before adding more map decoration.

- **2026-09-16 Codex — taxiway edge weathering implemented and visually verified.**
  - **Player-visible:** sparse warm-grey dust/scuff strips now break up both edges of Adelaide's
    taxiways. They sit inside the asphalt, stay clear of centreline/hold paint and disappear at
    overview distance instead of turning the map into an outline drawing.
  - **Scope:** `TaxiwayEdgeWear` generates deterministic 9 m patches on a 23 m rhythm with
    independent phase per side; all strips are combined into one non-shadowing mesh.
  - **Invariants / unchanged:** taxiway widths, sealed shoulders, routes, collision, operations,
    aircraft and saves are unchanged. No external asset or licence was added.
  - **Evidence:** straight-edge, deterministic/degenerate and full-Adelaide density tests added;
    `scripts/test-domain.sh` **249/249 passed**. Unity 6000.3.23f1 produced a fresh packaged Mac
    build. Close daylight and airport overview captures confirm the wear reads only near the
    ground; a 23:30 capture confirms it does not glow. Local evidence is under
    `work/review/taxi-edge-*.png` (git-ignored).
  - **NEXT:** improve the weakest aircraft silhouette—the Saab 340—before adding more ground noise.

- **2026-09-16 Codex — real-scale apron slab joints implemented and visually verified.**
  - **Player-visible:** Adelaide's concrete aprons now read as constructed slabs instead of
    broad unbroken sheets. A restrained 18 m expansion-joint grid is clipped to each real OSM
    apron outline, including concave boundaries.
  - **Scope:** presentation mesh only. `ApronSlabJoints` generates deterministic inset line
    segments; `BuildYpadTaxiwaysAndAprons` combines them into one non-shadowing matte mesh.
  - **Invariants / unchanged:** pavement outlines, taxi routes, stands, collision, simulation
    timing, saves, aircraft and the developer-only full-airport mode are unchanged. No external
    asset or licence was added.
  - **Acceptance / evidence:** rectangle, concave-outline and full-Adelaide density tests added;
    `scripts/test-domain.sh` **246/246 passed**. Unity 6000.3.23f1 produced a fresh packaged Mac
    build. Overview and follow-camera captures were inspected at noon, 17:30 golden hour and
    23:30 night; joints remain restrained up close and disappear cleanly at overview distance.
    Local evidence is in `work/review/apron-*.png` (git-ignored). The full EditMode run compiled
    and passed 440/441; its sole failure is the existing Gate 13 route tolerance (0.878 m movement
    against a 0.873 m cap), unrelated to presentation.
  - **Review tool:** packaged captures may use `-airsideReviewTime HH:mm` to override lighting only;
    the simulation clock, schedules, weather and saves remain untouched.
  - **NEXT:** merge the apron slice, then improve taxiway/shoulder edge breakup as the next ground
    pass before changing aircraft models.

- **2026-09-16 Cursor — verified taxi speeds.** Branch `cursor/taxi-speeds-verified-d77c`
  (rebased onto main after #278 merged).
  - **Player-visible:** Straight taxi is no longer a 15 kt crawl. Turboprops (ATR / Saab /
    Q400) and the 737 now use verified bands: ~25 kt on long taxiways, ~10 kt in turns,
    apron 15 kt (turboprop) / 10 kt (jet), 5 kt onto the stand, 3 kt pushback, 10 kt
    lineup, vacate settles to 20 kt after the 12 kt runway exit.
  - **How:** `GroundSpeedLimits` + bay vs gate wiring in `AdelaideGround`. Sources in
    `docs/data/AIRCRAFT_SPECIFICATIONS.md` and ADR 0045.
  - **Evidence:** `scripts/test-domain.sh` **242 passed** after rebase onto #278. Mac Unity EditMode + Play eyeball of a bay taxi-out and a Gate 13 jet
    still needed.
  - **NEXT:** Mac `scripts/test-unity.sh` and a packaged taxi watch. Merge when green.

- **2026-09-16 Cursor — camera zoom/pan feel + taxi prop blur.** Merged as #278.
  - **Player-visible:** Scroll zoom and drag pan feel snappier; turboprop and 737 fan
    discs engage at taxi RPM.

- **2026-09-16 Claude — bug sweep in progress (goal: 100 merged fixes). All 11 batches merged:
  100 fixes (batch 5 was 7, not 8 as first recorded), Unity EditMode 430/430. The goal is met;
  nothing below has been seen in a build.**
  - **Batch 11 — the three known-unfixed items are now fixed:** stand identifiers get their own
    material with `unity_GUIZTestMode` = LessEqual; `UpdateWindsock` ripples from each segment's
    captured rest rotation; the procedural ambience beds snap tone frequencies to whole cycles
    and crossfade the loop (`LoopFrequency`, `CrossfadeLoop`).
  - **Batch 11 — also:** `HangarDoorOpensWithinMetres` gates the door; the touchdown cue moved to
    its own audio child (it was moving the prototype transform and the tyre-smoke pool);
    `AwaySummary.Build` tolerates a missing fleet array; the soak scheduler skips an aircraft
    that can reach nothing; `AirsideRuntimeQuality.WritesPipelineAsset` keeps Play mode out of
    the tracked URP asset; `AirsideArtTextures` remembers misses and destroys the placeholder
    texture on a failed decode.
  - **Batch 10:** invariant `CombinedKey` numbers in `ArtGltfLoader`; `AirlineSave.IsRealDate`
    guards `ClockFor` (the start screen calls it from OnGUI); `build-mac.sh`, `bake-terrain.sh`
    and `test-unity.sh` report failures and name failed tests; field tags skip hidden views;
    `AirsideSceneIndex` drops destroyed entries and `FindGameObject` no longer uses `?.`;
    `EnsureFleetPickables` calls `AirsideNamedChildren.Forget` (nothing called it before).
  - **Batch 9:** mini-map press disarm and three-pass dot order (`MiniMapDotPass`); hangar list
    reuse; mute toast; `ReadAirlineControls` returns early while help is open; `EaseWeatherGloom`
    /`WeatherGloomTarget`; `RainRootPosition` follows the camera focus; cached `_birdPhaseSeed`;
    `StarFieldFade`; `_SHADOWS_SOFT` in `Airside/Surroundings` (shader change — not compiled by
    the -nographics run, needs an eyeball pass).
  - **Batch 8:** `SurfaceKind.Water` no longer maps to `mat_wet_v01`; translucent colours on
    opaque authored templates call `ApplyTransparent`; `InferSurfaceKindFromColor` picks Glass only
    for darker blue-tinted translucency; `AirsideMat001Menu.PreferredMap` takes v03 > v02 > v01
    (the 8 MAT-001 .mat files were regenerated); `AirsideDayVolume` noon/golden weights
    (`NoonPunchWeight`, `GoldenBloomWeight`); camera pitch waits out orbit suppression, follow
    releases hidden targets, `KeyboardPanMetresPerSecond` scales with distance, and
    `ClampCentreHeight` bounds lift; `AirsideEditorStartup` opens the scene once per session.
    The grading and camera feel are unverified in a build.
  - **Batch 7:** Esc menu is modal for keyboard and pointer, and Esc closes it first; camera
    right/middle drags need a field press; `AircraftViewParts.HasFans/HasSeparateElevators`;
    per-frame fleet pose memo.
  - **Batch 6:** fence gate openings (`TryGateGapOverlapping`); 12/30 drop and paint clip
    (`CrossRunwayDropMetres`, `ClipCrossRunwayPaintToMain`); airline mode skips
    `AirportSimulation.Update`; test-unity stale results guard; sync count and orphan metas.
  - **Batch 5 — assets fixed at source:** `trn_ground_*_v01.terrainlayer` `m_MaskMapRemapMax.w`
    now equals each layer's authored smoothness, and `AirsideTerrainBakerMenu` writes it on
    rebake. `AirsideTerrainGround.CalibratedLayer`'s smoothness cap is now a guard. The mask PNGs
    are unchanged (alpha is the roughness shape the remap scales).
  - **Batch 5 — fleet lamps:** `LandingLightsOn` / `FlapDegrees` take `drawnOnGround` for fleet
    aircraft; taxi lights need running engines.
  - **Batch 4:** Saab/Q400 prop discs; per-pixel shadow coordinate in `Airside/AdelaideGround`
    (shader change, not compiled by the -nographics test run; needs an eyeball pass); terminal
    prism winding; road height sampling; DevTools null stand; kinematic pick proxies; prefab
    miss cache; shared engine clip.
  - **Found, not fixed (corrected):** YPAD stand identifiers do render. A probe showed `TextMesh`
    defaults to LegacyRuntime with `GUI/Text Shader`, but that shader draws through geometry
    (ZTest Always), so labels can show through parked aircraft. A fix needs a depth-tested text
    material and a visual check.
  - **Batch 3 — dusk/night was effectively never shown:** lighting read `DayCycle` over
    simulation seconds, which starts at 08:00 on launch. It now uses the Adelaide wall clock
    (`DayCycle.AtLocalTime`). Expect real night lighting in evening playtests; still visually
    unverified.
  - **Batch 3 — more dark/odd full-airport materials:** `AirsideRuntimeMaterialBinder` made tree
    canopies glass; kit trunks were Grass; bark and rock were painted metal.
  - **Batch 2:** fleet queue slots at the hold and stand wait (`FleetVisual.QueueSlot`,
    `AdelaideGround.HoldingShortPose`); follow-camera release, hitch and first-visible fixes;
    F follows the selection; HUD wrap and clamp fixes; tag click-through; Esc menu wording;
    strict save state names; ground-path NaN guard.
  - **Dark full-airport materials, likely cause found and fixed:** `SurfaceKind.PaintedMetal`
    (InferFromMeshName's catch-all and the colour-inference default) mapped to
    `mat_corrugated_metal_v01` plus the corrugated mask, whose R channel (metallic) averages 0.55.
    Under the solid-colour sky, metallic surfaces reflect almost nothing, so they rendered near
    black. PaintedMetal now uses its own profile. The authored-template path also dropped
    explicit albedo.
  - **Also:** the wet-concrete albedo was applied in clear weather and never restored; mesh
    names hit aircraft-skin substrings; jet fan and prop spool keys collided; touchdown cues hit
    null or hidden views; daytime lights left enabled; per-frame `.name` allocations; quadratic
    night-glow collection; runtime textures kept their CPU copy.
  - **Not verified visually:** no packaged build or capture this session (standing
    no-rebuild rule). Dusk/night and a full-world profile are still open.

- **2026-09-16 Claude — diagnostic `-airsideFullAirport` terrain over-exposure fixed; default
  circuit untouched. Unity EditMode 389/389 after merging #265 (0 failed, 0 skipped).**
  - **Player-visible:** none by default; the release bare YPAD circuit, camera, simulation and
    saves are unchanged. In `-airsideFullAirport` the legacy Kingscote terrain now reads as
    textured olive grass with relief instead of a clipped white sheet.
  - **Cause, identified in a packaged build before any fix** (temporary `[DIAG]` instrumentation,
    since removed):
    - **White source:** hiding the `Terrain` alone removed all the white.
    - **Ruled out:** probes, reflection intensity, point/spot lights, fog, horizon dome,
      shadows, basemap distance, height blend, normal maps, lightmaps, stripped shaders and
      bad splat weights (sums 0.996–1.004).
    - **Smoothness (main driver):** URP Terrain/Lit reads mask-map alpha as smoothness. The
      generated masks average 0.45 / 0.32 / 0.27 / 0.04 against authored 0.12 / 0.18 / 0.08 /
      0.05. On plain URP Lit, grey 0.25 goes from 148 to 249 when smoothness goes 0 → 0.5.
    - **Albedo:** the untinted CC0 layers are too bright and cool under the release noon rig.
      Smoothness 0 alone still left 186–235.
    - **Not a factor:** textures, colour space (Gamma) and imports are fine.
  - **How:** `AirsideTerrainGround.TryBuild` now:
    - returns immediately when `AirsideBareField.Enabled` is true;
    - gives the terrain its own TerrainData copy with `CalibratedLayer` copies: albedo ×
      `LayerAlbedoScale` (0.72, 0.76, 0.42), mask smoothness capped at each layer's authored
      smoothness;
    - never writes to the baked assets.
  - **Evidence:**
    - **Tests:** new `AirsideTerrainGroundCalibrationTests` (4).
    - **Re-verified on merged code:** after merging #265, rebuilt and re-captured; the same
      samples agree within 1 level.
    - **Build:** fresh universal Mac build, captures via
      `-airsideSoak -airsidePinDaylight [-airsideFullAirport] -airsideReviewShot`.
    - **Full airport at noon:** terrain sample pixels went from 245–254 to 56–188 olive
      (e.g. 126,138,87).
    - **Default circuit at noon:** matches the pre-fix capture within 2 levels at every sample.
    - **Live-time launches:** both modes inspected.
    - **Screenshots:** `work/review/full-airport-exposure-*.png`,
      `work/review/default-circuit-*.png` (git-ignored, local only).
  - **Remaining limitations:**
    - **Dark legacy materials:** trees, hills, asphalt and other legacy Lit materials are
      now visibly under-exposed (near-black).
    - **Unchecked times of day:** dusk and night not inspected.
    - **Not re-authored:** the calibration is a runtime copy. The mask generator and the
      baked assets still carry high smoothness.
    - **Performance:** no full-world profile.
    - **Capture caveat:** the Mac must stay awake; captures stalled while it slept.
  - **NEXT:** Still do not enable full-airport for players. If it becomes a QA view, calibrate the
    dark legacy Lit materials next, then check dusk/night and profile.

- **2026-09-16 Codex — aircraft presentation completion pass; Unity EditMode 385/385.**
  - **Player-visible:** Wattlebird Jet's 737-8 finally has a live turbofan read: each
    intake's twelve blades spin with its own engine spool and resolve into a restrained
    blur at power, instead of appearing frozen beside animated regional turboprops.
    All four operational types now roll their tyres at the diameter authored into their
    model, so the larger 737/Q400 wheels no longer over-spin against ATR/SAAB gear.
  - **How:** AIR-005 fan parts are named, nested and pivot-rebaked at build time; motion
    adds phase/engine-driven fan RPM and a non-shadowing intake disc. `AircraftVisualProfile`
    now owns main/nose tyre radii (ATR .37/.31 m, 737 .62/.55 m, Q400 .50/.34 m, Saab
    .38/.28 m). This is presentation-only: no catalogue, route, phase, reservation,
    schedule or save data changed.
  - **Evidence:** Unity EditMode **385/385 passed**; a fresh universal Mac build succeeded.
    Deterministic AIR-001, AIR-005, AIR-006 and AIR-007 kit tests pass. The packaged
    Hangar gallery confirms all four true-scale silhouettes (including the 737); the
    saved airport was not advanced into a new 737 movement just for QA. `scripts/test-domain.sh`
    remains unavailable because the system .NET SDK is not on PATH.
  - **NEXT:** If a dedicated throwaway save is available, capture a 737 at idle, taxi and
    take-off to review the fan-blur transition in motion. This does not block the scoped
    presentation build or its automated coverage.

- **2026-09-15 Codex — graphics/performance audit; default circuit retained. Unity EditMode 383/383.**
  - **Player-visible:** the shipped bare YPAD circuit remains unchanged. A developer-only
    `-airsideFullAirport` capture now frames the old miniature world at its usable 155 m overview
    rather than inheriting the 3.1 km bare-field camera and appearing empty. It is not a release
    toggle: its legacy terrain/material stack is visibly over-exposed in the packaged capture.
  - **How:** `AirsideBareField` / `AirsideFocusMode` and retired surface/daylight/fence paths now
    use runtime launch flags instead of compile-time constants, removing three unreachable-code
    warnings and preserving the default focused circuit. The initial apron probe records its
    current band and refresh deadline after its first capture, preventing the first `ApplyDayCycle`
    from immediately issuing the same 128 px cubemap capture again. Camera framing and far plane
    select the appropriate real-metre or miniature scene envelope.
  - **Evidence:** Unity EditMode **383/383 passed**, with no C# compile errors or CS0162 warnings;
    a fresh universal Mac build completed. Default circuit and `-airsideFullAirport` packaged
    captures were inspected. The headless .NET harness was unavailable here (`dotnet` not on PATH).
  - **NEXT:** Do not enable full-airport mode for players. If that becomes a product decision,
    migrate/re-author its legacy terrain/material exposure and complete an independent full-world
    performance profile before changing the default.

- **2026-09-15 Cursor — bug hunt fixes (follow camera, regional backfill, save restore, domain harness).**
  - **Player-visible:** Following an aircraft that leaves Adelaide no longer snaps the camera onto a
    different one; the view stays where it is (F / Overview still work). Continue on a cramped old
    save can finish adding Rex/QantasLink on a later load instead of permanently dropping the rest
    of a carrier, and those joiners use the same 50D/50E clearance preference as live stand choice
    (ADR 0052). A mid-trip save with no destination is rejected instead of collapsing the flight.
  - **How:** `SetFollowTargets` keeps the same transform or `ReleaseFollow`s; regional backfill
    matches terminal-operator “fill remaining aircraft” and `SuggestStandFor`; restore requires a
    destination in taxi-out through landing. Headless harness excludes Unity-only mini-map /
    named-children / ground-look tests and compiles `RunwayRubberMarks`.
  - **Evidence:** `scripts/test-domain.sh` **226 passed** (no Unity editor here). New Unity
    EditMode follow tests in `PresentationLayoutTests` still need `scripts/test-unity.sh` on Mac.
  - **Unfixed (see PR):** regional stand released at pushback (intentional, ADR 0047) so taxi-out
    and taxi-in can overlap a bay; Q400 on 50D/50E still allowed when it is the last free stand;
    jet AwaitingStand HUD still lists regional bays (player is ATR-only today); Unity/packaged
    follow check open.
  - **NEXT:** Mac Unity EditMode + packaged follow of a departure going off-map. Do not start new
    visual work from this branch.

- **2026-09-15 Codex — AIR-006 Dash 8-400 nacelle-bay polish; Unity EditMode pending Mac.**
  - **Player-visible:** QantasLink's Q400 keeps its established long high-wing, six-prop,
    T-tail silhouette, but its rear nacelle gear-bay enclosures now curve with the pod rather
    than reading as blocks; the open main doors are slimmer at normal camera distance. The
    `mdl_dash8_q400_v01` path, 32.83 × 28.42 × 8.34 m envelope, tyre contact, type profile,
    schedules and saves remain unchanged.
  - **How:** `scripts/generate-air-006-dash8-q400.py` now emits rounded nacelle gear bays and
    thin door sheets at 182 named meshes / 27,288 triangles. Source + StreamingAssets kits and
    the 480 × 320 Hangar thumbnail are synced. New `scripts/test-air-006-dash8-q400.py` locks
    scale, rounded bay density, thin doors, centre saddle and index integrity.
  - **Evidence:** generator validation and `python3 scripts/test-air-006-dash8-q400.py` pass;
    offline front, side, elevated and Hangar thumbnail review completed. Unity 6.3 is unavailable
    here, so combined EditMode and packaged overview/follow QA remain open.
  - **NEXT:** Commit/push this narrow AIR-006 slice. Then run Unity EditMode and one fresh Mac
    build against the combined four-aircraft branch before any further visual work.

- **2026-09-15 Codex — AIR-001 ATR 42-600 close-view polish; Unity EditMode pending Mac.**
  - **Player-visible:** Emu Air and the player's ATR retain their v03 identity, but the four
    flight-deck panes now follow the curved nose without the previous flattened/stepped read.
    A broader shallow saddle joins the T-tail to the fin at follow-camera distance. The
    `mdl_atr42_starter_v03` path, 22.67 × 24.57 × 7.59 m envelope, six props, tyre contact,
    type profile, schedules and saves remain unchanged.
  - **How:** the v03 generator now emits curvature-fitted cockpit panels and a wider tailplane
    saddle; its 183 named meshes / 19,704 triangle budget is unchanged. Source +
    StreamingAssets kits and the 480 × 320 Hangar thumbnail are synced. The existing v03
    geometry test now locks the thin fitted panes and tail-saddle blend.
  - **Evidence:** generator validation and `python3 scripts/test-air-001-atr42-v03.py` pass;
    offline front, side, elevated and Hangar thumbnail review completed. Unity 6.3 is unavailable
    here, so combined EditMode and packaged overview/follow QA remain open.
  - **NEXT:** Commit/push this narrow AIR-001 slice, then polish AIR-006's remaining wing-root
    and gear-door blockiness in a separate commit. Do not start a new type.

- **2026-09-15 Codex — AIR-007 Saab 340B close-view polish; Unity EditMode pending Mac.**
  - **Player-visible:** Rex's Saab keeps its compact low-wing, conventional-tail identity, but
    its four flight-deck panes now follow the rounded nose rather than projecting as a dark
    box. Curved nacelle gear-bay fairings and smaller hubs/spinners remove the blocky underwing
    read. The `mdl_saab_340b_v01` path, 19.73 × 21.44 × 6.97 m envelope, tyre contact, motion
    names, type profile, schedule and save behaviour remain unchanged.
  - **How:** `scripts/generate-air-007-saab-340b.py` now emits 120 named meshes / 6,868
    triangles; source + StreamingAssets kits and its 480 × 320 Hangar thumbnail are synced.
    New `scripts/test-air-007-saab-340b.py` locks scale, fitted glazing, curved gear fairings,
    compact hubs and index integrity. No other aircraft or simulation code changed.
  - **Evidence:** generator validation and `python3 scripts/test-air-007-saab-340b.py` pass;
    offline front, side, elevated and Hangar thumbnail review completed. Unity 6.3 is unavailable
    here, so combined EditMode and packaged overview/follow QA remain open.
  - **NEXT:** Commit/push this narrow AIR-007 slice, then polish AIR-001's remaining cockpit
    glazing and tail joins in a separate commit. Do not start a new type.

- **2026-09-15 Codex — AIR-005 737-8 close-view polish; Unity EditMode pending Mac.**
  - **Player-visible:** Wattlebird Jet's 737 now has a skin-coloured flight-deck crown with
    three compact fitted windshield panes rather than a dark projecting visor; its keel fairing
    is a short, tapered wing-root transition and its split winglets are restrained at overview
    distance. The same `mdl_737_8_narrowbody_v01` path, 39.47 × 35.92 × 12.42 m envelope,
    nose-stop datum, tyre contact, gate loop and save behaviour remain intact.
  - **How:** `scripts/generate-air-005-narrowbody-737-8.py` now emits 199 named meshes / 17,404
    triangles; source + StreamingAssets kits and the 480 × 320 Hangar thumbnail are synced.
    New `scripts/test-air-005-737-8.py` locks scale, compact panes, the short fairing,
    restrained tips and index integrity. No other aircraft or simulation code changed.
  - **Evidence:** generator validation and `python3 scripts/test-air-005-737-8.py` pass;
    offline front, side, elevated and Hangar thumbnail review completed. Unity 6.3 is not
    available in this environment, so combined EditMode and packaged overview/follow QA remain
    open.
  - **NEXT:** Commit/push this narrow AIR-005 slice, then give AIR-007 Saab 340B the same
    close-view cockpit/nacelle/prop refinement in a separate commit. Do not start a new type.

- **2026-09-15 Claude — polish phase 6: runway fairness + stand suggestion (ADR 0052). Unity
  EditMode 376/376.**
  - Tower: a departure holding short ≥ 6 min that has waited longer than the first arrival
    gets the runway; otherwise arrivals still go first. No save change.
  - `AirlineOperations.SuggestStand`: free fitting stand, avoiding a Dash 8-400 beside another
    aircraft on 50D/50E when another stand is free, then shortest taxi in. AI aircraft use it
    (was first-fit); the player's stand button shows it as "Best stand". It never refuses
    the last free bay, so this softens — does not settle — the Q400 50D/50E question.
  - Polish plan phases 1–6 are done. Build from #259 was launched for Bailey (phases 1–4); the
    phase 5 perf and phase 6 logic changes still need a packaged look / soak.

- **2026-09-15 Claude — polish phase 5: per-frame garbage cut. Unity EditMode 370/370.**
  - `AirsideNamedChildren.Names` caches child names alongside the cached transforms; the 29
    per-frame part passes (gear, doors, lights, props, control surfaces, vehicles, apron
    people) read those instead of `Transform.name`, which allocated a string per read —
    hundreds per aircraft per frame with the v03 kits. `NestCrossPropellerBlades` Forgets the
    cache after renaming parts.
  - `SyncCommercialAircraftViews` (every frame) reuses its dictionary/sets/lists, swaps two
    view buffers instead of allocating, and remembers each view's id instead of parsing names.
  - `RefreshFleetFollowTargets` compares transforms instead of concatenating a name signature,
    and only re-checks pick proxies/markers when the on-field set changes.
  - Follow camera caches the target's visual profile component; intro styles built once.
  - Not yet profiled in a build; a Profiler GC Alloc before/after would confirm.

- **2026-09-15 Claude — polish phase 4: ground and runway look. Unity EditMode 369/369.**
  - `AdelaideGround.shader`: large soft light/dark patches from deterministic value noise
    (240 m, ±14 % brightness, slightly warmer when lighter) break the grass tiling; on High
    quality (`_GROUND_FAR_DETAIL`, `multi_compile_local`) each layer also mixes a rotated 4.3×
    sample from 120 m to 900 m away, and normal strength eases 70 % flatter with distance to
    stop far-field shimmer. Medium keeps the single sample.
  - Ground mesh edge rows now get one-sided slope normals instead of straight up — the likely
    cause of the faint line round the airfield ground edge (test: edge vs inner normal < 6°).
  - Runway rubber: four solid near-black 180 × 41 m slabs replaced by 310 seeded streaks on the
    main-gear tracks (`RunwayRubberMarks`), peaking ~430 m past each threshold, heavier on 05,
    two shades, two meshes, laid 16–22 mm above the runway top under the paint.
  - **Needs a packaged look** (not built, per standing rule): overview tiling, far-field
    shimmer, edge line gone, rubber reads as streaks not stripes; day/dusk/night.
  - Next in the plan: phase 5 performance, phase 6 game logic.

- **2026-09-15 Claude — polish phase 3: airfield mini-map. Unity EditMode 356/356.**
  - Unity regenerated the ATR v03 `.meta` GUIDs (source and StreamingAssets copies were
    committed identical, as with AIR-007 before); included here.
  - New bottom-left YPAD mini-map (`FieldMiniMap` pure helper + `AirsidePrototype.MiniMap`): the
    OSM runways, taxiways, aprons and terminals baked once into a texture; every on-field
    aircraft as a livery dot (yours larger, severity ring, selected yellow); the camera's
    ground footprint outlined with its focus point. Click a dot selects, click/drag elsewhere
    moves the camera (`AirsideCameraController.CentreOn`). N toggles; hides while overlays or
    help are open and whenever the window has no clear corner.
  - Packaged check open: map orientation matches the 3D view, drag feel, Retina crispness.
  - Next in the plan: phase 4 ground textures/graphics, 5 performance, 6 game logic.

- **2026-09-15 Cursor — YPAD surroundings P3: OSM land cover + arterial roads. Domain
  EditMode green; Unity EditMode pending Mac (`scripts/test-unity.sh` has no Unity here).**
  - **Player-visible:** Overview land around Adelaide Airport now shows real OSM parks,
    suburbs, car parks, sand/scrub and inland water (Patawalonga) instead of noise patches;
    arterial roads draw as dark ribbons. Coast/sea (P2) kept. Palette stays Airside /
    WLD-004 stylised (not photoreal).
  - **How:** new snapshot `docs/data/osm/ypad-landcover-2026-09-15.json`, generator
    `scripts/generate-ypad-landcover.py` → `AdelaideLandCover.cs` (260×260 @ 50 m + roads).
    `AirsideAdelaideSurroundings` tints from the grid and drops inland water; new
    `AirsideAdelaideRoads` builds ribbons. No simulation / save change.
  - **Evidence:** generator landmark probes (Royal Adelaide Golf=Park, Patawalonga=Water,
    Harbour Town=Parking/Commercial); `AdelaideLandCoverTests` 7/7; `scripts/test-domain.sh` 212/212 (Unity-only EditMode tests remain excluded from the headless harness — same gap as on main before this PR).
  - **NEXT:** Bailey to review overview look on Mac. Optional P4 polish (more road classes /
    labels) or P6 Hills backdrop. Packaged S1–S4 shots still open.

- **2026-09-15 Cursor — AIR-001 ATR 42-600 visual fidelity pass (v03). Unity EditMode
  pending Mac (`scripts/test-unity.sh` has no Unity binary here).**
  - **Player-visible:** Emu Air / player ATR silhouette tightened as a new v03 kit:
    four fitted cockpit panes with pillars, even Hangar-readable cabin windows, smoother
    blunt nose, compact nacelles blended into the high wing, clear fuselage-side main-gear
    sponsons (not Q400 nacelle gear), tighter wing-root / T-tail joins, six readable 3.93 m
    props. Envelope unchanged (22.67 × 24.57 × 7.59 m). Runtime prefers
    `mdl_atr42_starter_v03` with v02/v01 fallbacks.
  - **How:** new `scripts/generate-air-001-atr42-v03.py` (183 named meshes / 19,704 tris).
    Thumbnail regenerated from v03; StreamingAssets synced. Review board + multi-angle
    stills under `docs/art/candidates/` and `work/review/`. Q400 / Saab / 737 untouched.
    No simulation, catalogue id, save, reservation or schedule change.
  - **Evidence:** generator validate (exact bounds, tyres on y=0); offline multi-angle
    reviews; `scripts/test-air-001-atr42-v03.py` green. This environment cannot re-run
    `scripts/test-unity.sh` (Unity 6.3 missing).
  - **NEXT MILESTONE:** Bailey to review this ATR v03 PR, then reprioritise the next
    visual or systems slice. Decide the 50D/50E Q400 stand rule if a second Q400 is ever
    added.

- **2026-09-15 Cursor — AIR-005 737-8 visual revision (same asset id). Domain tests pending
  Unity EditMode on Mac.**
  - **Player-visible:** Wattlebird Jet's 737-8 silhouette is rebuilt in place: slender fuselage
    with fitted cabin glazing, pitched flight-deck panes, low swept wing with dual-feather
    winglets, large forward-hung turbofans with chevron nozzles, a deep wing-body fairing and a
    joined conventional tail. Hangar thumbnail regenerated from the runtime glTF. Envelope
    unchanged (39.47 × 35.92 × 12.42 m). Path stays `mdl_737_8_narrowbody_v01`.
  - **How:** rewrite of `scripts/generate-air-005-narrowbody-737-8.py` (204 named meshes /
    17,464 triangles, was 180 / 6,988). Fuselage loft interpolates stations in increasing-z
    order (`np.interp` requires that). StreamingAssets + Hangar thumb synced. No simulation,
    catalogue id, save, reservation or schedule change.
  - **Evidence:** generator validate (exact bounds, tyres on y=0, fuselage half-width ~1.88 m);
    offline before/after mesh reviews under `/opt/cursor/artifacts/737-8-*-review.png`. Unity
    EditMode / packaged overview-follow QA still open (no Mac build this session).
  - **NEXT MILESTONE:** Bailey to review this 737-8 visual PR, then reprioritise the next
    genuine aircraft slice (AIR-006 Dash 8 quality pass and AIR-007 Saab already on `main`).
    Decide the 50D/50E Q400 stand rule if a second Q400 is ever added.
- **2026-09-15 Cursor — AIR-006 Dash 8-400 visual quality pass (same asset id). Unity EditMode
  pending Mac re-run (`scripts/test-unity.sh` has no Unity binary here).**
  - **Player-visible:** QantasLink's Dash 8-400 silhouette tightened in place: continuous
    high-wing / fuselage saddle, single aerodynamic nacelle (intake → gear bay → exhaust),
    framed four-pane flight deck, pitched six-blade props with clear hubs/spinners, longer
    nacelle-mounted mains with open doors, soft fin-root fillet and rounded T-tail saddle,
    even cabin windows. Hangar thumbnail regenerated. Envelope unchanged
    (32.83 × 28.42 × 8.34 m). Path stays `mdl_dash8_q400_v01`.
  - **How:** further rewrite of `scripts/generate-air-006-dash8-q400.py` (182 named meshes /
    26752 triangles). Thumbnail colouring fixed so windscreen pillars / prop tips read at
    Hangar distance. StreamingAssets synced. Review renders in `work/review/`. No simulation,
    catalogue id, save, reservation or schedule change.
  - **Evidence:** generator validate (exact bounds, tyres on y=0); offline front/side/top/
    game-camera reviews. Prior Mac Unity EditMode on this branch: 331/331. This environment
    cannot re-run `scripts/test-unity.sh` (Unity 6.3 missing).
  - **NEXT MILESTONE:** Bailey to review this Dash 8 quality PR, then reprioritise the next
    genuine aircraft slice (AIR-007 Saab already on `main`). Decide the 50D/50E Q400 stand
    rule if a second Q400 is ever added.

- **2026-09-15 Claude — polish phase 2: status severity + stacked messages. Unity EditMode
  343/343.**
  - New pure `AircraftStatus`: HoldingShort / HoldingForLanding go Attention after 3 min and
    Warning after 10 min; a player aircraft needing a stand is Warning (others Attention).
    Fleet rows, the flight board phase and the player's field tags colour by severity
    (SafetyYellow / SignalRed); waiting rows show "waiting N min" and a bar filling towards the
    10-minute mark instead of no bar.
  - New `ToastQueue`: up to three messages stack (newest in the toast slot), repeats count
    "×N" instead of stacking, entries fade out, and the last ten appear under RECENT MESSAGES in
    the Flights panel.
  - Phase 1 merge note: Unity regenerated the AIR-007 Saab `.meta` GUIDs (source and
    StreamingAssets copies had been committed with identical GUIDs); those are in #254.
  - Packaged check open: severity colours readable on the dark panels; toast stack position in
    wide and narrow layouts.

- **2026-09-15 Claude — game polish pass, phase 1: HUD click-through fixes. Unity EditMode
  337/337 (after merging AIR-007).** Plan: `~/.claude/plans/woolly-noodling-finch.md` phases 1–6 (clicks, statuses,
  field mini-map, ground/graphics, performance, logic).
  - Field-tag pills and the toast now count as HUD (`_hudOverlays`, new pure `HudHitTest`), so
    clicking a tag no longer also 3D-picks behind it and dragging from a tag no longer pans.
  - A plain click on open ground clears the selection (camera and overlays stay put).
  - Packaged check still open: tag click, drag from tag, click-empty-ground deselect.

- **2026-09-15 Cursor — AIR-007 genuine Saab 340B visual (ADR 0050). Domain/EditMode harness run in this environment; Unity editor not available here.**
  - **Player-visible:** Rex's two Saab 340Bs now render as their own true-scale, compact
    low-wing turboprop with four-blade propellers, nacelle-mounted twin main gear and a
    conventional tail instead of borrowing the ATR. The Hangar type card is Genuine and
    uses a 480 × 320 transparent thumbnail rendered from the runtime model. Selection,
    marker, shadow and follow framing use the Saab footprint.
  - **Asset evidence:** AIR-007 is reproducibly generated by
    `scripts/generate-air-007-saab-340b.py`; exact 19.73 × 21.44 × 6.97 m bounds (standard
    wing, not the 22.75 m extended tips), 124 named meshes / 6,620 triangles, glTF/`.bin`
    runtime kit and editable FBX. Source and StreamingAssets copies match. Review renders
    under `work/review/saab-340b-review-*.png`.
  - **Invariants:** presentation-only type dispatch. No flight plan, airline, bay, route,
    schedule, timing, reservation, random draw, save field or migration changed. A true-scale
    low-wing primitive remains the missing-art fallback.
  - **Outstanding:** per Bailey's request, no app build or manual launch was performed;
    packaged overview/follow and day/dusk/night visual QA remains open. The 50D/50E Q400
    clearance shortfall from ADR 0049 is unchanged.
  - **NEXT MILESTONE:** with every live Adelaide type on a genuine model, pick the next
    one-at-a-time aircraft slice from ADR 0046 only after Bailey reprioritises — do not
    start a new family that is not yet in the simulation without that call. Decide the
    50D/50E Q400 stand rule if a second Q400 is ever added.

- **2026-09-15 Claude — wingtip clearance test fixed + aircraft dispatch tests. Unity EditMode
  331/331 (0 failed, 0 skipped).**
  - `Layout_ParkedAtrWingtipsKeepCodeCClearance` assumed the bay stop was the ATR's nose and used
    only the ATR span (it reported 16.7 m at 50D/50E). Replaced by
    `Layout_ParkedRegionalAircraftKeepCodeCClearance`: parked plan-view outlines (wing, fuselage,
    tailplane boxes from the runtime glTFs via new `AircraftModelBounds.TryMeasurePart`, root on
    the stop as drawn) for every bay pair and every ATR / Saab stand-in / Dash 8-400 pairing.
  - **Known limit it now exposes (needs Bailey's stand-assignment decision):** a Dash 8-400 on
    50D or 50E next to a turboprop on the other is only **3.3–3.5 m** apart (ICAO code C 4.5 m);
    two Q400s there would be 1.2 m (only one Q400 exists; the test guards that). Every other pair
    keeps ≥ 4.5 m (ATR–ATR at 50D/50E 5.7 m). A simple "no Q400 on 50D/50E" rule can strand an
    aircraft overnight with six aircraft on six bays, so no simulation change was made.
  - New `AircraftDispatchTests`: the real `BuildAircraftForType` builds each catalogue type with its
    own profile and drawn dimensions (placeholders draw at ATR size), AIR-006 has two six-blade
    propellers and is not ATR-sized, and QantasLink/Rex/Emu/Wattlebird/player fly their catalogue
    types with the matching visual profiles.
  - **NEXT MILESTONE (4)** unchanged: Saab 340B genuine model. Decide the 50D/50E Q400 rule first
    if a second Q400 is ever added.

- **2026-09-15 Codex — AIR-006 genuine Dash 8-400 visual (ADR 0049). Unity EditMode
  328/328 (0 failed, 0 skipped).**
  - **Player-visible:** QantasLink's existing Dash 8-400 now renders as its own true-scale,
    long high-wing turboprop with six-blade propellers, elongated nacelles, nacelle-mounted
    main gear and a T-tail instead of borrowing the ATR. Its Hangar type card is Genuine and
    uses a 480 × 320 transparent thumbnail rendered from the runtime model. Selection hitbox,
    marker, shadow and follow framing use the Q400 footprint.
  - **Asset evidence:** AIR-006 is reproducibly generated by
    `scripts/generate-air-006-dash8-q400.py`; exact 32.83 × 28.42 × 8.34 m bounds,
    139 named meshes / 6,728 triangles, glTF/`.bin` runtime kit and editable FBX. The
    OpenAI-generated image is retained only as a reviewed modelling candidate; the shipped
    geometry and thumbnail are project-owned. Source and StreamingAssets copies match.
  - **Invariants:** presentation-only type dispatch. No flight plan, airline, bay, route,
    schedule, timing, reservation, random draw, save field or migration changed. A true-scale
    primitive remains the missing-art fallback.
  - **Outstanding:** per Bailey's request, no app build or manual launch was performed;
    packaged overview/follow and day/dusk/night visual QA remains open. The only catalogue
    placeholder is now the Saab 340B.
  - **NEXT MILESTONE (4):** implement the Saab 340B as the next complete one-at-a-time
    aircraft slice with its own model, thumbnail, type profile and evidence. Do not start the
    previously listed A321neo until Bailey reprioritises it after the live stand-ins are closed.

- **2026-09-15 Claude — aircraft catalogue + Hangar types + player/AI presentation (ADR 0048).
  Unity EditMode 327/327 (0 failed, 0 skipped). Milestone 1 (Gate 13 737 loop) was already
  merged and verified (#246); this is milestone 2.**
  - **NEXT MILESTONE (3): create/integrate a genuine Dash 8-400 visual** — a true-scale,
    unbranded runtime model (32.83 × 28.42 × 8.34 m, within ±5%), thumbnail regenerated with
    `scripts/render-aircraft-thumbnails.py`, `AircraftCatalogue.Dash8Q400` set to Genuine with its
    model/thumbnail paths, and rendered instead of the ATR stand-in. Then milestone 4 (Saab 340B).
  - **Player-visible:** Hangar has *Fleet* (YOUR AIRLINE / OTHER OPERATORS) and *Aircraft types*
    tabs; type cards show a thumbnail rendered from the runtime model (ATR 42-600, 737-8) or a
    labelled PLACEHOLDER (Saab 340B, Dash 8-400), with role, dimensions, cruise, planning range,
    stand class, count at Adelaide/yours and model status. The player's aircraft carry a livery
    accent and "YOURS" badge in the Hangar, fleet panel, Flights board (now two sections),
    selection card and field tags; AI traffic is quieter (62%) there and on the route map.
  - **Data:** `Domain/AircraftCatalogue` is the single source (AircraftType named types are
    catalogue properties). Sources in `docs/data/AIRCRAFT_SPECIFICATIONS.md`. Dash 8-400
    practical range 1,800 → 1,500 km (below 1,596 km manufacturer); QantasLink routes still
    reachable. `AircraftModelBounds` checks genuine models ±5%.
  - **Outstanding:** packaged visual QA of the Hangar tabs, badges and thumbnails (no build
    requested). Saab 340B manufacturer range and 737-8 manufacturer cruise not yet recorded.

- **2026-09-15 Claude — Gate 13 737-8 operations (ADR 0047). Merged to `main` by PR #246
  (`bbe76b0`); feature branch deleted; Unity EditMode 319/319 (0 failed, 0 skipped),
  re-verified on merged `main`.**
  - **Player-visible:** the AIR-005 preview is replaced by one operational AI 737-8, **VH-WTJ**
    of fictional **Wattlebird Jet** (teal, no logo). It taxis in nose first to Gate 13, parks,
    pushes back tail first onto T1, disconnects the tug, taxis out forward and departs on a
    fixed MEL/SYD/MEL/BNE/SYD/PER/MEL/CBR rotation inside the 06:00–21:00 AI day. It is
    selectable/followable with registration, airline, type and live state (selection card now
    shows the type), and appears on field tags and the Flights board ("Gate 13").
  - **How:** generator adds `AdelaideLayout.TerminalGates` routes and a derived "Gate 13 apron
    link" filling the 50 m unpaved band between T1/T2 and the OSM apron edge (no overlap).
    `AdelaideGround` dispatches bay vs gate (`Bay()` throws for a gate); gate legs steer the
    body from a main-gear point 19 m behind the nose (`GroundLegPart.TrackMetres`), regional
    legs unchanged. `AirlineOperations.StandFits`/`FreeStandsFor`; `FreeStands()` = regional
    bays. Gate held while parked, taxiing in and through taxi-out; lead-in held during taxi
    in/out; derived from state (`GroundResourceHolder`), so no save change. The jet uses a
    deterministic rotation (no random draws), so regional sequences are untouched.
    `AddMissingTerminalOperators()` runs in `StartAtAdelaide` and after catch-up on load.
  - **Evidence:** new `TerminalGateOperationsTests` (14): one 737 at Gate 13, gate never a
    bay, type-separated stands, nose + main gear on rendered pavement for the whole taxi-in and
    taxi-out, no teleport/nose snap (0.1 s samples), nose-in stop heading, tail-first pushback
    with a <2° seam, reservations over 3 simulated days, second-jet refusal, save + catch-up ==
    live, old-save backfill idempotent, AIR-005 profile. Offline route plots
    `work/review/gate13-routes*.png` (not committed). Regional tests unchanged except two that
    counted every AI aircraft as regional.
  - **Outstanding:** packaged visual QA of Gate 13 taxi-in, pushback and taxi-out (no build
    requested). Known limits in ADR 0047: ATR circuit speeds and FL250 cruise cap for the jet,
    no cross-traffic taxiway reservations, no Gate 13 paint. Next asset per ADR 0046: AIR-006
    A321neo (not started).

- **2026-09-15 HANDOFF → next aircraft slice (Codex). AIR-005 Boeing 737-8-class is merged
  on `main` by PR #244 (`555587f`); Unity EditMode 305/305.**
  - **Player-visible outcome:** a new original, unbranded, true-scale 737-8-class jet is
    parked at Adelaide's real Gate 13 nose-stop. It is clickable and followable, with a
    narrowbody-sized selection marker, identity card, pick volume, ground shadow and camera framing.
    Overview, `R` and `Esc` clear its selection. It is silent while parked.
  - **Scope/invariants:** Gate 13 is a presentation anchor only and is deliberately kept
    out of regional bays 50A–50F. No flight schedule, save schema, stand/runway
    reservation, taxi timing or regional traffic changed. The jet cannot move until its
    uncovered apron lead-in and pushback turnout have explicit pavement and routes.
  - **Asset evidence:** AIR-005 is generated reproducibly by
    `scripts/generate-air-005-narrowbody-737-8.py`; 39.47 m long × 35.92 m span × 12.42 m
    high, 180 named meshes / 6,988 triangles. Source and StreamingAssets copies match.
    The OpenAI-generated image is retained only as a reviewed modelling candidate; the
    runtime model is project-owned procedural geometry.
  - **Verification:** Unity 6.3 EditMode **305/305**; Python generators compile; generated
    layout and runtime art sync complete. Per Bailey's standing preference, the app was
    **not rebuilt or manually launched**, so packaged overview/follow day/dusk/night QA
    remains open.
  - **Next:** add AIR-006 Airbus A321neo as the next one-at-a-time model. Separately, make
    Gate 13 operational only after adding its paved lead-in, terminal-stand resolver,
    pushback turnout and reservation tests. Then continue A220-300, 787, A320, Dash 8 Q400
    and Saab 340B one at a time.

- **2026-09-14 HANDOFF → ChatGPT (from Claude). State at `de6207a`, everything merged, no open
  branches or PRs, `scripts/test-unity.sh` 302/302.**
  - **What exists now:** live-time Adelaide (YPAD) airline game on the real OSM layout; player
    ATR plus AI Emu Air (2 ATR), Rex (2 Saab 340B) and QantasLink (1 Dash 8-400) on six real
    regional bays 50A–50F; flight planner (Tab) with aircraft switcher, destination list, trip
    timeline; route map with live plane icons, climb/cruise/descent altitude, tracking and deep
    zoom; field tags (L); Flights board (T), Hangar (H), Dev tools (F8), Controls (F1);
    Gulf St Vincent coast + OSM credit on the overview.
  - **Bailey's standing preferences:** economy/money is deferred; do **not** work on or re-raise
    first-session pacing (live time is deliberate); **don't rebuild or launch the app unless
    Bailey asks in that message** — verify with `scripts/test-unity.sh`; ship each change as a
    feature branch → PR → merge → delete branch, updating GAME.md + CHANGELOG.md in the same
    commit (main is protected: `gh api repos/Bazlinka/Airside/pulls -X POST …` then
    `gh pr merge N --merge`).
  - **Not yet seen in a packaged build:** runway exit roll (#237), quickest-stand button and
    bay names (#239), Emu Air hours (#240), away summary (#241), Rex/QantasLink (#242). The
    current `work/builds/Airside.app` predates these.
  - **Open follow-ups:** HUD-fit screenshots at 1280×720 / 1440×900 / 2560×1600 using the
    `-airsideSoak -airsideReviewPanel … -airsideReviewShot …` flags (needs a build); 30-min
    soak; faint line at the airfield ground edge; surroundings plan P3 (land cover, Patawalonga /
    West Lakes) in `docs/plans/ypad-surroundings-plan.md`; jets at terminal gates 13–29 (needs a
    narrowbody model + gate routes); real airline names must be reviewed before any public
    release (register row DAT-AIRLINES-REAL).
  - One old stash remains (`stash@{0}`, Codex fix superseded by #230) — safe to drop when
    Bailey agrees.

- **2026-09-14 Claude — regional carriers (Bailey: "more air traffic"; chose regional first, real
  airlines):**
  - Layout generator now emits BAY-5/50E and BAY-6/50F (existing four bays byte-identical).
    50G skipped: on the T4 bend, its generated lead-in starts on grass (checked with a route
    plot, `work/review/regional-bays-routes.png`). Bay spacing test replaced by a parked-ATR
    wingtip clearance check (min 16.7 m, 50D/50E; threshold 4.5 m code C).
  - `AircraftType.Saab340` (SF34, 500 km/h, 1 000 km) and `Dash8Q400` (DH8D, 667 km/h,
    1 800 km), both rendered with the ATR model; `TryFromId` knows all three.
  - `Airline.Rex()` (REX) / `Airline.QantasLink()` (QLK); `AirlineOperations.RegionalCarriers`
    (Rex VH-ZRC/ZRD, QantasLink VH-QOK), `AiNetworkFor` per carrier, opening departures at
    10/20/32/45/58 min. `AddMissingRegionalCarriers()` runs in `StartAtAdelaide` and after
    away catch-up in `ContinueAirline` (idempotent; only free stands; toast when joined) —
    `AirlineSave.Restore` itself is unchanged.
  - Only Emu Air gets the Emu decal now; the real carriers are colour-only (no logos).
    Register row DAT-AIRLINES-REAL flags the names for review before any public release.
  - Tests: new `RegionalCarriersTests` (4); stand markings expect 6 bays; Emu-specific tests
    select Emu by name. EditMode **302/302**. Not rebuilt.
- **Next:** jets at the terminal gates (13–29) need a narrowbody model and gate routes.

- **2026-09-14 Claude — away summary:** bay label lookup moved into Simulation
  (`AdelaideGround.StandLabel`, used by `AwaySummary` and delegated to by `StandNames.Display`);
  `DrawAwaySummary` measures lines with `CalcHeight` instead of a fixed 40 px row.
  EditMode **298/298**.

- **2026-09-14 Claude — Emu Air timetable:** `AirlineOperations.ScheduleAiDeparture` draws once
  (as before) over `AiNetwork` weights (KGC 3, PLO 3, WYA 2, MGB 2, MEL 2, CED/CPD/MQL/BHQ 1)
  and passes the ready time through `AiDepartureWithinHours` (06:00–21:00 Adelaide via new
  DST-aware `AirlineClock.AtLocal`). Opening departures at +10/+20 min are unchanged so a new
  game still sees traffic. No save change. `AirlineSoakTests` stall rule now exempts a parked
  aircraft with a departure booked within 12 h; new `AiTimetableTests` (4 simulated days from
  a near-midnight start). EditMode **298/298**.

- **2026-09-14 Claude — stand UX + board altitude:** `Presentation/StandNames` maps BAY-n to
  the real bay reference for every player-facing string (status, planner, toasts, guide,
  Flights board). AwaitingStand row adds a full-width quickest-free-bay button (shortest
  `TaxiInSecondsTo`, guide highlight moved to it) above the per-bay buttons. Flights board
  aircraft column appends `EnrouteAltitudeText`. DevTools keeps raw ids on purpose.
  EditMode **296/296** (`StandNamesTests` 2).

- **2026-09-14 Claude — HUD allocation pass:** base HUD styles (`panel/title/button`,
  `label/small/smallButton`) are cached fields; derived styles go through
  `AirsidePrototype.Styled(basis, variant, make)` (cache keyed by basis reference + variant,
  non-capturing lambdas). `FlightPlanner.DestinationsFor` has an in-place overload and a
  static comparer. EditMode **294/294**; not profiled in a build (no rebuilds unless asked).

- **2026-09-14 Claude — runway exit roll:** `CircuitProfile.RunwayExitKnots = 12`; rollout
  decelerates touchdown → 12 kt (rollout time and airspeed schedule derived from it),
  `AirsideFlightPath.Landing` distance uses the same exit speed, and
  `AdelaideGround.Vacate` path is entered at that speed. `GroundPath.SampleAt(0)` returns the
  entry speed (was always 0). Demo circuit (pre-airline background) now ends its landing at
  12 kt. Tests updated: rollout timing/decel, landing end speed, landing→vacate seam.
  EditMode **294/294**. Not rebuilt (Bailey: no rebuilds unless asked).

- **2026-09-14 Claude — review-shot flags:** soak mode accepts `-airsideReviewPanel` and
  `-airsideReviewShot <png>` (+ `-airsideReviewDelay`), reading the frame back via
  `ReadPixels` at end of frame (the ScreenCapture module is not in the project). Intended for
  the PROJECT_PLAN HUD-fit check at 1280×720 / 1440×900 / 2560×1600; not yet run (Bailey
  asked for no rebuilds / app launches). EditMode **294/294**. Map double-image fix (#235)
  is in `work/builds` (rebuilt at Bailey's request).

- **2026-09-14 Claude — route map double image fix (Bailey: "zooming out and in i see like two
  copies of australia"):** `AustraliaMapLens.ClampCenterToView` clamped to
  `min+half..max-half` even when the view was wider than the map (min above max), so each
  call flipped the centre between the two ends. Harmless while zoom only ran on scroll
  events; the eased zoom (#233) calls it every frame, so two alternating copies were drawn.
  Now centred on that axis (`ClampAxis`), test `ZoomedOut_CentreIsStableFrameToFrame`.
  Evidence: EditMode **294/294**. Packaged rebuild pending.

- **2026-09-14 Claude — OSM credit + Gulf St Vincent coast (Bailey: "overview only for now -
  do the attribution and coast first"):**
  - **Attribution (plan P0):** "Map data © OpenStreetMap contributors" drawn bottom-right on
    every frame (`DrawMapCredit`, `MapAttribution`). The ODbL gap is closed on screen; a
    credits screen is still needed before any public build.
  - **Coast (plan P2):** `scripts/generate-ypad-coast.py` (imports the layout generator's
    runway frame) → `Simulation/AdelaideCoast.cs`: real OSM coastline, 293 points → 126 at
    6 m, nearest 2.39 km from the runway midpoint, plus a closed sea polygon.
    `CoastGrid` (UnityEngine-free) builds a 24 × 24 km rectilinear grid (60 m within 5.2 km,
    320 m beyond) with lines exactly on the airfield ground rectangle, classifies sea by
    scanline parity and coast distance via a segment bucket. `AirsideAdelaideSurroundings`
    turns it into one vertex-coloured mesh: meets the airfield edge height exactly and tucks
    40 m under it, eases to the plain, 150 m beach, flat sea 5.2 m below the pavement;
    new `Airside/Surroundings` shader (sun + SH ambient, fog, fade to fog colour before the
    10 km far clip).
  - **Found and fixed:** `Airside/AdelaideGround` was never in the player build (not
    referenced, not in Always Included Shaders — the old build log never compiles it), so
    every packaged build showed the flat grass fallback slab. Both shaders are now in
    `GraphicsSettings` Always Included. Its lighting (`+0.28` multiplied by the ~2.0 sun)
    bleached the ground once it did render — now sun + `SampleSH` ambient + fog, tint 0.59.
    Surroundings colours were tuned against measured packaged-build pixels so the field
    edge blends.
  - Field tags that would overlap (aircraft parked side by side) now stack upward.
  - Review shots: `-airsideOverviewYaw/-Pitch/-Distance` re-aim the overview from the
    command line (sea view: `-airsideOverviewYaw 310 -airsideOverviewPitch 32
    -airsideOverviewDistance 3200`, with `-airsideSoak` for a separate save and
    `-screen-fullscreen 0` so `screencapture -l` can grab the window).
- **Evidence:** Unity 6.3 EditMode **293/293** (`CoastGridTests` 6); Mac build OK
  (`work/builds-next`); packaged screenshots `work/review/ypad-coast-overview-sw.png` (sea,
  beach, blended edge) and `work/review/ypad-before-shader-fix.png`.
- **Next:** a faint line remains along the airfield ground edge (the ground mesh's own
  boundary lip). Plan P3 (OSM land cover: golf courses, car parks, Patawalonga / West Lakes
  water) is the next overview step; Hills backdrop P6 after that.

- **2026-09-14 Claude — zoom feel, altitude, taxi speeds + surroundings plan
  (Bailey: "zoom is really quick … altitude needs to be integrated accurately … check
  realistic taxi speeds … plan map data so the ground looks like Adelaide"):**
  - **Zoom:** field camera scroll was applied instantly at ~30 % per wheel notch; now queued
    in log space (≈11 % per notch at any distance, per-frame cap for trackpads) and eased
    in over ~0.3 s. Scrolling over a HUD panel no longer also zooms the camera. Route map
    zoom (which multiplied on every trackpad event) uses the same eased model
    (`MapZoom`, ≈12 % per notch).
  - **Altitude:** new `Simulation/EnrouteProfile` shapes each away leg inside its existing
    `LegTiming` duration — climb 1 200 ft/min at 65 % speed, cruise level from leg length
    (≈6 000 ft + 25 ft/km, 8 000–FL250), descent 1 500 ft/min at 80 %, cruise speed solved
    so distance flown equals the leg (MEL ≈ FL220 / ~297 kt). It starts at the field
    departure's exit height and ends at the approach start height, so field ↔ map is
    continuous. Map icons now move by distance flown (slower in climb/descent) and label
    "FL220 ▲ · 297 kt · 613 km · lands 15:52"; fleet status lines show the level. The HUD
    readout (widened to 230) shows height above the field with ▲/▼ once airborne.
  - **Taxi speeds:** 15 kt straight kept (realistic), cornering lateral limit 0.35→0.5 m/s²
    (~10 kt round a 45 m fillet instead of ~6 kt), new speed zones: 10 kt for the first
    160 m of taxi-out and the last 160 m of taxi-in (apron lane), 5 kt for the last 45 m
    onto the stand. Zone edges get an inserted path point so the limit holds from that
    metre. Existing taxi-time range tests still pass.
  - **Plan:** `docs/plans/ypad-surroundings-plan.md` (research agent) — recommends
    stylised-but-grounded OSM surroundings (coast/sea first, land cover, roads, extruded
    buildings, GA DEM Hills silhouette), no Google/Cesium/Street View (licence + online-only
    + uncanny), Sentinel-2 tint only as optional last step. P0 is showing the OSM credit,
    which is currently not displayed anywhere (an ODbL gap).
- **Evidence:** Unity 6.3 EditMode **287/287** (new `EnrouteProfileTests` 7); Mac build to
  `work/builds-next/Airside.app` succeeded (Bailey's copy in `work/builds` was running).
  **Not yet eyeballed in the packaged app** — zoom feel is a by-hand check.
- **Next:** Bailey tries zoom/altitude in `work/builds-next`; answer the plan's open
  questions (surroundings scope, Mapland licence email, priority vs PROJECT_PLAN); P0
  attribution is small and should go in regardless. Not done: runway vacate still starts
  from a stop at the rollout end (real aircraft turn off at ~15 kt).

- **2026-09-14 Claude — live flights on the map, field tags, planner availability
  (Bailey: "do both and keep going … a mini plane logo on the map … zoom in and see it
  moving live"):**
  - **Route map:** every off-map flight is a livery-coloured plane silhouette (generated
    texture, `PlaneIcon()`) pointing along its **great-circle** track, positioned from
    sub-second `_preciseTime` so it glides rather than ticks. Flown part of the route solid,
    remaining faint; label "VH-EMA → MEL · 613 km to go · lands 15:52". Zoom now goes to
    60× (was 7×) with bigger scroll steps; clicking a plane selects it and **tracks** it
    (map stays centred, zoom ≥14×); dragging or *Stop tracking* / *Zoom out* releases.
    Selecting an away aircraft anywhere (fleet, hangar, flights board) opens the map
    tracking it. All map lines are now clipped to the map (zoomed coastlines used to spill
    over the HUD); off-map labels/dots are skipped.
  - **Field tags:** a registration pill (+ phase for your aircraft: free / planned /
    taxiing / needs stand …) floats over every aircraft on the field, fades out under
    140 m camera distance, hides under HUD panels, click selects. **L** toggles.
  - **Planner availability:** planning a busy aircraft shows "VH-X is free on BAY-2 —
    Plan VH-X instead", or when none is free, "Back over Adelaide about HH:MM".
  - Pure rules: `RouteMap.cs` (progress, great-circle point, segment clip) +
    `FlightPlanner.NextFreeAircraft` / `ExpectedBackAt`; `RouteMapTests` (3) and 2 more
    `FlightPlannerTests`.
- **Evidence:** Unity 6.3 EditMode **280/280**; Mac build succeeded; the packaged app
  (being played by Bailey at the time) showed VH-EMA's plane icon on the MEL great circle
  with distance/lands label, the planner availability hint and Zoom out. Field tags and
  tracking not yet eyeballed by Claude.
- **Next:** Bailey feedback on tags/tracking. Coastline is coarse at deep zoom — a
  denser outline (or city/airport detail) would make 30–60× look better.

- **2026-09-14 Claude — flight planner rework (Bailey: "flight planning, which plane gets
  selected, switching between them, choosing where to fly to isnt too good"):**
  - **Root cause of bad destination picking:** the map consumed every left press to start
    panning, so the 24 px invisible dot buttons rarely got the click. Now a press only pans
    after the 4 px drag threshold; a release before that is a click that picks the nearest
    aircraft or destination within 16 px (nearest wins, so Kingscote/Port Lincoln beside ADL
    are separable). Hovered dots grow, show a route preview and a name · km · time tip.
  - **Tab opens a Flight planner**, not a bare map: aircraft switcher (`<` `>` or `[` `]`),
    a destination list (reachable first, nearest first, km + flight time, locked group),
    then per destination: pushback in 3 min/15/30/1 h/2 h/4 h plus −/+5 min, and a trip
    timeline (pushback, airborne, lands, departs, back at Adelaide) before *Schedule*.
    An aircraft with a booked flight reopens on it ("Change plan" / "Update plan" /
    "Cancel booked flight"). Detail pane scrolls on short windows.
  - **Selection is one model:** the planner opens on the selected player aircraft, else the
    first parked with nothing planned; switching aircraft in the planner selects it and the
    camera follows, planner stays open. Outside the planner `[` `]` cycle all aircraft.
    Clicking an aircraft dot on the route map selects it. Esc closes an open panel before
    clearing the selection. The selection card hides while a panel is open (it covered
    the planner's buttons).
  - Pure rules in `Presentation/FlightPlanner.cs` (UnityEngine-free) with
    `FlightPlannerTests` (6).
- **Evidence:** Unity 6.3 EditMode **275/275, 0 failed**; `scripts/build-mac.sh` succeeded;
  packaged app driven by hand: Plan flight → list → map dot click on MEL → timeline →
  drag-pan → All destinations, camera followed VH-PAX. No flight was scheduled in that pass
  (Bailey's save was left untouched apart from autosave).
- **Next:** Bailey plays the planner in the built app. Candidates: aircraft on the overview
  are hard to see without following; a planner "next free aircraft" hint when all are busy.

- **2026-09-14 Claude main compile repair + branch collation (Bailey: "collate, merge, main, delete branches, build"):**
  `main` at `9084042` (Cursor stack #223–#229) **did not compile in Unity** — the stack
  was only checked with `scripts/test-domain.sh`, which does not build Presentation.
  Two breaks, both fixed:
  1. `AirsidePrototype.FleetVisuals.cs` `Object.Destroy` was ambiguous (`System` +
     `UnityEngine`); now `UnityEngine.Object.Destroy`. Same fix Codex had uncommitted on
     `fix/direct-aircraft-selection-build`.
  2. `AirsidePrototype.Airline.cs` map drag-panning used `_mapPanning` / `_mapPanGui`,
     which were never declared (not even on `cursor/immersive-map-hangar-601f`); declared.
  Also committed Unity's regenerated GUIDs for `ControlsHelp`, `DevTools` and their tests:
  the hand-written `.meta` GUIDs collided with the pushback tug, wheel chocks, safety cone
  and GPU cart prefabs.
  **Collation:** every other branch's content was already on `main` —
  `cursor/direct-aircraft-selection-601f`'s extra commit is the pre-squash copy of #223,
  `codex/adelaide-airside-realism` is superseded by the real YPAD layout, the rest are
  merged. All branches and extra worktrees removed.
- **Evidence:** Unity 6.3 EditMode **269/269, 0 failed, 0 skipped**; `scripts/build-mac.sh`
  succeeded. Packaged launch reached "airline started", but no frames were rendered
  afterwards because the Mac display was not visible (main thread idle waiting on frame
  present, 0.1 % CPU) — **visual/packaged pass of #223–#228 still not done**.
- **Next (Bailey):** open the built app with the screen awake: click-select aircraft,
  Map zoom/pan (Tab), Hangar (H), Flights (T), Dev Tools (F8), Controls (F1), dusk/night.

- **2026-09-14 Cursor — UI stack merged to `main` (#224–#228):**
  All of the following are on `main` now:
  - Zoomable Australia map + Hangar (H) — #224
  - Flights board (T) — #225
  - Dev Tools (F8) — #226
  - Controls help (F1) — #227
  - Live day/night lighting (daylight pin off) — #228
  Presentation/UI only across the stack; schedules, reservations, saves and
  airport geometry unchanged (except lighting follows the existing 24 h clock).
- **Evidence:** `scripts/test-domain.sh` peaked at **197/197** on Linux before
  merge. Unity EditMode / Mac packaged visual check (esp. night) still outstanding.
  No manual playtest claimed.
- **Next — Bailey:** Mac Unity + packaged pass (map/hangar/flights/dev tools/
  controls + dusk/night), then external playtest zip when free. Economy still
  deferred (ADR 0045).

- **2026-09-14 Cursor direct aircraft selection (`cursor/direct-aircraft-selection-601f`):**
  clicking a visible aircraft on the Adelaide field selects and follows that exact
  transform. Invisible `AircraftPick` proxies handle the pick; fleet-panel selection
  remains with a whole-row Select affordance. Merged via #223.

- **2026-09-14 Codex selectable aircraft (`codex/selectable-aircraft`):** fleet
  registrations are now presentation-only selection controls. Selecting an aircraft
  that is at Adelaide follows that exact 3D aircraft and highlights a compact identity
  card; selecting an off-map aircraft opens its route tracker instead. Overview, `R`
  and `Esc` clear the selection. Simulation, schedules, reservations, saves, aircraft
  models and airport geometry are unchanged.
- **Task packet / acceptance:** player-visible outcome is exact player-or-AI aircraft
  selection from the fleet panel; scope is airline HUD, fleet-view lookup and existing
  follow camera only; deterministic simulation and save invariants remain untouched.
  Acceptance is exact-transform follow, off-field rejection, selected-state identity,
  and a clean overview exit.
- **Evidence:** Unity EditMode **243/243**, including exact registered-target and
  off-field rejection checks. Per Bailey's preference, no manual playtest was run.
- **Watch — unverified in packaged play:** Bailey could not see or use the fleet-panel
  registration click target/highlight/details card in the packaged app. Treat that
  slice as incomplete; direct 3D selection above is the primary fix.

- **2026-09-14 Codex YPAD regional-stand wayfinding (`codex/ypad-wayfinding`):**
  the real 50A–50D regional bays now have procedural yellow lead-in lines, stop
  bars and Unity-rendered identifiers. `AdelaideStandMarkings` derives every
  position and label from Claude's generated `AdelaideLayout`; no stand coordinate
  is copied and no external asset is added. Simulation, routes, timing,
  reservations, saves and HUD are unchanged.
- **Evidence:** clean inherited baseline **238/238**; after implementation Unity
  EditMode **241/241**, including exact bay/reference, stop-point and finite-geometry
  checks. Per Bailey's request, no manual/external playtest was run; packaged
  camera-matrix verification remains outstanding.
- **Next:** merge/review `feature/ypad-real-layout` and this dependent branch in
  order. Then inspect the stand paint in the packaged overview/follow camera pass
  before adding more taxiway signs or apron detail.

- **2026-09-14 Claude real YPAD layout wired in (Bailey: "use the real OSM layout and wire it in", `feature/ypad-real-layout`):**
  the OpenStreetMap layout is now the single source of truth for Adelaide airside.
  - **Rendering:** `AirsidePrototype.YpadPavement.cs` builds every OSM taxiway as a
    ribbon with rounded joints and sealed shoulders, the real apron polygons
    (ear-clipped), yellow centrelines, hold bars at the 14 real holding positions, and
    extruded terminal / RFDS footprints. The rectangle-and-fillet skeleton is deleted.
    12/30 sits at its real crossing (centre 253, 410; yaw 73.2°, ~377 m NE of the
    05/23 midpoint). Ground and plateau grew to cover it (3 900 × 2 800 m).
  - **Ground motion:** `Simulation/GroundMotion.cs` gives every route a speed profile —
    15 kt taxi, 2 kt pushback, 8 kt lineup, cornering speed from turn radius at
    0.35 m/s² lateral, accel/brake limits — and durations come from it
    (`AdelaideGround`). Taxi-out 50D–50A ≈ 8–9 min (pushback, 25 s tug disconnect,
    3.3–3.4 km via T4-K-A-F2-F3-F6), taxi-in ≈ 4 min via E2-E1-A-K-T4, vacate E2 and
    lineup at the 05 threshold. Replaces the fixed 7/5 min, 60/90 s constants.
  - **Bays** BAY-1..4 = real 50D, 50C, 50B, 50A; pushback is a tail-first curve onto T4.
  - **Takeoff** now rolls from the 05 threshold (`CircuitProfile.TakeoffStartX` −1 500);
    roll/climb distances unchanged. The demo circuit keeps rolling from where it
    stops via `AirsideFlightPath.CircuitTakeoffOffsetX`.
  - **Fixed in passing:** the oriented-strip distance used the wrong rotation sign for
    Unity yaw (invisible while 12/30 was centred on the origin).
  - Default overview centres on (150, 350) so the terminal is in frame.
- **Evidence:** `scripts/test-unity.sh` exit 0, EditMode **238/238** (new
  `GroundMotionTests`; `AdelaidePavementTests` rewritten for the real layout: 12/30
  crossing, terminal side, apron strip clearance, routes on pavement/plateau and
  joined end to end, bay spacing). Packaged app: overview from the south-west matches
  Bailey's aerial photo (12/30 crossing the upper third, terminal beyond it left of
  05/23, RFDS left, GA apron top-left, F looping round the 05 end); VH-PAX pushback
  at 2 kt tail-first onto T4 past a parked Emu Air ATR, 0 kt tug pause, then taxi on
  the yellow centreline at 11 kt through the curve.
- **Codex branch `codex/adelaide-airside-realism`:** superseded — its
  `AdelaideAirsideLayout` constants describe the old invented geometry. Merging it
  now would conflict with this in `AirsidePrototype.FleetVisuals.cs` (its constants
  block no longer exists here) and `AirsideAdelaidePavement.cs`.
- **Next:** watch a full real-time arrival (E2 vacate, stand, taxi-in) and a departure
  through lineup and takeoff at the 05 threshold; then AI timetable + all flights on
  the map, dev test tools, hangar screen (Bailey's list).

- **2026-09-14 Claude live real time, typing, engine start, intro (Bailey's requests, `feature/live-real-time`):**
  - **Live Adelaide time (Bailey chose "always real time").** `AirlineClock` is now an
    instance with a UTC epoch: one simulated second per real second, shown in
    Adelaide local time with daylight saving. Pause, 2×–60× and Skip are gone; the
    menu no longer stops anything. Saves are **v3** (`EpochUtcTicks`); v2 aligns so
    its save moment reads as when it was saved; v1 uses a default epoch. Continue
    always syncs to now (`AwayCatchUp.LiveTarget`, which re-aligns past the 7-day cap
    or a backwards device clock). Emu Air's first departures are at +10 and +20 min.
  - **Typing no longer moves the camera:** `AirsideCameraController.KeyboardCaptured`
    while the start/away panels are up or a text field has focus.
  - **Engine start/shutdown:** `EngineStartSequence` — beacon T−3 min, doors close
    T−160 s, No.2 (right) T−120 s, No.1 (left) T−70 s, 30 s spool; after parking No.1
    then No.2 wind down, beacon off, doors open. Drives each propeller, beacon, doors,
    heat shimmer and engine note. Parked fleet props no longer spin (they did before,
    via the demo circuit's SkipGroundTaxi RPM). Soonest departure is "In 3 min".
  - **Launch intro:** 7 s eased camera glide in with wordmark and live local time;
    any key/click skips; capped timestep so it is not spent behind the loading hitch.
- **Evidence:** Unity EditMode **242/242** (new: `LiveClockTests`, `EngineStartSequenceTests`).
  Packaged app: intro plays; holding D/W in the name field leaves the camera still;
  Bailey's v2 save continued at 10:04 Mon 14 Sep matching the Mac clock; soak log
  showed beacon T−3, R spool 0.74 → 1, L 0.26 → 1, then TaxiOut; sub-second frames
  show blades turning at the stand. (Integer-second screenshots strobe-alias a
  6-blade prop at idle — 2,520°/s is a multiple of 60° — and look frozen.)
- **Next:** align the airfield with real YPAD geometry (OSM), then derive every
  ground speed from real route lengths (Bailey, same day).

- **2026-09-14 Claude soak and playtest packaging (PROJECT_PLAN step 3 acceptance, `test/soak`):**
  - `AirlineSoakTests`: 30 simulated days, player flown continuously, checked at
    every event for runway double-occupancy, stand double-booking, stuck waits and
    the airport stopping; every aircraft flies > 60 trips.
  - Soak mode for packaged builds (`-airsideSoak -airsideSoakMinutes N`, see Run it).
  - **Packaged soak, 30 min real time:** COMPLETE, no stalls, 120 fps throughout,
    managed heap flat at 4 MB, 0 exceptions, 0 "Not allowed". It ran at 1× after
    the first landing because the soak driver did not re-assert 60× after the
    game's deliberate drop to 1× for a stand choice — driver fixed.
  - **Packaged soak, 10 min at 60×:** 10 simulated hours, 7 trips, COMPLETE, no
    stalls, 120 fps, 0 exceptions. Logs kept in `work/soak/` (not committed).
  - `scripts/package-playtest.sh` builds from a clean tree and zips
    `work/playtest/Airside-<commit>.zip` with `docs/testing/PLAYTEST_TESTER_NOTE.md`
    as "READ ME FIRST" (unsigned-app open steps, no-coaching brief, five questions).
- **Next — Bailey:** send the zip to one person who has not seen the game (PROJECT_PLAN
  step 4); observe without coaching; bring back their answers.

- **2026-09-14 Claude first-flight guide (PROJECT_PLAN step 3, `feature/first-flight-guide`):**
  a numbered card under the clock walks a new player through one round trip —
  plan → wait for departure → departing → away → landing → choose a stand →
  taxiing in — with a pulsing highlight on Plan flight and the stand buttons, and
  a toast when the first trip completes. `FirstFlightGuide` derives the step from
  fleet state only (no save field), so Continue lands on the right step and the
  guide never returns once any player aircraft has flown a trip.
  `AirlineHudLayout` gained a guide slot (map and stacked fleet move below it),
  tested at all six sizes with and without it. **Readability:** the map panel is
  now near-opaque (runways read through it) and the ADL label sits left of its dot.
- **Evidence:** Unity EditMode **233/233**. Packaged app: new airline → step 1 with
  Plan flight highlighted → map → Melbourne "Now" → step 3 Departing; Continue
  restores step 3; map readable.
- **Next:** 30-minute soak, then a packaged build for one external playtester.

- **2026-09-14 Claude real 24-hour day (Bailey's decision, `feature/real-24h-day`):**
  `DayCycle.DaySeconds` is now 86,400 (was 1,200), so lighting time, the airline
  clock and real-length flights agree; `AirlineClock` reads it. Weather now changes
  hourly (was every 5 simulated minutes, which flickered at 60×). Lighting is still
  pinned to day by `PinDaylightPresentation` pending the night-lighting rework —
  unpinning it now gives a real sunrise/sunset.
- **Evidence:** Unity EditMode **232/232** (one helper test re-expressed in hours).
- **Resolved — iCloud (2026-09-14):** `~/Documents` is iCloud Drive and created
  `FleetVisual 2.cs`-style duplicates that broke the Unity compile. On Bailey's Mac
  the repo now lives at **`~/Code/Airside`** (outside iCloud); the old
  `~/Documents/Codex/Airside` path is a symlink to it. If duplicate-type compile
  errors reappear, look for `* 2.*` files first.

- **2026-09-14 Claude away catch-up and summary (PROJECT_PLAN core rules, `feature/away-catch-up`):**
  the airport keeps running while the game is closed. Saves are now **version 2**
  with `SavedAtUtcTicks` (v1 still loads, without catch-up). On Continue the
  restored `AirlineOperations` is advanced by real time away (≥ 60 s, capped at 7
  days) through the same event-driven update as live play, then an away summary
  says what each of your aircraft did and what needs you, plus the AI airline's
  trips. The game stays paused under the summary until dismissed. Airline time
  formatting moved to `Domain/AirlineClock`.
- **Evidence:** Unity EditMode **232/232**; `CatchUp_ReachesTheSameStateAsPlayingLive`
  compares catch-up against 13-second live stepping over 5 h 17 min. Packaged app:
  save back-dated 3 h 20 min → "You were away 3 h 21 min / VH-PAX has landed and is
  waiting for you to choose a stand / Emu Air flew 1 trip", clock 11:35, stand
  choice offered.
- **Next per PROJECT_PLAN step 3:** opening guidance for a first-time player, then a
  30-minute soak and a packaged build for one external playtester.

- **2026-09-14 Claude airline HUD layout (PROJECT_PLAN step 2, `fix/airline-hud-layout`):**
  the airline panels had hard-coded positions and were never in the fits-on-screen
  contract. `AirlineHudLayout` now places clock, fleet, toast, map and start panel
  from `HudLayout` (which gained `Viewport`): side by side on wide windows; on narrow
  ones the fleet stacks under the clock, the toast drops above the speed readout,
  and the open map covers the fleet. Tested at all six target sizes for bounds and
  overlaps. **Also fixed:** every map line (coastline, routes) scattered across the
  screen whenever the HUD scale was not 1 — `GUIUtility.RotateAroundPivot` takes a
  screen-space pivot; lines now rotate in GUI space.
- **Evidence:** Unity EditMode **228/228**. Packaged app at 800×500 (HUD scale
  0.55): start panel, clock/fleet/toast/map/readout/bar all clear, coastline
  correct.

- **2026-09-14 Claude trustworthy build (PROJECT_PLAN step 1, `fix/trustworthy-build`):**
  money is deferred (plan: no new economy subsystem before the first session is
  playable). Three fixes so the exact packaged build can be trusted:
  1. **Every aircraft pivot rebake was dead in packaged players.** `ArtGltfLoader`
     uploaded kit part meshes as no-longer-readable; the editor still allows
     reading them, players refuse (`Player.log`: 1,000 "Not allowed to access
     vertices" across props, spinners, rudder, spoilers, tyres, wheels, rims). Kit
     part meshes now stay readable; combined static kits still drop their CPU copy.
     Locked by `Atr42GltfKit_PartMeshesStayReadableForPivotRebakes`.
  2. **Airspeed readout followed the hidden demo circuit** in airline mode (118 kt
     while your aircraft taxied). It now reads the followed aircraft, else the first
     on the field, and measures taxi speed along the ground route.
  3. **Stale assertion:** `FlightPath_AirPhasesMoveAtAircraftSpeedNotTaxiSpeed`
     demanded 80 m/s mid climb-out from the pre-ADR-0044 curve; it now uses
     `CircuitProfile.InitialClimbKnots`.
- **Evidence:** Unity EditMode **221/221, 0 failed, 0 skipped**. Packaged Mac app:
  Player.log 0 "Not allowed" (was 1,000), 0 exceptions; props on their hubs;
  readout 0 kt on a parked aircraft.

- **2026-09-14 Claude airline save/load (ADR 0045, `feature/save-load`):** the
  airline game now persists. `AirlineSave` (Simulation) captures and restores
  `AirlineOperations` — clock, tower, RNG state, airlines and every aircraft's
  exact state — and rejects anything it cannot trust (version, unknown
  type/destination/airline, missing player, double-parked stand, clock mismatch).
  `AirlineSaveFile` writes JSON atomically to
  `persistentDataPath/airline-save.json`. Autosave after every command, within 2 s
  of any fleet event, every 20 s, on focus loss and on quit. The start screen
  offers **Continue** with day, time and trips; a new airline replaces the save.
  Continue rebuilds the clock and circuit at the saved time rather than stepping
  from zero. Only the airline layer is saved; the demo circuit is not (ADR 0041
  still stands for it).
- **Evidence:** Unity EditMode **216/217** (same pre-existing flight-path failure).
  `ResumedGame_ContinuesExactlyLikeOneThatNeverStopped` runs original and restored
  games 27 h further through JSON and they match, AI choices included. Packaged
  app: started a green airline, skipped to 08:52, quit, relaunched, Continue →
  same time and states, fleets drawn in 3D.
- **Watch:** old `airside-save-v1.json*` files from the removed Persistence
  assembly are still in the player's data folder; nothing reads them.
- **Next:** superseded — see the trustworthy-build entry above.

- **2026-09-13 Claude fleets in 3D (ADR 0045, `feature/fleet-3d-aircraft`):** once
  an airline starts, the field draws the fleets instead of the demo circuit. Bays
  on the terminal apron (BAY-1..4 at X −950/−800/−650/−500), pushback and taxi via
  the A↔F link at X −300 to hold at E2, lineup and backtrack, the existing
  takeoff/climb-out and approach/landing curves, backtrack-vacate to F, a queue
  for the stand on F, taxi in. Liveries: Emu Air decal (airline colour is now its
  brown #A66F32); player gets the traffic decal repainted in their colour.
  `FleetVisual` (Simulation, tested) maps fleet state to circuit phases;
  `AirsidePrototype.FleetVisuals.cs` holds routes and models; the prototype reads
  `VisualFlights` everywhere it used `_simulation.Flights`.
- **Evidence:** Unity EditMode **213/214** (same pre-existing
  `FlightPath_AirPhasesMoveAtAircraftSpeedNotTaxiSpeed` failure). Packaged Mac app
  watched end to end with Follow: pushback → taxi → hold bars → lineup → takeoff →
  hidden while away → approach → touchdown → rollout → vacate → wait on F →
  BAY-4 → parked. No exceptions in Player.log.
- **Runway time grew:** tower runway occupancy now includes a 60 s lineup and the
  whole approach plus a 90 s vacate, since all of it is drawn.
- **Watch:** Follow with nothing on the field leaves the camera where it was until
  an aircraft returns. Black upper fuselage is in the decal art (demo plane too).
- **Next:** save/load, then money.

- **2026-09-13 Claude player airline, first slice (ADR 0045, `feature/player-airline`):**
  playable at Adelaide. Start screen (name + livery), fleet panel, Australia-wide
  destinations map with range locks and live off-map tracking, schedule a
  departure, tower-sequenced runway, stand choice on landing, 10×/30×/60× and
  Skip (N). Emu Air flies two ATRs on its own. Logic is `AirlineOperations`
  (event-driven, UnityEngine-free); HUD is `AirsidePrototype.Airline.cs`.
- **Evidence:** Unity EditMode **211/212** — the 17 new airline tests pass; the one
  failure (`FlightPath_AirPhasesMoveAtAircraftSpeedNotTaxiSpeed`) also fails on
  clean `main` after #206 and is not from this branch. `scripts/build-mac.sh`
  succeeds; the packaged app was driven end to end (start → plan MEL → off-map
  tracking → landed → BAY-4 → taxi in).
- **Not yet:** the 3D aircraft are still the demo circuit and do not follow the
  fleets; no saving; no money. Next slice: drive the 3D runway movements from
  `AirlineOperations` (Takeoff/Landing states), then save/load.
- **Watch:** `DayCycle` is still one day per 20 real minutes while airline time is
  a real 24 h clock (HUD clock uses 24 h; lighting is pinned to day anyway) —
  Bailey to decide. Packaged `Player.log` shows ~536 "Not allowed to access
  vertices … isReadable is false" for the tyre/wheel/rim axle-pivot rebake, so
  the ADR 0042 wheel fix likely does not apply in builds (editor only).

- **2026-09-13 Claude player-airline design (ADR 0045, docs only):** Bailey's
  next direction is agreed — Adelaide starter airport that runs itself; the
  player runs one airline (own name/colours, 1 ATR to start) alongside AI Emu
  Air; player picks destination + departure time and the arrival stand, tower
  handles the runway; Australia-wide destinations map with range-locked far
  destinations; real-length flights with 10×/30×/60× and skip-to-next-event;
  money deferred. **Nothing built.** Next step: Bailey says go, then start the
  first slice in ADR 0045's order.

- **2026-09-13 Claude realistic circuit performance (ADR 0044):** the flight
  model is now derived from ATR 42 reference speeds rather than hand-picked
  durations. The old curve passed rotate at 179 kt and left the field at 257;
  it is 100 and 120 now. Real 3° glideslope, real flare from 30 ft floating
  300 m onto the touchdown markings with the sink arrested 584 → 60 ft/min.
  Circuit is 182 s (was 162). `CircuitProfile` is the single source of truth and
  is UnityEngine-free, so the speeds are covered headlessly.
- **Evidence:** `scripts/test-domain.sh` **140/140**; path curves replicated
  numerically — speeds within 2.3 kt, seams continuous to 0.0000 m. A live
  airspeed readout sits above the control bar; its schedule lives in
  `CircuitProfile` so the number on screen is test-covered.
- **Next gate — required:** Unity Play, and this one needs *judgement* rather
  than a checklist: does the round-out read as a landing, does the float look
  right, and is a 35-second takeoff roll (up from 19) too long to watch at 1x?
  The numbers are defensible; the feel is Bailey's call.

- **2026-09-13 Claude consolidation-graft audit (ADR 0043):** `c23cfa1` grafted
  73 branch tips into `main`'s history while discarding every one of their trees,
  so those commits read like delivered work and contain nothing. Audited all of
  them. Only the Pass A wheel fix was a real loss and it is already restored.
  Pass B is superseded, Pass C and Pass D are moot on the v02 aircraft, and the
  Adelaide landside branch is moot behind the bare-field flags. Nothing further
  restored — see the ADR before re-chasing any of those four commits.
- **Caveat that still matters:** the audit only finds lost *declarations*. A lost
  edit to an existing function is invisible to it — the `Landing(a)` compile break
  was that shape. If another defect of that kind appears, suspect the graft and
  diff the branch tip against its merge-base rather than trusting ancestry.

- **2026-09-13 Claude wheels, tyre smoke and camera (ADR 0042):** the wheel spin
  bug was a lost fix, not a new one — `RebakeWheelPivots` / `AirsideAircraftParts`
  were grafted into main's history by the consolidation without ever entering its
  tree, so the roll pass ran without the pivot rebake that makes it correct.
  Restored and locked by `AircraftPartsTests`. Rims now roll too (the old filter
  never matched them). Tyre smoke added at the real contact patches. Follow-off
  now releases the camera in place; F previously could not turn follow off at all
  because the camera and the prototype both handled the key in different phases of
  the frame.
- **Evidence:** `scripts/test-domain.sh` **121/121**; axle geometry checked
  against the shipped glTF.
- **Next gate — required:** Unity Play. Confirm the wheels turn on their axles
  (not orbiting), that tyre smoke reads well at touchdown and fades through the
  rollout, and that F / R / scroll / middle-drag behave as documented under
  "Run it".

- **2026-09-12 Claude strip to a bare circuit sandbox (ADR 0041):** the whole
  objective layer is gone — economy, routes, reputation, staffing, research,
  capacity, daily reports, turnarounds, event log, ATC phraseology, ground
  traffic — and so is the `Persistence` assembly. The game starts fresh on
  approach every launch and saves nothing. `AirportSimulation` now only drives
  one aircraft round the circuit. Both old HUDs are deleted; the HUD is one
  IMGUI bar with pause, follow, 1×, 2×, 4× plus a pause menu (Resume, Restart
  circuit, Quit). `forceSingleInstance` is on and Quit really quits.
- **Evidence:** `scripts/test-domain.sh` **89/89**. `HudLayout` checked from
  320×240 to 3456×2168 against a Rect/Mathf shim; those cases are committed in
  `PresentationLayoutTests`.
- **Next gate — required:** Presentation could not be compiled on the Linux VM
  (no Unity, no committed UnityEngine shim). A real Unity compile,
  `scripts/test-unity.sh`, `scripts/build-mac.sh` and a packaged-player check of
  the control bar, pause menu, 2× rate and single-instance quit must all pass
  before this is trusted. Aircraft visuals were deliberately untouched, so any
  change in how the aeroplane looks or animates is a regression.

- **2026-09-12 Cursor app icon (main):** BRD-002 `airside_app_icon_v01.png` is
  on `main` under Brand + candidates, wired as the default Standalone Player
  icon (`guid d7022c2c4a14457c8d291f16ba58e04a`). Prompt evidence in
  `docs/art/prompts/brd-002-app-icon-generation-2026-09-12.md`. Next verify:
  Mac Unity build → Dock / Finder / .app icon. No simulation change.
- **2026-09-12 Codex consolidation and Mac validation:** `main` now records every
  remaining remote branch tip in its history while retaining the latest Adelaide
  pavement and AIR-001 v02 tree. Obsolete branch snapshots can be deleted without
  losing their commits. Draft PR #190's v06-only wheel/gear/window work is retained
  in history and superseded by the production-proportioned v02 aircraft.
- **Repairs made during the gate:** restored Unity compilation after the landing
  path gained a lane offset; matched the 600 m flare/1,050 m rollout to approach
  speed with a 60-second landing phase; removed the takeoff/departure speed drop;
  and refreshed stale route, reputation, taxi-fillet and legacy-terrain assertions.
- **Evidence:** Unity 6.3 LTS EditMode **300/300 passed** and
  `scripts/build-mac.sh` produced a universal Mac app. `scripts/test-domain.sh`
  could not run because the standalone .NET 8 SDK is not installed on this Mac;
  Unity compiled and executed the same project tests. Next gate is packaged-player
  visual inspection of the Adelaide pavement and AIR-001 v02 motion.
- **2026-09-12 Codex aircraft finish:** branch `feature/atr42-visual-finish`,
  layered on the movement-fix branch below. AIR-001 v02 is preferred, with v01
  and v06 fallbacks. GlTF and bin ship in both Art and StreamingAssets; the new
  version key avoids silently loading the old v01 Resources prefab.
- **Player outcome / scope:** stout cabin, blunt drooped nose and rising rear pressure cone, fitted rounded
  planar cabin glazing, four broad planar cockpit panes with a narrow centre post, correctly placed passenger/cargo doors,
  reference-shaped swept fin, joined dorsal fairing and fin-crown T-tail, seated antenna, reference-area tapered high wing, compact single-piece nacelles and wing-root fairings. Existing moving-part names, props,
  gear, 24.57 × 7.59 × 22.67 m envelope and simulation remain intact.
- **Evidence:** `python scripts/test-air-001-atr42-v02.py` passes bounds, ground
  contact, articulation names, finite/nondegenerate triangles, skin winding,
  glazing clearance, complete feature inventory and positive overlap through the
  fuselage–dorsal-fairing–fin–tail-saddle–tailplane junction. 150 parts / 14,456 triangles.
  Static mesh review: `docs/art/candidates/air_001_atr42_v02_mesh_review.png`.
  Colours/lighting there are approximate, not a Unity screenshot.
- **Next / remaining gate:** Unity compile/EditMode and packaged Mac overview +
  follow at day/dusk/night; inspect gear, props, flaps, elevators and doors in
  motion. No Unity/.NET on this host. No v02 FBX/prefab bake claimed: the supported
  glTF runtime path supplies v02. Keep draft until the Unity gate passes.
- **Decision:** ADR 0040; no save migration or simulation change.

### Previous movement handoff

- **2026-09-12 Codex movement follow-up:** `feature/aircraft-movement-fixes`,
  based on the pavement branch below. Draft pending Unity validation.
- **Player outcome / scope:** commercial taxi entry retains the runway; ATC starts
  landing separation only after taxiing clear; bare-circuit holds do not claim
  runway vacation. Flight curves brake to rest, accelerate from rest and blend
  takeoff pitch into departure. Scope: CommercialFlight, AirportSimulation,
  AirsideFlightPath and their existing EditMode tests.
- **Acceptance / evidence:** four focused regression tests added for future taxi
  entry reservations, actual-vacate separation timing (including no repeated
  reset), circuit hold ownership and speed/pitch seams. `git diff --check` passed.
  Neither test suite ran: .NET SDK and Unity editor are absent from this host.
  Numerical endpoint checks passed; these are not C# compilation or Unity proof.
- **Next:** run `scripts/test-domain.sh`, then `scripts/test-unity.sh` and Mac
  overview/follow playtest: landing → rollout stop → takeoff → climb. Exercise
  the full taxi loop with traffic to verify separation starts after runway exit.
  Keep the draft unmerged until Unity checks pass, per AGENTS.md.
- **Constraints:** deterministic clock, reservations-before-use and save schema
  preserved. Existing circuit-only scene and geometry remain as below; wiring
  the new Adelaide taxi pavement into simulation is still a separate task.

### Prior pavement handoff (still applicable)

- **Last updated:** 2026-09-11 (Claude — YPAD silhouette geometry corrections)
- **Branch:** `claude/adelaide-pavement-review-41kjaa`
- **Do next:** On a Mac with Unity 6.3 LTS: checkout this branch, run
  `scripts/test-unity.sh`, `scripts/build-mac.sh`, then Play overview + follow.
  **`AirsidePrototype` has not been compiled anywhere** — it needs UnityEngine,
  and there is no editor on this Cloud Linux VM. Its five rewritten builders
  were Roslyn-parsed and type-checked against a UnityEngine shim, which catches
  syntax and signature errors but not everything. Treat the first Unity compile
  as the real check.
  Then confirm by eye, in this order:
  1. **Stubs read as taxiways, not blobs** — D/E/D2/E2, A–F links and apron
     entries should be ~23 m wide with a flare only at the junctions.
  2. **The fence sits on the ground** all the way round, not floating.
  3. Taxiway F now sits much further out (182.5 m from the runway centreline),
     so the overview framing changes — check the camera still reads well.
  4. Hold-short bars at 90 m from the runway centreline; solid taxi centrelines.
  5. Rain: 12/30 and the fillets should darken along with 05/23.
  6. Circuit still 05/23 only. Inspect Player.log.
- **In progress / half-done:** Geometry corrections implemented and
  headless-checked (236 passed). Unity Play / Mac build still required.
- **Watch for / assumptions:**
  - Layout: `AirsideAdelaidePavement` — F at 182.5 m, A at 290 m, apron at 450 m,
    RFDS at −230 m; true concave fillets (ADR 0039 supersedes 0036/0037/0038 here)
  - Fence: `AirsideAdelaidePerimeter.FenceBaseY` seats it on the landform (ADR 0039)
  - Sim taxi graph + `SkipGroundTaxi` unchanged (aircraft does not use new taxi)
  - Pre-existing headless failures, also red on `main` — exactly four:
    `Taxiing_ReleasesEachSegmentBeforeReservingTheNext`,
    `TaxiRoutes_UseDoglegThroatBeforeStandLeadIn`,
    `AwaySummary_ReportsRouteIncomeAndReputationChange`,
    `Weights_KeepDryGrassDominantAcrossTheOverviewCore` (the last is
    `AirsideTerrainField`, the 1:20 world — not the Adelaide ground)
  - Save schema / circuit skip unchanged; **no buildings** (apron pads empty)
- **Known unverified:** `CrossYawDegrees = 73°` and the decision to cross both
  runways at their midpoints are **not** checked against the published YPAD DAP.
  73° is plausible (the designators allow 61°–79°) but the comment that justified
  it was arithmetically wrong. Confirm before hanging sim topology off it.
- **Decisions:** ADR 0039 (geometry corrections). ADR 0036/0037/0038 still apply
  except where 0039 supersedes them.
- **Open question for Bailey:** next — (a) wire sim taxi onto F/A/D/E, (b) more
  DAP taxilane detail on the apron pad, or (c) first landside building?
- **Diminishing returns:** bare-circuit *aircraft* polish is done; keep pavement
  densification only while it still changes the overview read.

---

## Current milestone

A bare circuit sandbox. One ATR-class aircraft flies a continuous circuit at
Adelaide (YPAD) — approach, landing, rollout to rest, takeoff, fly-out — and
recycles onto a fresh approach. The airport sits at a named location (Kingscote
by default; Port Lincoln and Coober Pedy also available) and runs a day/night
cycle, a real 24-hour day, driving the sun and ambient
light. Deterministic weather changes through the day and drives the wet-surface,
rain and spray presentation.

The aircraft is flown to ATR 42 reference speeds — Vr 100 kt, Vapp 110,
touchdown 95, climb-out 170 — on a true 3° glideslope with a real flare
(ADR 0044). Phase durations are derived from those speeds, never picked.

There are no objectives, no economy, no scoring and no progression, and nothing
is saved between runs (ADR 0041). The player has exactly five controls — pause,
follow, and 1×/2×/4× time — plus a pause menu, and free camera orbit, zoom and
pan. What is on screen is the aeroplane, the runway and the ground.

## Visual asset contract

The approved visual direction, exact asset paths, animation responsibilities and
production order live in
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` (decision 0022). The first playable
moves from procedural primitives to approved art in batches, with primitives kept
as fallbacks during integration.

The immediate visual target is a premium stylised-realism miniature of a regional
Australian airport. Generated images establish composition, palette, fictional
liveries and UI direction. Runtime aircraft, buildings and service vehicles remain
true 3D assets; animation and VFX mirror simulation state and never drive it.

- Anti-aliasing is on: High keeps 4× MSAA on the PC pipeline plus SMAA (high) on
  the runtime camera; Medium uses 2× MSAA + SMAA. Vsync is on (`vSyncCount` 1).
- The post stack runs a deliberate grade only — the template default profile's depth of field, motion blur, lens distortion, chromatic aberration, lens flare and panini are pinned off.
- The simulation keeps running when the window loses focus (`runInBackground`).

## Invariants

- Domain and simulation rules remain independent of Unity scenes.
- Nothing persists between runs; there is no save file and no schema to migrate.
- Time comes from an injected clock.
- Runways, taxiways and stands must be reserved before use, and a lone aircraft
  must never block itself (`AirportSimulation.ReservationConflicts` stays zero).
- Frame rate must not change simulation outcomes.
- Pausing and opening the pause menu freeze every presentation rate together
  (`SimulationFrozen`), not just the aircraft.
- Exactly one `AirsidePrototype` may exist; a duplicate bootstrap destroys itself.
- No external data or asset enters the project without a recorded licence.

## Run it

Open `game/Airside` in Unity 6.3 LTS and press Play.

The game opens with a short intro (any key skips). Time is **live Adelaide time**:
once an airline starts, one second in the game is one real second, and the clock
shows the real local time. There is no pause, no time rates and no skip; the menu
does not stop the airport. On-screen controls at the bottom centre are **Follow ·
Overview**, with live airspeed in knots just above.

Airline (ADR 0045): name your airline and pick a livery on the start screen. The
fleet panel (top right) plans flights and offers stands when your aircraft lands;
click a registration (or click the aircraft on the field) to follow it. The map
(top left, or Tab) shows every destination — green in range, grey locked — and
tracks aircraft that are away.

Simulation:

- Escape: clears aircraft selection and returns to overview when one is selected;
  otherwise opens or closes the menu (Resume, Restart circuit, Quit) — time keeps running
- Tab: open or close the destinations map (scroll to zoom, drag to pan; state labels appear when zoomed)
- H: open or close the Hangar (all aircraft + flight progress)
- M: mute audio

Camera:

- Click an on-field aircraft to select and follow it. A coastal-blue ring marks
  the selected aircraft; a details card sits above the control bar. Fleet-panel
  rows also select. Dragging to pan does not select.
- F: toggle follow. Turning it off hands the camera back **where it is** —
  position, angle and zoom are kept and you are free to move from there. It
  does not drag you back to the overview.
- R / Overview: reset to the overview framing and clear the current aircraft
  selection
- Right-drag: orbit / look around
- Left-drag or middle-drag: pan across the field by grabbing the ground under the
  cursor (drops follow, since panning a followed aircraft would only fight the
  follow). A left press on a HUD panel stays a click, and a left press only becomes
  a drag after a few pixels of movement so a plain click can still select an aircraft.
- Scroll: zoom toward the ground under the pointer while free. While following this
  biases the phase framing rather than setting an absolute distance, so it survives
  the follow easing instead of being erased on the next frame.
- WASD: pan (drops follow, same as drag)
- Q / E: orbit left / right without a mouse
- Z / X: lower / raise the camera

Follow, reset and direct aircraft selection are owned by `AirsidePrototype`;
camera movement is read by `AirsideCameraController`. Exactly one owner each —
two owners is why F used to toggle follow off in `Update` and straight back on
in `LateUpdate`.

Run checks with `scripts/test-unity.sh`. Build the local Mac app with `scripts/build-mac.sh`.

Soak a packaged build unattended (PROJECT_PLAN acceptance): launch with
`-airsideSoak -airsideSoakMinutes 30`. A fresh airline flies itself in live time
(its first departure at four minutes, so every soak covers an engine start), a
`[Airside soak]` heartbeat goes to `~/Library/Logs/DefaultCompany/Airside/Player.log`
every minute (live time, trips, fps, memory, fleet states with engine spool, beacon
and doors), a STALL error is logged
if the clock stops, and the app quits with a COMPLETE line. Soak saves go to
`airline-save-soak.json`, never the player's save.
Without a Mac Unity editor, `scripts/test-domain.sh` runs the same
Domain/Simulation EditMode tests headlessly via `dotnet test` (.NET 8 SDK) — a
fast supplementary check, not a replacement for a real Unity run before merging.
It does not cover Presentation, which needs UnityEngine.

## Current evidence

- **Circuit visible polish** on `cursor/circuit-visible-polish-0c44`: bare HUD
  hides economy chrome; coast muted; engine range 220 m; gear doors transit-only;
  glass prop discs; landing follow framing; soft rotate cue.
  **Verified headless:** `scripts/test-domain.sh` **214 passed** (1 new FocusMode HUD test; 4 pre-existing failures also red on main). **Not yet
  verified:** Unity EditMode / Play, `scripts/test-unity.sh`, `scripts/build-mac.sh`.

- **Plane / ground dynamics polish** on `cursor/plane-ground-dynamics-polish-0c44`:
  Adelaide authored ground mesh (ADR 0035) replaces the 16 m tiled grass cube;
  runway keeps 3 100 × 45 m with multi-scale asphalt + outside dirt shoulders;
  touchdown smoke restored independent of world props; gear eases; tires spin from
  distance/radius; oleo settle; softer prop disc; ATR material/LOD polish.
  **Verified headless:** `scripts/test-domain.sh` **213 passed** (6 new Adelaide
  ground tests); 4 pre-existing failures also red on `main`. **Not yet verified:**
  Unity EditMode / Play, `scripts/test-unity.sh`, `scripts/build-mac.sh`, packaged
  overview/follow loops (no Unity editor on this Cloud Linux VM).

- **Final AIR-001 ATR 42-class starter** on `feature/atr42-final-aircraft`:
  production identity `mdl_atr42_starter_v01`, exact 22.67 × 24.57 × 7.59 m
  three-view envelope and 3.93 m six-blade props. Six-wheel gear, doors and
  restrained flight controls are separate and runtime-pivoted. The Resources
  prefab and StreamingAssets fallback are integrated; targeted aircraft Unity
  tests pass and the packaged Mac follow view has been inspected. Decision 0034.

- **Circuit flight-state cues** on `cursor/bare-adelaide-field-bc75`: ATR circuit
  gear / lights / props / one-shot touchdown at `TouchdownProgress`. Cabin doors
  stay shut on the circuit.

- **Real-metre runway markings** on `cursor/bare-adelaide-field-bc75`: the 3 100 ×
  45 m slab now has ICAO-ish threshold bars (12 per end), aiming points at 400 m,
  dashed centreline (30/20), 0.90 m edge lines, and TDZ pairs at 150/300/600/750/900 m.
  Numbers live in `AirsideRunwayMarkings` (no UnityEngine). The 300 m pair is
  centred on `AirsideFlightPath.TouchdownX` (-1250). Paint is combined per family,
  not hundreds of cubes. WLD-001 is not used.

- **Bare Adelaide field** on `cursor/bare-adelaide-field-bc75`: visible world is
  one 3 100 × 45 m runway (YPAD 05/23), 3 400 × 2 309 m / 785 ha empty ground,
  one turboprop and pinned daylight. No buildings, cars, signs, taxiways or
  decorative lights. Decision 0032. `scripts/test-domain.sh` is the headless
  check for the new metre constants.

- CC0 Unity Terrain ground on `feature/cc0-terrain-ground`: 256 × 220 × 8 m
  TerrainData (heightmap 257, alphamap 256), four CC0 TerrainLayers on the
  built-in URP Terrain Lit shader, plus a Poly Haven worn-concrete apron.
  Operational plateau dead level at **-0.0450 min and max** across X [-64, 66] ×
  Z [-12, 60], lowest pad **3.5 cm** proud; normalized heights **0.1053–0.5538**
  (no clamping); relief **3.57 m over 220 m**; overview core **67.1% dry grass,
  15.0% green, 17.8% worn dirt**; dirt shoulder **2.20–3.20 m**; lag correlation
  decays monotonically **0.799 at 11 m → 0.430 at 32 m** with no resurgence at
  any tile size. Albedo tile-scale luminance spread **0.5–2.0 points** with
  detail std **8.9–23.8**. `scripts/test-domain.sh` **200 passed** (19 new
  terrain tests, mutation-checked). Decision 0031. **The bake has not been run:
  needs `scripts/bake-terrain.sh` on the Mac, then Unity compile, Mac build and
  packaged day/dusk/night/rain QA.**

- Runtime airfield performance **P0–P2 plus GPU-state + paint/probe/kit-combine
  pass, merged to `main` as PR #183**: combined operational pads (6)
  replace 745 Terrain11 tiles; textures/materials cached; Addressables on demand;
  High/Medium ladder; probe bands. Per-frame `Renderer.material` clones removed;
  scene index; one star mesh; probes `RenderProbe` after world combine. Taxi
  paint is strips not 1 m cubes; kit fence/forecourt/GSE/planters/chocks/belt
  loader stamp cached combined meshes; static combine skips moving GSE/clouds/
  birds/boats/`antenna_dish`. Ambient audio `Resources.Load` is deferred off
  Awake. Medium thins fillet lights, fence rails, window PointLights and scrub.
  `AirsidePrototype.cs` brace depth 0. Decision 0029. Mac Play visual-first-frame
  still required.

- Layering / collision / route **100-fix** on `cursor/layering-collision-bugfix-100-d7f0`: dogleg lead-ins, apron throat, stand spacing 14/24/34, GT off-field + run-up bay, selective yield, `scripts/test-domain.sh` **177 passed** (`CollisionPass100Tests`).

- Fidelity-board integration **merged via #167**: scrub/terrain v02, surface
  `tx_*_v02` + wet concrete, ARFF prefab v02 densify, CHR dual wands,
  turnaround GSE zone layout, docs Approved · Integrated.
- `scripts/test-domain.sh`: **136/136** on the integrate branch before merge.
- Day/night readability merged via #158 (Mac noon/midnight overview pending).
- 50-item bugfix pass merged via #157.
- Eucalyptus VEG-001 v02 merged via #156 (Mac overview vs REF still pending).
- Forecourt PRP-003 v02 merged via #155 (Mac overview vs REF still pending).
- Fence/gate PRP-002 v02 merged via #154 (Mac overview vs REF still pending).
- Character kits CHR-001/002 v02 merged via #153 (Mac overview/follow vs REF-003
  still pending).

## Next work

1. Watch the loop in Unity Play (F, one circuit, no HUD). Then **one taxiway
   and one stand** only when Bailey says so.
2. No new economy systems; no Companion/CloudKit; no buildings/GSE restore.
3. **Player airline at Adelaide (ADR 0045).** First slice built on
   `feature/player-airline` (everything except save/load); fleets drawn in 3D on
   `feature/fleet-3d-aircraft`; save/load on `feature/save-load`. Next: economy.
