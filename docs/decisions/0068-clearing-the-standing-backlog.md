# 0068 — Clearing the standing backlog

Date: 2026-09-21

## Decision

Bailey asked what was still open across the session and then said "fix all of em." Seven items,
in the order GAME.md had them flagged:

1. **Go-around teleport, smoothed.** `FleetGoAroundRejoinWorldPosition` (new) blends the world
   position from where the go-around's own racetrack ends to the ordinary pinned holding
   position over `GoAroundRejoin.BlendSeconds` (6s, eased). The two systems have no shared
   parameterisation to interpolate through physically — a fixed-radius circuit at 305m versus a
   queue-slot-pinned point on the glideslope — so this is a cosmetic position blend, not a
   simulated rejoin flight path. Gear and pitch still snap at the same instant as before (both
   driven by phase+progress, untouched here) — a smaller residual gap than the 400-1,100m/
   210-250m position jump this fixes, and not attempted blind alongside it.
2. **Terminal glazing now follows the real wall.** `AdelaideTerminalArchitecture.AirsideWallZAt`
   interpolates Z from the exact vertices already authored in `AdelaideLayout.Terminals`'s OSM
   footprint, replacing a single hardcoded Z (435.55) that put the near end of the glazing run
   2.14m off the real wall. Mullions measure "proud" from their own left-neighbour bay's Z
   rather than a second wall lookup at the gap's position — the wall's slope is steep enough in
   a couple of segments that a fixed absolute offset could land level with or behind that bay's
   own glass.
3. **Rain and clouds drift with the real wind**, not a fixed direction. Both now read
   `AirlineOperations.Wind` (the same source the windsock already used — previously the *only*
   wind-reactive visual) via `RunwayWeather.UnityYawFromTrue`, and wrap on both axes instead of
   just X.
4. **Overcast now shows visibly more cloud than a partly-cloudy day.** Each of the fixed 16 (9
   demo) cloud clusters has its own `CloudCover` reveal threshold spread evenly 0..1, ramped
   over a small band so a cluster fades in rather than popping. Regenerating cluster geometry
   per weather change was rejected as unnecessary risk/cost; toggling the alpha of already-built
   clusters achieves the same "more cover, more cloud" reading.
5. **Airline rename now has a HUD control.** A `GUI.TextField` + RENAME button in the Stats
   workspace's header, right-aligned before CLOSE — the one raw-IMGUI exception in an otherwise
   painter-driven page, since the shared `HudDrawList` has no editable-text primitive.
6. **The Stats page's empty lower-right filled with real content**, not padding: "N of M
   milestones reached" under the Milestones caption, and a lifetime "N contracts fulfilled
   all-time" line below Recent Contracts (`AirlineCareerState.CompletedContractIds.Count` —
   uncapped, unlike the 10-entry `ContractHistory` list, so it stays accurate once a career
   outgrows that list).
7. **A stroke-painted alphabet now exists for stand/gate references** (0-9 plus the exact
   letters Adelaide's real bay and gate references use — A-G, L, R — verified against
   `AdelaideLayout`, not guessed), extending `AirsideStripMarkings`'s existing runway-numeral
   7-segment system. **Not wired into the actual stand labels** — see Consequences.

## Reason

Every item was already diagnosed in a previous session pass; this round's job was fixing them,
not rediscovering them. Two needed a materially different approach than "just do the obvious
thing" to stay within what's safe to ship without a Unity editor to check the result:

- The go-around teleport was explicitly deferred twice before ("too risky to fix blind... a
  wrong blend could look worse than the current hard cut") because a *physically accurate*
  rejoin needs two structurally different flight-path systems reconciled. The insight this round
  is that the fix doesn't need to be physically accurate to stop reading as a bug — a smoothed
  cosmetic blend between two already-correct endpoints removes the jump without inventing new
  flight-path geometry to get subtly wrong.
- The apron stand-label alphabet was flagged as "a real, larger follow-up, not attempted blind"
  for good reason: swapping the label rendering path from a currently-working, legible bold
  `TextMesh` to hand-authored stroke geometry (position, rotation, scale all matching each
  stand's own `LabelYawDegrees`/`LabelCharacterSize` convention) is real integration risk for a
  purely cosmetic consistency win, with zero way to catch a wrong orientation or a garbled
  glyph without seeing it rendered. Building and thoroughly testing the *alphabet itself*
  carries none of that risk (it's pure geometry generation, fully exercised by
  `AirsideStripMarkingsTests`) and is real, usable progress; wiring it into the actual paint
  is the part that still needs eyes on a result, so it stays undone here rather than risk
  breaking working labels on a purely stylistic upgrade.

## Consequences

- (1), (3), (4), (5) are Presentation/Unity-only, reviewed by inspection — reasoned and traced,
  not confirmed by eye, same standing caveat as the rest of this session's Presentation work.
- (2), (6), (7) are the first Presentation fixes this session with *real* automated proof
  beyond inspection: `AirsideAdelaidePavement.cs` and `AirsideStripMarkings.cs` hold no
  UnityEngine types and are already in the headless harness, and (6)'s Model/Layout/Painter
  triad is the same UnityEngine-free ADR 0057 pattern the whole Stats workspace already uses.
- (6) caught a real bug in its own first draft during this pass: reserving worst-case space in
  `VisibleMilestones` for the new fulfilled-line without also bounding it defensively let the
  line run 24px past the footer on a cramped stacked layout (1024×640, a real tested viewport)
  once a career had a full history list. Fixed with the same defensive floor-check
  `ContractsWorkspacePainter.PaintActive` already uses for its own terms list — stop drawing
  rather than overflow — and caught before merge by a new test that paints a fully-populated
  worst-case model at every tested viewport and inspects the actual draw-list output, not just
  the layout formula (which is exactly what let the bug through the first pass).
- (7)'s `GlyphMask` cannot render every letter unambiguously — a known, accepted limit of
  7-segment displays: B and D use the same lowercase-style forms a digital clock would (visually
  close to 8/0), and R is the minimal two-segment form conventionally used where a 7-segment
  alphabet needs one at all. Every supported character's mask is verified distinct from every
  other's (`NoTwoSupportedGlyphsPaintIdentically`), so within this specific alphabet nothing is
  actually ambiguous even though the shapes are stylised.
- `AirsideStripMarkings.DesignationNumerals`'s existing runway-numeral output is unchanged byte-
  for-byte (`DesignationNumerals_StillPaintsTheRealRunwayEndsUnchanged`, and the original 0/1/2/
  3/5 masks are asserted to their exact prior values) — extending the alphabet touched a shared
  private method, so proving the existing, already-shipped 05/23/12/30 markings didn't move was
  the bar for calling this safe.

## Tests

`scripts/test-domain.sh`: **542/542** (16 new tests across `GoAroundRejoinTests`,
`AdelaideTerminalArchitectureTests`, `AirsideStripMarkingsTests`, and `StatsWorkspaceTests`).
Stats workspace re-rendered via `scripts/hud-mockup` + `render-hud-mockups.py` to confirm the
new milestones-summary and lifetime-fulfilled lines lay out cleanly (the rename text field is
raw IMGUI outside the draw list the mockup renders, so it's covered by layout-bounds tests
instead — `scripts/test-domain.sh`, not a render). Items (1), (3), (4), (5) remain
reasoned-not-confirmed, same as the rest of this session's Presentation work; a real Unity look
is still the standing, cumulative ask underneath all of it.
