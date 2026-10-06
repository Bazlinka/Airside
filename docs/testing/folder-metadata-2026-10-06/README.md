# Unity folder metadata repair verification — 6 October 2026

Task outcome: make the existing Unity asset audit pass in a tracked checkout,
preserving authored folder GUIDs. Scope: Resources.meta, three Animation folder
sentinels, .gitignore and the audit's treatment of .gitkeep. ADR 0233. No gameplay,
rendering, save-format or imported asset content change.

## Results

- Baseline on main at `19fd9688`: audit fails for missing Resources.meta and orphan
  Animation/Aircraft.meta, Vehicles.meta and World.meta.
- Repaired workspace: `python3 scripts/audit-unity-assets.py` passes — **1,663
  unique GUIDs, 349 byte-identical runtime art mirrors, 70 committed materials**.
- Fresh tracked checkout, created with `git checkout-index --all` into ignored
  `work/folder-metadata/checkout/`: the same audit passes with the same counts.
  This proves the directories persist without relying on untracked local folders.
- In that disposable checkout, deleting Resources/Airside.meta causes the expected
  missing-metadata failure. Restoring it returns the audit to normal.
- Giving Resources.meta the same GUID as Resources/Airside.meta causes the
  expected duplicate-GUID failure. Restoring it returns the audit to normal.
- Final audit after both fault checks passes. Existing Animation folder metadata
  files are unchanged; their GUIDs are retained. Whitespace checks pass.

Only .gitkeep is exempted as a Git-only hidden sentinel; directory metadata and
all imported asset files remain checked. No new simulation tests are needed for
this metadata/checker repair. No Unity editor is installed in this worker, and no
native compilation, visual review or new playtest acceptance is claimed.

## Next

Review this independent repair PR. AI freight remains in draft PR #527 with native
verification pending. Cargo apron/handling and outstation freight remain backlog;
this repair does not implement those features or imply release readiness.
