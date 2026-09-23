# 0108 — Beginner aircraft airframe shapes

Date: 23 September 2026.

## Decision

Correct the ATR 42's out-of-order forward fuselage stations so the nose runs
monotonically to its intended tip. Re-seat the taxi light on that corrected
underside. Retain the Dash 8 Q400 and Saab 340B station envelopes but keep
their forward cross-sections fuller before the radome cap, avoiding a needle
silhouette. The Saab's box wing-root fairings become rounded lofts. Match the
separate radome shells to the revised skin without covering cockpit glazing.

## Reason and limits

These are the most visible physical-model defects at the normal miniature
camera angle. The ATR station reversal was also a genuine interpolation error.
The work improves silhouette and surface continuity, not exact manufacturer
measurement. It leaves wings, propeller animation, gear articulation, pivot,
type identity and the prior fitted liveries intact.

## Affected systems and migration

AIR-001/006/007 procedural geometry, their runtime glTF/FBX kits and Hangar
thumbnails only. Existing kit paths, object names, planning envelopes and
fallbacks remain. No simulation, command, save format, control, route, data
source or migration. Geometry is original Airside work, without downloaded
models or real logos.

## Acceptance

Generator and shape-regression checks pass; every named part connects to the
fuselage within 5 cm; before/after multi-view renders are inspected; a Mac
build compiles and packages the synced art.
