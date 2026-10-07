## Summary
<!-- What changed and why, in a few bullets. Link the task issue: Closes #NNN -->

## Scope
<!-- Files or folders touched; what must remain unchanged. Owner: tool + account. -->

## Verification
<!-- What you ran and the real result, e.g. `scripts/test-domain.sh`: N passed / M failed. -->

## Not verified
<!-- Be explicit. Cloud tools cannot run Unity: say "unverified in Unity" and list what to check on the Mac (look, performance, UnityEngine tests). -->

## Docs updated
- [ ] `GAME.md` "Where to resume" block replaced (not stacked) — if state changed
- [ ] `CHANGELOG.md` one line — if behaviour changed
- [ ] ADR `docs/decisions/YYYY-MM-DD-slug.md` — if a design decision changed

## Test plan
- [ ] CI `headless` green
- [ ] Mac: `scripts/test-unity.sh` and anything under "Not verified"
