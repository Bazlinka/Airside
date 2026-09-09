# Bug audit — layering / collision / routes (~100 items)

Source: `origin/main` at `a93748a`, branch `cursor/bug-audit-100-0740`.
Scope: Simulation, Domain, Persistence, `TaxiVisualPath`, `HudLayout`,
`AirsidePrototype` aircraft/GSE/scenery paths, EditMode tests.
Prior passes reviewed: `#157` (50-bugs), `#159` (20-smooth),
`BUG_AUDIT_2026-09-07/08`.

Priority tags: **(A)** planes bumping / collision · **(B)** wrong routes ·
**(C)** layering / z-fight / interpenetration · **(D)** other real bugs.

---

## A — Planes bumping / collision / reservation gaps

1. **Stand spacing vs wingspan** — `AirportTaxiNetwork.StandZ` (14/20/26) + `BuildAircraft` wing extent ~±3.9 m  
   Adjacent stand centres are only 6 m apart; turboprop half-span ~3.9 m so parked dual commercials / GT+commercial wingtip-interpenetrate.  
   **Fix:** Widen stand Z spacing (e.g. ≥10 m) or shrink presentation wing span to match.

2. **Stand 3 lead-in clips Stand 2 box** — `AirportTaxiNetwork.Create` + `CommercialFlight.ResourcesForPhase`  
   Chord (8,9)→(17,26) passes ~3 m west of Stand 2 centre; `LEAD-IN-3` does not share a resource with `STAND-2`, so taxi-to-3 can overlap a parked aircraft.  
   **Fix:** Dogleg lead-ins clear of other stands, or reserve a shared apron conflict resource along the chord.

3. **Stand 2 lead-in near Stand 1** — same as above for (8,9)→(17,20)  
   Mid-chord sits close enough that wing envelopes graze Stand 1.  
   **Fix:** Same dogleg / apron-hold pattern as item 2.

4. **GT run-up bay sits on A2 centreline** — `GroundTrafficAircraft.BuildCircuit` Reposition bay `(5,9)`  
   Commercial A2 is z=9 from x=−12..8; GT-202 holds on the live centreline and will visually collide with any A2 user once corridor frees.  
   **Fix:** Move bay off-centreline (e.g. `(5, 12)` or a dedicated run-up pad north of Alpha).

5. **GT “off-field hold” parks at runway entry** — `GroundTrafficAircraft.Reposition` when `AlternateStand` is default  
   Hold returns early without moving; `Position` stays at leg-0 `From` `(−24,0)` while corridor is free — sits on the A1 entry in front of commercials.  
   **Fix:** Snap / keep position at Away `(−60,−30)` (or a hold pad) while waiting for a free stand.

6. **GT Departing crosses runway without reservation** — `BuildCircuit` “Departing” uses `None` resources  
   Path `(−24,0)`→`(−36,−4)` overlaps commercial landing/takeoff west rollout while runway may be owned by a commercial.  
   **Fix:** Require `RUNWAY-09-27` (or a runway-exit resource) for the Departing leg.

7. **Dual commercials share one approach line** — `PositionFor(Approach/Landing)`  
   Both flights use the identical `(−72..−50, z=0)` corridor; sequenced holds still stack at the same XYZ at approach end / landing start.  
   **Fix:** Offset number-two laterally (or vertically) by a fixed presentation lane.

8. **Number-two stacks on leader at flare start** — Approach stall at progress=1 + Landing start both at `(−50, 1.55, 0)`  
   When leader enters Landing, trailer is still clamped at the same point for up to the go-around window.  
   **Fix:** Hold number-two further out on final (lower approach progress ceiling while waiting).

9. **Departed climb occupies takeoff climb corridor** — `PositionFor(Departed)` `(55,14,0)` vs takeoff end `(52,12,0)`  
   Six-second reset leaves the departed model in the departure climb while the next cycle can take off into nearly the same airspace.  
   **Fix:** Push Departed further downrange / higher, or hide the model once clear.

10. **Taxi-hold visual creep toward the aircraft ahead** — `VisualPhaseProgress` vs `StallOneSecond`  
    Between whole-second stalls, `_preciseTime` advances so mid-taxi holds creep forward along the segment then snap back — reads as closing on the aircraft that owns the corridor.  
    **Fix:** Drive taxi visuals from `Operation.PhaseProgress(simNow)` (stalled clock), not raw `_preciseTime - PhaseStartedAt`.

11. **GSE park bays sit on / over Alpha** — `UpdateServiceVehicles` parks at z≈11.2–12.5, x≈−4.5..2  
    A2 centreline is z=9; bus/fuel/bag parks are inside turboprop wing/fuselage sweep while commercials taxi A2.  
    **Fix:** Move park bays north of apron edge (z≥14) clear of Alpha.

12. **Service bus at Stand 1 overlaps Alpha** — active bus at `(14.2, standZ−3.4)` → z=10.6 for Stand 1  
    Within ~1.5 m of A2; taxiing traffic clips the bus.  
    **Fix:** Place starboard bus further from Alpha or only when standZ is high enough.

13. **Fuel truck service pose in lead-in chord** — `(14.6, standZ+3.1)`  
    For Stand 1/2 this sits near lead-in asphalt; outbound taxi / GT lead-in can clip the truck.  
    **Fix:** Tuck fuel under the wing further east (higher X) or gate movement on lead-in clear.

14. **Pushback apron lane is only one-wide but paths cross stands** — `ApronLane` + `PositionFor(Pushback)` to `(12, standZ−2)`  
    Reservation serialises pushbacks, yet the push-end points for adjacent stands still occupy a shared apron strip that GT lead-ins traverse without `ApronLane`.  
    **Fix:** Have GT lead-in / commercial taxi-out also claim `ApronLane` (or stand-local apron resources).

15. **Commercial releases stand at TaxiOut start while still on lead-in** — `ResourcesForPhase(TaxiOut)` omits stand  
    Stand becomes free for GT the instant TaxiOut begins; GT can start inbound toward a stand the commercial has not visually cleared.  
    **Fix:** Keep stand (or lead-in+stand) until lead-in segment is released.

16. **Corridor is exclusive but lead-ins are not mutually exclusive** — `CommercialFlight` / `GroundTrafficAircraft`  
    Two aircraft on different lead-ins can be geometrically adjacent on the apron with no shared lock.  
    **Fix:** Add an apron-throat resource covering x∈[8,17], z∈[9,26].

17. **GT Yield drops all held resources including stand mid-dwell?** — `Yield` releases everything on any overlap  
    If a commercial’s next-phase resource list ever includes the GT’s stand while GT is parked, Yield clears stand ownership and `_onLeg=false` while still “At stand” visually until re-acquire — another GT/commercial can grab it for a tick.  
    **Fix:** Yield only the conflicting resources, or never yield a parked stand to non-stand commercial claims.

18. **Reposition GT and ArriveDepart can both want corridor; loser freezes mid-field** — `ChooseCorridorGrantee`  
    Correct for sim, but the waiting aircraft keeps its last on-path XYZ with engines/lights on and no hold-short pad — looks like a nose-to-nose standoff.  
    **Fix:** Present a hold-short pose at the previous junction waypoint while `IsHolding && WantsCorridorNow`.

19. **Landing rollout and GT A1 entry share `(−24,0)`** — `LandingPosition` end + GT A1 start  
    Vacate notify starts separation, but GT can already own corridor/A1 and sit at the exit while the commercial is still rolling to the same point.  
    **Fix:** Keep runway+A1 (or exit resource) through landing rollout; block GT A1 until vacate.

20. **Takeoff lineup bezier dips south into GT Departing path** — `TakeoffPosition` bend `(−23.2, −0.85)`  
    Early takeoff swerves into the same south-west quadrant GT uses for Departing.  
    **Fix:** Keep lineup on centreline or bend north, away from off-field exit.

---

## B — Wrong routes / GT vs commercial geometry mismatch

21. **Taxi-out early chord cuts the lead-in** — `TaxiOutPosition` t&lt;0.18 Smooth(pushEnd → join≈Alpha)  
    Skips the stand→lead-in polyline and drives diagonally across apron/grass (worse for Stand 2/3).  
    **Fix:** Follow reverse `PositionAlongTaxiRoute` from the first outbound sample; only ease heading, not position off-polyline.

22. **Pushback end is not on the taxi route** — `PositionFor(Pushback)` → `(12, standZ−2)`  
    For Stand 2/3, z−2 is several metres off the lead-in chord, forcing the taxi-out join to cut corners.  
    **Fix:** Push back along −X on the stand centreline to a point on the lead-in segment.

23. **Equal-time taxi segments ≠ equal geometry** — `TaxiVisualPath.PositionAt` / `SegmentFor`  
    A1≈15 m, A2≈20 m, lead-in≈10–19 m all get 1/3 of 25 s → unrealistic speed jumps and desync vs GT’s 14/14/10 s legs.  
    **Fix:** Weight segment windows by chord length (keep reservation index in sync).

24. **Commercial vs GT waypoint timing mismatch** — `AirportTaxiNetwork` vs `GroundTrafficAircraft.BuildCircuit`  
    Same corners, different durations; when both visible on Alpha they appear to take incompatible lines/speeds even when reservations prevent overlap.  
    **Fix:** Share one route table (points + seconds per segment) for commercial and GT.

25. **SmoothStep inside segments bunches motion at corners** — `TaxiVisualPath`  
    Easing makes aircraft crawl near waypoints then rush mid-segment — looks like corner cutting relative to centreline paint.  
    **Fix:** Use linear local progress (or constant-speed arc-length), keep SmoothStep only for airborne.

26. **Taxi A2 edge lights placed on a phantom eastern exit** — `BuildRunwayEdgePointLights` ~4679–4684  
    Lights at `(26/22/18, z=2.2..7)` do not lie on sim A2 `(x=−12..8, z=9)`; night taxi follows lights into nowhere.  
    **Fix:** Place A2 lights along z=9 between x=−12 and x=8 (and lead-ins).

27. **Lead-in “throat” pad always aims at (11,11)** — `CreateTaxiLeadPad`  
    Fixed throat ignores stand Z; Stand 3 asphalt spur points at Stand 1/2 apron instead of the real chord.  
    **Fix:** Throat target = early sample on that stand’s lead-in (e.g. 25% of chord).

28. **Stand 3 paved lead-in overlays Stand 1/2 pavement** — `CreateTaxiLeadPad(26)`  
    Wide chord pad (4.6 m) paints asphalt through other stand boxes.  
    **Fix:** Narrower path or dogleg pads matching the corrected route.

29. **GT Away teleports from offField to Away** — legs Departing→Away  
    Ends Departing at `(−36,−4)` then next leg `From=To=away(−60,−30)` — instant 30 m jump.  
    **Fix:** Add a short reposition leg, or set Away.From = offField.

30. **Commercial TaxiIn starts at A1 while Landing ends at same point — OK, but TaxiOut reverse joinT=0.30 skips most of lead-in visually while sim still reserves lead-in** — `TaxiOutPosition` / `SegmentFor`  
    Visual is already near Alpha while SegmentFor index still says lead-in for the first third of phase.  
    **Fix:** Align `joinT` / routeT mapping with equal (or length-weighted) segment windows.

31. **Look-ahead yaw on taxi uses future segment across corners** — `UpdateAircraftVisual` lookAhead 0.08  
    Near segment boundaries LookRotation aims at the next chord early → nose cuts the corner while wheels follow the polyline.  
    **Fix:** Clamp look-ahead to remain inside the current reservation segment.

32. **GT MoveTowards catch-up at 10 u/s can chord across grass after yield retarget** — `UpdateGroundTrafficVisual`  
    When sim target jumps after Yield/stand change, non-holding MoveTowards takes a straight line not the taxi polyline.  
    **Fix:** After large target deltas, snap (as hold does) or path-follow waypoints.

33. **Reposition circuit never uses stand lead-in geometry** — GT-202 only A1/A2/bay  
    Fine for role, but bay at (5,9) has no paved spur — aircraft sits on unmarked Alpha.  
    **Fix:** Add a short paved run-up stub off Alpha for the bay point.

34. **Commercial pushback Smooth ignores apron paint lanes** — `PositionFor(Pushback)`  
    Straight lerp with no curve onto the marked lead-in.  
    **Fix:** Ease onto the first taxi-out route sample.

35. **Takeoff after hold-short: lineup bezier then jumps to (−20.5,0)** — `TakeoffPosition`  
    Discontinuous heading/path vs taxi-out arrival at (−24,0).  
    **Fix:** Single continuous spline from taxi-out end through roll.

36. **Approach path is runway-axis only — no downwind/base** — `PositionFor(Approach)`  
    ATC issues downwind/base/final while the model flies a straight long final — route/ATC mismatch.  
    **Fix:** Piecewise circuit matching MidDownwind/Base/Final timing windows.

37. **Landing rollout decelerates with quadratic ease but tire RPM stays high** — `LandingPosition` + `RollLandingGearTires`  
    Visual speed drops while tire spin uses constant Landing multiplier — wheels skate.  
    **Fix:** Scale tire RPM by positional delta per frame.

38. **GT Progress linear in time, commercial SmoothStep — meeting geometry differs** — presentation  
    Even on identical segments they don’t share centreline timing.  
    **Fix:** Item 24 shared table; drop SmoothStep on ground.

---

## C — Layering / z-fighting / mesh interpenetration

39. **Ground shadow at y=0.05 fights apron/decals** — `UpdateGroundShadow`  
    Shadow disc coplanar with wear quads (y=0.02) and pad tops (~0.06).  
    **Fix:** Lift shadow to ~0.08–0.12 and/or use a polygon offset / higher render queue.

40. **EnsureGroundShadow local Y −0.65 then world-repositioned** — first frame can flash under terrain.  
    **Fix:** Initialise shadow at apron height in `EnsureGroundShadow`.

41. **Aircraft bank on ground dips wings into apron** — `TurnBankDegrees` up to 8° on taxi  
    Half-span × sin(8°) ≈ 0.5 m — wingtips bury into concrete.  
    **Fix:** Zero bank for ground phases (or ≤2°).

42. **Pitch on landing flare with gear down drives nose gear into runway** — `PhasePitchDegrees` Landing −5°  
    Combined with y=0.7 root and kit offset −0.7, flare pitch stubs the nose.  
    **Fix:** Reduce flare pitch or raise root Y slightly during flare.

43. **Stairs / chocks / GPU share one stand’s equipment while second commercial also at stand** — `UpdateStandEquipment`  
    Non-focused aircraft gets no props; focused stairs can remain where the other fuselage is if focus switches mid-frame.  
    **Fix:** Per-stand prop sets, or hide props for one tick on focus change.

44. **Stairs tip into fuselage** — stairs at `(17.9, z+0.15)` with pitch → cabin  
    Kit long-axis tip can interpenetrate cabin door mesh.  
    **Fix:** Stop X short of the door contact point; clamp pitch.

45. **Chocks Y 0.12–0.42 vs tire contact** — may float or sink through tires after kit densify.  
    **Fix:** Snap chock Y to sampled gear-wheel bottom.

46. **GPU cart at (14.2, 0.35, z+0.15) intersects nose gear / prop arc** — Stand equipment  
    **Fix:** Move beside nose, outside prop disc radius.

47. **Pushback tug at nose during AtStand then lerps through gear** — `UpdateStandEquipment`  
    Tug path `(17→12, z→z−2)` crosses nose-gear track.  
    **Fix:** Offset tug laterally (stand Z − 1.2) during push.

48. **Cabin door opens into stairs** — `UpdateCabinDoor` + stairs arrive early in turnaround  
    Door bias and stairs both active — meshes cross.  
    **Fix:** Open door only after stairs arrive (progress gate).

49. **Prop disc / blade dual draw at RPM threshold** — `ApplyPropBlurToHub`  
    Toggling can show both or neither for a frame; disc at nacelle can z-fight spinner.  
    **Fix:** Hysteresis on highRpm; offset disc along spinner axis.

50. **Engine heat quads inside nacelle / wing root** — `UpdateEngineHeat` scale pulse  
    **Fix:** Author heat pivots behind exhaust; clamp scale.

51. **Nav light PointLights inside wingtip meshes** — `EnsureNavPointLight`  
    Range 8 lights bloom through wing solid.  
    **Fix:** Parent lights slightly outboard of tip verts.

52. **Landing SpotLight casts through fuselage onto cockpit** — soft shadows from nose lamp  
    **Fix:** Culling mask / shorter range / aim further down.

53. **Wet puddle quads at local y=0 z-fight apron** — `UpdateWetPuddles` / puddle build  
    **Fix:** y=0.03–0.05 + transparent queue.

54. **Runway wear decals at y=0.02 vs runway tiles ~−0.08..0** — `CreateDecalQuad`  
    Flicker depending on camera angle.  
    **Fix:** Consistent decal height above max runway top + offset.

55. **Taxi chord pads (height 0.12 at y=0) vs apron slabs (~0.11)** — `CreateTaxiChordPad`  
    Overlapping asphalt/concrete shimmer.  
    **Fix:** Single pavement height constant; pads slightly proud (e.g. +0.01).

56. **Stand 3 apron pads created with mixed Y −0.0035..0.004** — `EnsureStandThreeVisual`  
    Internal z-fight within the pad mosaic.  
    **Fix:** Uniform Y for all Stand 3 pads.

57. **Apron north extension tiles fight Stand 3 pads** — same region z≈23–28  
    **Fix:** Don’t stamp north extension where Stand 3 pads exist (or merge meshes).

58. **Grass pads under taxi leads peek through asphalt edges** — tiled grass Y≈−0.65 with thin pads  
    Grazing overview shows grass sparkle at lead-in edges.  
    **Fix:** Widen leads or depress grass under paved AABBs.

59. **Ground traffic and commercial identical kit at same height 0.7** — no vertical bias when close  
    When reservations fail visually (items above), meshes occupy one volume with no depth cue.  
    **Fix:** Prefer hard separation (A/B fixes); optional tiny Y bias is a last resort only.

60. **Shadow scales with lossyScale and can become huge under parent non-uniform scale** — `UpdateGroundShadow`  
    **Fix:** Use constant world scale for the disc.

61. **Touchdown smoke spawned at aircraft Y+0.15 intersects gear doors** — `UpdateTouchdownSmoke`  
    **Fix:** Emit from wheel contact points.

62. **Taxi spray follows first commercial only and can attach to airborne-ish takeoff &lt;0.48** — `UpdateTaxiSpray`  
    Spray under climbing gear.  
    **Fix:** Require gearBias deployed and altitude &lt; threshold.

63. **Marshaller at fixed world Z=20 overlaps Stand 2 aircraft** — `PlacePerson` Stand 2 marshaller `(22.5,0,20)`  
    Aircraft nose at (17,20) — marshaller under/through nose.  
    **Fix:** Place relative to stand nose (x≈19–20) and animate clear after park.

64. **Flap/spoiler animation rotates whole named mesh through wing body** — `UpdateControlSurfaces`  
    Densified kits may use different pivots.  
    **Fix:** Only rotate authored hinge children; skip if densify parts lack hinges.

65. **Gear door open angle 78° swings through wing fairing** — `UpdateAircraftLightsAndGear`  
    **Fix:** Per-kit door angles; clamp.

---

## D — Other real sim / presentation / HUD / persistence bugs

66. **TryRespawnCommercial omits join-downwind ATC** — `AirportSimulation.TryRespawnCommercial`  
    Fresh AS-xxx after reset gets no `IssueJoinLeftDownwind` (SpawnCommercial does).  
    **Fix:** Record the same ATC join phrase as SpawnCommercial.

67. **NotifyRunwayVacated on Departed after airborne** — settlement block ~630  
    Starts 12 s wake/separation when the aircraft is already at Departed climb; confuses ATC state vs landing vacate.  
    **Fix:** Notify vacate when Takeoff releases runway (phase entry to Departed / end of roll), not as a second vacate event with landing semantics.

68. **IssueReportRunwayVacated logged at Landing entry** — `RecordPhaseClearance`  
    Phrases “report vacated” before rollout begins.  
    **Fix:** Issue that call when transitioning Landing→TaxiIn (with NotifyRunwayVacated).

69. **FocusFlight / CurrentDelay* ignore non-focus delays** — HUD accessors  
    Secondary at-stand delay hidden when focus is the other flight (partially mitigated by FocusFlight-at-stand, still wrong for dual delays).  
    **Fix:** Surface per-flight delay lines for every AtStand commercial.

70. **VisualPhaseProgress AtStand uses Floor(_preciseTime) but other phases use fractional _preciseTime** — stutter at stand vs smooth taxi.  
    **Fix:** Use one time base; prefer `PhaseProgress` for all phases.

71. **Autosave every 15 s even when paused** — `Update` save uses `_clock` which doesn’t advance while paused, but still rewrites file when condition on elapsed holds from before pause… actually `_nextAutosaveSecond` compares clock — OK while paused. However `_preciseTime` still… not advanced when paused. OK.  
    *(replace with real bug)* **Research toast / ops toast can fire on catch-up burst** — MaybeShow* after multi-second jump.  
    **Fix:** Suppress toasts during load catch-up flag.

72. **HudLayout does not clamp stacked right panels to viewport** — `HudLayout.Create`  
    Large `operationsHeight` + report + offer can push `RouteOfferPanel.yMax` past short virtual heights not covered by tests.  
    **Fix:** Shrink operations height or stack/scroll when `nextY + offer > viewportHeight - Margin`.

73. **Left panel fixed 760 cap vs overflowing text in OnGUI** — prototype draws more lines than panel height.  
    **Fix:** Scroll view or dynamic height from content.

74. **Traffic wait monitor Describe returns only first warning** — `TrafficWaitMonitor.Describe`  
    Dual holds only show one aircraft.  
    **Fix:** Join all warning waits.

75. **GT hold reason “awaiting corridor” when DesiredSegment default** — `SynchronizeAllTraffic`  
    Off-field stand wait reports corridor phrasing (`DesiredSegment` default).  
    **Fix:** Branch on warmup/stand-wait vs corridor-want.

76. **OperationalEventLog can retain only Insolvent forever and stop trimming** — `IndexOfFirstRemovable`  
    If capacity filled with Insolvent-titled rows, trim breaks (`removeAt < 0`) and list grows past capacity.  
    **Fix:** Fall back to removing oldest even if Insolvent when nothing else remains.

77. **Day settle closeTime formula brittle** — `SettleDaysUpTo` uses `DaySeconds * (_daysSettled - 8/24)`  
    Relies on StartHour=8; Domain `DayCycle` change would desync weather costing.  
    **Fix:** Centralise “midnight of day N” on `DayCycle`.

78. **Weather costs use closeTime weather, not dominant-day weather** — same method  
    A single instant at day boundary sets the day’s operating weather cost.  
    **Fix:** Sample midday or majority block.

79. **Approach claims stand from spawn but not runway — OK; however second commercial Approach hold log attributes to Primary.AircraftId** — `TrySpawnSecondCommercial`  
    Misleading ops log.  
    **Fix:** Log under a system id or the would-be flight.

80. **CompareFlights sort on respawn reorders list under camera follow index** — mitigated by AircraftId remap, but `CycleOrStartFollow` index can still point at the other aircraft after sort.  
    **Fix:** Follow by AircraftId, not list index.

81. **EnablePriorityCrew mid-turnaround toggles flag off then on** — `TurnaroundWorkflow.EnablePriorityCrew`  
    Brief window where PriorityCrewEnabled is false during compute — any parallel read sees wrong state (safe today only because single-threaded).  
    **Fix:** Compute without clearing the public flag.

82. **Boarding task start uses Max(cleaning, deplane) but CompletionOffset uses cleaningEnd+boarding only** — `Tasks` vs `ComputeCompletionOffset`  
    Load bags can finish after CompletionOffset’s max in some staffing mixes? Actually max(refuel, load, boarding) — boardingStart in Tasks can differ from ComputeCompletionOffset’s boardingEnd = cleaningEnd+18 (skips Max with deplane only via cleaning). Inconsistent remaining times on HUD vs IsComplete.  
    **Fix:** Share one schedule builder for Tasks and CompletionOffset.

83. **CabinDoorBias closes door as soon as Pushback starts — stairs may still be visible one frame** — presentation order UpdateAircraft then UpdateStandEquipment.  
    **Fix:** Hide stairs before door close, or gate door on equipment clear.

84. **Ground traffic enginesOn true while “Run-up hold”** — `GroundTrafficEnginesOn`  
    Run-up hold returns AtStand visual phase but enginesOn is true (only Away/Waiting excluded) — props spin during hold OK; lights use AtStand → taxi lights off / engines on inconsistency.  
    **Fix:** Treat Run-up hold as Pushback/Taxi for lights.

85. **GT Away still UpdateAircraftLightsAndGear with AtStand** — dark cabin while model visible off-field.  
    **Fix:** Hide GT when Away, or use Departed visual phase.

86. **MoveGroundTraffic when holding snaps instantly — good — but rotation still Slerps toward zero-length move** — can leave GT facing wrong way after yield snap.  
    **Fix:** On hold snap, keep last non-zero travel heading.

87. **ReservationConflicts increments only for non-commercial blockers** — dual commercial corridor contention under-counted.  
    **Fix:** Count commercial-vs-commercial stalls separately for soak metrics.

88. **CanLeavePhase Pushback→TaxiOut does not check corridor until ResourcesForPhase** — OK, but YieldGroundTrafficToCommercials clears next phase for all flights every second even when far from transition — GT unnecessarily yields mid-stand when commercial is still on approach.  
    Actually next phase of Approach is Landing (runway+stand), not corridor. TaxiIn next is AtStand. Pushback next is TaxiOut (corridor). So GT yields corridor while commercial still pushing — correct.  
    *(replace)* **Primary-only finance brief concurrency** — verify `DailyFinanceBrief` uses all commercials (GAME.md claims fixed); grep if any path still uses 1.  
    **Fix:** Confirm `concurrentFlights` in brief builder; add test for StandCount=3.

89. **SeededRandomSource / save seed remap** — already fixed; **StableId default Equals**  
    `default(StableId)` has null Value; Equals/GetHashCode null-safe, but `DesiredSegment` default can match empty resource checks inconsistently in ATC strings.  
    **Fix:** Use nullable or explicit `HasValue`.

90. **HudLayout ScaleFor clamps min 0.8 — on small windows virtual UI exceeds physical** — tests expect 0.8 at 1280×720.  
    Panels can draw off-screen physically while virtual layout self-tests pass.  
    **Fix:** Allow lower min scale or letterbox.

91. **OnGUI and Canvas/Toolkit HUD triple-draw risk** — SyncCanvasHud hides canvas when toolkit active, but OnGUI path may still run.  
    **Fix:** Early-out OnGUI when toolkit owns chrome.

92. **Camera follow phase uses commercial progress only** — GT never followable; OK.  
    **Follow yaw bias + bank** can orbit into wing during taxi bank (item 41).  
    **Fix:** Disable ground bank (41) fixes follow too.

93. **Persistence: commands at second 0 replay — OK; BuildThirdStand visual only via EnsureStandThreeVisual** — if capacity expands mid-session before Ensure runs, lead-in 3 exists in sim without asphalt for a time.  
    **Fix:** Call EnsureStandThreeVisual immediately after successful build command.

94. **AirsideSaveData.Migrate rejects &gt;Current but Validate duplicates check** — fine.  
    **commands null coalesced in Validate but not in Migrate** — null commands NRE if Migrate called without Validate.  
    **Fix:** `commands ??= new()` in Migrate.

95. **AirportRoutes.Update marks expired offers as declines** — correct per #157; rapid catch-up can increment OffersDeclined many times without player seeing offers.  
    **Fix:** Separate MissedOffers counter for HUD honesty.

96. **ATC IssueGroundHold only breaks after first holding GT** — second holding GT silent.  
    **Fix:** Rotate / list all GT holds.

97. **Commercial TaxiIn holds Corridor entire phase — serialises dual traffic heavily** — by design, but presentation shows long nose-to-tail queues that look “stuck” without hold-short markings.  
    **Fix:** Draw hold-short bars at A1 entry / Alpha junctions when waiting.

98. **PhaseDurations AtStand 45 vs turnaround CompletionOffset** — early leave allowed; PhaseDurationSeconds still 45 for VisualPhaseProgress non-bound path. Bound path OK. Pushback VisualPhaseProgress uses 12 s even if… OK.  
    **Landing lights stay on during whole Takeoff including climb** — `landingLights` true for all Takeoff.  
    **Fix:** Extinguish landing lights after rotate (progress≥0.48) or when gear retracts.

99. **Taxi lights require `!airborne` but airborne false while gearBias≥0.5 on takeoff roll — OK; after retract airborne true — taxi lights off. Approach night: taxiLights false because airborne true** — only landing lights. OK.  
    **Beacon uses unscaledTime — keeps blinking while sim paused** — noticeable pause break.  
    **Fix:** Drive beacon with presentation/sim time that freezes on pause.

100. **Windsock / flag / birds use unscaledTime while paused** — same pause leak as 99.  
     **Fix:** Multiply those motions by `PresentationDeltaTime` / paused gate.

---

## Suggested first fix batch (highest leverage)

| Order | Items | Why |
| --- | --- | --- |
| 1 | 1, 2, 3, 16 | Hard wingtip / lead-in collisions |
| 2 | 4, 5, 6, 11, 12 | GT + GSE on live taxi/runway |
| 3 | 21, 22, 23, 24, 30 | Route fidelity GT vs commercial |
| 4 | 7, 8, 10 | Dual approach stacking + hold creep |
| 5 | 39, 41, 55, 56 | Overview z-fight / wing-in-apron |

## Verification notes

- No Unity editor in this environment; items are code-trace defects.
- Prefer probes in EditMode (dual-stand wing distance, lead-in vs stand AABB, GT hold position, taxi-out off-polyline deviation) before presentation playtest.
- `scripts/test-domain.sh` for Simulation/Domain; Mac `scripts/test-unity.sh` before merge of fixes.
