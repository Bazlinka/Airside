# 0069 — Real YPAD operational buildings and painted stand labels

Date: 2026-09-21

## Decision

Replace the apron stand-reference `TextMesh` labels with the existing stroke-painted marking
alphabet, and add the missing Adelaide Airport operational context from a dated OpenStreetMap
snapshot: 78 footprints covering the control tower, fire station, hangars, freight/catering and
support buildings.

The existing detailed terminal and Royal Flying Doctor Service shells remain authoritative and
are filtered out of the new snapshot. Retail, residential, roof/carport objects and the broad
airport boundary are also excluded. Buildings are rendered as simple footprint-accurate prisms;
the control tower gets a narrower shaft and full-footprint cab so its silhouette reads at the
overview scale. This is a location/fidelity layer only and does not alter simulation, navigation
or save data.

## Reason

The game already had a mature aircraft, GSE, character, material and HUD asset stack. Replacing
those wholesale would throw away integrated work without evidence that it is the current visual
bottleneck. The packaged Mac build instead showed two specific gaps: stand identifiers were
floating font geometry, and the real Adelaide apron sat in an implausibly empty field beyond the
terminal. These changes address the observed gaps with source-controlled, reproducible geometry.

OpenStreetMap supplies footprint and tagged height data for the real airport landmarks. Where a
height is absent, the generator uses a documented category default; this is presentation
approximation, not a claim about exact building height. The normalized JSON snapshot is committed
so builds do not depend on live network data.

## Consequences

- The airport now has recognisable operational massing in the correct runway coordinate frame.
- `scripts/generate-ypad-buildings.py` can reproduce `AdelaideBuildings.cs`; importing a newer
  `.osm` extract remains an explicit, reviewable action.
- OSM attribution is already continuously visible in the packaged game and the dataset is
  registered in `docs/data/ASSET_AND_DATA_REGISTER.md`.
- Footprint prisms are the correct first fidelity step, not finished hero models. The terminal,
  control tower cab glazing, hangar doors and roof forms can receive authored detail later without
  moving their surveyed footprints.
- Existing aircraft/GSE/people/UI assets remain in place until a visual review identifies a
  concrete replacement need.

## Verification

- `scripts/test-domain.sh`: 546/546.
- Unity EditMode: 780/782. The new stand-label and building tests pass; the two failures are the
  pre-existing `AirlineSaveTests.VersionFiveSave_MigratesSingaporePlaceholderTo787WithoutLosingRotation`
  and `TerminalGateOperationsTests.Reservations_GateLeadInAndRunwayHeldBeforeMovementAndReleased`.
- Fresh packaged Mac build completed. Deterministic 1920×1080 review captures verified the stand
  paint, operational-building massing and the control-tower shaft/cab silhouette in the real game.
