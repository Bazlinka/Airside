# 0066 — Career Stats workspace, and nine features supporting it

Date: 2026-09-21

## Decision

Ten features, requested together ("Add 10 new features to the game... implement and merge") as
a direct follow-up to the previous round's brainstorm on stats/profile visibility, airline
customisation and pricing. Listed in build order, since several are foundations the later ones
read from:

1. **Lifetime revenue.** `AirlineCareerState.LifetimeRevenue` — every settlement's payment,
   accumulated and never reduced by spending (unlike `Funds`). A real answer to "how much have
   I actually earned" versus the current balance.
2. **Contract history.** `AirlineCareerState.ContractHistory` — the last `MaxContractHistory`
   (10) fulfilled contracts, newest first, each with its route and total paid across every
   rotation (`ActiveRouteContract.TotalPaid`, new).
3. **Airline rename.** `Airline.Rename` (validated: non-empty, ≤24 chars) +
   `AirlineOperations.RenameAirline` (player-only). Mutates the existing `Airline` instance in
   place — every fleet aircraft, save record and HUD reference already holds that same object,
   so nothing needs re-wiring.
4. **Livery recolour.** `Airline.Repaint` (validated `#RRGGBB`) + `AirlineOperations.SetLivery`
   (player-only), same in-place pattern.
5. **Aircraft resale.** `AirlineOperations.SellAircraft` — refunds `ResaleFraction` (0.55) of the
   type's `AircraftAcquisition` price, only for a player aircraft `AtStand` (never mid-rotation).
   The starter ATR was never bought, so it has no listed price and cannot be resold — refused
   rather than inventing a number.
6. **Reliability-linked pay.** `FlightEconomics.ReliabilityMultiplier` scales only the flat
   per-rotation `baseRevenue` — neutral (1.0×) at and above `ContractMarket.DomesticReliabilityFloor`
   (70, an existing threshold reused rather than a new one invented), a real penalty below it
   (0.92× / 0.8×). Contract-specific pay (`PaymentPerRotation`, `CompletionReward`) is never
   scaled — a contract's advertised numbers always pay exactly what it advertised. "Work harder
   for things" now applies to the boring flat rate, not just the tier gate at purchase time.
7. **Career milestones.** `CareerMilestones.Reached` — 11 fixed achievements (first rotation,
   10/25 rotations, first contract, each tier, fleet growth, first jet, elite reliability),
   evaluated fresh from state already tracked elsewhere. Nothing new to persist.
8. **Next-tier progress.** `CareerProgress.NextTier` — reads the exact rotation/reliability
   thresholds `AirlineCareerState.EvaluateTier` checks (now named constants,
   `RegionalRotations`/`RegionalReliability`/etc., shared by both so they cannot drift apart) and
   names precisely what's still missing, including a specific aircraft type where that's the
   actual gate (e.g. Domestic needs a Dash 8 or a jet, not just rotations). A direct answer to
   "what is Regional? how do I unlock the next one?" from two sessions ago.
9. **Resale value surfaced in Fleet.** The Fleet workspace's selected-aircraft detail now shows
   "Resale value $X" for a bought aircraft (omitted for the starter ATR, which has none) — the
   resale command from (5) had no HUD entry point to discover it existed without this.
10. **The Stats/Career workspace.** A fifth HUD tab (`HudWorkspace.Stats`) built the same way as
    the other four (ADR 0057): a UnityEngine-free `StatsWorkspaceModel`/`StatsWorkspaceLayout`/
    `StatsWorkspacePainter`, so the same numbers drive the runtime HUD, the headless tests and
    the offline mockup renderer. Two columns: Overview (funds, lifetime revenue, reliability,
    tier, fleet size) and Next Tier progress on the left; Milestones and Recent Contracts on the
    right. Verified by actually rendering it (`scripts/hud-mockup` + `render-hud-mockups.py`),
    not just reasoning about the layout — see Consequences.

## Reason

Two prior sessions built up the need for this: ADR 0063 found `OperatingTier.Regional` and
`RouteBand.Regional` sharing a word with nothing to tell them apart ("what is Regional?"); the
subsequent brainstorm turn asked directly for "a way of viewing stats (accurately) as well as a
way of modifying your profile... What other stuff can we add? Should pricing be modified? Work
harder for things?" Every item above answers one clause of that directly rather than guessing at
scope: stats (1, 2, 7, 8, 10), profile (3, 4), pricing/"work harder" (5, 6), and the resale
command needed a way to actually find it (9).

Deliberately not attempted: a UI to trigger rename/livery from the HUD (the backend commands
exist and are tested; wiring a text-input control and a colour picker into IMGUI is a separate,
riskier piece of interaction work than a read-only stats page, and this round already carries
five Domain/Simulation changes plus a new workspace) — flagged as the natural next step, not
silently dropped.

## Consequences

- **Save format bumped to v9** (`AirlineSaveData.CurrentVersion`): adds `CareerLifetimeRevenue`
  and a capped `ContractHistory` list (JsonUtility-friendly `CompletedContractSaveRecord`
  mirror of the Simulation-layer `CompletedContractRecord`). A pre-9 save loads with 0 lifetime
  revenue and no history — the same "no retroactive credit" tolerance every earlier version
  bump has applied. Round-tripped by `AirlineCareerTests.Save_RoundTripsLifetimeRevenueAndContractHistory`
  (headless — `AirlineSaveTests.cs` itself needs real `UnityEngine.JsonUtility` and is excluded
  from `scripts/test-domain.sh`, same as before this round).
- **Airline became mutable** (`Name`/`LiveryHex` gained validated public setters) — a deliberate,
  narrow break from full immutability, chosen over swapping the object out because every
  existing reference (fleet aircraft, HUD, save capture) already holds the same instance.
  `Rename`/`Repaint` had to be `public`, not `internal`: Domain and Simulation are separate
  asmdef assemblies with no `InternalsVisibleTo` between them, so `AirlineOperations` (Simulation)
  could not otherwise call them — validation happens in the method itself instead.
- **Reliability multiplier is the one change with real economy-balance risk**, and was checked
  against it directly: `FreshCareer` (100% reliability, `StartingReliability`) is unaffected
  since 100 ≥ 70 stays at 1.0×, so no existing exact-funds test anywhere in the 500+ tests this
  session started from needed touching — verified by running the full suite, not just the new
  contract-settlement tests. Only a career whose reliability has actually slipped below 70 pays
  less on the flat rate; contract pay is completely untouched by it.
- **The Stats workspace was rendered, not just reasoned about**: `scripts/hud-mockup` +
  `render-hud-mockups.py` produced a real PNG (`hud-stats.png`) showing all five sections
  correctly laid out, no clipping or overlap, at 2015×1260. Its bottom half is empty in that
  render — the same "sparse workspace" character ADR 0062 already found and partly fixed on
  Contracts — not addressed here; flagged as a follow-up, not a regression, since the page is
  new and every value on it is already accurate.
- The Fleet workspace's new resale-value line was also confirmed by re-rendering
  `hud-fleet.png` — reads cleanly next to the existing capability lines.
- `HudShell.Tabs`/`FillTabs`/`NavStrip` were already fully generic over tab count (no hardcoded
  "4" beyond one now-updated test assertion and a few doc comments) — adding a fifth tab needed
  no layout-math changes, only the new tab's own model/layout/painter.

## Tests

`scripts/test-domain.sh`: **526/526** (500 baseline this session + 26 new: `AirlineCareerTests`
×6, `FlightEconomicsTests` ×1, `CareerProgressTests` ×4, `CareerMilestonesTests` ×4,
`AirlineOperationsTests` ×6, `FleetWorkspaceTests` ×1, `StatsWorkspaceTests` ×5, plus one
existing `HudShellTests`/`CareerExpansionTests` assertion each updated for the new tab count and
save version). The Stats workspace and Fleet's resale line are also visually verified via the
mockup renderer, not inspection-only like most Presentation work this session.
