# Writing for Airside

How the game talks to the player. This covers every string the player can read: the HUD,
toasts, the Flight Manual, the title screen, setup, contract cards and refusal messages. The
rules are in ADR 0129.

## Voice

- Talk like a duty manager on the radio to a colleague: short, plain, a bit dry.
- Say "you" and "your airline". Never "the player".
- Say what happened, then what to do. "A 737-8 costs $28,000. You have $19,400." — not "Unfortunately
  you do not currently have sufficient funds to purchase this aircraft."
- No cheerleading or marketing ("Awesome!", "exciting new routes await"). One "!" is allowed for
  a real milestone (a contract finished, a tier reached).
- Australian spelling: colour, centre, licence (noun), kerb, programme only for a TV programme.

## Sentences and punctuation

- One idea per sentence. Most are under 15 words.
- **No em dashes in player text.** Use a full stop, a comma or a colon instead:
  - status and detail: `Holding short 23: QFA412 landing`
  - reason: `Kingscote is 1,650 km away, beyond the Saab 340B's range.`
- The middle dot `·` only separates short facts in a compact line (`3 flights · $12,400`). Don't
  use it inside sentences.
- Don't use semicolons. Two sentences read better.
- Numbers are digits (`4 flights`, `30 min`), and money is `$12,400`.
- Buttons are verbs in capitals: PLAN FLIGHT, ACCEPT, EXPAND BASE.
- Card titles and headings use sentence case: "Your first flight", not "Your First Flight".

## Words

Use one word for one thing, everywhere.

| Say | Not |
|---|---|
| flight (Adelaide out and back, paid when it parks) | rotation, service, trip, sector |
| stand, gate, bay (the real names) | parking spot, slot |
| tier (Provisional, Regional, Domestic, International) | operating tier, stage, level |
| contract | job, mission |
| repeat schedule | delegation, automation |
| check (maintenance) | routine check, service |
| base, outstation | hub |
| reliability | trust score, rating |

"Rotation" can stay in code and ADRs. The player never sees it.

## Patterns

- **Refusal:** say what blocks it and how to fix it: "The Jet-gate base is needed for a 737-8. Expand
  your base in Career."
- **Toast:** say what happened, then the number that matters: "VH-PAA paid $12,400 · on time,
  7 in a row."
- **Hold line:** "What it's doing: why." For example: "Waiting to push: QFA412 is on the taxi route."
- **Goal:** start with a verb, and keep the count in the title: "Complete 30 flights", "Serve 4
  regional towns".
