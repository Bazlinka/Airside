# Fitted door and service details across the fleet

Date: 2026-10-07. Task #578. Owner: Codex / Bazlinka.

## Decision

Extend the existing fleet livery presentation with one shared, deterministic
surface-detail pass for all 14 aircraft. Derive door seams, handle surrounds,
latches, thresholds, hinge marks and forward service-hatch outlines from the actual
outward-facing source triangles. Cargo doors use paired lower latches; the Bell's
sliding doors get an upper rail. Retain the original models, fictional identities,
operator palette, markings and neutral airframe finish.

## Reason

The fleet already has different paint silhouettes. Fine detail was uneven: some
widebody doors were plain leaf meshes while other types had separate hardware.
A common finish improves close views without regenerating approved geometry or
adding a second set of asset paths and prefab bakes.

## Integration

ArtPresentationLoader applies the pass to both prefabs and glTF aircraft before
rig construction. Patches follow source triangles at 6 mm separation (12 mm for
handle bars), live beneath their panel transforms and follow existing hinge
rebakes. Two shared metal materials per panel, no per-frame construction, no new
textures/colliders. A generated-mesh owner tracks hinge-rebaked replacements.
Missing or unreadable art retains its existing fallback. Source asset IDs and
StreamingAssets mirrors are unchanged.

## Affected systems and migration

Aircraft presentation and pure clipping geometry only. No simulation, aircraft
metrics, save schema, service layout or persistence migration. Revert the loader
calls to restore the prior appearance. The user explicitly authorised
implementation and merge with light testing; native Unity appearance and motion
remain unverified. Evidence: `docs/testing/fleet-surface-details-2026-10-07/`.
