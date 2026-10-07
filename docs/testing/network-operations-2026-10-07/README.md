# All-base Operations verification

Task #577; 7 October 2026. Native Unity behavior is unverified.

Deterministic checks cover owned aircraft across bases without AI contamination, availability after booking, outbound/turnaround/inbound direction and next event, read-only projections, overdue/check state, clearing Fleet filters on selection and reaching the final aircraft by scrolling at 1440×900, 1280×720, 900×720 and 800×600 (at least three rows fit).

Validation: 35 focused tests passed after the compact-layout correction. The Unity asset audit passed (1,803 unique GUIDs, 388 identical art mirrors, 70 committed character materials); generated presentation-map and diff checks passed. An initial full run on main `1fd4c9fc` encountered the known `GroundSeparationTests.BusyDay_NoAircraftDriveThroughEachOther` failure; its upstream fix #579 merged while work was in progress. This branch was synced onto that fix (`f2e4642b`). Final combined regression/CI results are recorded in the pull request. `hud-operations-network.png` is exported from the shared C# painter with a real headless airline fixture and a schematic backdrop. It is not a Unity capture.

On the Mac:
1. Build/run Unity 6.3; open Operations with Adelaide and Melbourne aircraft. Verify total/available counts against Fleet.
2. Select Melbourne from My airline while Fleet previously had Adelaide/Flying filters. Confirm registration/base and existing plan/check/track/camera controls.
3. Follow the network service through booking, outbound, destination turnaround and inbound. Check the next event changes; inspect the airborne view and return to Fleet.
4. Scroll a growing fleet and select its last row; compare 800×600, 1280×720 and 1440×900 text/click bounds.
5. Switch to Airport movements, use Arrivals/Departures, scroll, inspect AI/stand blockers, then return to My airline. Confirm no commands are submitted merely by navigation.
6. Run `scripts/test-unity.sh`; confirm save/reload still uses the existing schema.
