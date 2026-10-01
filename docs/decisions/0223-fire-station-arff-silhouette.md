# 0223 — Fire-station / ARFF silhouette accuracy

Date: 2026-10-01 · Owner: Cursor

**Decision:** restyle the surveyed Adelaide Airport Fire Station
(`OSM-217602806`, `AdelaideBuildingKind.FireStation`) with presentation-only
`BuildingDetail` geometry: taller/wider appliance bay doors, a hose-drying /
observation tower with obstruction light, concrete appliance parking pads with
yellow bay cues, and a safety-yellow sign fascia over the bay header. Reuse
existing `BuildingPart` materials (Trim, Equipment, EquipmentRed, Canopy,
ObstructionLight). No new assets, no footprint or height change.

**Reason:** Phase 3 airside accuracy — the station already has a low roof,
plinth, lit packs and red crash tenders (ADR 0124 / 0199 / 0213), but from
overview it still reads as a low shed. A hose tower, wider bays and forecourt
pads are the ARFF silhouette without Bailey photos or a new mesh kit. The
regional ARFF fidelity board (`ref_regional_arff_facility_fidelity_v01`) and
legacy `mdl_arff_*` prototype shed stay out of scope: YPAD uses the OSM
civic station (red-brown shell), not the pale-grey Kingscote-style shed.

**Affected systems:** `BuildingDetail` / `BuildingDetail.Interiors` fire-station
path; `BuildingDetailTests`.
**Migration impact:** none.

## Current path (baseline)

| Piece | Where | Today |
|---|---|---|
| Footprint | `AdelaideBuildings` `OSM-217602806` | 8.0 m, 6-vertex OSM polygon; longest edge ≈ 35.1 m |
| Kind switch | `BuildingDetail.For` | `AddApplianceBays` → windows → plinth → low roof (`RoofRiseScale`) |
| Bay sizing | `AddApplianceBays` | ≤5 bays; `doorHeight = min(5.2, h−1.5)`; `doorWidth = min(4.6, pitch−1.2)`; pitch divisor 6 m → **4 bays × 4.6 × 5.2** |
| Appliances | `AddAppliance` | `EquipmentRed` crash tender (cab/body/ladder/wheels/light bar) when `BoxInside` |
| Shell colour | `YpadPavement` | `(0.48, 0.24, 0.20)` red-brown batch `YPAD fire station` |
| EquipmentRed spawn | `SpawnBuildingDetail` | `(0.72, 0.09, 0.07)` shared mesh |
| Legacy ARFF | `AirsidePrototype` CreateBlock / prefab | Hardcoded field props near (−28, 30) — **unchanged** |

Audit note (`docs/data/ypad-buildings-audit.md`): height 8 m is rule-default
(unsourced). This ADR does **not** change surveyed height.

## Geometry constants (public on `BuildingDetail`)

Promote today’s private appliance sizes and the new silhouette knobs so
headless tests can lock them (same pattern as tower cab scales in ADR 0221).

```csharp
// Existing (make public; keep values unless noted)
public const float ApplianceLengthMetres = 8.2f;
public const float ApplianceWidthMetres = 2.6f;

// Bay door / pitch (changed)
public const float ApplianceBayPitchDivisorMetres = 5.5f;   // was 6f
public const int ApplianceBayMaxCount = 4;                  // was 5 — keeps ~4 full-width bays on OSM-217602806
public const float ApplianceBayDoorHeightMaxMetres = 6.2f;  // was 5.2f
public const float ApplianceBayDoorWidthMaxMetres = 5.2f;   // was 4.6f
public const float ApplianceBaySideClearanceMetres = 0.9f;  // was 1.2f
public const float ApplianceBayHeaderClearanceMetres = 0.85f; // was 1.5f (height − clearance)

// Hose / observation tower
public const float HoseTowerShaftWidthMetres = 1.8f;
public const float HoseTowerShaftHeightMetres = 12.0f;      // from baseY; ~4 m above 8 m eave
public const float HoseTowerCabinHeightMetres = 1.6f;
public const float HoseTowerCabinScale = 1.35f;             // cabin wider than shaft
public const float HoseTowerMastMetres = 2.4f;

// Forecourt pads (Canopy = light concrete already spawned)
public const float AppliancePadThicknessMetres = 0.08f;
public const float AppliancePadLengthMetres = 9.0f;         // outward from wall
public const float AppliancePadWidthExtraMetres = 0.6f;     // doorWidth + extra
public const float AppliancePadOutsetMetres = 0.35f;        // start just outside wall

// Sign fascia (Equipment = yellow-orange conspicuity; no baked glyphs)
public const float FireStationFasciaHeightMetres = 0.55f;
public const float FireStationFasciaLengthFraction = 0.38f; // of front edge
public const float FireStationFasciaDepthMetres = 0.12f;
```

On the surveyed station these yield ≈ **4 bays**, door ≈ **5.2 × 6.15 m**,
hose tower top ≈ **baseY + 12 + 2.4 + light**, pads ≈ **9 × 5.8 m** per bay.

## Implementation sketch (pure math)

1. **`AddApplianceBays`** — swap magic numbers for the public consts above;
   keep open/closed shutter logic and `AddAppliance` unchanged in shape.
2. **`AddFireStationHoseTower(set, xz, front, baseY, height)`** — pick a
   point inside the footprint opposite the front edge (centroid biased away
   from the bay wall, `Contains` + margin ≥ shaft/2). Emit Trim shaft,
   wider Trim cabin, Trim mast, one `ObstructionLight`. Call from the
   `FireStation` arm of `For` after bays/roof.
3. **`AddAppliancePads`** — for each bay centre along the front edge, emit a
   thin `Canopy` slab outward (`outward ≈ PadOutset + PadLength/2`) and two
   thin `Equipment` edge strips (yellow bay cues). Pads may sit slightly
   outside the footprint slack already allowed by
   `EveryBuilding_KeepsItsDetailOnOrAboveItsFootprint` (8 m).
4. **Sign fascia** — one `Equipment` `OnWall` plate centred on the front
   edge above `doorHeight` (alongside the existing Trim header bar). No text.
5. Do **not** add `BuildingPart` values unless Canopy-for-pads proves
   confusing in review; prefer zero spawn-path edits.

## Exact files to edit

| File | Change |
|---|---|
| `game/Airside/Assets/Airside/Presentation/BuildingDetail.cs` | Public consts; `FireStation` switch calls hose-tower helper |
| `game/Airside/Assets/Airside/Presentation/BuildingDetail.Interiors.cs` | Bay sizing; `AddFireStationHoseTower`; pads + fascia |
| `game/Airside/Assets/Airside/Tests/EditMode/BuildingDetailTests.cs` | New FireStation locks (below) |
| `docs/decisions/0223-fire-station-arff-silhouette.md` | This ADR |
| `GAME.md` / `CHANGELOG.md` / `docs/plans/visual-overhaul-plan.md` | Status |

Optional (only if Canopy reuse is rejected in implementation): one
`BuildingPart.Apron` + spawn colour in
`AirsidePrototype.YpadPavement.cs` — still presentation-only.

## Headless tests to add / extend

File: `BuildingDetailTests.cs` (already in `scripts/dotnet-harness`).

1. **`FireStation_HasTallerWiderApplianceBayDoors`**
   - `Of(FireStation)` → openings and/or Door boxes.
   - Max door/opening height ≥ `ApplianceBayDoorHeightMaxMetres − 0.05`.
   - Max door/opening width ≥ `ApplianceBayDoorWidthMaxMetres − 0.05`.
   - Door top still `< HeightMetres`.
2. **`FireStation_HasHoseTowerAboveTheRoof`**
   - At least one Trim box with `Top > HeightMetres + 3`.
   - Exactly one `ObstructionLight` (shared with tower buildings globally —
     assert station set has ≥1, or assert light above station roof).
   - Shaft XZ inside footprint (`Contains`).
3. **`FireStation_HasApplianceParkingPadsAndYellowBayCues`**
   - ≥1 `Canopy` box with `Height ≈ PadThickness` and centre outside the
     front wall (dot with outward > 0).
   - ≥2 thin `Equipment` strips associated with bay width.
4. **`FireStation_HasYellowSignFasciaAboveTheBays`**
   - ≥1 `Equipment` box on/near front wall with
     `Height ≈ FireStationFasciaHeightMetres` and
     `Y > doorHeight` band.
5. Keep / do not weaken:
   - `Hangars_HaveADoorAndFireStationsHaveApplianceBays` (door count 1–5)
   - `FreightAndFireStation_GetLowRoofsPlinthsAndLitWallPacks`
   - `EveryBuilding_KeepsItsDetailOnOrAboveItsFootprint`
   - `RoofPlant_SitsInsideTheRoofAndIsTheSameEveryRun` (determinism)

Run: `scripts/test-domain.sh` (BuildingDetail suite). Unity EditMode when on Mac.

## Acceptance criteria

- Player-visible: from overview, the OSM fire station reads as ARFF — wide
  open/closed bay doors, red tenders, hose tower silhouette, forecourt pads,
  yellow fascia — without new meshes or textures.
- Pure-math: all new geometry comes from `BuildingDetail.For(FireStation, …)`
  boxes; headless tests lock constants without Unity.
- Footprint, 8 m height, kind, Id and name unchanged.
- No baked sign glyphs; no simulation / reservation / save changes.
- Triangle budget: one station × (~4 pads + tower ~6 boxes + fascia) is
  negligible vs suburb trees.

## Unchanged systems

- `AdelaideBuildings` / `generate-ypad-buildings.py` / OSM snapshot
- Domain, Simulation clock/reservations, save schema
- Control tower (ADR 0221), hangar tenant colours (ADR 0222)
- Legacy field ARFF shed/truck (`AirsidePrototype` CreateBlock /
  `mdl_arff_shed_*` / `mdl_arff_truck_*`) and ARFF lightbar blink
- Terminal / freight / hangar hollow equipment paths
- Phase 2 tree / palm / windbreak placement
- Shell batch colour for `YPAD fire station` (unless a later facade ADR)

## Out of scope / backlog

- Sourced height from AIP / imagery (audit still marks default)
- Replacing red-brown shell with pale-grey corrugated cladding atlas
- Wiring Resources ARFF truck prefab to the OSM station forecourt
- Animated bay shutters or responding appliances
- Second fire-station footprint if OSM gains one
