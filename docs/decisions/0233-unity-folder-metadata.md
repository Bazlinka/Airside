# ADR 0233 — Preserve authored Unity folder metadata in Git

Date: 6 October 2026. Status: implemented repair in PR #528; Bailey explicitly authorised integration onto main.

The asset audit on main reports a Resources directory without metadata and three
orphan Animation folder metadata files. Resources.meta was explicitly ignored;
Git did not retain the empty Aircraft, Vehicles and World directories.

Track Resources.meta with one stable GUID and retain the three existing Animation
folder GUIDs by committing .gitkeep files inside those directories. The audit
ignores .gitkeep, which Unity treats as a hidden Git sentinel, while continuing to
check the folders themselves and all imported files. Do not delete existing GUIDs
or weaken duplicate/malformed GUID, orphan metadata or packaged mirror checks.

No gameplay, art geometry, save format, animation content or third-party asset is
introduced. Native compilation is not asserted. Scope: .gitignore, Resources.meta,
three folder sentinels, the audit, and handoff documentation. Acceptance: audit
passes both in the workspace and from a fresh tracked checkout; a real missing
metadata file or duplicate GUID still fails. Evidence and commands live in
`docs/testing/folder-metadata-2026-10-06/README.md`.
