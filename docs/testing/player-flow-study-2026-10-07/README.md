# Player-flow design study verification

7 October 2026; task #567. All screenshots here are browser prototype captures, not native Unity screenshots. Runtime Unity code and saves are outside this change.

`verification.json`: twelve Chromium journey groups pass, covering setup/identity, plan/review/back, booking/cancel, stand reservations, contract completion/abandonment, purchase/sale/base, maintenance/waits, ferry/cargo/remote cameras, repeats/away catch-up, attention/blocker views, backup recovery and settings. Model integrity separately verifies valid/malformed saves, duplicate bookings/settlement, incompatible stands and separate inbound-ferry stand reservations.

At 1440×900, 1280×720, 900×720, 800×600 and 390×844, all main workspaces have no document-level horizontal overflow; large/dark settings and modal focus/default restoration are exercised. Zero page JavaScript errors and zero failed non-favicon resource responses were observed. Scrollable tables are intentional. Static browser checks do not establish human task discoverability or game performance.

`checks.json`: Node syntax checks pass for both scripts. Required supplementary `scripts/test-domain.sh` completed with **1,842 passed, zero failed** and Unity-NUnit compile-only check completed. No Unity run, native font/input check, game command integration, physical movement or frame-time test was performed.

Reproduce with the docs server from the repository root:

```bash
python3 -m http.server 8765 --directory docs
python3 scripts/verify-player-flow-study.py
```

The test needs Python Playwright, Chromium at `/usr/bin/chromium`, and Node for model checks. The prototype itself needs only a browser. The test creates its own browser contexts and never reads a real airline save. Runtime verification remains a separate future implementation task.
