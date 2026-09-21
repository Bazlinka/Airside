# 0080 — Build identity so two Macs can be compared

Date: 21 September 2026. Bailey: after a merge and a rebuild, tell which version
is running where. On the other Mac, `git pull` and a rebuild sometimes shows a
feature and then it is gone.

## Decision

Bake a git stamp into the running game. It is not committed.

`scripts/stamp-build-identity.sh` writes
`game/Airside/Assets/StreamingAssets/build-identity.txt`:

- `commit` — 8-character SHA
- `commitFull`
- `branch`
- `subject` — first-line commit subject
- `committedAt` — committer time (`%cI`)
- `dirty` — `true` when `git status` is not clean (the stamp file itself does not count)
- `stampedAt` — UTC time this file was written

The file and its Unity `.meta` are gitignored. Committing them would churn and
could name a different commit from the one that contains them.

When it is written:

- Unity editor load, if the commit / branch / dirty flag changed
- Entering Play (`--refresh`, so "built" is when this editor session started)
- `scripts/build-mac.sh`, before Unity copies StreamingAssets into the `.app`
  (`--refresh`). The Mac `Info.plist` `CFBundleShortVersionString` and
  `CFBundleVersion` are set to the same short SHA, with `-dirty` if needed.
  Finder Get Info shows that.

What the player sees:

- Always, bottom edge, left of the map credit: `927634e1 · main` or
  `927634e1 · main · local changes`. Missing stamp: `build unstamped`.
- Pause menu, under Quit: that line plus `committed … UTC · built … UTC`
  (both clocks converted to UTC so two Macs agree).
- One `Debug.Log` at startup: `Airside ` plus the pause-menu line.

`BuildIdentity` in Domain parses the text. Presentation only reads the file.

## Reason

A feature that appears on one Mac and vanishes on the other is almost always a
different HEAD, uncommitted edits, or an `.app` / editor session that was not
rebuilt after the pull. The SHA and the dirty flag make that visible without
opening a terminal. Commit time says which source; built time says whether
this machine actually restamped after the pull.

## Affected systems

`scripts/stamp-build-identity.sh`, `scripts/build-mac.sh`,
`scripts/rebuild-and-open-mac.sh`, `BuildIdentity`, `BuildIdentityReader`,
`BuildIdentityStamp` (editor), pause menu and bottom-edge label, `.gitignore`.

## Migration impact

None. The stamp is not saved and does not change simulation or save schema.

## Guardrails

- Do not commit `build-identity.txt`.
- Domain stays free of Unity and of file I/O.
- A missing stamp must not stop the game.
- Editor batch tests do not run the stamp (no git dependency in `test-unity.sh`).
