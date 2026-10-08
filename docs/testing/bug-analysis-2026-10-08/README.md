# Bug analysis — 8 Oct 2026 (Claude, static + headless)

Scope: `main` at `7f9621b`. Method: full headless suite (`scripts/test-domain.sh`: 2,026 pass, 10 fail),
plus throwaway checks (not committed) that painted every HUD page at 4 window sizes, priced every
aircraft on every route, and bought/flew every aircraft type on every reachable route.
**Unity was not available**: nothing here was seen in the player. Sim soak showed no stuck aircraft,
double-booked stands or payout/forecast mismatches. 18 findings below, not 50.

Confidence: **A** = reproduced by running code/tests; **B** = read in code, not run; **C** = heuristic.

## A. Reproduced

### 1. Flight Manual credits drop sections at 800×600 — A
`FlightManualPainter.Paint` (`Presentation/FlightManual.cs`) `break`s when a section does not fit, so
"Land cover" and "Weather and live traffic" vanish. These are CC BY / ODbL attributions.
Fails `FlightManual_FitsAndNavigates` (800x600 p7 lost 'Land cover').
**Fix:** split the credits page in two (map/data, sound/terrain), or paginate; never `break` silently.

### 2. Contracts shows zero offers at 800×600 — A
`ContractsWorkspaceLayout.Create` stacks the columns when narrow and gives the active card up to 62% of
body height (273 px → 169), leaving 92 px for offers; minus caption 28 = 64 < `OfferHeight` 86, so
`VisibleOffers` = 0. Fails `Contracts_LayoutKeepsBothColumnsInsideTheSurface`.
**Fix:** `top = min(wanted, body.Height*0.62, body.Height - 12 - (CaptionHeight + 8 + OfferHeight))`;
painter already drops terms that do not fit.

### 3. Operations airline view shows 2 rows at 800×600 — A
`OperationsWorkspaceLayout.Create(airlineView:true)` still reserves day strip, tabs and column header.
Fails `GrowingOverview_LastAircraftRemainsReachableInCompactWindows` (needs >= 3 rows).
**Fix:** hide or compact the tab strip in airline view when body height is small.

### 4. Twelve destinations have no demand entry — A
`RouteForecast.DemandFor` lacks NOU, POM, CGK, BKK, SGN, MNL, PVG, ICN, KIX, NRT, HNL, LAX → default 60.
787-9 margin/hour: SIN ~$2,394, HKG ~$1,802, BKK ~$268, LAX ~$281.
**Fix:** add realistic entries (e.g. NRT/BKK ~150–200, LAX ~150, HNL ~120).

### 5. Revenue ignores passengers carried; bigger aircraft earn less — A
`RouteForecast.For`: `revenue = basePay * (0.35 + 0.9 * passengers/seats)`; cost and `Weight` are the same
for every jet. Sydney: 737-8 carries 170, earns $8,179; 787-9 carries 260, earns $7,625 (cost ~equal).
A $85k A350 is worse than a $28k 737-8 on every route under ~4,300 km.
**Fix:** revenue = fare(km, band) × passengers (+ fixed component); keep cost scaling with size.

### 6. A321neo to Bali/Auckland out-earns every widebody — A
Follows from 5. A321neo DPS ≈ $1,931/h (DPS demand 200 × Pacific 1.7), above 787-9 ($1,357/h).
**Fix:** resolve 5, then re-balance DPS demand and Pacific multiplier.

### 7. Jets/Dash 8/E190 are allowed on regional strips and always lose money — A
`RouteAccess.Allows` is `band <= ceiling`; every jet loses $170–500/h to KGC/CED/CPD etc.; Dash 8 loses
on all 8 regional strips, E190 on 7. `ContractMarket.PickDestination` sends jets to regional strips when
reliability < 70, and Domestic-ceiling types there 2/3 of the time.
**Fix:** drop negative-forecast destinations from the contract pool; optionally a "loses money" tag in the planner.

### 8. Stale tests (drift) — A
- `Melbourne_CruisesInTheLowTwenties…`: expects 270–320 kt, gets 264 (ATR planning cruise now 510 km/h).
- `Economics_TheHelicopter…`: expects cost/km 1.45, code `RotorcraftCostPerKm` = 1.0 (**decide which is intended**).
- `TheHelicopterHasItsOwnEngineClass…`: expects `eng_b412_idle_v01`, asset is `v02`.
- `NightSkyReview*` (3): no drawable cruise overflight in first 300 s since the flight-time retune.
**Fix:** update tests, or code where the test is right.

### 9. House-style test fails — A
`TheManualGoalsAndNewsReadInTheHouseStyle`: semicolon in the "Aircraft sound" credit. **Fix:** reword.

## B. Read in code

### 10. Hotkeys fire while typing in the Rename box
`ReadSimulationControls` guards only the setup screen. In Career (profile view) the `GUI.TextField`
(`AirsidePrototype.Airline.cs` ~2660) does not capture the keyboard, so T/C/H/L/N/F/R/M/Tab switch pages,
mute or reset the view mid-name. **Fix:** return early when `GUIUtility.keyboardControl != 0`.

### 11. Autosave hitch
`AirlineSaveFile.Write` calls `TryReadFile(path…)` (read + JSON parse) every save only to decide on `.bak`.
**Fix:** check once per session, or rotate `.bak` without parsing.

### 12. Save errors other than IO escape
`SaveAirline` catches `IOException`/`UnauthorizedAccessException` only. **Fix:** catch `Exception`, toast once.

### 13. Daily report lost if not running 23:00–24:00
`AnnounceTheDay` reports only when local hour >= 23, so that day's flights roll into the next report.
**Fix:** report on first update after the day number changes.

### 14. Plural errors
"1 of 1 flights done" (`ContractsWorkspace` ~110); "1 destinations"/"1 flights" (`CareerTrackWorkspace` ~87);
"needs 1 flights" (`FleetWorkspace` ~518, `AirlineOperations.Fleet` ~99, `Purchase` ~40, `CareerRoadmap` ~83).
**Fix:** shared `Plural(n, "flight")` helper.

### 15. "Miss it and lose 0 reliability"
`ContractsWorkspace.FillActive` always appends the loss. **Fix:** omit when 0.

### 16. Weak validation in `Airline`
Constructor does not enforce `Rename`'s 24-char limit; `IsValidHex` uses `NumberStyles.HexNumber`, which allows
whitespace (`"# 12345"`). **Fix:** shared name check; validate hex with explicit char test.

### 17. Null `CareerState`
`MarketOffers` (lines ~78–90) and `AcceptContract` dereference `CareerState` after guarding it earlier. Low risk.

## C. Heuristic

### 18. Probable text clipping at 800×600
(`HudPainter` hard-clips.) "Flies regional routes (Kingscote, Port Lincoln)", Fleet subtitle with 6 segments,
"SORT · FLEET" button, Career subtitle and "Finish a regional contract", Stats "Regional starter base →
Expanded regional base". **Fix:** use `HudShell.FitText`-style shrink, or shorten.

## Not bugs (checked)
Overdue-check pay cut (reliability multiplier), award-sized payout differences, and curfew-delayed rotations
explained all payout/elapsed-time differences in the sweep.
