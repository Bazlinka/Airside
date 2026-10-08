# Save recovery — 8 October 2026

Task #599; base main `8f735ae3`; branch `codex/save-recovery-20261008`.

The existing atomic writer retains one previous readable JSON as `.bak`.
Continue prefers the primary and falls back if it is missing or unreadable.
The title summary labels recovered data and an amber toast warns of missing
recent changes. Writing after recovery preserves the good backup when the
primary was missing or unreadable.

## Verification

- Headless main baseline: **1,985 passed / 3 failed / 1,988 total**; Unity NUnit 3.5 compile check passed. Failure details: `baseline-results.txt`. Changed Unity-dependent files are excluded from this harness.
- Native Unity compiled; **2,514 passed / 3 failed / 2 inconclusive / 1 skipped**, 2,520 total, 322.5 s. All **11 AirlineSaveTests passed**, including both new recovery cases and atomic-write/corruption coverage. Summary and exact failures: `native-results.json`.
- Both runs fail the same unchanged tests: busy-day ground separation (three waiting/taxiing overlap episodes), night-sky review framing and night-sky review yaw. No changes to those systems or assertions are included. The full regression is not green.
- Asset audit passed: 1,881 unique GUIDs, 407 identical runtime art mirrors, 70 character materials. `git diff --check` passed.
- Full native XML/log remain in ignored `work/editmode-results.xml` and `work/editmode-test.log`.

Native fixtures use random temporary paths and clean up primary, temporary and
backup files. Coverage: healthy primary preference, repeated backup rotation,
corrupt/missing primary recovery, failed read when both copies are corrupt, and
backup preservation plus a readable fresh primary after recovery.

## Limits and manual check

No real player save is modified. Schema and simulation/economy behaviour are
unchanged. Parseable saves rejected by semantic restore still use the existing
refusal. No packaged title/toast visual acceptance or performance claim is made.

For a packaged UI check, use an isolated test save, save twice, corrupt its main
JSON, relaunch, inspect “Recovery copy” on the Continue card, then Continue and
check the warning. Never damage the player's actual save for this check.
